namespace HC.Business.Geo;

/// <summary>
/// How the location lookup is set up, read once at startup from 'GeoLookup' in appsettings.json (see
/// HC.Services/Program.cs, which builds this and registers the lookup with it).
///
/// 'Enabled' false stops every lookup without touching a row: hits are still recorded with their address and
/// the time they were seen, and the report says how many of them have no place - which is what a shop that
/// does not want to call a third-party service at all wants.
/// 'BaseUrl' is the lookup's own address, so a mirror or a paid provider can replace the free one without a
/// rebuild ('json/{ip}?fields=...' is appended to it).
/// 'Timeout' is short on purpose: a page's own view is never made to wait on an answer about where it is
/// being read from.
/// </summary>
public sealed class GeoLookupSettings
{
    /// <summary>The free lookup this tracker is written against - no key, and answered from a cache.</summary>
    public const string DefaultBaseUrl = "http://ip-api.com/";

    public bool Enabled { get; init; } = true;

    public string BaseUrl { get; init; } = DefaultBaseUrl;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The address to ask about in place of the caller's when the caller's own can never be a place - a
    /// developer's 'localhost', which arrives as '::1', or an office network's '192.168.x.x' (see
    /// <see cref="IpAddresses.IsPublic"/>). It is filled only while the shop is being developed
    /// (HC.Services/Program.cs reads it in Development and nowhere else), so a deployed shop never files its
    /// visitors anywhere but where they are - and the hit still records the address it really came from, so
    /// nothing is written that did not happen.
    /// </summary>
    public string? DevelopmentAddress { get; init; }
}
