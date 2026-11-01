using HC.Business.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

/// <summary>
/// The one-dimension read the dashboard's tile makes: where the period's hits came from at a single size
/// (countries, states or cities), most hits first.
///
/// It is the report's own roll-up (<see cref="GetReportAsync"/>) cut to what a tile draws - one list rather
/// than three, a handful of rows rather than ten of each - and it counts from the same rows with the same
/// grouping helpers, so a tile and the Portal Access screen can never tell two different stories about the
/// same week. Nothing here is a new rule: the period resolves to this week when none is named
/// (<see cref="AccessTrackingRules.ResolvePlaceDays"/>), the dimension and the limit are normalised by
/// <see cref="AccessTrackingRules"/>, and what the figures cannot say on their own comes back as messages.
/// </summary>
public partial class AccessTrackingService
{
    public async Task<PortalAccessPlacesDto> GetPlacesAsync(
        string? dimension,
        DateOnly? from,
        DateOnly? to,
        int? limit,
        CancellationToken cancellationToken = default)
    {
        var (first, last) = AccessTrackingRules.ResolvePlaceDays(from, to, DateOnly.FromDateTime(DateTime.UtcNow));
        var by = AccessTrackingRules.PlaceDimension(dimension);
        var keep = AccessTrackingRules.PlaceLimit(limit);
        var (start, endExclusive) = Window(first, last);

        // Only the columns being counted are read, and only for the days asked about: the tile runs every time
        // the dashboard is opened, so it has to be as cheap as the read it replaces.
        var rows = await RowsBetween(start, endExclusive)
            .Select(row => new AccessRow(row.AccessOn, row.Ip, row.Portal, row.Country, row.CountryCode, row.Region, row.City))
            .ToListAsync(cancellationToken);

        var located = rows.Where(row => !string.IsNullOrWhiteSpace(row.Country)).ToList();
        var totalHits = rows.Count;

        var places = by switch
        {
            AccessPlaceDimension.City => CityPlaces(located, totalHits, keep),
            AccessPlaceDimension.Region => RegionPlaces(located, totalHits, keep),
            _ => CountryPlaces(located, totalHits, keep)
        };

        var answer = new PortalAccessPlacesDto
        {
            From = first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            To = last.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Dimension = AccessTrackingRules.DimensionName(by),
            Title = TitleFor(by),
            TotalHits = totalHits,
            UniqueVisitors = DistinctAddresses(rows),
            LocatedHits = located.Count,
            UnlocatedHits = totalHits - located.Count,
            PlaceCount = by switch
            {
                AccessPlaceDimension.City => CityCountOf(located),
                AccessPlaceDimension.Region => RegionCountOf(located),
                _ => CountryCountOf(located)
            },
            Places = places
        };

        answer.Messages = PlaceMessages(answer, located);

        return answer;
    }

    /// <summary>What the list of this size is headed, in the words the tile shows ('States', not 'Regions').</summary>
    private static string TitleFor(AccessPlaceDimension dimension)
    {
        return dimension switch
        {
            AccessPlaceDimension.Region => "States",
            AccessPlaceDimension.City => "Cities",
            _ => "Countries"
        };
    }

    /// <summary>
    /// What the tile's figures cannot say on their own, in the words it shows them: an empty period, the hits
    /// nobody could place (they are in the totals and in no row), a list cut short (the rest is on the Portal
    /// Access screen), and traffic from outside India - a shop that only delivers in India reads that one as a
    /// finding rather than as a mistake in the count.
    /// </summary>
    private static string[] PlaceMessages(PortalAccessPlacesDto answer, List<AccessRow> located)
    {
        var messages = new List<string>();

        if (answer.TotalHits == 0)
        {
            messages.Add("Nothing was recorded in this period yet.");
            return messages.ToArray();
        }

        if (answer.UnlocatedHits > 0)
        {
            messages.Add(
                $"{answer.UnlocatedHits} of the {answer.TotalHits} hit(s) have no place: the address was private " +
                "(a local or office network) or the location lookup had no answer for it, so they are in the " +
                "totals above and in none of the rows.");
        }

        if (answer.PlaceCount > answer.Places.Count)
        {
            messages.Add(
                $"This is the biggest {answer.Places.Count} of {answer.PlaceCount} {answer.Title.ToLowerInvariant()} " +
                "- the Portal Access screen lists the rest.");
        }

        var abroad = located.Count(row => !IsKnownIndian(row));

        if (abroad > 0)
        {
            messages.Add($"{abroad} of the {answer.TotalHits} hit(s) came from outside India.");
        }

        return messages.ToArray();
    }
}
