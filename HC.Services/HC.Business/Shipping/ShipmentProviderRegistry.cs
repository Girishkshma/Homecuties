using HC.Business.Dtos;
using Microsoft.Extensions.Configuration;

namespace HC.Business.Shipping;

/// <summary>
/// The registry, built from what DI was given. It holds no state of its own beyond the list of adapters
/// and the configured default name, so it is as cheap to create as the request that needs it (see the
/// registration in HC.Services/Program.cs, which passes every <see cref="IShipmentProvider"/> in the
/// container).
/// </summary>
public class ShipmentProviderRegistry : IShipmentProviderRegistry
{
    /// <summary>The configuration key naming the provider a parcel is recorded against by default.</summary>
    public const string DefaultProviderKey = "Shipping:DefaultProvider";

    private readonly IReadOnlyList<IShipmentProvider> _providers;
    private readonly string _configuredDefaultName;

    public ShipmentProviderRegistry(IEnumerable<IShipmentProvider> providers, IConfiguration configuration)
    {
        _providers = providers.ToList();
        _configuredDefaultName = (configuration[DefaultProviderKey] ?? string.Empty).Trim();
    }

    public IReadOnlyList<IShipmentProvider> All => _providers;

    public IShipmentProvider? Default =>
        Find(_configuredDefaultName)
        ?? _providers.FirstOrDefault(p => p.IsConfigured)
        ?? _providers.FirstOrDefault();

    public bool HasAnyConfigured => _providers.Any(p => p.IsConfigured);

    public IShipmentProvider? Find(string? providerName)
    {
        var name = (providerName ?? string.Empty).Trim();

        if (name.Length == 0)
            return null;

        return _providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<ShipmentProviderInfo> Describe()
    {
        var defaultName = Default?.Name ?? string.Empty;

        return _providers
            .Select(provider =>
            {
                var info = provider.Describe();

                // The registry owns the questions every adapter would otherwise have to answer about the
                // shop's setup rather than about itself: which provider is the default, and what the
                // provider can do at all (an adapter that mints its own references, or one with no courier
                // behind it to ask). Set here so a new adapter cannot forget to say - the screens read them
                // off the list they are already looking at (see ShipmentProviderInfo).
                info.IsDefault = string.Equals(provider.Name, defaultName, StringComparison.OrdinalIgnoreCase);
                info.AwbGeneratedBySystem = provider.AwbGeneratedBySystem;
                info.ReportsTracking = provider.ReportsTracking;

                return info;
            })
            .ToList();
    }
}
