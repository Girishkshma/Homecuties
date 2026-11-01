using HC.Business;
using HC.Business.Geo;
using Xunit;

namespace HC.Tests;

/// <summary>
/// What the portal access tracker decides before anything is written or read: which addresses are places
/// worth asking about, what a page address and a portal name are allowed to be, how wide the frequency
/// chart's slices are, and what a country's share of a period is.
///
/// These are the rules the API and the admin screen both stand on (see HC.Business.AccessTrackingRules and
/// HC.Business.Geo.IpAddresses), and they are pure, so they are pinned here rather than through a database and
/// a lookup service that would be slow and flaky to stand up.
/// </summary>
public class AccessTrackingTests
{
    /// <summary>
    /// The addresses that can be placed and the ones that cannot. A private, loopback or carrier-grade address
    /// is never a country, and asking a lookup about one spends a call to learn nothing - while an unreadable
    /// address is not a place either.
    /// </summary>
    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("49.36.120.9", true)]
    [InlineData("2001:4860:4860::8888", true)]
    [InlineData("10.0.0.5", false)]
    [InlineData("172.16.5.4", false)]
    [InlineData("172.31.255.254", false)]
    [InlineData("172.32.0.1", true)]
    [InlineData("192.168.1.1", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("169.254.1.1", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("::ffff:10.0.0.5", false)]
    [InlineData("::ffff:8.8.8.8", true)]
    [InlineData("unknown", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyARealAddressInTheWorldIsAPlace(string? ip, bool isPlace)
    {
        Assert.Equal(isPlace, IpAddresses.IsPublic(ip));
    }

    /// <summary>
    /// The address as the tracker stores it: no port, no brackets, and IPv4 kept in its own shape, so a
    /// visitor arriving over '::ffff:1.2.3.4' is the same visitor as one arriving over '1.2.3.4' - two forms of
    /// one address would otherwise be counted as two visitors.
    /// </summary>
    [Theory]
    [InlineData("  49.36.120.9  ", "49.36.120.9")]
    [InlineData("49.36.120.9:51234", "49.36.120.9")]
    [InlineData("[2001:db8::1]:443", "2001:db8::1")]
    [InlineData("::ffff:49.36.120.9", "49.36.120.9")]
    [InlineData("2001:0db8:0000:0000:0000:0000:0000:0001", "2001:db8::1")]
    [InlineData("not an address", AccessTrackingRules.UnknownIp)]
    [InlineData("", AccessTrackingRules.UnknownIp)]
    [InlineData(null, AccessTrackingRules.UnknownIp)]
    public void AnAddressIsStoredInOneForm(string? ip, string expected)
    {
        Assert.Equal(expected, IpAddresses.Normalize(ip));
    }

    /// <summary>
    /// What a page address may be: an absolute http/https address or a path within the shop, and nothing else.
    /// The address is only ever displayed, but 'javascript:void(0)' in a column of page names says nothing
    /// about where the shop is being read from.
    /// </summary>
    [Theory]
    [InlineData("https://homecuties.com/product/10004", "https://homecuties.com/product/10004")]
    [InlineData("http://homecuties.com/", "http://homecuties.com/")]
    [InlineData("/search?q=lamp", "/search?q=lamp")]
    [InlineData("  /cart  ", "/cart")]
    [InlineData("javascript:void(0)", null)]
    [InlineData("data:text/html;base64,AAAA", null)]
    [InlineData("product/10004", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void APageAddressIsAnAddressOrNothing(string? pageUrl, string? expected)
    {
        Assert.Equal(expected, AccessTrackingRules.CleanPageUrl(pageUrl));
    }

    /// <summary>
    /// A page address longer than the column is cut to fit rather than failing the insert: the hit is worth
    /// more than the tail of its query string.
    /// </summary>
    [Fact]
    public void AnOverlongPageAddressIsCutToTheColumn()
    {
        var address = "https://homecuties.com/search?q=" + new string('a', 1000);

        var cleaned = AccessTrackingRules.CleanPageUrl(address);

        Assert.NotNull(cleaned);
        Assert.Equal(AccessTrackingRules.MaxPageUrlLength, cleaned!.Length);
        Assert.StartsWith("https://homecuties.com/search?q=", cleaned);
    }

    /// <summary>
    /// The portal a hit is filed under: the front's own name when it is a plain one, and the storefront's when
    /// it is missing or is something that is not a name at all. The name comes from the browser, so this is
    /// what keeps it from writing a column full of markup.
    /// </summary>
    [Theory]
    [InlineData(null, AccessTrackingRules.DefaultPortal)]
    [InlineData("", AccessTrackingRules.DefaultPortal)]
    [InlineData("Admin", "Admin")]
    [InlineData("  Web  ", "Web")]
    [InlineData("Mobile App", "Mobile App")]
    [InlineData("Web<script>", AccessTrackingRules.DefaultPortal)]
    [InlineData("Web;DROP TABLE", AccessTrackingRules.DefaultPortal)]
    public void APortalIsANameOrTheStorefront(string? portal, string expected)
    {
        Assert.Equal(expected, AccessTrackingRules.PortalName(portal));
    }

    /// <summary>
    /// A report's portal filter: an empty box means every portal, not the storefront alone - which is exactly
    /// the mistake that would hide the admin team's own sessions from the screen.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("all", null)]
    [InlineData("Web", "Web")]
    [InlineData(" Admin ", "Admin")]
    [InlineData("Web<script>", null)]
    public void AnEmptyPortalFilterCountsEveryPortal(string? portal, string? expected)
    {
        Assert.Equal(expected, AccessTrackingRules.PortalFilter(portal));
    }

    /// <summary>
    /// How wide the frequency chart's slices are: hour by hour for a day or two, day by day up to two months,
    /// month by month beyond that - so a year's report is twelve bars and not eight thousand.
    /// </summary>
    [Theory]
    [InlineData("2026-08-09", "2026-08-09", AccessFrequencyUnit.Hour)]
    [InlineData("2026-08-09", "2026-08-10", AccessFrequencyUnit.Hour)]
    [InlineData("2026-08-09", "2026-08-11", AccessFrequencyUnit.Day)]
    [InlineData("2026-08-01", "2026-10-01", AccessFrequencyUnit.Day)]
    [InlineData("2026-08-01", "2026-10-02", AccessFrequencyUnit.Month)]
    [InlineData("2025-01-01", "2026-01-01", AccessFrequencyUnit.Month)]
    public void TheChartIsAsWideAsThePeriodAllows(string from, string to, AccessFrequencyUnit expected)
    {
        Assert.Equal(expected, AccessTrackingRules.UnitFor(DateOnly.Parse(from), DateOnly.Parse(to)));
    }

    /// <summary>The start of the slice a hit belongs to, which is what makes two hits in one hour one bar.</summary>
    [Fact]
    public void AHitBelongsToTheStartOfItsSlice()
    {
        var hit = new DateTime(2026, 8, 9, 14, 37, 12, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 8, 9, 14, 0, 0, DateTimeKind.Utc), AccessTrackingRules.BucketStart(hit, AccessFrequencyUnit.Hour));
        Assert.Equal(new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc), AccessTrackingRules.BucketStart(hit, AccessFrequencyUnit.Day));
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), AccessTrackingRules.BucketStart(hit, AccessFrequencyUnit.Month));
    }

    /// <summary>
    /// Every slice of the period is drawn, including the ones nothing was seen in: a quiet Tuesday and a
    /// Tuesday nobody looked are not the same finding, and a chart that leaves the empty ones out hides that.
    /// </summary>
    [Fact]
    public void EverySliceOfThePeriodIsDrawn()
    {
        Assert.Equal(
            24,
            AccessTrackingRules.Buckets(new DateOnly(2026, 8, 9), new DateOnly(2026, 8, 9), AccessFrequencyUnit.Hour).Count());

        Assert.Equal(
            3,
            AccessTrackingRules.Buckets(new DateOnly(2026, 8, 9), new DateOnly(2026, 8, 11), AccessFrequencyUnit.Day).Count());

        Assert.Equal(
            3,
            AccessTrackingRules.Buckets(new DateOnly(2026, 6, 15), new DateOnly(2026, 8, 2), AccessFrequencyUnit.Month).Count());
    }

    /// <summary>The label a slice is drawn under - the same words on both sides of the screen.</summary>
    [Fact]
    public void ASliceIsLabelledWithTheSlicesOwnWords()
    {
        var slice = new DateTime(2026, 8, 9, 14, 0, 0, DateTimeKind.Utc);

        Assert.Equal("09 Aug 14:00", AccessTrackingRules.BucketLabel(slice, AccessFrequencyUnit.Hour));
        Assert.Equal("09 Aug", AccessTrackingRules.BucketLabel(slice, AccessFrequencyUnit.Day));
        Assert.Equal("Aug 2026", AccessTrackingRules.BucketLabel(slice, AccessFrequencyUnit.Month));
    }

    /// <summary>
    /// A place's share of the period: the bars the screen draws, so the biggest place has to read as the
    /// biggest bar, and a period with no hits at all is 0% rather than a division by zero.
    /// </summary>
    [Theory]
    [InlineData(25, 100, 25.0)]
    [InlineData(1, 3, 33.3)]
    [InlineData(0, 10, 0.0)]
    [InlineData(5, 0, 0.0)]
    public void APlacesShareIsItsPartOfThePeriod(int hits, int total, double expected)
    {
        Assert.Equal(expected, AccessTrackingRules.Share(hits, total));
    }

    /// <summary>
    /// A place or network name as the column can hold it: cut to fit rather than failing the insert, and an
    /// empty one is no name at all.
    /// </summary>
    [Fact]
    public void ANameIsCutToItsColumnAndEmptyIsNoName()
    {
        Assert.Null(AccessTrackingRules.Clip("   ", 100));
        Assert.Null(AccessTrackingRules.Clip(null, 100));
        Assert.Equal("Tamil Nadu", AccessTrackingRules.Clip("  Tamil Nadu ", 100));
        Assert.Equal("Tamil", AccessTrackingRules.Clip("Tamil Nadu", 5));
    }

    /// <summary>
    /// The size a place list is counted at: the three the tile offers, 'state' accepted as the shop's own word
    /// for a region, and anything else - an empty ask, a typo, a dimension that does not exist - read as the
    /// country rather than refused. A tile that cannot be told what to count still shows something true.
    /// </summary>
    [Theory]
    [InlineData("country", AccessPlaceDimension.Country)]
    [InlineData("Country", AccessPlaceDimension.Country)]
    [InlineData("region", AccessPlaceDimension.Region)]
    [InlineData("state", AccessPlaceDimension.Region)]
    [InlineData("  STATE  ", AccessPlaceDimension.Region)]
    [InlineData("city", AccessPlaceDimension.City)]
    [InlineData("cities", AccessPlaceDimension.Country)]
    [InlineData("", AccessPlaceDimension.Country)]
    [InlineData(null, AccessPlaceDimension.Country)]
    public void APlaceListIsCountedAtTheSizeAskedFor(string? dimension, AccessPlaceDimension expected)
    {
        Assert.Equal(expected, AccessTrackingRules.PlaceDimension(dimension));
    }

    /// <summary>
    /// The dimension comes back to the screen as its own name, so the answer and the button that asked for it
    /// agree: 'state' is asked for, 'region' is stored and answered, and the screen writes 'States' over it.
    /// </summary>
    [Fact]
    public void ADimensionIsAnsweredWithItsOwnName()
    {
        Assert.Equal("country", AccessTrackingRules.DimensionName(AccessPlaceDimension.Country));
        Assert.Equal("region", AccessTrackingRules.DimensionName(AccessPlaceDimension.Region));
        Assert.Equal("city", AccessTrackingRules.DimensionName(AccessPlaceDimension.City));
    }

    /// <summary>
    /// How many rows a tile keeps: its own handful when nothing is said (and when something impossible is said),
    /// the caller's number when one is given, and never more than the cap - a list longer than that is a report,
    /// and the Portal Access screen is where a report is read.
    /// </summary>
    [Theory]
    [InlineData(null, AccessTrackingRules.DefaultPlaceLimit)]
    [InlineData(0, AccessTrackingRules.DefaultPlaceLimit)]
    [InlineData(-5, AccessTrackingRules.DefaultPlaceLimit)]
    [InlineData(5, 5)]
    [InlineData(AccessTrackingRules.MaxPlaceLimit, AccessTrackingRules.MaxPlaceLimit)]
    [InlineData(AccessTrackingRules.MaxPlaceLimit + 1, AccessTrackingRules.MaxPlaceLimit)]
    [InlineData(int.MaxValue, AccessTrackingRules.MaxPlaceLimit)]
    public void ATileKeepsTheAsksRowsAndNeverMoreThanTheCap(int? limit, int expected)
    {
        Assert.Equal(expected, AccessTrackingRules.PlaceLimit(limit));
    }

    /// <summary>
    /// The week the tile opens on starts on Monday: a Wednesday's week began two days before it, a Monday is
    /// its own first day, and a Sunday belongs to the week that began six days earlier rather than to the one
    /// about to begin - the shop reads its week as Monday to Sunday.
    /// </summary>
    [Theory]
    [InlineData("2026-11-01", "2026-10-26")] // Sunday
    [InlineData("2026-10-26", "2026-10-26")] // Monday
    [InlineData("2026-10-28", "2026-10-26")] // Wednesday
    [InlineData("2026-10-31", "2026-10-26")] // Saturday
    [InlineData("2026-11-02", "2026-11-02")] // the next Monday
    [InlineData("2027-01-01", "2026-12-28")] // across the year's own end
    public void AWeekStartsOnItsMonday(string day, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), AccessTrackingRules.WeekStart(DateOnly.Parse(day)));
    }

    /// <summary>
    /// The days the tile reads: an empty ask is this week (Monday up to today, not a whole week into the
    /// future, which would be a run of empty days), and a period that is asked for is read by the same rule the
    /// report reads one by - a single day, a reversed pair as that one day, and a period wider than the cap
    /// trimmed at its far end.
    /// </summary>
    [Fact]
    public void TheTileOpensOnThisWeekAndReadsWhateverElseItIsGiven()
    {
        var today = new DateOnly(2026, 10, 28); // a Wednesday

        Assert.Equal(
            (new DateOnly(2026, 10, 26), today),
            AccessTrackingRules.ResolvePlaceDays(null, null, today));

        Assert.Equal(
            (new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)),
            AccessTrackingRules.ResolvePlaceDays(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), today));

        // A Sunday's 'this week' still began on the Monday before it, so the tile is never a single day by accident.
        Assert.Equal(
            (new DateOnly(2026, 10, 26), new DateOnly(2026, 11, 1)),
            AccessTrackingRules.ResolvePlaceDays(null, null, new DateOnly(2026, 11, 1)));

        // One day named on its own is that one day (up to today), and two days the wrong way round are read as the
        // single day they name rather than as an empty period.
        Assert.Equal(
            (new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 28)),
            AccessTrackingRules.ResolvePlaceDays(new DateOnly(2026, 10, 28), null, today));
        Assert.Equal(
            (new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 10)),
            AccessTrackingRules.ResolvePlaceDays(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 10), today));
    }

    /// <summary>
    /// The point a visitor agreed to share, read as a place in the world: rounded to about a hundred metres
    /// before it is stored, so the column holds which part of a city a reader is in and not the door they are
    /// behind. A device that says how sure it is keeps that number (in whole metres); one that says nothing
    /// (or says zero) still has a point, it simply comes without a promise about how close it is.
    /// </summary>
    [Fact]
    public void ASharedPointIsKeptToAboutAHundredMetres()
    {
        var point = AccessTrackingRules.ResolveSharedPoint(13.0826802, 80.2707184, 41.6);

        Assert.NotNull(point);
        Assert.Equal(13.083, point.Latitude);
        Assert.Equal(80.271, point.Longitude);
        Assert.Equal(42, point.AccuracyMeters);

        // An accuracy of zero, a negative one or none at all is not a claim: the point stands, the promise does
        // not.
        Assert.Null(AccessTrackingRules.ResolveSharedPoint(13.0826802, 80.2707184, 0)!.AccuracyMeters);
        Assert.Null(AccessTrackingRules.ResolveSharedPoint(13.0826802, 80.2707184, -5)!.AccuracyMeters);
        Assert.Null(AccessTrackingRules.ResolveSharedPoint(13.0826802, 80.2707184, null)!.AccuracyMeters);
    }

    /// <summary>
    /// Nothing is written for a point that is not a place: half of one (a browser that sent only the latitude),
    /// one off the globe, and the NaN and infinity a broken or overreaching client can put in JSON. Every one of
    /// those hits keeps the place its address was looked up to, which is what every hit had before the shop
    /// began asking.
    /// </summary>
    [Theory]
    [InlineData(null, 80.2707184)]
    [InlineData(13.0826802, null)]
    [InlineData(null, null)]
    [InlineData(90.5, 80.2707184)]
    [InlineData(-90.5, 80.2707184)]
    [InlineData(13.0826802, 180.5)]
    [InlineData(13.0826802, -180.5)]
    [InlineData(double.NaN, 80.2707184)]
    [InlineData(13.0826802, double.NaN)]
    [InlineData(double.PositiveInfinity, 80.2707184)]
    [InlineData(13.0826802, double.NegativeInfinity)]
    public void APointThatIsNotAPlaceIsNotWritten(double? latitude, double? longitude)
    {
        Assert.Null(AccessTrackingRules.ResolveSharedPoint(latitude, longitude, 40));
    }

    /// <summary>
    /// The edges of the world are still places (a point on the equator or the date line is a visitor), and a fix
    /// the device itself says is wider than a hundred kilometres is not kept: the address already says more
    /// than a guess that wide, so keeping it would only put a visitor in the wrong city with confidence.
    /// </summary>
    [Fact]
    public void TheEdgesOfTheWorldArePlacesButAWildGuessIsNot()
    {
        var equator = AccessTrackingRules.ResolveSharedPoint(0, 0, 10);
        Assert.NotNull(equator);
        Assert.Equal(0, equator.Latitude);
        Assert.Equal(0, equator.Longitude);

        Assert.NotNull(AccessTrackingRules.ResolveSharedPoint(-90, -180, 10));
        Assert.NotNull(AccessTrackingRules.ResolveSharedPoint(90, 180, 10));

        Assert.Null(AccessTrackingRules.ResolveSharedPoint(13.0826802, 80.2707184, AccessTrackingRules.MaxSharedAccuracyMeters + 1));
        Assert.NotNull(AccessTrackingRules.ResolveSharedPoint(13.0826802, 80.2707184, AccessTrackingRules.MaxSharedAccuracyMeters));
    }
}


