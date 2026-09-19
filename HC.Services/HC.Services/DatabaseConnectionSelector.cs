using Microsoft.Extensions.Configuration;

namespace HC.Services;

/// <summary>
/// Resolves which entry of the "ConnectionStrings" configuration section the application uses.
/// The active entry is selected by the "Database:Provider" setting, which accepts either a short
/// provider name ("Local", "Remote") or the full connection string key ("LocalConnection",
/// "RemoteConnection"). Because the setting is read from <see cref="IConfiguration"/> it can be
/// switched without changing code, e.g. via appsettings.json ("Database": { "Provider": "Local" }),
/// a launch profile, the environment variable "Database__Provider" or the command line argument
/// "--Database:Provider=Local".
/// </summary>
public static class DatabaseConnectionSelector
{
    /// <summary>Configuration key that selects the active connection string.</summary>
    public const string ProviderConfigurationKey = "Database:Provider";

    /// <summary>Connection string key for the local SQL Server Express instance.</summary>
    public const string LocalConnectionName = "LocalConnection";

    /// <summary>Connection string key for the remote SQL Server instance.</summary>
    public const string RemoteConnectionName = "RemoteConnection";

    /// <summary>Legacy key kept working for configuration files that predate the provider switch.</summary>
    public const string LegacyConnectionName = "DefaultConnection";

    private const string ConnectionNameSuffix = "Connection";

    /// <summary>
    /// Returns the "ConnectionStrings" key that has to be used: the entry selected by
    /// "Database:Provider" (which may be given as "Local"/"Remote" or as the full key) or, when that
    /// entry is not configured, the legacy "DefaultConnection" entry.
    /// Falls back to <see cref="RemoteConnectionName"/> when no provider is configured.
    /// </summary>
    public static string ResolveConnectionName(IConfiguration configuration)
    {
        var provider = configuration[ProviderConfigurationKey];

        var connectionName = string.IsNullOrWhiteSpace(provider)
            ? RemoteConnectionName
            : Normalize(provider);

        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString(connectionName)))
        {
            return connectionName;
        }

        // Older appsettings.json files only define "DefaultConnection"; keep them usable.
        return string.IsNullOrWhiteSpace(configuration.GetConnectionString(LegacyConnectionName))
            ? connectionName
            : LegacyConnectionName;
    }

    private static string Normalize(string provider)
    {
        provider = provider.Trim();

        return provider.EndsWith(ConnectionNameSuffix, StringComparison.OrdinalIgnoreCase)
            ? provider
            : provider + ConnectionNameSuffix;
    }
}
