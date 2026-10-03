namespace HC.Business;

/// <summary>
/// Why an order is being sent back - <c>OrderReturns.ReasonCode</c>, alongside the asker's own words in
/// <c>Reason</c>. A coded reason is what lets the shop count returns by cause later ("most returns are
/// size") without reading free text, and is what the storefront's picker and the server's validation
/// both use, so a caller can never write a reason the screen does not offer.
///
/// <see cref="CustomerReasons"/> is the picker 'My Orders' shows on a delivered order; the courier has no
/// picker at all - its two reasons are written by the tracking pull when the parcel's own status says so
/// (see <see cref="HC.Business.ShipmentStatusFlow"/>), for an ask that only ever asks
/// (see <see cref="OrderReturnOrigin.Courier"/>).
/// </summary>
public static class OrderReturnReason
{
    // What the customer picks from.

    /// <summary>Changed their mind (the wrong size, no longer wanted, ordered twice).</summary>
    public const string NotNeeded = "NotNeeded";

    /// <summary>The wrong item arrived (a different product, size or colour than ordered).</summary>
    public const string WrongItem = "WrongItem";

    /// <summary>It arrived damaged or broken.</summary>
    public const string Damaged = "Damaged";

    /// <summary>It is not what the product page said it would be.</summary>
    public const string NotAsDescribed = "NotAsDescribed";

    /// <summary>Anything else - the words in <c>Reason</c> carry it.</summary>
    public const string Other = "Other";

    // What the courier reports.

    /// <summary>The customer refused the parcel at the doorstep.</summary>
    public const string RefusedAtDoor = "RefusedAtDoor";

    /// <summary>The courier could not deliver it and the parcel is coming back (RTO).</summary>
    public const string ReturnedToOrigin = "ReturnedToOrigin";

    /// <summary>The reasons a customer may ask with - the storefront's picker, in the order shown.</summary>
    public static readonly string[] CustomerReasons = { NotNeeded, WrongItem, Damaged, NotAsDescribed, Other };

    /// <summary>True when this is a reason a customer may send - the server accepts nothing else.</summary>
    public static bool IsCustomerReason(string? reasonCode) =>
        reasonCode != null && CustomerReasons.Contains(reasonCode);

    /// <summary>True when this is one of the two reasons the courier's own tracking reports.</summary>
    public static bool IsCourierReason(string? reasonCode) =>
        reasonCode is RefusedAtDoor or ReturnedToOrigin;

    /// <summary>
    /// The wording shown for a reason: what the order history and the admin order screen write next to the
    /// asker's own words. It lives here, beside the codes, so no server-side text has to invent its own
    /// name for a reason. (The storefront's picker spells its own labels, because it has to offer the
    /// choices before an ask exists - see 'My Orders' - and the codes are the contract between the two.)
    /// </summary>
    public static string Label(string? reasonCode) => reasonCode switch
    {
        NotNeeded => "No longer needed",
        WrongItem => "Wrong item sent",
        Damaged => "Arrived damaged",
        NotAsDescribed => "Not as described",
        Other => "Other reason",
        RefusedAtDoor => "Refused at the door",
        ReturnedToOrigin => "Returned to sender (could not be delivered)",
        _ => "Return"
    };
}
