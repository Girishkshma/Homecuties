using HC.Business.Dtos;
using HC.Business.Geo;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HC.Business;

/// <summary>
/// The portal access tracker: one row per page view saying where the reader's address is and when it was
/// seen - never who.
///
/// This file holds what a hit costs (the write) and what keeps the table from growing without end (the
/// purge); the reads the admin screen makes are in 'AccessTrackingService.Report.cs' and
/// 'AccessTrackingService.Log.cs', and the decisions that are neither are pinned in
/// <see cref="AccessTrackingRules"/> and <see cref="IpAddresses"/>, where the tests can reach them.
/// </summary>
public partial class AccessTrackingService : IAccessTrackingService
{
    /// <summary>How many rows one purge statement removes - a batch small enough to not hold the table.</summary>
    private const int PurgeBatchSize = 5000;

    /// <summary>How many batches one purge call runs before it reports what it did (1,000,000 rows).</summary>
    private const int PurgeMaxBatches = 200;

    /// <summary>How long the shop keeps its access history when nobody says otherwise (a year).</summary>
    private const int RetentionDefaultDays = 365;

    /// <summary>Never keep less than a month: a report reads the month to date, and it must have one.</summary>
    private const int RetentionMinDays = 30;

    /// <summary>Ten years is the ceiling - beyond that the screen's own period cannot reach the rows.</summary>
    private const int RetentionMaxDays = 3650;

    private readonly HomecutiesDbContext _context;
    private readonly IIpGeoLocator _geo;
    private readonly ILogger<AccessTrackingService> _logger;

    public AccessTrackingService(HomecutiesDbContext context, IIpGeoLocator geo, ILogger<AccessTrackingService> logger)
    {
        _context = context;
        _geo = geo;
        _logger = logger;
    }

    public async Task<ResultDto> TrackAsync(
        AccessTrackingRequestDto? request,
        string? callerIp,
        CancellationToken cancellationToken = default)
    {
        // The address is the one the API read off the connection and never one the browser claimed, so a
        // visitor cannot file themselves in a country they are not in.
        var address = IpAddresses.Normalize(callerIp);

        // What the browser did say is the page it opened, which no header carries for the first request of a
        // session ('Referer' names the page before it). It is only ever displayed, so it is cleaned rather
        // than trusted.
        var pageUrl = AccessTrackingRules.CleanPageUrl(request?.PageUrl) ?? AccessTrackingRules.UnknownPage;
        var portal = AccessTrackingRules.PortalName(request?.Portal);

        var location = await LocateSafelyAsync(address, cancellationToken);

        var tracking = new AccessTracking
        {
            Ip = address,
            Url = pageUrl,
            AccessOn = DateTime.UtcNow,
            Portal = portal,
            City = AccessTrackingRules.Clip(location?.City, AccessTrackingRules.MaxPlaceLength),
            Region = AccessTrackingRules.Clip(location?.Region, AccessTrackingRules.MaxPlaceLength),
            Country = AccessTrackingRules.Clip(location?.Country, AccessTrackingRules.MaxPlaceLength),
            CountryCode = AccessTrackingRules.Clip(location?.CountryCode, 2),
            Organization = AccessTrackingRules.Clip(location?.Organization, AccessTrackingRules.MaxOrganizationLength)
        };

        try
        {
            _context.AccessTrackings.Add(tracking);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Tracking is not worth a page: a database that refuses the row is logged and answered, not thrown
            // back at the browser that is drawing the shop.
            _logger.LogWarning(exception, "A portal access from {Address} could not be recorded.", address);

            return new ResultDto
            {
                Result = 0,
                Messages = new[] { "The access could not be recorded." }
            };
        }

        return new ResultDto
        {
            Result = 1,
            Messages = new[] { "Access tracked" }
        };
    }

    public async Task<ResultDto> PurgeAsync(int olderThanDays, CancellationToken cancellationToken = default)
    {
        var days = Math.Clamp(
            olderThanDays <= 0 ? RetentionDefaultDays : olderThanDays,
            RetentionMinDays,
            RetentionMaxDays);

        var cutoff = DateTime.UtcNow.Date.AddDays(-days);
        var removed = 0;

        // Batched, because a year of traffic deleted in one statement holds the table - and the screens that
        // write to it - for as long as the delete runs. The batch size is a constant of this class (never a
        // caller's number), and the cutoff is a parameter.
        var sql = $"DELETE TOP ({PurgeBatchSize}) FROM dbo.AccessTracking WHERE AccessOn < {{0}}";

        for (var batch = 0; batch < PurgeMaxBatches; batch++)
        {
            var affected = await _context.Database.ExecuteSqlRawAsync(
                sql,
                new object[] { cutoff },
                cancellationToken);

            removed += affected;

            if (affected < PurgeBatchSize)
                break;
        }

        _logger.LogInformation(
            "Portal access purge: {Removed} row(s) older than {Days} day(s) removed (cutoff {Cutoff:yyyy-MM-dd}).",
            removed,
            days,
            cutoff);

        return new ResultDto
        {
            Result = 1,
            Messages = new[]
            {
                removed == 0
                    ? $"Nothing older than {days} day(s) was there to remove."
                    : $"{removed} access row(s) older than {days} day(s) were removed."
            }
        };
    }

    /// <summary>
    /// The location lookup wrapped so that it can only ever answer: an implementation that throws still leaves
    /// the hit to be stored with its address alone. A cancellation is this request ending, so it is passed on
    /// rather than swallowed.
    /// </summary>
    private async Task<IpGeoLocation?> LocateSafelyAsync(string address, CancellationToken cancellationToken)
    {
        try
        {
            return await _geo.LocateAsync(address, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogDebug(
                exception,
                "The location lookup failed for {Address}; the hit is recorded with its address alone.",
                address);

            return null;
        }
    }
}


