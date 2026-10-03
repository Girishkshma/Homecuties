using HC.Business.Dtos;

namespace HC.Business.Shipping;

/// <summary>
/// One shipping provider the shop books and tracks its parcels with - a courier aggregator such as
/// Shiprocket, behind a single shape so the rest of the app never names one of them.
///
/// Everything provider-specific lives behind this interface: the URLs, the login and its token, the
/// field names of the answers, the vocabulary of the couriers. Everything above it works with
/// <see cref="ShipmentTrackingSnapshotDto"/> and with the shop's own wording (see
/// <see cref="ShipmentStatusFlow"/>), so a second aggregator is added by writing one more adapter and
/// registering it - no screen, no order rule and no database column has to change. Which adapters are
/// active, and which one is the default, is configuration (<c>Shipping:DefaultProvider</c> and each
/// provider's own section - see <see cref="IShipmentProviderRegistry"/>); more than one may be active
/// at once, and a parcel remembers the provider it was booked with
/// (<c>OrderShipments.Provider</c>), so the two can be used side by side.
///
/// Two kinds of provider sit side by side, and what the shop has to supply is what separates them: an
/// aggregator whose panel books the consignment and hands back the AWB (Shiprocket, and any second
/// aggregator added later - see <see cref="AwbGeneratedBySystem"/>), and a service the shop arranges
/// itself, where the parcel is real even though no API is behind it - handed over in person, or given to
/// a local courier dealt with by phone - so the reference is minted here and there is nobody to ask
/// where the parcel is (see <see cref="OrderShipment.CustomProvider"/>). Both kinds are recorded,
/// dispatched and shown through the same column and the same screens.
///
/// Two rules every adapter must keep, both deliberate:
///
///  * A lookup NEVER throws - a failed one comes back as
///    <see cref="ShipmentTrackingSnapshotDto.Succeeded"/> false with a sentence for the person waiting
///    (the same rule <see cref="IPincodeService"/> follows). Nothing in the shop is allowed to break
///    because a courier's website did.
///
///  * It decides nothing. An adapter reports the courier's own wording and the numbers that came with
///    it; what that means for the parcel and for the order is <see cref="ShipmentStatusFlow"/>'s
///    decision, never an adapter's.
///
/// The interface is deliberately small: it holds what the shop asks a provider to do today, which is to
/// say where a parcel is. A provider whose booking, label or cancellation is brought in-house later
/// grows the one method it needs here - the screen that offers it and the record it writes follow - and
/// nothing above the adapters learns which provider it was.
/// </summary>
public interface IShipmentProvider
{
    /// <summary>
    /// The adapter's stable id, written to <c>OrderShipments.Provider</c> when a parcel is recorded
    /// (e.g. "Shiprocket"). It is what ties a row in the database back to the adapter that can track it,
    /// so it must not change once parcels have been booked with it.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// False when the adapter is missing what it needs (the credentials of its own configuration
    /// section, e.g. 'Shiprocket:Email' / 'Shiprocket:Password') or was switched off. The startup log
    /// reports it, and the admin order screen marks it 'not set up' - while still offering it, because a
    /// parcel really did go out with it and recording it against the right provider matters more than
    /// the lookup working today. A lookup through such an adapter is never sent anywhere: it comes back
    /// with <c>Succeeded</c> false and one sentence for the person waiting, exactly like any other
    /// failed lookup (see <see cref="ShipmentTrackingSnapshotDto"/>).
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Who this adapter is, whether it is usable and where it talks to - shown in the startup log and
    /// offered on the admin order screen when the shop team records a parcel. Never includes
    /// credentials.
    /// </summary>
    ShipmentProviderInfo Describe();

    /// <summary>
    /// What the courier says about this AWB right now, or why it could not be asked. Never throws.
    /// </summary>
    Task<ShipmentTrackingSnapshotDto> TrackByAwbAsync(string awbNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when this provider has no panel to take a consignment number from, so the system mints the
    /// parcel's reference instead (see <see cref="CreateAwb"/>). The admin screens then offer the AWB box
    /// as optional and say one will be given, and a blank AWB is not a missing one - for every other
    /// provider a blank AWB is the courier's number that has not been typed in yet.
    /// </summary>
    bool AwbGeneratedBySystem { get; }

    /// <summary>
    /// True when this provider can be asked where a parcel is. False for a service with no tracking behind
    /// it: the pull never asks it, so nobody - the customer least of all - is handed a sentence about a
    /// lookup that was never going to happen, and the order is kept up to date by the shop team's own
    /// status moves instead.
    /// </summary>
    bool ReportsTracking { get; }

    /// <summary>
    /// The reference to record for a parcel this provider carries, minted here because the provider gives
    /// none of its own. Only ever asked when <see cref="AwbGeneratedBySystem"/> is true: an adapter whose
    /// AWB comes from the courier answers with an empty string and is never called.
    ///
    /// <paramref name="orderId"/> is the order the parcel belongs to, and one parcel row per order (see
    /// <c>OrderShipments</c>) is what keeps the reference unique. It is written to
    /// <c>OrderShipments.AwbNumber</c> and read back from there, so the screens, the customer and the order
    /// history treat it exactly like the courier's own number.
    /// </summary>
    string CreateAwb(long orderId, DateTime now);
}
