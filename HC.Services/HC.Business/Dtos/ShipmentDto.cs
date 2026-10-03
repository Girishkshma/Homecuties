using System.Text.Json.Serialization;

namespace HC.Business.Dtos;

/// <summary>
/// What one shipping provider said about one AWB, in plain fields - the answer
/// <see cref="Shipping.IShipmentProvider"/> hands back and nothing more. It is a snapshot of one call:
/// no database, no order, no shop wording (that is <see cref="ShipmentStatusFlow"/>'s job).
///
/// The shape is deliberately provider-neutral: every adapter (Shiprocket today, another aggregator
/// tomorrow) fills in these same fields, so everything above the adapters - the shipment row, the
/// order status it may advance, the two screens that show it - is written once and works with all of
/// them.
///
/// <c>Succeeded</c> means the courier was actually reached and answered - not that the parcel is fine.
/// A call that fails (no credentials, no network, an AWB the provider does not know) comes back with
/// Succeeded false and a sentence in <c>Message</c> for the person waiting, never as an exception: a
/// tracking lookup is never allowed to break the screen it is shown on (the same rule PincodeService
/// follows).
/// </summary>
public class ShipmentTrackingSnapshotDto
{
    [JsonPropertyName("succeeded")]
    public bool Succeeded { get; set; }

    /// <summary>Why the call failed, in words for the customer/shop team ("" when it succeeded).</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    /// <summary>The AWB that was asked about (echoed back, so a caller never mixes two parcels up).</summary>
    [JsonPropertyName("awbNumber")]
    public string AwbNumber { get; set; } = "";

    [JsonPropertyName("courierName")]
    public string CourierName { get; set; } = "";

    /// <summary>The tracking page the provider reports, when it reports one (else "").</summary>
    [JsonPropertyName("trackingUrl")]
    public string TrackingUrl { get; set; } = "";

    /// <summary>The courier's latest status wording, exactly as reported ("" when it said nothing).</summary>
    [JsonPropertyName("statusText")]
    public string StatusText { get; set; } = "";

    /// <summary>The provider's own status code next to the wording, for support only.</summary>
    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; set; }

    /// <summary>When the courier says the parcel was delivered, if it said so with a usable date.</summary>
    [JsonPropertyName("deliveredOn")]
    public DateTime? DeliveredOn { get; set; }
}

/// <summary>
/// One shipping provider as the screens and the startup log see it: which adapter it is, whether it is
/// ready to be used, and where it talks to. It is what the admin order screen offers when the shop team
/// records a parcel, so the provider of a shipment is picked from what is actually wired up rather than
/// typed in by hand.
///
/// <c>Name</c> is the value written to <c>OrderShipments.Provider</c>; the credentials themselves are
/// never part of this shape (only whether they are present: <c>Configured</c>).
/// </summary>
public class ShipmentProviderInfo
{
    /// <summary>The adapter's stable id, as stored in <c>OrderShipments.Provider</c> (e.g. "Shiprocket").</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>What the shop team reads in the provider list.</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// True when the adapter has everything it needs (credentials present and the integration switched
    /// on) - a provider can be registered and not usable, which is exactly what the startup log warns
    /// about.
    /// </summary>
    [JsonPropertyName("configured")]
    public bool Configured { get; set; }

    /// <summary>
    /// True for a provider whose AWB the system mints itself (<c>IShipmentProvider.AwbGeneratedBySystem</c>)
    /// - a service the shop arranges, with no panel to give a parcel a consignment number. The admin
    /// screens then offer the AWB box as optional and say a reference will be given for it.
    /// </summary>
    [JsonPropertyName("awbGeneratedBySystem")]
    public bool AwbGeneratedBySystem { get; set; }

    /// <summary>
    /// True when this provider can be asked where a parcel is (<c>IShipmentProvider.ReportsTracking</c>).
    /// False for a service with no tracking behind it, where the admin screen offers no 'Track now'.
    /// </summary>
    [JsonPropertyName("reportsTracking")]
    public bool ReportsTracking { get; set; }

    /// <summary>True for the provider a parcel is recorded against when none is named.</summary>
    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    /// <summary>The API being talked to, for the startup log (never the credentials).</summary>
    [JsonPropertyName("apiBaseUrl")]
    public string ApiBaseUrl { get; set; } = "";

    /// <summary>The tracking path in use, with '{0}' where the AWB goes.</summary>
    [JsonPropertyName("trackingPath")]
    public string TrackingPath { get; set; } = "";
}

/// <summary>
/// The parcel of one order as the screens show it: the AWB, the courier, the tracking link and the
/// courier's latest wording (with the shop's own wording derived from it), plus the order status the
/// pull left the order in.
///
/// One shape for both audiences - the admin Shipment card and 'My Orders' - so the shop and the customer
/// can never read two different stories about the same parcel. The one exception is what the courier
/// billed the shop for the parcel (<c>FreightCharge</c>), which is the shop's own figure: only the admin
/// reads carry it, and the customer's are answered without it. It is also the result of the
/// customer-side refresh: <c>Result</c> and <c>Messages</c> say what the pull did.
/// </summary>
public class OrderShipmentDto
{
    /// <summary>1 = the caller gets an answer to show; 0 = nothing could be done (see <c>Messages</c>).</summary>
    [JsonPropertyName("result")]
    public int Result { get; set; }

    [JsonPropertyName("messages")]
    public string[] Messages { get; set; } = Array.Empty<string>();

    [JsonPropertyName("orderId")]
    public long OrderId { get; set; }

    /// <summary>
    /// Which leg of the order this parcel is: 'Forward' (the one that went out) or 'Reverse' (the one
    /// coming back - a return's pickup, or the courier's own return-to-origin; see
    /// <c>OrderShipment.DirectionForward/DirectionReverse</c> and <see cref="IsReverse"/>). The Return card
    /// of the admin order screen is what reads a reverse parcel today; 'My Orders' shows the forward one
    /// with the return's own status beside it.
    /// </summary>
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "Forward";

    /// <summary>True when this is the parcel coming back - a return's own leg.</summary>
    [JsonPropertyName("isReverse")]
    public bool IsReverse { get; set; }

    /// <summary>False while the shop has not recorded an AWB for this order yet.</summary>
    [JsonPropertyName("hasShipment")]
    public bool HasShipment { get; set; }

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";

    /// <summary>
    /// True when the provider this parcel was booked with can be asked where it is. False when it went out
    /// with a service the shop arranges itself (see <c>IShipmentProvider.ReportsTracking</c> and
    /// OrderShipments.Provider): there is no courier to ask and no tracking page to offer, so both screens
    /// leave tracking out for it rather than showing a parcel that can never bring news. The AWB is a real
    /// reference either way - the shop's own one in that case.
    /// </summary>
    [JsonPropertyName("reportsTracking")]
    public bool ReportsTracking { get; set; }

    [JsonPropertyName("courierName")]
    public string CourierName { get; set; } = "";

    [JsonPropertyName("awbNumber")]
    public string AwbNumber { get; set; } = "";

    /// <summary>The tracking page the provider reported for the AWB, when it reported one.</summary>
    [JsonPropertyName("trackingUrl")]
    public string TrackingUrl { get; set; } = "";

    /// <summary>
    /// What the courier billed the shop for this parcel, when the shop team recorded it with the AWB.
    /// This is the shop's own figure for the books, not the customer's, so the customer-facing reads of
    /// a parcel leave it null (see ShipmentTrackingService) and it never reaches 'My Orders'.
    /// </summary>
    [JsonPropertyName("freightCharge")]
    public decimal? FreightCharge { get; set; }

    /// <summary>The courier's own wording, shown when the shop has no wording of its own for it.</summary>
    [JsonPropertyName("providerStatus")]
    public string ProviderStatus { get; set; } = "";

    /// <summary>The provider's status code, for support (see OrderShipments.ProviderStatusCode).</summary>
    [JsonPropertyName("providerStatusCode")]
    public int? ProviderStatusCode { get; set; }

    /// <summary>The shop's own words for where the parcel is (ShipmentStatusFlow.Describe), or "".</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>The stage behind <c>Status</c>, as a name ("InTransit") - so the screens can style on it.</summary>
    [JsonPropertyName("stage")]
    public string Stage { get; set; } = nameof(ShipmentStage.Unknown);

    /// <summary>True once the parcel is delivered (the only stage with a date to show).</summary>
    [JsonPropertyName("delivered")]
    public bool Delivered { get; set; }

    /// <summary>True once the courier has nothing more to say (delivered, coming back, or called off).</summary>
    [JsonPropertyName("closed")]
    public bool Closed { get; set; }

    [JsonPropertyName("deliveredOn")]
    public DateTime? DeliveredOn { get; set; }

    /// <summary>The latest line of the tracking trail (the courier's own sentence).</summary>
    [JsonPropertyName("lastStatusText")]
    public string LastStatusText { get; set; } = "";

    /// <summary>When the courier was last asked about this parcel (null = never).</summary>
    [JsonPropertyName("lastCheckedOn")]
    public DateTime? LastCheckedOn { get; set; }

    /// <summary>The order's status after the pull, when the caller is an order screen.</summary>
    [JsonPropertyName("orderStatusId")]
    public short? OrderStatusId { get; set; }

    [JsonPropertyName("orderStatus")]
    public string? OrderStatus { get; set; }

    /// <summary>
    /// Which parcel this is (<c>OrderShipments.ShipmentID</c>), so a form can correct THIS one - its items, its
    /// freight - rather than whichever parcel happens to be found first. An order can go out in more than one
    /// parcel, and the shop team is looking at a particular one.
    /// </summary>
    [JsonPropertyName("shipmentId")]
    public long ShipmentId { get; set; }

    /// <summary>
    /// What this parcel carries: the order's units the shop team picked when they recorded it, one entry per SKU
    /// (<c>OrderShipmentItems</c>). Empty for a parcel written before the shop said what was in it, and for a
    /// reverse leg - which brings the goods back rather than taking them out. It is what the parcel's own courier
    /// bill is split across (see HC.Business.OrderItemMoneyWriter), so an empty list is read as 'not recorded
    /// yet' rather than 'nothing in it'.
    /// </summary>
    [JsonPropertyName("items")]
    public List<OrderShipmentItemDto> Items { get; set; } = new();

    /// <summary>
    /// How many parcels of this leg this answer counted, so a screen can say "this order went out in three parcels"
    /// rather than describing the first and leaving the rest to be discovered by asking. 1 for a read that only
    /// ever looked at one parcel (a single parcel is what a read without a count means), which makes the ordinary
    /// case read exactly as it always did.
    /// </summary>
    [JsonPropertyName("parcelsInLeg")]
    public int ParcelsInLeg { get; set; } = 1;
}

/// <summary>
/// What one parcel carries, as the screens show it: a SKU of the order and how many units of it are in this
/// parcel ('HC-1042 x 2'). The name of the product is deliberately not here - the order screen has the order's
/// own lines in front of it and reads the name from those, so a parcel never becomes a second, staler copy of
/// what the goods are called.
/// </summary>
public class OrderShipmentItemDto
{
    /// <summary>The order line's SKU (<c>OrderItems.SKU</c>).</summary>
    [JsonPropertyName("sku")]
    public string Sku { get; set; } = "";

    /// <summary>How many units of <see cref="Sku"/> are in this parcel.</summary>
    [JsonPropertyName("quantity")]
    public short Quantity { get; set; }
}

/// <summary>
/// What the shop team types on the admin order screen when a parcel exists or is being booked: the AWB (and, when
/// they have it, the courier, the tracking link and what the courier billed for it), and which of the order's
/// units are in it. The consignment itself is created in the provider's panel by hand - this is only this side's
/// record of it - except for a provider that has no panel (<c>IShipmentProvider.AwbGeneratedBySystem</c>), where a
/// blank <c>AwbNumber</c> means 'give this parcel the shop's own reference' and the server mints it.
///
/// One call records ONE parcel. An order can go out in more than one - each a real consignment with its own AWB
/// and its own bill - so the screen records them one at a time: <c>ShipmentId</c> names the parcel being corrected
/// when the team already has it in front of them, and a parcel that is neither named nor found by its AWB is a new
/// one (see ShipmentTrackingService.SaveAsync).
/// </summary>
public class SaveOrderShipmentRequest
{
    /// <summary>
    /// The parcel this call is about, when the screen already holds one (<c>OrderShipmentDto.ShipmentId</c>): its
    /// AWB, courier, freight charge and contents are corrected in place. Null - what every form that is booking a
    /// parcel sends - means 'find the parcel by its AWB, and if there is none, this is a new one'.
    /// </summary>
    [JsonPropertyName("shipmentId")]
    public long? ShipmentId { get; set; }

    /// <summary>
    /// Which leg this parcel is: blank (the default) is a parcel going out to the customer, and
    /// 'Reverse' is the one coming back - the pickup the shop books once a return has been approved, or
    /// the courier's own return-to-origin. Anything else is refused, because a parcel is one of those two
    /// things and no third one exists (see OrderShipment.IsValidDirection). The Return card of the admin
    /// order screen records a reverse parcel with this field; the Shipment card and the Shipped move leave
    /// it blank, exactly as before.
    /// </summary>
    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    /// <summary>
    /// Which shipping provider the parcel was booked with (<c>ShipmentProviderInfo.Name</c>). Blank
    /// means the configured default (<c>Shipping:DefaultProvider</c>), which is what a shop booking with
    /// one aggregator leaves it as.
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    /// <summary>
    /// The AWB the courier gave this parcel, typed in from the provider's panel. Blank is accepted only for
    /// a provider whose reference the system mints (see <c>IShipmentProvider.CreateAwb</c>); for any other
    /// provider a blank one is the courier's number that has not been typed in yet, and is refused.
    /// </summary>
    [JsonPropertyName("awbNumber")]
    public string AwbNumber { get; set; } = "";

    [JsonPropertyName("courierName")]
    public string? CourierName { get; set; }

    [JsonPropertyName("trackingUrl")]
    public string? TrackingUrl { get; set; }

    /// <summary>The provider's own shipment id, when the team pasted it in (optional, support only).</summary>
    [JsonPropertyName("shiprocketShipmentId")]
    public long? ShiprocketShipmentId { get; set; }

    /// <summary>
    /// What the courier billed the shop for this parcel (the provider's own freight charge), when the
    /// team has it - recorded with the AWB for the books, and never shown to the customer. Leave it null
    /// to keep the figure already recorded: the field is optional on every form, so one that does not
    /// ask for it (the Shipped move) can never wipe what the Shipment card holds.
    /// </summary>
    [JsonPropertyName("freightCharge")]
    public decimal? FreightCharge { get; set; }

    /// <summary>
    /// Which of the order's units this parcel carries, and how many of each - what the Shipment card's picker
    /// collects, and what the parcel's own bill is split across for the books
    /// (HC.Business.OrderItemMoneyWriter).
    ///
    /// Left NULL to mean "this form is not about the contents" and keep whatever the parcel already carries - which
    /// is what the Shipped move sends, so dispatching an order can never wipe the picker's work. An EMPTY list is
    /// a real answer and a different one: 'this parcel carries none of the order's units', which is refused for a
    /// forward parcel (a parcel going out with nothing in it is a parcel nobody can bill for) and allowed for a
    /// reverse leg, where the shop may simply not know what is coming back.
    ///
    /// A SKU that is not one of the order's lines is refused, and so is a quantity that would take a SKU past the
    /// units that order actually has - the parcels of an order never claim more goods than the order holds.
    /// </summary>
    [JsonPropertyName("items")]
    public List<SaveOrderShipmentItemRequest>? Items { get; set; }
}

/// <summary>
/// One entry of the Shipment card's picker: a SKU of the order and how many of its units are in this parcel.
/// </summary>
public class SaveOrderShipmentItemRequest
{
    /// <summary>The order line's SKU (<c>OrderItems.SKU</c>) exactly as the order screen shows it.</summary>
    [JsonPropertyName("sku")]
    public string Sku { get; set; } = "";

    /// <summary>How many units of <see cref="Sku"/> are in this parcel - one or more.</summary>
    [JsonPropertyName("quantity")]
    public short Quantity { get; set; } = 1;
}

/// <summary>
/// The answer of the throttled pull 'My Orders' makes when it opens: which of the customer's parcels
/// were looked up, with the fresh snapshot of each, so the page can show them without a second round
/// trip. Only the parcels that were actually stale are in <c>Shipments</c> - a page open never re-asks
/// the courier about a parcel it asked about a minute ago.
/// </summary>
public class RefreshOrderShipmentsDto
{
    [JsonPropertyName("result")]
    public int Result { get; set; }

    [JsonPropertyName("messages")]
    public string[] Messages { get; set; } = Array.Empty<string>();

    [JsonPropertyName("shipments")]
    public List<OrderShipmentDto> Shipments { get; set; } = new();
}
