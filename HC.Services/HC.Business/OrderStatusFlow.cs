namespace HC.Business;

/// <summary>
/// The order lifecycle in one place: Orders.OrderStatusID 1 = Pending, 2 = Confirmed, 3 = Shipped,
/// 4 = Delivered, 5 = Cancelled, 6 = Returned (see HC.Data/Scripts/SeedOrderStatuses.sql).
///
/// Every step depends on the one before it. An order is placed as Pending, the money turns it into
/// Confirmed, the parcel into Shipped and finally Delivered. From there it ends one of two ways: the
/// customer keeps it, or the parcel comes back and the order becomes Returned - the sale reversed, its
/// money owed back and its units back on the shelf (see HC.Business.OrderReturnFlow, which is what moves
/// it). Cancelling is the other way out of the chain and it is only open while the order is still in the
/// shop's hands. Returned and Cancelled are terminal.
///
/// The storefront and the admin area both read this map, so the buttons a customer is offered and
/// the steps the shop team may take can never drift apart: 'My Orders' offers 'Cancel Order' for
/// exactly the statuses <see cref="CanCustomerCancel"/> allows and 'Return' for
/// <see cref="CanCustomerReturn"/>, and the server accepts nothing else.
/// </summary>
public static class OrderStatusFlow
{
    public const short Pending = 1;
    public const short Confirmed = 2;
    public const short Shipped = 3;
    public const short Delivered = 4;
    public const short Cancelled = SkuAvailability.CancelledOrderStatusId; // 5

    /// <summary>
    /// The end of a returned order: the parcel is back with the shop, the units are on the shelf (or
    /// written off) and the money is owed back. Reached from Delivered (the customer sent it back) and
    /// from Shipped (the courier reported a refusal / return-to-origin) - see HC.Business.OrderReturnFlow.
    /// </summary>
    public const short Returned = SkuAvailability.ReturnedOrderStatusId; // 6

    /// <summary>Where each status may go next. An empty entry is the end of the line.</summary>
    private static readonly Dictionary<short, short[]> Transitions = new()
    {
        [Pending] = new[] { Confirmed, Cancelled },
        [Confirmed] = new[] { Shipped, Cancelled },
        [Shipped] = new[] { Delivered, Cancelled, Returned },
        [Delivered] = new[] { Returned },
        [Cancelled] = Array.Empty<short>(),
        [Returned] = Array.Empty<short>()
    };

    /// <summary>The statuses <paramref name="statusId"/> may be moved to next (empty at the end of the line).</summary>
    public static short[] NextFrom(short statusId) =>
        Transitions.TryGetValue(statusId, out var next) ? next : Array.Empty<short>();

    /// <summary>True when the status chain allows this step - the guard every status change goes through.</summary>
    public static bool CanMove(short fromStatusId, short toStatusId) =>
        NextFrom(fromStatusId).Contains(toStatusId);

    /// <summary>
    /// True once the customer's money is with us: Confirmed, Shipped or Delivered. A returned order is
    /// NOT paid any more - the sale has been reversed, so its money is owed back to the customer
    /// (<see cref="OrderPaymentStatus.RefundRequested"/> on the payment row), which is a different thing
    /// from "paid" and is what keeps the refund visible on the admin order screen.
    /// </summary>
    public static bool IsPaid(short statusId) => statusId is Confirmed or Shipped or Delivered;

    /// <summary>
    /// True once the order has left the shop (Shipped, Delivered or Returned), so it can no longer be
    /// cancelled: a returned order has a return behind it, not a cancellation.
    /// </summary>
    public static bool HasShipped(short statusId) => statusId is Shipped or Delivered or Returned;

    /// <summary>The only status that still owes money, so the only one 'Pay now' may be offered for.</summary>
    public static bool CanCustomerPay(short statusId) => statusId == Pending;

    /// <summary>
    /// True while the customer may cancel the order from 'My Orders': Pending (nothing has been paid)
    /// or Confirmed (paid - the payment is given back). Once the order is Shipped the parcel is on its
    /// way and the shop team handles it, so a cancellation then becomes a return.
    /// </summary>
    public static bool CanCustomerCancel(short statusId) => statusId is Pending or Confirmed;

    /// <summary>
    /// True while the customer may ask for a return from 'My Orders': the order has been delivered, so
    /// it is theirs to send back. This is the status rule alone - whether the return window
    /// (<c>Returns:WindowDays</c>, counted from the forward parcel's delivery) is still open, and whether
    /// one has already been asked for, is checked by OrderReturnFlow, which owns both.
    /// </summary>
    public static bool CanCustomerReturn(short statusId) => statusId == Delivered;
}
