using System.Text.Json.Serialization;

namespace HC.Business.Dtos;

/// <summary>
/// What a shop front sends when a page has been opened: the page, where the reader came from, and which front
/// it is. Nothing about the reader - no id, no cookie, no name - and no place either: the address is read off
/// the caller's own connection by the API, and the place it is in is looked up server-side (see
/// HC.Business.Geo.IpGeoLocator), so a browser cannot claim to be in a country it is not.
///
/// The one thing the browser may add is a point the visitor chose to share (see
/// HC.Web/src/app/geo-consent): it is optional, left out by every visitor who did not agree, and it is written
/// beside the place the address was looked up to rather than in place of it - so what the report counts is
/// still the address's own answer, and a point can only ever add to the picture, never redirect it.
/// </summary>
public class AccessTrackingRequestDto
{
    /// <summary>The page that was opened ('https://homecuties.com/product/10004?ref=...').</summary>
    [JsonPropertyName("pageUrl")]
    public string? PageUrl { get; set; }

    /// <summary>The page the reader arrived from, as the browser reported it.</summary>
    [JsonPropertyName("referrerUrl")]
    public string? ReferrerUrl { get; set; }

    /// <summary>Which shop front sent the hit - 'Web' for the storefront when it is left out.</summary>
    [JsonPropertyName("portal")]
    public string? Portal { get; set; }

    /// <summary>
    /// The latitude the visitor agreed to share, or null - which is what every hit recorded without consent
    /// carries. Read, checked and rounded by HC.Business.AccessTrackingRules.ResolveSharedPoint before it is
    /// stored; an unreadable pair is dropped and the hit is placed by its address alone.
    /// </summary>
    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    /// <summary>The longitude that goes with <see cref="Latitude"/> - neither half is stored on its own.</summary>
    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    /// <summary>
    /// How close the device said the shared point is, in metres, or null when it did not say. A fix wider than
    /// the tracker keeps is refused with the point, because the address already places the hit better.
    /// </summary>
    [JsonPropertyName("accuracyMeters")]
    public double? AccuracyMeters { get; set; }
}

/// <summary>
/// One place in a roll-up: a country, a region, a city or a portal, with how often it was seen and how many
/// different addresses came from it. 'Share' is its part of the period's hits, which is what the screen's bars
/// are drawn from.
/// </summary>
public class PortalAccessPlaceDto
{
    /// <summary>The place as it reads in the report ('India', 'Tamil Nadu, India', 'Chennai', 'Web').</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// The value this row narrows the log by - the country, region, city or portal name as it is stored, which
    /// is what a click on the row sends to the log ('Tamil Nadu', never the 'Tamil Nadu, India' the row reads
    /// as).
    /// </summary>
    [JsonPropertyName("filter")]
    public string? Filter { get; set; }

    /// <summary>The country's ISO code, when the row is a place that has one ('IN').</summary>
    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; set; }

    /// <summary>How many hits came from this place in the period.</summary>
    [JsonPropertyName("hits")]
    public int Hits { get; set; }

    /// <summary>How many different addresses the hits came from - the visitors, as this tracker can count them.</summary>
    [JsonPropertyName("visitors")]
    public int Visitors { get; set; }

    /// <summary>The place's part of the period's hits, as a percentage with one decimal.</summary>
    [JsonPropertyName("share")]
    public double Share { get; set; }
}

/// <summary>
/// One slice of the frequency chart: an hour, a day or a month of the period and what was seen in it. Every
/// slice of the period is present, including the ones with no hits at all - an empty Tuesday is a finding.
/// </summary>
public class PortalAccessFrequencyDto
{
    /// <summary>The start of the slice, UTC.</summary>
    [JsonPropertyName("bucketStart")]
    public DateTime BucketStart { get; set; }

    /// <summary>The slice as the chart writes it ('09 Aug', '09 Aug 14:00', 'Aug 2026').</summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("hits")]
    public int Hits { get; set; }

    [JsonPropertyName("visitors")]
    public int Visitors { get; set; }
}


/// <summary>
/// Where the shop is being read from over a period, and how often: the numbers at the top of the screen, the
/// roll-ups by country, region and city, the per-portal split, and the frequency chart's slices.
///
/// 'From'/'To' are the days the server actually read, which are not always the ones asked for (an empty pair
/// is the month to date, a reversed pair reads as one day, a period wider than a year is trimmed), and
/// 'Unit' says how wide the chart's slices are - the server decides that from the period, so the screen and the
/// API can never disagree about it.
/// </summary>
public class PortalAccessReportDto
{
    [JsonPropertyName("from")]
    public DateTime From { get; set; }

    [JsonPropertyName("to")]
    public DateTime To { get; set; }

    /// <summary>'Hour', 'Day' or 'Month' - what one bar of the frequency chart covers.</summary>
    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "Day";

    [JsonPropertyName("totalHits")]
    public int TotalHits { get; set; }

    /// <summary>Different addresses in the period - one visitor is one address, which is as close as this gets.</summary>
    [JsonPropertyName("uniqueVisitors")]
    public int UniqueVisitors { get; set; }

    /// <summary>Hits the location lookup placed.</summary>
    [JsonPropertyName("locatedHits")]
    public int LocatedHits { get; set; }

    /// <summary>Hits with no place at all (a private address, or a lookup with no answer for it).</summary>
    [JsonPropertyName("unlocatedHits")]
    public int UnlocatedHits { get; set; }

    [JsonPropertyName("countryCount")]
    public int CountryCount { get; set; }

    [JsonPropertyName("regionCount")]
    public int RegionCount { get; set; }

    [JsonPropertyName("cityCount")]
    public int CityCount { get; set; }

    /// <summary>The biggest countries of the period, most hits first.</summary>
    [JsonPropertyName("countries")]
    public List<PortalAccessPlaceDto> Countries { get; set; } = new();

    /// <summary>The biggest regions ('Tamil Nadu, India'), most hits first.</summary>
    [JsonPropertyName("regions")]
    public List<PortalAccessPlaceDto> Regions { get; set; } = new();

    /// <summary>The biggest cities, most hits first.</summary>
    [JsonPropertyName("cities")]
    public List<PortalAccessPlaceDto> Cities { get; set; } = new();

    /// <summary>The per-portal split - the storefront against the admin area.</summary>
    [JsonPropertyName("portals")]
    public List<PortalAccessPlaceDto> Portals { get; set; } = new();

    [JsonPropertyName("frequency")]
    public List<PortalAccessFrequencyDto> Frequency { get; set; } = new();

    /// <summary>What the figures cannot say by themselves, in the words the screen shows.</summary>
    [JsonPropertyName("messages")]
    public string[] Messages { get; set; } = Array.Empty<string>();
}

/// <summary>
/// One place, at one size, over one period - what a dashboard tile draws: the countries, the states or the
/// cities the period's hits came from, most hits first, with the totals they sit inside.
///
/// It is the report's own roll-up (see <see cref="PortalAccessReportDto"/>), cut to a single dimension and a
/// handful of rows: a tile asks one question ('which cities') and is answered without pulling the country,
/// region and city lists it will not draw.
/// </summary>
public class PortalAccessPlacesDto
{
    /// <summary>The days the server actually read - this week's Monday to today when none were asked for.</summary>
    [JsonPropertyName("from")]
    public DateTime From { get; set; }

    [JsonPropertyName("to")]
    public DateTime To { get; set; }

    /// <summary>What the rows are counted at: 'country', 'region' or 'city'.</summary>
    [JsonPropertyName("dimension")]
    public string Dimension { get; set; } = "country";

    /// <summary>The single row's heading, in the screen's own words ('Countries', 'States', 'Cities').</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    /// <summary>Every hit of the period, including the ones no place could be named for.</summary>
    [JsonPropertyName("totalHits")]
    public int TotalHits { get; set; }

    /// <summary>Different addresses in the period - one visitor is one address, which is as close as this gets.</summary>
    [JsonPropertyName("uniqueVisitors")]
    public int UniqueVisitors { get; set; }

    /// <summary>Hits the location lookup placed, which are the hits the rows below are counted from.</summary>
    [JsonPropertyName("locatedHits")]
    public int LocatedHits { get; set; }

    /// <summary>Hits with no place at all (a private address, or a lookup with no answer for it).</summary>
    [JsonPropertyName("unlocatedHits")]
    public int UnlocatedHits { get; set; }

    /// <summary>How many different places the period had at this size, before the list was cut.</summary>
    [JsonPropertyName("placeCount")]
    public int PlaceCount { get; set; }

    /// <summary>The biggest places of the period at this size, most hits first.</summary>
    [JsonPropertyName("places")]
    public List<PortalAccessPlaceDto> Places { get; set; } = new();

    /// <summary>
    /// What the figures cannot say by themselves: an empty period, the hits with no place, a list cut short,
    /// and traffic from outside India. Shown under the rows, so a small number never reads as a fault.
    /// </summary>
    [JsonPropertyName("messages")]
    public string[] Messages { get; set; } = Array.Empty<string>();
}

/// <summary>
/// One page of the raw log behind the report: the hits themselves, newest first, each with the address it came
/// from and the place that address was looked up to. This is what an unusual figure is checked against.
/// </summary>
public class PortalAccessLogPageDto
{
    /// <summary>The days the server actually read.</summary>
    [JsonPropertyName("from")]
    public DateTime From { get; set; }

    [JsonPropertyName("to")]
    public DateTime To { get; set; }

    /// <summary>The page returned (1-based) and how big it is.</summary>
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    /// <summary>How many hits the period and filters hold in total - what the pager counts.</summary>
    [JsonPropertyName("totalHits")]
    public int TotalHits { get; set; }

    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }

    [JsonPropertyName("items")]
    public List<PortalAccessLogItemDto> Items { get; set; } = new();
}

/// <summary>One recorded hit as the log shows it.</summary>
public class PortalAccessLogItemDto
{
    [JsonPropertyName("accessOn")]
    public DateTime AccessOn { get; set; }

    /// <summary>The caller's address - the one part of the row that can identify a visitor, kept because it is
    /// also the only way to count them.</summary>
    [JsonPropertyName("ip")]
    public string Ip { get; set; } = "";

    [JsonPropertyName("portal")]
    public string Portal { get; set; } = "";

    /// <summary>The page that was opened.</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; set; }

    /// <summary>The network behind the address - a provider, a crawler, an office.</summary>
    [JsonPropertyName("organization")]
    public string? Organization { get; set; }

    /// <summary>
    /// The point the visitor chose to share on this hit - about a hundred metres of it - or null on every hit
    /// recorded without their agreement, which is most of them. Beside it, not instead of it, the place the
    /// address was looked up to is what the row already reads as.
    /// </summary>
    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    /// <summary>The longitude that goes with <see cref="Latitude"/>, null exactly when it is.</summary>
    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }
}

