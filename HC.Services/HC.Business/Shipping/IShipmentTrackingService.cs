using HC.Business.Dtos;

namespace HC.Business.Shipping;

/// <summary>
/// The parcel side of an order: recording it, asking the courier about it, and letting the order follow
/// it. Everything provider-specific happens behind <see cref="IShipmentProvider"/> - this is the layer
/// the order screens talk to, and it works the same whichever provider a parcel was booked with.
///
/// Every method answers rather than throws: a lookup failure is a sentence in the result for the person
/// waiting, never an exception on a page (the same rule <see cref="IPincodeService"/> follows).
/// </summary>
public interface IShipmentTrackingService
{
    /// <summary>
    /// The shipping providers this shop is set up with, in registration order and with the default
    /// marked - the list the admin order screen offers when the shop team records a parcel.
    /// </summary>
    IReadOnlyList<ShipmentProviderInfo> GetProviders();

    /// <summary>
    /// The parcel recorded for an order as it stands (<c>null</c> when none has been recorded yet). It
    /// never calls the courier, so it is safe on any screen; use <see cref="RefreshAsync"/> for a fresh
    /// look.
    ///
    /// A parcel is read by leg: blank <paramref name="direction"/> is the one that went out (what every
    /// screen but one means), and <c>OrderShipment.DirectionReverse</c> is the one coming back - the pickup
    /// the Return card of the admin order screen shows.
    /// </summary>
    Task<OrderShipmentDto?> GetForOrderAsync(long orderId, string? direction = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The parcels of several orders at once, keyed by order id - one query for a whole page. What the
    /// courier billed for a parcel is left out of the answer: this read is the customer's own page.
    /// </summary>
    Task<Dictionary<long, OrderShipmentDto>> GetForOrdersAsync(IEnumerable<long> orderIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// The parcels coming back, keyed by order id - one query for a whole page, and the return's own counterpart
    /// of <see cref="GetForOrdersAsync"/>. An order with no return parcel is simply absent: a reverse leg exists
    /// only once a return has been approved (the pickup the shop team booked) or the courier itself reported the
    /// parcel on its way back, and until then 'My Orders' shows the return's own status instead. What the courier
    /// billed is stripped here too, because this read is the customer's own page.
    /// </summary>
    Task<Dictionary<long, OrderShipmentDto>> GetReverseForOrdersAsync(IEnumerable<long> orderIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the parcel the shop team booked in the provider's panel: the AWB, the courier and the
    /// tracking link when they have them, and what the courier billed for it (the shop's own figure,
    /// never shown to the customer). This never moves the order - a parcel booked is not a parcel
    /// dispatched, and the order follows the courier's own reports (see <see cref="RefreshAsync"/>).
    ///
    /// A blank AWB is accepted for a provider that gives no consignment numbers of its own, and one is
    /// minted for the parcel (see <see cref="IShipmentProvider.CreateAwb"/>); for every other provider a
    /// blank one is refused, because the AWB is the courier's own.
    /// </summary>
    Task<OrderShipmentDto> SaveAsync(long orderId, SaveOrderShipmentRequest request, long currentUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when this request really records a parcel: a consignment number was typed, or the provider
    /// named gives the parcel a reference of its own (see <see cref="IShipmentProvider.AwbGeneratedBySystem"/>).
    ///
    /// The admin's Shipped move asks this before writing anything, because 'is this a parcel?' is the
    /// provider's own answer and only this service knows the registry: an order dispatched with no
    /// consignment number and an aggregator behind it has no parcel and no freight, while the same move
    /// against the shop's own service does have one - and the freight billed for it belongs there.
    /// </summary>
    bool RecordsParcel(SaveOrderShipmentRequest request);

    /// <summary>
    /// Brings a parcel the shop carries itself level with the order that has just been moved, as of
    /// <paramref name="now"/> (the caller's own clock, so the move and the parcel it moved are one moment):
    /// a dispatched order puts its parcel on the way, a delivered one ends its journey and writes down the
    /// day it arrived. For such a parcel the shop's own move IS its movement (see
    /// <see cref="IShipmentProvider.ReportsTracking"/>) - there is nothing behind it that would ever report
    /// anything - and this is what keeps its card and 'My Orders' from saying "the shop's own delivery
    /// arrangement" for a parcel that has already arrived, without ever saying when.
    ///
    /// A parcel with a courier behind it is left untouched: its provider is asked, never assumed, so the last
    /// thing a real lookup wrote is not this method's to overwrite. A move that says nothing about a parcel
    /// (Confirmed, Cancelled) mirrors nothing, and neither does an order with no parcel recorded.
    ///
    /// Answers with the sentence for the shop team, to sit beside the move's own answer, or "" when there was
    /// nothing to bring level.
    /// </summary>
    Task<string> MirrorOrderStatusAsync(long orderId, short orderStatusId, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the courier about this order's parcel right now and writes down what it said, moving the
    /// order (and its units) along if the courier's status allows it. The throttle that protects the
    /// customer-facing pull does not apply here: the shop team asked on purpose.
    /// </summary>
    Task<OrderShipmentDto> RefreshAsync(long orderId, long currentUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The throttled pull 'My Orders' makes when it opens: the customer's live parcels that have not been
    /// looked up recently are refreshed and answered in <c>Shipments</c>. A page open never re-asks the
    /// courier about a parcel it asked about a minute ago, and a parcel the courier is done with is left
    /// alone for good.
    /// </summary>
    Task<RefreshOrderShipmentsDto> RefreshForCustomerAsync(long customerId, CancellationToken cancellationToken = default);
}
