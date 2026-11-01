using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

/// <summary>
/// One portal access: the address it came from, where that address is and when it was seen - never who.
///
/// The row answers "from which location is the shop being looked at, and how often" and nothing else, so no
/// customer id, no admin id and no cookie is written here. The address is the caller's own, read off the
/// connection by the API (see UtilitiesController) rather than taken from the browser, and the place columns
/// are filled from the location lookup (see HC.Business.Geo.IpGeoLocator): a hit whose lookup had no answer
/// is still a hit, which is why the place columns are nullable.
///
/// 'Portal' is which shop front the hit came from - 'Web' for the storefront, which is the front that records
/// its page views today, and the same column would hold 'Admin' if the admin area ever records its own. One
/// table, one report, whichever fronts are tracked.
/// 'CountryCode' is the two-letter ISO code the lookup names beside the country, which is what flags a
/// country name the lookup spells differently from day to day.
/// </summary>
public partial class AccessTracking
{
    public long Id { get; set; }

    /// <summary>Caller's address as text - IPv4 or IPv6 (45 characters holds the longest IPv6 form).</summary>
    public string Ip { get; set; } = null!;

    /// <summary>The page that was opened (an absolute URL, or the path when only a path is known).</summary>
    public string Url { get; set; } = null!;

    public DateTime AccessOn { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public string? Country { get; set; }

    /// <summary>The network the address belongs to (the lookup's 'org'), left null when it names none.</summary>
    public string? Organization { get; set; }

    /// <summary>Which shop front the hit came from - see HC.Business.AccessTrackingRules.PortalName.</summary>
    public string? Portal { get; set; }

    /// <summary>The ISO 3166-1 alpha-2 code of Country ('IN', 'US'), null when not located.</summary>
    public string? CountryCode { get; set; }
}
