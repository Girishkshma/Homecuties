using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HC.Business.Geo;

/// <summary>
/// Answers "where is this address?" from the free lookup the shop uses ('GeoLookup:BaseUrl' - ip-api.com's
/// keyless endpoint unless it is pointed somewhere else), and remembers every answer it gets.
///
/// Three things shape this class:
///   - An address is in one place for a long time, so an answer is reused for a month and a visitor who comes
///     back every day costs one call, not thirty. An address the lookup knows nothing about is remembered for
///     hours rather than a month, because a mobile network's address does move.
///   - A page view must never be refused, slowed down or lost because a third party is unavailable. Nothing
///     here throws: a timeout, a refused connection, a rate limit or a shape this code does not recognise all
///     answer 'no location', and the hit is stored with its address and time alone.
///   - Addresses that cannot be places - loopback, private networks, carrier-grade NAT - are never sent at all
///     (see <see cref="IpAddresses.IsPublic"/>): a developer's localhost is not a lookup. The one exception is
///     the stand-in address a shop may name while it is being developed ('GeoLookup:DevelopmentAddress', read in
///     Development only - see HC.Services/Program.cs), which is asked about in such an address's place so the
///     Portal Access screen can be read before the shop is deployed.
/// </summary>
public sealed class IpGeoLocator : IIpGeoLocator
{
    /// <summary>A located address stays located: a month's reuse is the same answer, one call.</summary>
    private static readonly TimeSpan FoundLifetime = TimeSpan.FromDays(30);

    /// <summary>The lookup answered but had no place for it (a reserved address, a satellite link).</summary>
    private static readonly TimeSpan UnknownLifetime = TimeSpan.FromHours(6);

    /// <summary>The lookup could not be asked at all - tried again soon, but not on every single hit.</summary>
    private static readonly TimeSpan RetryLifetime = TimeSpan.FromMinutes(5);

    /// <summary>How many addresses are remembered at once - expired entries are dropped first.</summary>
    private const int CacheLimit = 20000;

    /// <summary>Answers already fetched, shared by every request (this class is created per request).</summary>
    private static readonly ConcurrentDictionary<string, CachedLocation> Answers = new();

    private readonly HttpClient _http;
    private readonly GeoLookupSettings _settings;
    private readonly ILogger<IpGeoLocator> _logger;

    /// <summary>The HttpClient comes from the container, so its base address and timeout are set once.</summary>
    public IpGeoLocator(HttpClient http, GeoLookupSettings settings, ILogger<IpGeoLocator> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;
    }

    public async Task<IpGeoLocation?> LocateAsync(string? ip, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            return null;

        var address = AddressToLocate(ip);

        if (address is null)
            return null;

        if (Answers.TryGetValue(address, out var cached) && cached.ExpiresOn > DateTimeOffset.UtcNow)
            return cached.Location;

        var (location, lifetime) = await FetchAsync(address, cancellationToken);
        Remember(address, location, lifetime);

        return location;
    }

    /// <summary>
    /// The address the lookup is asked about: the caller's own when the world can be asked about it (see
    /// <see cref="IpAddresses.IsPublic"/>), and the stand-in address a shop names while it is being developed
    /// when the caller's own is nowhere (a browser on the developer's machine arrives as '::1'). The hit itself
    /// still records the address it came from - only the question is changed, and only in Development, where
    /// Program.cs is the only place that fills the stand-in in.
    /// </summary>
    private string? AddressToLocate(string? ip)
    {
        if (IpAddresses.IsPublic(ip))
            return IpAddresses.Normalize(ip);

        return IpAddresses.IsPublic(_settings.DevelopmentAddress)
            ? IpAddresses.Normalize(_settings.DevelopmentAddress)
            : null;
    }

    /// <summary>Asks the lookup about one address; answers the place (or null) and how long to keep the answer.</summary>
    private async Task<(IpGeoLocation? Location, TimeSpan Lifetime)> FetchAsync(string address, CancellationToken cancellationToken)
    {
        try
        {
            var path = $"json/{address}?fields=status,message,country,countryCode,regionName,city,org";

            using var response = await _http.GetAsync(path, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "The location lookup answered {StatusCode} for {Address}; the hit is stored without a place.",
                    (int)response.StatusCode,
                    address);

                return (null, RetryLifetime);
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            // The lookup says so itself when it has no place for an address ('fail' plus a message), which is
            // an answer and not a fault: there is no point asking again in five minutes.
            if (!string.Equals(Text(root, "status"), "success", StringComparison.OrdinalIgnoreCase))
                return (null, UnknownLifetime);

            var country = Text(root, "country");

            if (country.Length == 0)
                return (null, UnknownLifetime);

            var location = new IpGeoLocation(
                City: NullIfEmpty(Text(root, "city")),
                Region: NullIfEmpty(Text(root, "regionName")),
                Country: country,
                CountryCode: NullIfEmpty(Text(root, "countryCode")),
                Organization: NullIfEmpty(Text(root, "org")));

            return (location, FoundLifetime);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            // A timeout, a refused connection, a body that is not JSON, an address the client will not send:
            // all of them mean the same thing to a page view - there is no place to write this time.
            _logger.LogDebug(
                exception,
                "The location lookup could not be asked about {Address}; the hit is stored without a place.",
                address);

            return (null, RetryLifetime);
        }
    }

    private static void Remember(string address, IpGeoLocation? location, TimeSpan lifetime)
    {
        if (Answers.Count >= CacheLimit)
            DropExpired();

        Answers[address] = new CachedLocation(location, DateTimeOffset.UtcNow.Add(lifetime));
    }

    private static void DropExpired()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in Answers)
        {
            if (entry.Value.ExpiresOn <= now)
                Answers.TryRemove(entry.Key, out _);
        }
    }

    /// <summary>A property of the lookup's JSON as trimmed text - empty when missing or not a string.</summary>
    private static string Text(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? (value.GetString() ?? string.Empty).Trim()
            : string.Empty;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    /// <summary>An answer from the lookup and the moment it stops being reused.</summary>
    private sealed record CachedLocation(IpGeoLocation? Location, DateTimeOffset ExpiresOn);
}

