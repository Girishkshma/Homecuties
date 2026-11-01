using HC.Business.Dtos;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

/// <summary>
/// The report the admin screen reads: where the period's hits came from and how often.
///
/// Everything is counted from the rows themselves rather than from a summary table, because the one question
/// this tracker exists to answer - 'is the shop being looked at from here, and how much' - has to be answerable
/// for any period the screen asks about, and a summary table can only answer for the periods somebody thought
/// to build. A window is never wider than a year (see <see cref="OrderMoney.ResolvePeriod"/>) and only the
/// columns being counted are read, which is what keeps that affordable.
///
/// The plan is UTC, like the rows and like the finance screen's: a day is a UTC day, and the screen says so.
/// </summary>
public partial class AccessTrackingService
{
    public async Task<PortalAccessReportDto> GetReportAsync(
        DateOnly? from,
        DateOnly? to,
        string? portal,
        CancellationToken cancellationToken = default)
    {
        var (first, last) = ResolveDays(from, to);
        var (start, endExclusive) = Window(first, last);
        var portalName = AccessTrackingRules.PortalFilter(portal);

        var rows = await RowsBetween(start, endExclusive)
            .Where(row => portalName == null || row.Portal == portalName)
            .Select(row => new AccessRow(row.AccessOn, row.Ip, row.Portal, row.Country, row.CountryCode, row.Region, row.City))
            .ToListAsync(cancellationToken);

        var unit = AccessTrackingRules.UnitFor(first, last);
        var located = rows.Where(row => !string.IsNullOrWhiteSpace(row.Country)).ToList();
        var totalHits = rows.Count;

        var report = new PortalAccessReportDto
        {
            From = first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            To = last.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Unit = unit.ToString(),
            TotalHits = totalHits,
            UniqueVisitors = DistinctAddresses(rows),
            LocatedHits = located.Count,
            UnlocatedHits = totalHits - located.Count,
            CountryCount = CountryCountOf(located),
            RegionCount = RegionCountOf(located),
            CityCount = CityCountOf(located),
            Countries = CountryPlaces(located, totalHits),
            Regions = RegionPlaces(located, totalHits),
            Cities = CityPlaces(located, totalHits),

            Portals = Places(
                rows,
                row => string.IsNullOrWhiteSpace(row.Portal) ? "unknown" : row.Portal!,
                _ => null,
                row => row.Portal,
                totalHits,
                limit: int.MaxValue),
            Frequency = Frequency(rows, first, last, unit)
        };

        report.Messages = Messages(report, located, portalName);

        return report;
    }

    /// <summary>
    /// One roll-up (countries, regions, cities, portals): the places of the period grouped by name, most hits
    /// first, each with how many different addresses came from it and its share of the period.
    ///
    /// Two spellings of one country are one row (the comparison ignores case), because 'india' and 'India' are
    /// one place and a report that splits them says something untrue about both.
    /// </summary>
    private static List<PortalAccessPlaceDto> Places(
        IEnumerable<AccessRow> rows,
        Func<AccessRow, string> name,
        Func<AccessRow, string?> countryCode,
        Func<AccessRow, string?> filter,
        int totalHits,
        int limit = AccessTrackingRules.TopPlaces)
    {
        return rows
            .GroupBy(name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PortalAccessPlaceDto
            {
                Name = group.Key,
                CountryCode = group
                    .Select(countryCode)
                    .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code)),
                Filter = group
                    .Select(filter)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                Hits = group.Count(),
                Visitors = DistinctAddresses(group),
                Share = AccessTrackingRules.Share(group.Count(), totalHits)
            })
            .OrderByDescending(place => place.Hits)
            .ThenBy(place => place.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// The period's countries, most hits first - the roll-up the report lists and the dashboard's tile draws.
    ///
    /// It is defined once and read by both, so a tile and the full report can never disagree about which
    /// country is biggest: two spellings of one country are one row, and a hit whose place nobody could name is
    /// in no row at all (it is in the totals instead, and the answer says so).
    /// </summary>
    private static List<PortalAccessPlaceDto> CountryPlaces(
        List<AccessRow> located,
        int totalHits,
        int limit = AccessTrackingRules.TopPlaces)
    {
        return Places(located, row => row.Country!, row => row.CountryCode, row => row.Country, totalHits, limit);
    }

    /// <summary>The period's states ('Tamil Nadu, India'), most hits first - a hit with no state named is in none.</summary>
    private static List<PortalAccessPlaceDto> RegionPlaces(
        List<AccessRow> located,
        int totalHits,
        int limit = AccessTrackingRules.TopPlaces)
    {
        return Places(
            located.Where(row => !string.IsNullOrWhiteSpace(row.Region)),
            row => PlaceLabel(row.Region!, row.Country),
            row => row.CountryCode,
            row => row.Region,
            totalHits,
            limit);
    }

    /// <summary>The period's cities ('Chennai, Tamil Nadu'), most hits first - a hit with no city named is in none.</summary>
    private static List<PortalAccessPlaceDto> CityPlaces(
        List<AccessRow> located,
        int totalHits,
        int limit = AccessTrackingRules.TopPlaces)
    {
        return Places(
            located.Where(row => !string.IsNullOrWhiteSpace(row.City)),
            row => PlaceLabel(row.City!, row.Region),
            row => row.CountryCode,
            row => row.City,
            totalHits,
            limit);
    }

    /// <summary>How many countries the placed hits came from ('India' and 'india' are one).</summary>
    private static int CountryCountOf(List<AccessRow> located)
    {
        return located.Select(CountryKey).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    /// <summary>
    /// How many states the placed hits came from - two states of one name in two countries are two states, so
    /// the country is part of what makes them the same one.
    /// </summary>
    private static int RegionCountOf(List<AccessRow> located)
    {
        return located
            .Where(row => !string.IsNullOrWhiteSpace(row.Region))
            .Select(row => $"{CountryKey(row)}|{row.Region}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    /// <summary>How many cities the placed hits came from, the same way as the states.</summary>
    private static int CityCountOf(List<AccessRow> located)
    {
        return located
            .Where(row => !string.IsNullOrWhiteSpace(row.City))
            .Select(row => $"{CountryKey(row)}|{row.Region}|{row.City}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    /// <summary>
    /// The frequency chart's slices: every hour, day or month of the period in order, including the ones with
    /// no hits at all, so a quiet stretch is drawn as the gap it is rather than left out of the chart.
    /// </summary>
    private static List<PortalAccessFrequencyDto> Frequency(
        List<AccessRow> rows,
        DateOnly first,
        DateOnly last,
        AccessFrequencyUnit unit)
    {
        var byBucket = rows
            .GroupBy(row => AccessTrackingRules.BucketStart(row.AccessOn, unit))
            .ToDictionary(group => group.Key, group => group.ToList());

        return AccessTrackingRules.Buckets(first, last, unit)
            .Select(bucket =>
            {
                var label = AccessTrackingRules.BucketLabel(bucket, unit);

                if (!byBucket.TryGetValue(bucket, out var inBucket))
                    return new PortalAccessFrequencyDto { BucketStart = bucket, Label = label };

                return new PortalAccessFrequencyDto
                {
                    BucketStart = bucket,
                    Label = label,
                    Hits = inBucket.Count,
                    Visitors = DistinctAddresses(inBucket)
                };
            })
            .ToList();
    }

    /// <summary>
    /// The findings the figures cannot say by themselves - an empty period, a portal filter, the hits whose
    /// address could not be placed, and the traffic that came from outside India. They are what stops a small
    /// number from reading as a fault.
    /// </summary>
    private static string[] Messages(PortalAccessReportDto report, List<AccessRow> located, string? portalName)
    {
        var messages = new List<string>();

        if (report.TotalHits == 0)
        {
            messages.Add("No portal access was recorded in this period.");
            return messages.ToArray();
        }

        if (portalName is not null)
            messages.Add($"Counting the '{portalName}' portal alone.");

        if (report.UnlocatedHits > 0)
        {
            messages.Add(
                $"{report.UnlocatedHits} of the {report.TotalHits} hit(s) have no place: the address was private " +
                "(a local or office network) or the location lookup had no answer for it.");
        }

        var abroad = located.Count(row => !IsKnownIndian(row));

        if (abroad > 0)
            messages.Add($"{abroad} of the {report.TotalHits} hit(s) came from outside India.");

        return messages.ToArray();
    }

    /// <summary>Different addresses in the group - one visitor is one address, which is as close as this gets.</summary>
    private static int DistinctAddresses(IEnumerable<AccessRow> rows)
    {
        return rows.Select(row => row.Ip).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    /// <summary>
    /// What makes two rows the same country: its code where the lookup named one, its name otherwise, so a
    /// country whose name the lookup spells differently from day to day is still counted once.
    /// </summary>
    private static string CountryKey(AccessRow row)
    {
        return string.IsNullOrWhiteSpace(row.CountryCode) ? row.Country ?? "" : row.CountryCode;
    }

    /// <summary>'Chennai, Tamil Nadu' - or the place alone when nothing larger is known.</summary>
    private static string PlaceLabel(string place, string? parent)
    {
        return string.IsNullOrWhiteSpace(parent) ? place : $"{place}, {parent}";
    }

    private static bool IsKnownIndian(AccessRow row)
    {
        return string.Equals(row.CountryCode, "IN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.Country, "India", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The days the report reads: the ones asked for, the month to date when none are, trimmed to a year and
    /// never running past today - the same rule the finance screen's period resolves to.
    /// </summary>
    private static (DateOnly From, DateOnly To) ResolveDays(DateOnly? from, DateOnly? to)
    {
        return OrderMoney.ResolvePeriod(from, to, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    /// <summary>The days as the UTC half-open window the rows are read by ('to' included, the day after it not).</summary>
    private static (DateTime Start, DateTime EndExclusive) Window(DateOnly from, DateOnly to)
    {
        return (
            from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }

    /// <summary>The hits of a window, with the rows they are read from - nothing tracked, so nothing to write back.</summary>
    private IQueryable<AccessTracking> RowsBetween(DateTime start, DateTime endExclusive)
    {
        return _context.AccessTrackings
            .AsNoTracking()
            .Where(row => row.AccessOn >= start && row.AccessOn < endExclusive);
    }

    /// <summary>One row as the report reads it - the columns it counts, and nothing else.</summary>
    private readonly record struct AccessRow(
        DateTime AccessOn,
        string Ip,
        string? Portal,
        string? Country,
        string? CountryCode,
        string? Region,
        string? City);
}


