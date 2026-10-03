using System;

namespace HC.Data.Entities;

/// <summary>
/// The parcel side of an order - where it is with the courier, written down as it is learned.
///
/// Nothing about the parcel used to be stored: the AWB lived in the courier aggregator's panel and in
/// whoever booked it, so 'My Orders' could only ever say "Shipped" and a customer asking "where is my
/// parcel?" had to be answered by looking the number up by hand.
///
/// The consignment is usually created by hand in the provider's own panel
/// (HC.Business.Shipping.ShipmentTrackingService only reads); for a service the shop arranges itself there
/// is no panel at all, and the reference is minted on this side instead (see <see cref="CustomProvider"/>).
/// Either way this row is where the AWB, the courier, the tracking link and the courier's latest status are
/// kept, so both the storefront and the admin order screen can answer that question.
///
/// One row per order (unique index, see CreateOrderShipmentsTable.sql): a return is booked as a new
/// purchase and a replacement is a new order - the same reasoning that gives OrderPayments one current
/// attempt.
/// </summary>
public partial class OrderShipment
{
    /// <summary>
    /// The provider the shop books with first, and what a row written without one falls back to
    /// (<c>IShipmentProvider.Name</c> of the Shiprocket adapter). It is the default, not the only one:
    /// a parcel remembers the provider it was booked with, and one whose provider is no longer
    /// registered is tracked through the configured default (see IShipmentProviderRegistry).
    /// </summary>
    public const string ShiprocketProvider = "Shiprocket";

    /// <summary>
    /// The service the shop arranges itself, and the id its adapter is recorded under (the second half of
    /// that pairing: <c>IShipmentProvider.Name</c>). A parcel handed over in person, or given to a local
    /// courier dealt with by phone, is real without any panel behind it - so it is registered like any
    /// other provider, except that the reference written on the parcel is minted on this side
    /// (see <c>IShipmentProvider.CreateAwb</c>) and the parcel is never asked about anywhere.
    /// </summary>
    public const string CustomProvider = "Custom";

    public long ShipmentId { get; set; }

    public long OrderId { get; set; }

    /// <summary>
    /// Which provider booked this parcel (<c>IShipmentProvider.Name</c>, e.g. "Shiprocket"). This is what
    /// ties the row to the adapter that can track it, so a second provider can be used side by side with
    /// the first - and a parcel whose provider is no longer registered still falls back to the default.
    /// </summary>
    public string Provider { get; set; } = ShiprocketProvider;

    /// <summary>The courier carrying the parcel, as the provider names it (e.g. <c>Delhivery</c>).</summary>
    public string? CourierName { get; set; }

    /// <summary>
    /// The AWB ('tracking number') the courier gave the parcel. This is the key the tracking call is
    /// made with, and the number the customer quotes when they call.
    /// </summary>
    public string? AwbNumber { get; set; }

    /// <summary>The provider's own shipment id, so support can find the shipment in its panel.</summary>
    public long? ShiprocketShipmentId { get; set; }

    /// <summary>The tracking page the provider reports for the AWB, linked from 'My Orders'.</summary>
    public string? TrackingUrl { get; set; }

    /// <summary>
    /// What the courier billed the shop for this parcel - the freight charge the provider's own panel
    /// shows for it. The shop team types it in with the AWB and it is kept for the books, so it is the
    /// shop's own figure: the customer-facing reads of a parcel never carry it (see
    /// <see cref="HC.Business.Shipping.ShipmentTrackingService"/>), and a form that does not ask for it
    /// leaves whatever was recorded here alone.
    /// </summary>
    public decimal? FreightCharge { get; set; }

    /// <summary>
    /// The courier's own status wording exactly as the courier said it ('Out for Delivery'). Kept
    /// verbatim so nobody here has to guess what was really reported; the shop's own wording is derived
    /// from it at read time (<see cref="HC.Business.ShipmentStatusFlow"/>).
    /// </summary>
    public string? ProviderStatus { get; set; }

    /// <summary>
    /// The courier's status CODE next to the wording. Kept for support and shown as a fallback only -
    /// the mapping deliberately reads the wording, because the numbers are the provider's business
    /// (see <see cref="HC.Business.ShipmentStatusFlow"/>).
    /// </summary>
    public int? ProviderStatusCode { get; set; }

    /// <summary>When the courier says the parcel was delivered - the one date 'My Orders' shows.</summary>
    public DateTime? DeliveredOn { get; set; }

    /// <summary>The latest line of the tracking trail (the courier's own sentence), shown under the status.</summary>
    public string? LastStatusText { get; set; }

    /// <summary>
    /// When the courier was last asked about this parcel - the throttle that keeps 'My Orders' from
    /// calling the courier on every page open (see <c>Shipping:SyncThrottleMinutes</c>).
    /// </summary>
    public DateTime? LastCheckedOn { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual Order Order { get; set; } = null!;
}
