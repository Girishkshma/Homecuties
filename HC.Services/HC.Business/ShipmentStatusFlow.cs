namespace HC.Business;

/// <summary>
/// Where a parcel has got to, in the shop's own words - the only thing the rest of the app asks
/// about a shipment ('where is it?', 'is it over?', 'may the order follow it?').
///
/// The courier's own wording is what we store (OrderShipments.ProviderStatus) and this enum is derived
/// from it, never the other way round: the provider renames and adds statuses, so the mapping reads
/// the text and anything unrecognised simply stays <see cref="Unknown"/> - a new courier wording must
/// never be able to move an order to the wrong place.
/// </summary>
public enum ShipmentStage
{
    /// <summary>Nothing readable came back (or nothing has been asked yet).</summary>
    Unknown = 0,

    /// <summary>AWB assigned / label or manifest generated - the parcel is booked but still with us.</summary>
    Booked,

    /// <summary>The courier has it and it is moving.</summary>
    InTransit,

    /// <summary>On the delivery van today.</summary>
    OutForDelivery,

    /// <summary>Handed to the customer - the end of the parcel's journey.</summary>
    Delivered,

    /// <summary>A failed attempt: the parcel is still with the courier and will be tried again.</summary>
    Undelivered,

    /// <summary>Return to origin - the parcel is on its way back to us.</summary>
    Rto,

    /// <summary>The provider (or the courier behind it) called the shipment off; it never travelled.</summary>
    Cancelled
}

/// <summary>
/// The courier's wording to the shop's own answers: which <see cref="ShipmentStage"/> a tracking
/// status means, what it is called on screen, and the one order status a stage may move the order to.
///
/// Two rules keep this safe, and both are deliberate:
///
///  * The mapping only ever reads the courier's TEXT ('Out for Delivery'), never its numeric status
///    code - the numbers are the provider's business and are kept on the shipment row for support
///    only. An unrecognised wording is <see cref="ShipmentStage.Unknown"/>, which moves nothing.
///
///  * A courier status can advance an order (the parcel was picked up => Shipped, delivered =>
///    Delivered) but can never cancel one and can never start a refund. 'Returned to origin' and a
///    failed delivery are shown to the shop team as they are; writing an order off (and the money with
///    it, see HC.Business.RazorpayRefunds) stays a human decision made on the admin order screen.
///
/// The moves themselves are still the order lifecycle's (see <see cref="OrderStatusFlow.CanMove"/>):
/// a stage only proposes, and the chain decides - so an unpaid order is never shipped by a courier
/// scan, and a delivered parcel can never rewind an order that is already delivered or cancelled.
/// </summary>
public static class ShipmentStatusFlow
{
    /// <summary>
    /// The stage the courier's own status wording means. Unknown wording (or none at all) is
    /// <see cref="ShipmentStage.Unknown"/> - never a guess.
    /// </summary>
    public static ShipmentStage FromProviderText(string? providerStatus)
    {
        if (string.IsNullOrWhiteSpace(providerStatus))
            return ShipmentStage.Unknown;

        // Compared on letters and digits only, so 'RTO-Initiated', 'rto initiated' and
        // 'RTO Initiated' are one wording.
        var text = Normalize(providerStatus);

        // Most specific first. Reading them in this order is the whole trick: 'RTO Delivered' is on its
        // way back to us (not into the customer's hands) and 'Undelivered' contains 'delivered'.
        if (ContainsAny(text, "rto", "return to origin", "returned to origin", "returning to origin",
                "return to sender", "returned to sender"))
        {
            return ShipmentStage.Rto;
        }

        if (ContainsAny(text, "cancel"))
            return ShipmentStage.Cancelled;

        if (ContainsAny(text, "undelivered", "not delivered", "delivery attempt", "delivery failed",
                "delivery refused", "refused delivery", "consignee refused"))
        {
            return ShipmentStage.Undelivered;
        }

        if (ContainsAny(text, "out for delivery", "ofd"))
            return ShipmentStage.OutForDelivery;

        if (ContainsAny(text, "delivered"))
            return ShipmentStage.Delivered;

        if (ContainsAny(text, "in transit", "intransit", "transit", "picked up", "picked", "dispatched",
                "shipped", "reached", "arrived", "hub", "outscan", "forwarded"))
        {
            return ShipmentStage.InTransit;
        }

        // Booked but not collected yet: the parcel is still in the shop, so the order has not left it.
        if (ContainsAny(text, "awb assigned", "awb generated", "label generated", "manifest",
                "pickup scheduled", "pickup generated", "pickup pending", "pickup error",
                "pickup failed", "pickup rescheduled", "ready to ship", "booked", "shipment created"))
        {
            return ShipmentStage.Booked;
        }

        return ShipmentStage.Unknown;
    }

    /// <summary>
    /// What 'My Orders' and the admin order screen call this stage, or "" when the stage says nothing
    /// useful (the courier's own wording is shown instead - see <see cref="FromProviderText"/>).
    /// </summary>
    public static string Describe(ShipmentStage stage) => stage switch
    {
        ShipmentStage.Booked => "Parcel booked - waiting for pickup",
        ShipmentStage.InTransit => "On the way",
        ShipmentStage.OutForDelivery => "Out for delivery",
        ShipmentStage.Delivered => "Delivered",
        ShipmentStage.Undelivered => "Delivery attempted - the courier will try again",
        ShipmentStage.Rto => "Returning to the shop",
        ShipmentStage.Cancelled => "Pickup cancelled",
        _ => string.Empty
    };

    /// <summary>
    /// The order status a courier stage may move an order to, or null when the courier has no business
    /// changing it.
    ///
    /// Only two things are proposed: a parcel that is actually moving (in transit / out for delivery)
    /// makes the order Shipped, and a delivered parcel makes it Delivered. Nothing else moves an order -
    /// a booked-but-not-collected parcel is NOT 'Shipped' (a label printed is not a dispatch, and the
    /// storefront shows 'Parcel booked' anyway), and an exception, an RTO or a cancelled pickup is the
    /// shop team's call, not the courier's.
    ///
    /// Whether the move is allowed at all is still the order lifecycle's decision
    /// (<see cref="OrderStatusFlow.CanMove"/>), which is what keeps an unpaid order, an already
    /// delivered order and a cancelled order exactly where they are.
    /// </summary>
    public static short? OrderStatusFor(ShipmentStage stage) => stage switch
    {
        ShipmentStage.InTransit or ShipmentStage.OutForDelivery => OrderStatusFlow.Shipped,
        ShipmentStage.Delivered => OrderStatusFlow.Delivered,
        _ => null
    };

    /// <summary>
    /// Where a parcel the shop carries itself has got to, from the order status the shop team has just moved
    /// the order to. Nobody is ever going to report on such a parcel (see
    /// <see cref="IShipmentProvider.ReportsTracking"/>), so the shop's own move IS the parcel's movement -
    /// and reading it here means both screens describe that parcel in the one vocabulary a courier's status
    /// is described in, instead of it standing still forever.
    ///
    /// Only the two moves <see cref="OrderStatusFor"/> proposes for a real courier are mapped: a dispatched
    /// order puts its parcel on the way and a delivered order ends its journey. Everything else is null, and
    /// deliberately so - an order that is only Confirmed has no parcel out yet, and a cancelled order's
    /// parcel is left exactly as it was recorded, because 'Pickup cancelled' is not what happened to a parcel
    /// the shop delivered itself (this is the mapping that would have to lie to say otherwise).
    /// </summary>
    public static ShipmentStage? OwnDeliveryStageFor(short orderStatusId) => orderStatusId switch
    {
        OrderStatusFlow.Shipped => ShipmentStage.InTransit,
        OrderStatusFlow.Delivered => ShipmentStage.Delivered,
        _ => null
    };

    /// <summary>
    /// What is written on a parcel the shop carries itself for the stage
    /// <see cref="OwnDeliveryStageFor"/> worked out: the shop's own move, in the words the parcel is stored
    /// with, so that everything which reads a parcel keeps reading one kind of thing.
    ///
    /// It lives here, beside <see cref="FromProviderText"/> - the only thing that ever reads it - because the
    /// two are one idea: a stage is written down as this text and read back out of it, so a status written
    /// here and a stage read later can never drift apart. The round trip is checked (see the shipping
    /// harness), so a wording that stopped meaning what it says would be caught rather than believed.
    /// </summary>
    public static string OwnDeliveryText(ShipmentStage stage) => stage switch
    {
        ShipmentStage.InTransit => "Dispatched through the shop's own delivery",
        ShipmentStage.Delivered => "Delivered through the shop's own delivery",
        _ => string.Empty
    };

    /// <summary>
    /// True once there is nothing left to learn from the courier, so the tracking pull stops asking: a
    /// delivered parcel, one coming back, or a pickup that was called off. A failed delivery is NOT
    /// closed - the courier tries again, and that next attempt is exactly what 'My Orders' waits for.
    /// </summary>
    public static bool IsClosed(ShipmentStage stage) => stage
        is ShipmentStage.Delivered or ShipmentStage.Rto or ShipmentStage.Cancelled;

    /// <summary>
    /// True while an order in this status can still follow its parcel, so a tracking pull is worth
    /// making for it: a Confirmed order whose parcel was collected becomes Shipped, and a Shipped one
    /// becomes Delivered. Nothing else - an unpaid order has not left the shop and the courier is not
    /// allowed to ship it, and Delivered and Cancelled are the end of the line. This is the 'is it worth
    /// asking?' question (<see cref="OrderStatusFlow.CanMove"/> still decides each move).
    /// </summary>
    public static bool CanOrderFollow(short orderStatusId) =>
        OrderStatusFlow.CanMove(orderStatusId, OrderStatusFlow.Shipped) ||
        OrderStatusFlow.CanMove(orderStatusId, OrderStatusFlow.Delivered);

    /// <summary>
    /// When the parcel was delivered: the date the courier reported, or now when the courier said
    /// 'Delivered' without a usable date. Null for every other stage, so this column can never claim a
    /// parcel arrived when it did not.
    /// </summary>
    public static DateTime? DeliveredOn(ShipmentStage stage, DateTime? providerDate, DateTime now) =>
        stage == ShipmentStage.Delivered ? providerDate ?? now : null;

    /// <summary>Lower case, letters and digits only, single spaces - so wording differences do not matter.</summary>
    private static string Normalize(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        var lastWasSpace = true;

        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static bool ContainsAny(string text, params string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            if (text.Contains(phrase, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
