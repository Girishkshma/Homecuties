namespace HC.Business.Geo;

/// <summary>
/// Where an address is, as the lookup service knows it: the country, the region of that country, the city and
/// the network the address belongs to. Never who - there is no name, no customer, no device in this answer,
/// and the record it is written to has no column for one.
/// </summary>
/// <param name="City">The city, or null when the lookup does not name one.</param>
/// <param name="Region">The state or region ('Tamil Nadu'), or null when the lookup does not name one.</param>
/// <param name="Country">The country ('India') - the one part of an answer that is always present.</param>
/// <param name="CountryCode">The country's ISO 3166-1 alpha-2 code ('IN').</param>
/// <param name="Organization">The network the address belongs to (an internet provider, a crawler, an office).</param>
public sealed record IpGeoLocation(
    string? City,
    string? Region,
    string? Country,
    string? CountryCode,
    string? Organization);

/// <summary>
/// The one thing the tracker asks of the outside world: where is this address?
///
/// It answers null rather than throwing when it cannot say - the address is private, the lookup is switched
/// off, the network is down, the provider is rate-limiting - because a page view is never refused and never
/// made to wait on an answer about itself. What was recorded without a place is counted by the report, so a
/// lookup that stays down is visible instead of silent.
/// </summary>
public interface IIpGeoLocator
{
    /// <summary>
    /// The place the given address is in, or null when there is nothing to say about it. Answers are cached
    /// (an address is in one place for a long time), so asking twice about the same visitor costs one call.
    /// </summary>
    Task<IpGeoLocation?> LocateAsync(string? ip, CancellationToken cancellationToken = default);
}
