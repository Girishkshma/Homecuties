using HC.Business.Dtos;
using HC.Data.Entities;

namespace HC.Business.Shipping;

/// <summary>
/// The service the shop arranges itself: a parcel handed over in person, or given to a local courier the
/// shop deals with by phone or by hand. It is a real consignment with no API behind it, which is the whole
/// point of this adapter - before it, such an order could only be recorded as 'dispatched', with nothing
/// written down about the parcel and no number for the customer to quote.
///
/// What it deliberately does NOT do, and why:
///
///  * It never issues an AWB, because there is no panel to take one from. The reference is minted here
///    instead (<see cref="AwbGeneratedBySystem"/> + <see cref="CreateAwb"/>) - a short, readable number
///    the shop writes on the parcel and the customer can quote, e.g. 'HC261002-000123' for order 123
///    recorded on 2 October 2026. One parcel row per order is what keeps it unique, so nothing here has to
///    keep a counter or ask the database a question.
///
///  * It is never asked where a parcel is (<see cref="ReportsTracking"/> is false): there is nothing at
///    the other end, so a lookup would only write a failure down for the person waiting. The order is kept
///    up to date by the shop team's own status moves, which is what they do today anyway.
///
/// Nothing has to be configured for it - no credentials, no endpoint - so it is always
/// <see cref="IsConfigured"/> and the shop's screens always offer it.
/// </summary>
public class CustomShipmentProvider : IShipmentProvider
{
    /// <summary>
    /// What every reference minted here starts with, so one is recognisable at a glance (on a parcel, on
    /// the phone, in the order history) as the shop's own rather than a courier's.
    /// </summary>
    private const string AwbPrefix = "HC";

    /// <summary>What the shop team reads when this adapter is offered on the admin order screen.</summary>
    private const string ProviderDisplayName = "Custom courier service";

    /// <summary>
    /// The provider-neutral sentence a lookup through this adapter gets - for a caller that asks anyway
    /// ('Track now' on an old screen, or the API directly). The two screens do not offer tracking for a
    /// parcel with no courier behind it, so nobody normally sees it.
    /// </summary>
    private const string NoCourierMessage =
        "This parcel is carried by the shop's own delivery arrangement, so there is no courier to ask about " +
        "it. Move the order along from the status list when it moves.";

    /// <summary>
    /// The id a parcel recorded here is written under (<c>OrderShipments.Provider</c>), taken from the
    /// entity's own constant so the column and the adapter can never drift apart.
    /// </summary>
    public string Name => OrderShipment.CustomProvider;

    /// <summary>
    /// Always true: the service is the shop's own, so there is no credential, endpoint or switch to get
    /// wrong - unlike an aggregator, which can be registered and not usable.
    /// </summary>
    public bool IsConfigured => true;

    /// <summary>
    /// The shop's own reference, minted by <see cref="CreateAwb"/>: there is no panel that could give this
    /// parcel a consignment number.
    /// </summary>
    public bool AwbGeneratedBySystem => true;

    /// <summary>
    /// Nobody to ask - see the class summary. The two screens read this to leave tracking out for such a
    /// parcel instead of offering a button that cannot answer.
    /// </summary>
    public bool ReportsTracking => false;

    /// <summary>
    /// Who this adapter is, whether it can be used and where it talks to: no API and no tracking path,
    /// which is exactly what it is, and never any credentials (there are none to leak). What it can do
    /// (it mints the reference, it cannot be tracked) is filled in by the registry, which owns that answer
    /// for every adapter alike - see ShipmentProviderRegistry.Describe.
    /// </summary>
    public ShipmentProviderInfo Describe() => new()
    {
        Name = Name,
        DisplayName = ProviderDisplayName,
        Configured = IsConfigured,
        ApiBaseUrl = string.Empty,
        TrackingPath = string.Empty
    };

    /// <summary>
    /// The reference for this order's parcel: 'HC' + the day it was recorded (yyMMdd) + the order id, so
    /// it is short enough to read out over the phone, sorts by the day it was written down, and can never
    /// clash with another parcel - one row per order is what makes that true (see IShipmentProvider).
    /// </summary>
    public string CreateAwb(long orderId, DateTime now) => $"{AwbPrefix}{now:yyMMdd}-{orderId:D6}";

    /// <summary>
    /// This adapter is never asked, and never throws either: it answers with the one sentence that says why
    /// (<see cref="NoCourierMessage"/>), under the same rule as every other provider - a lookup that cannot
    /// be made is a sentence for the person waiting, not an exception on a page. The AWB is echoed back so
    /// a caller can never mix two parcels up.
    /// </summary>
    public Task<ShipmentTrackingSnapshotDto> TrackByAwbAsync(
        string awbNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(Failed(awbNumber, NoCourierMessage));
    }

    /// <summary>The same shape every adapter answers a failed lookup with (see ShiprocketShipmentProvider).</summary>
    private static ShipmentTrackingSnapshotDto Failed(string awb, string message) => new()
    {
        Succeeded = false,
        Message = message,
        AwbNumber = awb
    };
}
