using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

/// <summary>
/// The return lifecycle in one place: who may ask for a return, what the shop team's answer does, and what
/// the order and its history say afterwards.
///
/// It lives here for the same reason <see cref="SkuAvailability"/> does: more than one screen moves a
/// return along, and the rules must not drift between them. The customer asks from 'My Orders'
/// (OrderService.RequestReturnAsync), the shop team answers from the admin order screen
/// (AdminDashboardService), and the courier raises its own ask when a parcel comes back
/// (<see cref="RequestAsync"/> with <see cref="OrderReturnOrigin.Courier"/>, called by the tracking pull).
/// The rules they all share:
///   * only a DELIVERED order can be returned by the customer (<see cref="OrderStatusFlow.CanCustomerReturn"/>),
///     inside the return window (<c>Returns:WindowDays</c>, counted from the day the parcel arrived) and
///     only once at a time (<see cref="OrderReturnStatus.IsOpen"/>, which a filtered index in the
///     database enforces as well);
///   * a courier's own report only ever ASKS (Origin 'Courier'): the shop team decides, so a parcel that
///     came back undelivered never turns into a refund by itself;
///   * approving means the parcel is coming back ('Arranged') and refusing leaves the order exactly as it
///     was - the order only becomes 'Returned' when the return is closed, which is also when its units go
///     back on the shelf and the money is asked for (see AdminDashboardService).
///
/// What is written where: the ask, the answer and every date live on <see cref="OrderReturn"/>; each step is
/// also written to <see cref="OrderHistory"/> as it happens, because that is the trail both 'My Orders' and
/// the admin order screen already read. The refund is deliberately NOT described here - money is the
/// payment's business (<see cref="OrderPaymentStatus.RefundRequested"/> on OrderPayments).
///
/// The methods below own the moves; the callers own their wording (a customer is told one thing, the shop
/// team another). They assume the caller's own transaction: nothing here commits.
/// </summary>
public static class OrderReturnFlow
{
    /// <summary>
    /// How long a customer has to ask for a return when the configuration says nothing
    /// (<c>Returns:WindowDays</c>).
    /// </summary>
    public const int DefaultWindowDays = 7;

    /// <summary>
    /// When the parcel reached the customer - what the return window is counted from. The courier's own
    /// delivery date (the forward leg's <see cref="OrderShipment.DeliveredOn"/>) is the truth when it is
    /// known; otherwise the moment the order was moved to Delivered, which is what the order history
    /// recorded for it (a shop team moving an order by hand writes one there too). Null only when the
    /// order carries neither, i.e. it was never delivered.
    ///
    /// The caller must have loaded <see cref="Order.OrderShipments"/> and <see cref="Order.OrderHistories"/>.
    /// </summary>
    public static DateTime? DeliveredOn(Order order)
    {
        var forward = order.OrderShipments
            .FirstOrDefault(s => s.Direction == OrderShipment.DirectionForward);

        if (forward?.DeliveredOn is { } courierSays)
            return courierSays;

        return order.OrderHistories
            .Where(h => h.OrderStatusId == OrderStatusFlow.Delivered)
            .OrderByDescending(h => h.HistoryDate)
            .Select(h => (DateTime?)h.HistoryDate)
            .FirstOrDefault();
    }

    /// <summary>
    /// The last day the customer may ask for a return: the day the parcel arrived plus the configured
    /// window. Null while that day is unknown (see <see cref="DeliveredOn"/>).
    /// </summary>
    public static DateTime? WindowEndsOn(Order order, int windowDays)
        => DeliveredOn(order) is { } delivered ? delivered.AddDays(windowDays) : null;

    /// <summary>
    /// True while the window is still open. A null end means nobody knows when the parcel arrived, and the
    /// customer is given the benefit of the doubt rather than refused on a date we do not have.
    /// </summary>
    public static bool IsWithinWindow(DateTime? windowEndsOn, DateTime now) =>
        windowEndsOn == null || now <= windowEndsOn.Value;

    /// <summary>
    /// True when the customer may ask for a return of this order right now: it has been delivered, no
    /// return is open on it, and the window has not passed. 'My Orders' offers the button on exactly this
    /// and the server accepts exactly this, so the two can never disagree.
    ///
    /// The caller must have loaded what <see cref="DeliveredOn"/> reads (see above).
    /// </summary>
    public static bool CanCustomerAsk(Order order, OrderReturn? openReturn, int windowDays, DateTime now) =>
        OrderStatusFlow.CanCustomerReturn(order.OrderStatusId) &&
        openReturn == null &&
        IsWithinWindow(WindowEndsOn(order, windowDays), now);

    /// <summary>
    /// The return currently open on this order, or null when there is none - the one thing that makes
    /// 'a delivered order is returned once' true in the service as well as in the database.
    /// </summary>
    public static Task<OrderReturn?> FindOpenAsync(
        HomecutiesDbContext context,
        long orderId,
        CancellationToken cancellationToken = default) =>
        context.OrderReturns.FirstOrDefaultAsync(
            r => r.OrderId == orderId && OrderReturnStatus.Open.Contains(r.Status),
            cancellationToken);

    /// <summary>
    /// Writes an ask down: a new open return in <see cref="OrderReturnStatus.Requested"/>, with the reason
    /// and the order history line that goes with it. The caller checks whether the ask is allowed
    /// (<see cref="CanCustomerAsk"/>, or the shop team's own judgement for what a courier reports).
    ///
    /// Idempotent on purpose: when the order already has an open return, that one is returned and nothing is
    /// written. That is what stops a courier reporting the same refusal twice - or a customer
    /// double-tapping a button - from opening two returns for one parcel.
    /// </summary>
    public static async Task<OrderReturn> RequestAsync(
        HomecutiesDbContext context,
        Order order,
        string origin,
        string reasonCode,
        string reason,
        string? requestedBy,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindOpenAsync(context, order.OrderId, cancellationToken);
        if (existing != null)
            return existing;

        var request = new OrderReturn
        {
            OrderId = order.OrderId,
            Origin = origin,
            ReasonCode = reasonCode,
            Reason = Truncate(reason, 500)!,
            Status = OrderReturnStatus.Requested,
            RequestedOn = now,
            RequestedBy = Truncate(requestedBy, 100),
            CreatedOn = now
        };

        context.OrderReturns.Add(request);

        AddHistory(context, order, now,
            $"{AskLead(origin)}: {OrderReturnReason.Label(reasonCode)} - {request.Reason}" +
            (request.RequestedBy == null ? "." : $" (by {request.RequestedBy})."));

        await context.SaveChangesAsync(cancellationToken);

        return request;
    }

    /// <summary>
    /// The shop team saying yes: the ask becomes <see cref="OrderReturnStatus.Arranged"/> - the parcel is
    /// coming back, so a pickup is booked from the Return card (or the courier is already carrying it back)
    /// - and who answered, when and why are written on the return and into the order history.
    ///
    /// Nothing else moves, on purpose: the order stays where it is (a delivered order is still delivered
    /// until the parcel is back) and the money is not touched until the return closes. An ask that has
    /// already been answered or taken back is refused, because a second answer is how one parcel becomes
    /// two refunds.
    ///
    /// The message is the sentence for the shop team - the caller relays it in its own answer.
    /// </summary>
    public static async Task<(bool Ok, string Message)> ApproveAsync(
        HomecutiesDbContext context,
        Order order,
        OrderReturn request,
        string? decidedBy,
        string comment,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        if (!OrderReturnStatus.CanDecide(request.Status))
            return (false, AlreadyAnswered(request));

        request.Status = OrderReturnStatus.Arranged;
        RecordDecision(request, decidedBy, comment, now);

        AddHistory(context, order, now,
            $"Return approved ({OrderReturnReason.Label(request.ReasonCode)}): {comment}{ByName(decidedBy)}");

        await context.SaveChangesAsync(cancellationToken);

        return (true,
            "The return is approved. The order stays as it is; record the parcel coming back on this card. " +
            "The units go back on the shelf and the refund is asked for when the return is closed.");
    }

    /// <summary>
    /// The parcel is physically back with the shop: the return becomes <see cref="OrderReturnStatus.Received"/>
    /// and its units leave the delivery pools for the shop's own 'Returned' one
    /// (<see cref="SkuAvailability.MarkUnitsReturnedAsync"/>) - in the building, in front of somebody, and
    /// deliberately not on sale again until the return is closed.
    ///
    /// Nothing else moves: the order is still where it was, because a returned order becomes 'Returned' when the
    /// return is closed - and that close is also when the money is asked for (see
    /// <see cref="AdminDashboardService"/>). A return that was never approved, or one already closed, is
    /// refused: a parcel cannot come back twice.
    ///
    /// The caller must have loaded <see cref="Order.OrderItems"/> (the units are the order's own). The caller's
    /// own transaction is assumed: nothing here commits.
    /// </summary>
    public static async Task<(bool Ok, string Message)> MarkReceivedAsync(
        HomecutiesDbContext context,
        Order order,
        OrderReturn request,
        string? receivedBy,
        string? comment,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        if (!OrderReturnStatus.CanMarkReceived(request.Status))
        {
            return (false, request.Status == OrderReturnStatus.Received
                ? "This return's parcel is already booked back in."
                : "Only an approved return, whose parcel is on its way back, can be booked in here.");
        }

        request.Status = OrderReturnStatus.Received;
        request.ReceivedOn = now;
        request.UpdatedOn = now;

        var units = await SkuAvailability.MarkUnitsReturnedAsync(context, order, now, cancellationToken);

        AddHistory(context, order, now,
            $"The parcel came back: {UnitCount(units)} in the shop{Note(comment)}{ByName(receivedBy)}");

        await context.SaveChangesAsync(cancellationToken);

        return (true,
            $"The parcel is booked back in ({UnitCount(units)}). Look the parcel over and write off anything " +
            "that came back broken, then close the return - the order becomes 'Returned', the units go back " +
            "on sale and the refund is asked for at that point.");
    }

    /// <summary>
    /// The shop team looking over what came back: the units they name are written off
    /// (<see cref="SkuAvailability.MarkUnitsDamagedAsync"/> - out of every sellable pool for good, so closing
    /// the return can never put them back on sale), and who looked, when and what they wrote are kept on the
    /// return (<c>InspectionOn/By/Comment</c>).
    ///
    /// Two things are deliberate. It is per unit, because one of three identical tops can come back torn while
    /// the other two are fine. And it is not required: a return with nothing wrong with it is closed without an
    /// inspection, which is why 'nobody looked' (<c>InspectionOn</c> null) is a different thing from 'somebody
    /// looked and found nothing' - the latter is written down, with no units written off.
    ///
    /// It happens while the return is <see cref="OrderReturnStatus.Received"/> and no later: after the close the
    /// units are on sale again, and writing one off then would mean taking a unit off the shelf it may already
    /// have been sold from.
    ///
    /// The caller must have loaded <see cref="Order.OrderItems"/>. The caller's own transaction is assumed.
    /// </summary>
    public static async Task<(bool Ok, string Message)> InspectAsync(
        HomecutiesDbContext context,
        Order order,
        OrderReturn request,
        IEnumerable<string> damagedSkus,
        string? inspectedBy,
        string? comment,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        if (!OrderReturnStatus.CanInspect(request.Status))
        {
            return (false, "Only a return whose parcel is back in the shop can be looked over.");
        }

        var writtenOff = await SkuAvailability.MarkUnitsDamagedAsync(
            context, order, damagedSkus.ToList(), now, cancellationToken);

        request.InspectionOn = now;
        request.InspectionBy = Truncate(inspectedBy, 100);
        request.InspectionComment = Truncate(comment, 500);
        request.UpdatedOn = now;

        AddHistory(context, order, now, writtenOff == 0
            ? $"The returned parcel was looked over - nothing was written off{Note(comment)}{ByName(inspectedBy)}"
            : $"The returned parcel was looked over: {UnitCount(writtenOff)} written off as damaged" +
              $"{Note(comment)}{ByName(inspectedBy)}");

        await context.SaveChangesAsync(cancellationToken);

        return (true, writtenOff == 0
            ? "The parcel is marked as looked over, with nothing written off. Close the return when you are " +
              "ready - its units go back on sale."
            : $"{UnitCount(writtenOff)} taken out of the sellable stock as damaged. Close the return when you " +
              "are ready - the rest goes back on sale.");
    }

    /// <summary>
    /// The return is done: it becomes <see cref="OrderReturnStatus.Closed"/>, the ORDER becomes
    /// <see cref="OrderStatusFlow.Returned"/>, and every unit the return brought back goes on sale again
    /// (<see cref="SkuAvailability.ReleaseReturnedUnitsAsync"/>). A unit the inspection wrote off is in none of
    /// those pools any more, so it stays written off.
    ///
    /// The money is deliberately NOT here. This closes the parcel's side of it; the caller asks for the refund
    /// in the same transaction (see <see cref="RazorpayRefunds"/>) - a refund is the payment's business, and
    /// this class describes nothing about money.
    ///
    /// Only a return whose parcel is back with the shop can be closed (<see cref="OrderReturnStatus.CanClose"/>):
    /// closing one whose parcel never came back is how a refund is given for something the customer still has.
    /// The caller must have loaded <see cref="Order.OrderItems"/>.
    /// </summary>
    public static async Task<(bool Ok, string Message)> CloseAsync(
        HomecutiesDbContext context,
        Order order,
        OrderReturn request,
        string? closedBy,
        string? comment,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        if (!OrderReturnStatus.CanClose(request.Status))
        {
            return (false, "Only a return whose parcel is back in the shop can be closed.");
        }

        var released = await SkuAvailability.ReleaseReturnedUnitsAsync(context, order, now, cancellationToken);

        request.Status = OrderReturnStatus.Closed;
        request.ClosedOn = now;
        request.UpdatedOn = now;

        // The end of a delivered (or refused) order: the same move a cancellation makes, so the stock counters
        // and the order status can never describe two different things (see SkuAvailability).
        order.OrderStatusId = OrderStatusFlow.Returned;

        AddHistory(context, order, now,
            $"The order came back to us: {UnitCount(released)} back on sale{Note(comment)}{ByName(closedBy)}");

        await context.SaveChangesAsync(cancellationToken);

        return (true,
            $"The return is closed - {UnitCount(released)} back on sale, and the order is now 'Returned'.");
    }

    /// <summary>
    /// The shop team saying no: the ask becomes <see cref="OrderReturnStatus.Rejected"/> and the order is
    /// left exactly as it was - nothing moved, nothing is owed. The note is what the customer is told, so
    /// the admin screen insists on one (see AdminDashboardService).
    /// </summary>
    public static async Task<(bool Ok, string Message)> RejectAsync(
        HomecutiesDbContext context,
        Order order,
        OrderReturn request,
        string? decidedBy,
        string comment,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        if (!OrderReturnStatus.CanDecide(request.Status))
            return (false, AlreadyAnswered(request));

        request.Status = OrderReturnStatus.Rejected;
        RecordDecision(request, decidedBy, comment, now);

        AddHistory(context, order, now, $"Return refused: {comment}{ByName(decidedBy)}");

        await context.SaveChangesAsync(cancellationToken);

        return (true, "The return is refused. The order is left exactly as it was and nothing is owed.");
    }

    /// <summary>
    /// The customer changing their mind before anybody has answered: the ask becomes
    /// <see cref="OrderReturnStatus.Withdrawn"/> and the order carries on as if it had never been made -
    /// which is only possible while it is still <see cref="OrderReturnStatus.Requested"/>, so an ask the
    /// shop team has already approved (or whose parcel is already coming back) cannot be called off from
    /// 'My Orders'.
    /// </summary>
    public static async Task<(bool Ok, string Message)> WithdrawAsync(
        HomecutiesDbContext context,
        Order order,
        OrderReturn request,
        string? withdrawnBy,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        if (!OrderReturnStatus.CanWithdraw(request.Status))
        {
            return (false,
                "This return request can no longer be taken back - we have already answered it. " +
                "Please contact support@homecuties.com and we will help.");
        }

        request.Status = OrderReturnStatus.Withdrawn;
        request.UpdatedOn = now;

        AddHistory(context, order, now, $"Return request taken back before it was decided{ByName(withdrawnBy)}");

        await context.SaveChangesAsync(cancellationToken);

        return (true,
            "Your return request has been taken back. The order is unchanged, and you can ask again while " +
            "the return window is open.");
    }

    /// <summary>How the history line of an ask opens, in the voice of whoever asked.</summary>
    private static string AskLead(string origin) => origin == OrderReturnOrigin.Courier
        ? "The courier reports a parcel coming back"
        : "Return requested";

    /// <summary>Why an ask cannot be answered again - a second answer would be a second return.</summary>
    private static string AlreadyAnswered(OrderReturn request) => request.Status switch
    {
        OrderReturnStatus.Arranged or OrderReturnStatus.Received => "This return has already been approved.",
        OrderReturnStatus.Closed => "This return has already been closed.",
        OrderReturnStatus.Rejected => "This return has already been refused.",
        OrderReturnStatus.Withdrawn => "This return was taken back by the customer.",
        _ => "This return is no longer open."
    };

    private static void RecordDecision(OrderReturn request, string? decidedBy, string comment, DateTime now)
    {
        request.DecisionOn = now;
        request.DecisionBy = Truncate(decidedBy, 100);
        request.DecisionComment = Truncate(comment, 500);
        request.UpdatedOn = now;
    }

    /// <summary>' (by jane)' for the order history, or '' when the person is not known.</summary>
    private static string ByName(string? loginId) => string.IsNullOrWhiteSpace(loginId) ? "" : $" (by {loginId})";

    /// <summary>' - the note' for the order history, or '' when nobody wrote one.</summary>
    private static string Note(string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? string.Empty : $" - {comment.Trim()}";

    /// <summary>'1 unit' or '3 units' - how a stock count is written on the order's own trail.</summary>
    private static string UnitCount(int units) => units == 1 ? "1 unit" : $"{units} units";

    /// <summary>
    /// A line on the order's own trail. It carries the order's CURRENT status, because that is still where
    /// the order is: a return moves the money and the stock later, and the timeline should say what was
    /// asked, answered or done rather than pretend the order moved.
    /// </summary>
    private static void AddHistory(HomecutiesDbContext context, Order order, DateTime now, string comment) =>
        context.OrderHistories.Add(new OrderHistory
        {
            OrderId = order.OrderId,
            HistoryDate = now,
            OrderStatusId = order.OrderStatusId,
            Comments = Truncate(comment, 2000)!
        });

    /// <summary>Trimmed to fit the column it is written into, never longer (see CreateOrderReturnsTable.sql).</summary>
    private static string? Truncate(string? value, int maxLength)
    {
        if (value == null)
            return null;

        var text = value.Trim();

        if (text.Length == 0)
            return null;

        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
