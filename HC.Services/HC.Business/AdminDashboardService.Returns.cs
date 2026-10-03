// ============================================================
// AdminDashboardService.Returns.cs
// Partial class: AdminDashboardService - the shop team's answer to a return
// (approve it: the parcel is coming back / refuse it: the order is unchanged)
// ============================================================

using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

public partial class AdminDashboardService : IAdminDashboardService
{
    /// <summary>
    /// Why a returned order's refund was asked for - written on the payment row
    /// (<c>RefundRequestedComment</c>), so the shop team reads it beside the amount when they approve it. The
    /// cancellation path writes its own reason ("Order cancelled by the customer"), and this is the return's.
    /// </summary>
    private const string ReturnedRefundReason = "Order returned";

    /// <summary>
    /// The shop team's answer to a return: approve it and the parcel is coming back (the return becomes
    /// 'Arranged', so the pickup is booked from the Return card next), or refuse it and the order is left
    /// exactly as it was. A note is required either way, because it is what the customer is told.
    ///
    /// The move itself is <see cref="OrderReturnFlow.ApproveAsync"/>/<see cref="OrderReturnFlow.RejectAsync"/>,
    /// so the same rules the storefront's own ask obeys are applied here - including the one that matters
    /// most: an ask that has already been answered cannot be answered again (a second answer is how one
    /// parcel becomes two refunds).
    ///
    /// The answer deliberately moves nothing else. The order is still delivered, its units are still where
    /// the delivery left them and its money is still with us until the parcel is back and the return is
    /// closed - which is the next step of the same card, not this one.
    /// </summary>
    public async Task<AdminResultDto> DecideOrderReturnAsync(
        long orderId,
        AdminOrderReturnDecisionRequest request,
        long currentUserId)
    {
        var order = await _context.Orders
            .Include(o => o.OrderStatus)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null)
            return Refused("Order not found.");

        var openReturn = await OrderReturnFlow.FindOpenAsync(_context, orderId);

        if (openReturn == null)
        {
            return Refused($"There is no return waiting for an answer on {OrderNumber(orderId)}.");
        }

        var comment = (request.Comment ?? string.Empty).Trim();

        if (comment.Length == 0)
        {
            return Refused(request.Approved
                ? "Say why the return is approved - the customer is told this note."
                : "Say why the return is refused - the customer is told this note.");
        }

        var loginId = await GetAdminLoginIdAsync(currentUserId);
        var now = DateTime.UtcNow;

        var (ok, message) = request.Approved
            ? await OrderReturnFlow.ApproveAsync(_context, order, openReturn, loginId, comment, now)
            : await OrderReturnFlow.RejectAsync(_context, order, openReturn, loginId, comment, now);

        if (!ok)
            return Refused(message);

        return new AdminResultDto { Result = 1, Messages = new[] { message } };
    }

    /// <summary>
    /// 'Parcel received' on the Return card - the parcel is physically back with the shop. The move itself is
    /// <see cref="OrderReturnFlow.MarkReceivedAsync"/>: the return becomes 'Received', its units leave the
    /// delivery pools for the shop's own 'Returned' one (off sale until the return is closed) and the order's own
    /// trail says what arrived.
    ///
    /// Deliberately nothing else moves. The order stays where it is - a returned order becomes 'Returned' when the
    /// return is closed - and the money is not touched until that close, so a parcel sitting in the shop over the
    /// weekend cannot leave the books describing a refund that nobody has decided on. A return that was never
    /// approved, or one already closed, is refused by the flow: a parcel cannot come back twice.
    ///
    /// The return and its units move in one transaction, so a return can never say the parcel is back while its
    /// units are still out with the customer.
    /// </summary>
    public async Task<AdminResultDto> MarkOrderReturnReceivedAsync(
        long orderId,
        AdminOrderReturnReceivedRequest request,
        long currentUserId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var order = await _context.Orders
                .Include(o => o.OrderStatus)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return Refused("Order not found.");

            var openReturn = await OrderReturnFlow.FindOpenAsync(_context, orderId);

            if (openReturn == null)
                return Refused($"There is no return waiting on {OrderNumber(orderId)}.");

            var (ok, message) = await OrderReturnFlow.MarkReceivedAsync(
                _context,
                order,
                openReturn,
                await GetAdminLoginIdAsync(currentUserId),
                request.Comment,
                DateTime.UtcNow);

            if (!ok)
            {
                await transaction.RollbackAsync();

                return Refused(message);
            }

            await transaction.CommitAsync();

            return new AdminResultDto { Result = 1, Messages = new[] { message } };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();

            return Refused("The parcel could not be booked back in. Nothing was saved - please try again.");
        }
    }

    /// <summary>
    /// The inspection of a parcel that came back - the units the shop team ticked were found broken. The move
    /// itself is <see cref="OrderReturnFlow.InspectAsync"/>: each named unit is written off (out of every sellable
    /// pool for good, so closing the return can never put it back on sale) and who looked, when and what they
    /// wrote are kept on the return.
    ///
    /// Only a return whose parcel is back in the shop can be looked over, which is exactly when the card offers
    /// it: after the close the units are on sale again, and writing one off then would take a unit off a shelf it
    /// may already have been sold from. The units are the order's own (see
    /// <see cref="SkuAvailability.MarkUnitsDamagedAsync"/>): a SKU that arrived with somebody else's order is
    /// ignored rather than written off.
    /// </summary>
    public async Task<AdminResultDto> MarkOrderReturnUnitsDamagedAsync(
        long orderId,
        AdminOrderReturnInspectionRequest request,
        long currentUserId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var order = await _context.Orders
                .Include(o => o.OrderStatus)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return Refused("Order not found.");

            var openReturn = await OrderReturnFlow.FindOpenAsync(_context, orderId);

            if (openReturn == null)
                return Refused($"There is no return waiting on {OrderNumber(orderId)}.");

            var (ok, message) = await OrderReturnFlow.InspectAsync(
                _context,
                order,
                openReturn,
                request.DamagedSkus ?? Array.Empty<string>(),
                await GetAdminLoginIdAsync(currentUserId),
                request.Comment,
                DateTime.UtcNow);

            if (!ok)
            {
                await transaction.RollbackAsync();

                return Refused(message);
            }

            await transaction.CommitAsync();

            return new AdminResultDto { Result = 1, Messages = new[] { message } };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();

            return Refused("The inspection could not be saved. Nothing was written off - please try again.");
        }
    }

    /// <summary>
    /// 'Close return' - the last step of a return, and the one that settles it: the order becomes 'Returned', every
    /// unit the return brought back goes on sale again (the ones written off stay written off) and the refund of
    /// what the customer paid is asked for. The move itself is <see cref="OrderReturnFlow.CloseAsync"/>, which
    /// deliberately knows nothing about money.
    ///
    /// The refund is where the two doors into 'Returned' meet: a return closed here and a Delivered order moved to
    /// Returned by hand ask in the same way, through <see cref="RazorpayRefunds.RequestRefundAsync"/> - the money is
    /// owed back, the request is written on the payment row, and the shop team approves it on the Refund card,
    /// which is what sends it. Nothing reaches the gateway from here, so a return closed in the evening is refunded
    /// in the morning by somebody who looked at it, and a refund Razorpay refuses stays owed and visible.
    ///
    /// Whether the customer's money was taken is read from the order BEFORE it moves to 'Returned' (that is the
    /// status that says they paid for it): an order that was never charged simply closes.
    /// </summary>
    public async Task<AdminResultDto> CloseOrderReturnAsync(
        long orderId,
        AdminOrderReturnCloseRequest request,
        long currentUserId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var order = await _context.Orders
                .Include(o => o.OrderStatus)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return Refused("Order not found.");

            var openReturn = await OrderReturnFlow.FindOpenAsync(_context, orderId);

            if (openReturn == null)
                return Refused($"There is no return waiting on {OrderNumber(orderId)}.");

            var moneyWasTaken = OrderStatusFlow.IsPaid(order.OrderStatusId);

            var (ok, message) = await OrderReturnFlow.CloseAsync(
                _context,
                order,
                openReturn,
                await GetAdminLoginIdAsync(currentUserId),
                request.Comment,
                DateTime.UtcNow);

            if (!ok)
            {
                await transaction.RollbackAsync();

                return Refused(message);
            }

            // The order is 'Returned' now, and the refund goes in with the same transaction: the request is asked
            // for here (a customer's cancellation asks in exactly this way) and the approval on the Refund card is
            // what sends the money. Nothing is asked for an order that was never charged, and nothing twice for one
            // already refunded - both are the request's own rule (see RazorpayRefunds.RequestRefundAsync).
            if (moneyWasTaken)
            {
                var (owed, refundMessage) = await RazorpayRefunds.RequestRefundAsync(
                    _context, order, ReturnedRefundReason, moneyWasTaken);

                message += " " + refundMessage;

                if (owed)
                {
                    await AddRefundHistoryAsync(
                        order, DateTime.UtcNow, RefundStillOwedNote(order.OrderId), currentUserId);
                }
            }

            await transaction.CommitAsync();

            return new AdminResultDto { Result = 1, Messages = new[] { message } };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();

            return Refused("The return could not be closed. Nothing was saved - please try again.");
        }
    }

    /// <summary>Result = 0 with one sentence for the shop team - the shape every admin action answers with.</summary>
    private static AdminResultDto Refused(string message) =>
        new() { Result = 0, Messages = new[] { message } };
}
