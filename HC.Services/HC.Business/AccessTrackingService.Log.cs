using HC.Business.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

/// <summary>
/// The raw log behind the report: the hits themselves, newest first, each with the address it came from and
/// the place that address was looked up to. A figure on the screen is a claim; this is what it is checked
/// against - which is also why the log is where a place row on the screen leads.
///
/// A hit also carries the point the visitor chose to share, when they did (most do not) - shown beside the
/// place rather than in place of it, because the place is the address's own answer and the point is the
/// visitor's addition to it.
///
/// The filters are the ones the report's rows offer (a country, a region, a portal), each applied only when it
/// is given, so 'the log of India' is one click rather than a second report.
/// </summary>
public partial class AccessTrackingService
{
    /// <summary>How many hits a page of the log holds when the screen says nothing.</summary>
    private const int DefaultPageSize = 50;

    /// <summary>A page of fewer than ten rows wastes the reader's click; more than two hundred is not read.</summary>
    private const int MinPageSize = 10;
    private const int MaxPageSize = 200;

    public async Task<PortalAccessLogPageDto> GetLogAsync(
        DateOnly? from,
        DateOnly? to,
        string? portal,
        string? country,
        string? region,
        string? city,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (first, last) = ResolveDays(from, to);
        var (start, endExclusive) = Window(first, last);
        var query = RowsBetween(start, endExclusive);

        var portalName = AccessTrackingRules.PortalFilter(portal);
        if (portalName is not null)
            query = query.Where(row => row.Portal == portalName);

        var countryName = AccessTrackingRules.Clip(country, AccessTrackingRules.MaxPlaceLength);
        if (countryName is not null)
            query = query.Where(row => row.Country == countryName);

        var regionName = AccessTrackingRules.Clip(region, AccessTrackingRules.MaxPlaceLength);
        if (regionName is not null)
            query = query.Where(row => row.Region == regionName);

        var cityName = AccessTrackingRules.Clip(city, AccessTrackingRules.MaxPlaceLength);
        if (cityName is not null)
            query = query.Where(row => row.City == cityName);

        var totalHits = await query.CountAsync(cancellationToken);

        // The size and the page are clamped rather than refused: a pager that asks for page 9 of a list that
        // has three answers the last page instead of an error the screen would have to explain.
        var size = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, MinPageSize, MaxPageSize);
        var pageCount = (int)Math.Ceiling(totalHits / (double)size);
        var pageNumber = Math.Clamp(page, 1, pageCount == 0 ? 1 : pageCount);

        var items = await query
            .OrderByDescending(row => row.AccessOn)
            .ThenByDescending(row => row.Id)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(row => new PortalAccessLogItemDto
            {
                AccessOn = row.AccessOn,
                Ip = row.Ip,
                Portal = row.Portal ?? "",
                Url = row.Url,
                City = row.City,
                Region = row.Region,
                Country = row.Country,
                CountryCode = row.CountryCode,
                Organization = row.Organization,
                Latitude = row.Latitude,
                Longitude = row.Longitude
            })
            .ToListAsync(cancellationToken);

        return new PortalAccessLogPageDto
        {
            From = first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            To = last.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Page = pageNumber,
            PageSize = size,
            TotalHits = totalHits,
            PageCount = pageCount,
            Items = items
        };
    }
}
