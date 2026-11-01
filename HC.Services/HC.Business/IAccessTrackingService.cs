using HC.Business.Dtos;

namespace HC.Business;

/// <summary>
/// Where the shop's portals are being read from, and how often - the tracking this shop keeps instead of
/// analytics about people.
///
/// The write side is one call per page view (<see cref="TrackAsync"/>): the caller's address is read off the
/// connection by the API and the place it is in is looked up server-side, so what is recorded is a place and a
/// time and nothing that says who. A visitor who agrees to share their position adds one thing to that - the
/// point their device reports, rounded and checked (<see cref="AccessTrackingRules.ResolveSharedPoint"/>) and
/// written beside the place rather than in place of it; a visitor who declines adds nothing and their page views
/// are recorded exactly as they always were. The read side answers the admin screen - the period's figures and
/// roll-ups (<see cref="GetReportAsync"/>), the raw hits behind them (<see cref="GetLogAsync"/>) - and the
/// housekeeping that keeps the table from growing without end (<see cref="PurgeAsync"/>).
/// </summary>
public interface IAccessTrackingService
{
    /// <summary>
    /// Records one portal access: where the caller's address is, which page was opened, and when.
    ///
    /// Never throws and never refuses a page view: a location lookup that is down, an address that is private
    /// and a database that cannot be written to all answer with a result the caller may show or ignore -
    /// tracking is not worth a customer's page.
    /// </summary>
    /// <param name="request">
    /// What the shop front sent - the page and the portal, plus the point the visitor agreed to share when they
    /// did. No claimed place: the place is the API's own answer about the caller's address.
    /// </param>
    /// <param name="callerIp">The caller's address, as the API read it off the connection (X-Forwarded-For first).</param>
    Task<ResultDto> TrackAsync(AccessTrackingRequestDto? request, string? callerIp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Where the period's hits came from: the totals, the top countries, regions and cities, the per-portal
    /// split and the frequency chart's slices. An empty period is the month to date (see
    /// <see cref="OrderMoney.ResolvePeriod"/>, the rule the finance screen reads its period by).
    /// </summary>
    Task<PortalAccessReportDto> GetReportAsync(DateOnly? from, DateOnly? to, string? portal, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same roll-up cut to one size - the countries, the states or the cities of the period, most hits
    /// first - for the dashboard's tile, which shows one list rather than three.
    ///
    /// An empty period is this week (Monday to today, see <see cref="AccessTrackingRules.ResolvePlaceDays"/>),
    /// which is what the tile opens on; a period that is given is trimmed exactly as the report trims it.
    /// </summary>
    /// <param name="dimension">'country', 'region'/'state' or 'city'; anything else is the country.</param>
    /// <param name="limit">How many places to keep (the tile's handful when nothing is said).</param>
    Task<PortalAccessPlacesDto> GetPlacesAsync(string? dimension, DateOnly? from, DateOnly? to, int? limit, CancellationToken cancellationToken = default);

    /// <summary>One page of the raw hits behind the report, newest first, optionally filtered by portal or place.</summary>
    Task<PortalAccessLogPageDto> GetLogAsync(DateOnly? from, DateOnly? to, string? portal, string? country, string? region, string? city, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the hits older than the given number of days, in batches, and says how many it removed. The
    /// retention is clamped to a sane range (a month to ten years) so a slip of a digit cannot empty the table.
    /// </summary>
    Task<ResultDto> PurgeAsync(int olderThanDays, CancellationToken cancellationToken = default);
}
