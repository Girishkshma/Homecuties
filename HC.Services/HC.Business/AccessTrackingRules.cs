using System.Globalization;

namespace HC.Business;

/// <summary>
/// How wide one slice of the frequency chart is: a day of traffic is read hour by hour, a couple of months
/// day by day, and anything longer month by month, so the chart never draws more bars than a screen can read.
/// </summary>
public enum AccessFrequencyUnit
{
    Hour,
    Day,
    Month
}

/// <summary>
/// The sizes a place is counted at: the country, the state (region) inside it, or the city inside that.
///
/// One at a time is what a tile shows - a list of countries and a list of cities are two answers to two
/// questions, and mixing them into one list would let a country and a city of the same size read as equal.
/// </summary>
public enum AccessPlaceDimension
{
    Country,
    Region,
    City
}

/// <summary>
/// The decisions the portal access tracker makes that are not about the database: which portal a hit is
/// filed under, what a page address is allowed to be, how wide the frequency chart's slices are, and what a
/// country's share of a period is.
///
/// They live here, apart from the read and the write, because the API and the admin screen both depend on
/// them and neither should have to reproduce them - and because they are pure, so the tests can pin them
/// without a database, a lookup service or a request (see HC.Tests.AccessTrackingTests).
/// </summary>
public static class AccessTrackingRules
{
    /// <summary>The longest page address the column holds - the tracker stores at most this much of one.</summary>
    public const int MaxPageUrlLength = 400;

    /// <summary>The longest an IP address can be written (an IPv6 address in its full form).</summary>
    public const int MaxIpLength = 45;

    /// <summary>The longest place name the columns hold.</summary>
    public const int MaxPlaceLength = 100;

    /// <summary>The longest network name ('org') the column holds.</summary>
    public const int MaxOrganizationLength = 200;

    /// <summary>The longest portal name the column holds.</summary>
    public const int MaxPortalLength = 20;

    /// <summary>The portal a hit is filed under when the caller names none - the storefront's own.</summary>
    public const string DefaultPortal = "Web";

    /// <summary>What is written for a page nobody could name, so the row is still a hit at a known time.</summary>
    public const string UnknownPage = "unknown";

    /// <summary>What is written for an address nothing could read, so the row is still a hit.</summary>
    public const string UnknownIp = "unknown";

    /// <summary>How many places each of the report's roll-ups (countries, regions, cities) keeps.</summary>
    public const int TopPlaces = 10;

    /// <summary>
    /// How many places a single-dimension read keeps when the caller names no limit - the handful a dashboard
    /// tile can draw without pushing the rest of the screen down.
    /// </summary>
    public const int DefaultPlaceLimit = 8;

    /// <summary>
    /// The most places one read will group, whatever the caller asks for: a list longer than this is a report,
    /// not a tile, and the Portal Access screen is where a report is read.
    /// </summary>
    public const int MaxPlaceLimit = 50;

    /// <summary>A period of two days or less is read hour by hour - the only width where the hour matters.</summary>
    private const int HourUnitMaxDays = 2;

    /// <summary>Up to two months is read day by day; anything longer is read month by month.</summary>
    private const int DayUnitMaxDays = 62;

    /// <summary>
    /// The portal name a hit is filed under: the caller's own when it is a plain name, and the storefront's
    /// when it names none or names something that is not one.
    ///
    /// The name comes from the browser, so it is not believed as written: letters, digits and the separators a
    /// portal name is made of are kept, the length is capped at the column's own, and anything else falls back
    /// to 'Web' rather than writing a name the report would have to clean up later.
    /// </summary>
    public static string PortalName(string? portal)
    {
        var name = (portal ?? string.Empty).Trim();

        if (name.Length == 0)
            return DefaultPortal;

        if (name.Length > MaxPortalLength)
            name = name[..MaxPortalLength];

        var usable = name.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or ' ');
        return usable ? name : DefaultPortal;
    }

    /// <summary>
    /// The portal a report is asked to count, or null when it is asked to count every one of them: an empty
    /// filter means 'all portals', and it must not be read as the storefront alone.
    /// </summary>
    public static string? PortalFilter(string? portal)
    {
        var name = (portal ?? string.Empty).Trim();

        if (name.Length == 0 || string.Equals(name, "all", StringComparison.OrdinalIgnoreCase))
            return null;

        if (name.Length > MaxPortalLength)
            name = name[..MaxPortalLength];

        var usable = name.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or ' ');
        return usable ? name : null;
    }

    /// <summary>
    /// The page address a hit is filed under: an absolute http/https address or a path within the shop, cut
    /// to the column's length. Anything else - a 'javascript:' address, a data URL, a fragment of broken text
    /// - is not an address at all, so it is not stored.
    ///
    /// The address comes from the browser and is only ever reported on (it is never fetched or followed), so
    /// this is about keeping the column readable rather than about trusting it: a report of page names full
    /// of 'javascript:void(0)' says nothing about where the shop is being looked at.
    /// </summary>
    public static string? CleanPageUrl(string? pageUrl)
    {
        var address = (pageUrl ?? string.Empty).Trim();

        if (address.Length == 0)
            return null;

        if (address[0] == '/')
            return address.Length > MaxPageUrlLength ? address[..MaxPageUrlLength] : address;

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
            return null;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return null;

        return address.Length > MaxPageUrlLength ? address[..MaxPageUrlLength] : address;
    }

    /// <summary>
    /// A place or network name as the column can hold it: trimmed, and cut to the column's length rather than
    /// failing the insert. An empty name is no name at all, so it is written as null.
    /// </summary>
    public static string? Clip(string? value, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();

        if (text.Length == 0)
            return null;

        return text.Length > maxLength ? text[..maxLength] : text;
    }

    /// <summary>
    /// The size a caller asked the places to be counted at. 'state' is accepted alongside 'region' because that
    /// is the word the shop uses for it, and anything else - an empty ask, a typo - is the country, which is the
    /// widest and so the one that cannot mislead about how far the list reaches.
    /// </summary>
    public static AccessPlaceDimension PlaceDimension(string? dimension)
    {
        var name = (dimension ?? string.Empty).Trim();

        if (name.Equals("city", StringComparison.OrdinalIgnoreCase))
            return AccessPlaceDimension.City;

        if (name.Equals("region", StringComparison.OrdinalIgnoreCase) || name.Equals("state", StringComparison.OrdinalIgnoreCase))
            return AccessPlaceDimension.Region;

        return AccessPlaceDimension.Country;
    }

    /// <summary>The dimension as the answer and the screen name it ('country', 'region', 'city').</summary>
    public static string DimensionName(AccessPlaceDimension dimension)
    {
        return dimension switch
        {
            AccessPlaceDimension.Region => "region",
            AccessPlaceDimension.City => "city",
            _ => "country"
        };
    }

    /// <summary>
    /// How many places a read keeps: the caller's number, or the tile's handful when it names none, never more
    /// than <see cref="MaxPlaceLimit"/> and never fewer than one - a list of nothing is not a shorter answer.
    /// </summary>
    public static int PlaceLimit(int? limit)
    {
        if (limit is null or <= 0)
            return DefaultPlaceLimit;

        return Math.Min(limit.Value, MaxPlaceLimit);
    }

    /// <summary>
    /// The Monday the week of the given day began on - the shop's week starts on Monday, so a Sunday belongs to
    /// the week that began six days before it rather than to the one about to begin.
    /// </summary>
    public static DateOnly WeekStart(DateOnly day)
    {
        var sinceMonday = ((int)day.DayOfWeek + 6) % 7;
        return day.AddDays(-sinceMonday);
    }

    /// <summary>
    /// The days a place tile reads: this week when the caller names no days at all (Monday up to and including
    /// today), and otherwise the same rule the report and the finance screen read a period by - so a tile that
    /// opens on a Wednesday shows Monday and Tuesday's hits and today's, and a period it is given is trimmed
    /// and capped exactly as the full report would trim it.
    /// </summary>
    public static (DateOnly From, DateOnly To) ResolvePlaceDays(DateOnly? from, DateOnly? to, DateOnly today)
    {
        if (from is null && to is null)
            return (WeekStart(today), today);

        return OrderMoney.ResolvePeriod(from, to, today);
    }

    /// <summary>
    /// How wide the frequency chart's slices are over the period: hour by hour for a day or two, day by day
    /// up to two months, month by month beyond that.
    /// </summary>
    public static AccessFrequencyUnit UnitFor(DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber + 1;

        if (days <= HourUnitMaxDays)
            return AccessFrequencyUnit.Hour;

        return days <= DayUnitMaxDays ? AccessFrequencyUnit.Day : AccessFrequencyUnit.Month;
    }

    /// <summary>The start of the slice a hit belongs to (the hour, the day or the month it was seen in).</summary>
    public static DateTime BucketStart(DateTime accessOn, AccessFrequencyUnit unit)
    {
        return unit switch
        {
            AccessFrequencyUnit.Hour => new DateTime(accessOn.Year, accessOn.Month, accessOn.Day, accessOn.Hour, 0, 0, accessOn.Kind),
            AccessFrequencyUnit.Month => new DateTime(accessOn.Year, accessOn.Month, 1, 0, 0, 0, accessOn.Kind),
            _ => new DateTime(accessOn.Year, accessOn.Month, accessOn.Day, 0, 0, 0, accessOn.Kind)
        };
    }

    /// <summary>
    /// Every slice of the period, in order, so a day without a single hit is drawn as the gap it is instead of
    /// being left out of the chart - a quiet Tuesday and a Tuesday nobody looked at are not the same thing.
    /// </summary>
    public static IEnumerable<DateTime> Buckets(DateOnly from, DateOnly to, AccessFrequencyUnit unit)
    {
        var start = BucketStart(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), unit);

        // The period's last day is included whole: an hourly chart of one day is that day's 24 hours, not the
        // hour it began at, which is what the day it starts on would otherwise say.
        var last = unit == AccessFrequencyUnit.Hour
            ? BucketStart(to.ToDateTime(new TimeOnly(23, 0), DateTimeKind.Utc), unit)
            : BucketStart(to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), unit);

        for (var bucket = start; bucket <= last; bucket = NextBucket(bucket, unit))
        {
            yield return bucket;
        }
    }

    /// <summary>The label a slice is drawn under: '09 Aug 14:00', '09 Aug' or 'Aug 2026'.</summary>
    public static string BucketLabel(DateTime bucketStart, AccessFrequencyUnit unit)
    {
        var format = unit switch
        {
            AccessFrequencyUnit.Hour => "dd MMM HH:00",
            AccessFrequencyUnit.Month => "MMM yyyy",
            _ => "dd MMM"
        };

        return bucketStart.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A place's share of the period's hits, as a percentage with one decimal - what the report's bars are
    /// drawn from, so the biggest place always reads as the biggest bar.
    /// </summary>
    public static double Share(int part, int total)
    {
        return total <= 0 ? 0 : Math.Round(part * 100.0 / total, 1);
    }

    private static DateTime NextBucket(DateTime bucket, AccessFrequencyUnit unit)
    {
        return unit switch
        {
            AccessFrequencyUnit.Hour => bucket.AddHours(1),
            AccessFrequencyUnit.Month => bucket.AddMonths(1),
            _ => bucket.AddDays(1)
        };
    }
}

