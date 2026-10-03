using HC.Business.Dtos;

namespace HC.Business.Shipping;

/// <summary>
/// The shipping providers that are actually wired up, and which one is the default. This is the one
/// place that knows there is more than one of them: the tracking service (and through it the two order
/// screens) asks the registry for the adapter that belongs to a parcel, rather than depending on an
/// adapter directly.
///
/// Adapters are registered in DI (see HC.Services/Program.cs); which of them the shop actually uses is
/// configuration, so a provider can be added, switched off or promoted to the default without a code
/// change and without a release:
///
///   "Shipping": { "DefaultProvider": "Shiprocket" }
///
/// The default is what a parcel is recorded against when the shop team names no provider ('Shipping:
/// DefaultProvider', else the first provider that is configured, else the first one registered), and it
/// is what a parcel falls back to on the tracking pull when the provider it was booked with is no longer
/// registered - so removing an adapter from configuration never leaves an existing parcel untrackable.
/// </summary>
public interface IShipmentProviderRegistry
{
    /// <summary>Every registered provider, in registration order (configured or not).</summary>
    IReadOnlyList<IShipmentProvider> All { get; }

    /// <summary>
    /// The provider used when none is named, or null when no provider is registered at all. Prefers
    /// 'Shipping:DefaultProvider' when that name is registered, otherwise the first configured provider,
    /// otherwise the first registered one.
    /// </summary>
    IShipmentProvider? Default { get; }

    /// <summary>True when at least one registered provider has everything it needs to be called.</summary>
    bool HasAnyConfigured { get; }

    /// <summary>
    /// The named provider, or null when the name is blank or is not registered. Matching ignores case,
    /// because the name travels through configuration, a database column and a browser.
    /// </summary>
    IShipmentProvider? Find(string? providerName);

    /// <summary>What each registered provider is, for the startup log and the admin order screen.</summary>
    IReadOnlyList<ShipmentProviderInfo> Describe();
}
