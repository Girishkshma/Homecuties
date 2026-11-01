using System.Net;
using System.Text;
using HC.Business.Geo;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HC.Tests;

/// <summary>
/// What the lookup is asked about, and what it refuses to ask at all. Every page view the shop records from a
/// browser on the developer's own machine arrives over loopback, which is nowhere in the world - so the tracker
/// must not spend a call on it, and the only thing that can put a place on such a hit is the stand-in address a
/// shop names while it is being developed ('GeoLookup:DevelopmentAddress').
///
/// The lookup is answered by a stub here rather than by ip-api.com: what is pinned is which address is asked
/// about (and that a real visitor's own address always wins), never what a third party says about it.
/// </summary>
public class IpGeoLocatorTests
{
    /// <summary>What the stub lookup answers for any address it is asked about.</summary>
    private const string Nagpur =
        "{\"status\":\"success\",\"country\":\"India\",\"countryCode\":\"IN\",\"regionName\":\"Maharashtra\"," +
        "\"city\":\"Nagpur\",\"org\":\"Reliance Jio Infocomm Limited\"}";

    /// <summary>
    /// A caller no lookup can place - the developer's own localhost - is never sent, and the hit is recorded
    /// with its address alone. Nothing here may reach the network: a call spent on '::1' learns nothing.
    /// </summary>
    [Fact]
    public async Task ALoopbackCallerIsNeverAskedAbout()
    {
        var lookup = new StubLookup();

        var location = await Locator(lookup, new GeoLookupSettings()).LocateAsync("::1");

        Assert.Null(location);
        Assert.Empty(lookup.Asked);
    }

    /// <summary>
    /// The stand-in address is asked about in the loopback's place, and every place column the row has is
    /// answered from it - city, region, country, country code and the network - so the Portal Access screen
    /// can be read while the shop is being written.
    /// </summary>
    [Fact]
    public async Task TheDevelopmentAddressIsAskedAboutInPlaceOfTheLoopback()
    {
        var lookup = new StubLookup();

        var location = await Locator(lookup, new GeoLookupSettings { DevelopmentAddress = "49.36.120.9" })
            .LocateAsync("::1");

        Assert.NotNull(location);
        Assert.Equal("Nagpur", location.City);
        Assert.Equal("Maharashtra", location.Region);
        Assert.Equal("India", location.Country);
        Assert.Equal("IN", location.CountryCode);
        Assert.Equal("Reliance Jio Infocomm Limited", location.Organization);

        var asked = Assert.Single(lookup.Asked);
        Assert.Contains("/json/49.36.120.9?", asked);
    }

    /// <summary>
    /// A visitor who really is somewhere is asked about as themselves: the stand-in only fills in for addresses
    /// that are nowhere, so a development machine holding one never reads a real customer's country wrongly.
    /// </summary>
    [Fact]
    public async Task ARealCallerIsAskedAboutInsteadOfTheDevelopmentAddress()
    {
        var lookup = new StubLookup();

        var location = await Locator(lookup, new GeoLookupSettings { DevelopmentAddress = "49.36.120.10" })
            .LocateAsync("1.1.1.1");

        Assert.NotNull(location);

        var asked = Assert.Single(lookup.Asked);
        Assert.Contains("/json/1.1.1.1?", asked);
        Assert.DoesNotContain("49.36.120.10", asked);
    }

    /// <summary>
    /// A stand-in that is itself a private or unreadable address is no stand-in at all: '192.168.1.50' and a
    /// typo are both nowhere, so the loopback hit stays unplaced instead of being filed under a made-up place.
    /// </summary>
    [Theory]
    [InlineData("192.168.1.50")]
    [InlineData("127.0.0.1")]
    [InlineData("not an address")]
    [InlineData("")]
    public async Task AStandInThatIsNotAPlaceIsIgnored(string standIn)
    {
        var lookup = new StubLookup();

        var location = await Locator(lookup, new GeoLookupSettings { DevelopmentAddress = standIn })
            .LocateAsync("::1");

        Assert.Null(location);
        Assert.Empty(lookup.Asked);
    }

    /// <summary>
    /// With the lookup switched off nothing is asked at all - not even about the stand-in - and the hits are
    /// still recorded, which is what a shop that would rather not call a third party gets.
    /// </summary>
    [Fact]
    public async Task ASwitchedOffLookupAsksNothing()
    {
        var lookup = new StubLookup();

        var location = await Locator(
                lookup,
                new GeoLookupSettings { Enabled = false, DevelopmentAddress = "49.36.120.11" })
            .LocateAsync("1.1.1.2");

        Assert.Null(location);
        Assert.Empty(lookup.Asked);
    }

    /// <summary>
    /// A lookup that answers in its own words for 'no place for this address' is not a place either, and the hit
    /// is stored without one rather than with an empty one.
    /// </summary>
    [Fact]
    public async Task AnAddressTheLookupKnowsNothingAboutHasNoPlace()
    {
        var lookup = new StubLookup("{\"status\":\"fail\",\"message\":\"reserved range\"}");

        var location = await Locator(lookup, new GeoLookupSettings()).LocateAsync("1.1.1.3");

        Assert.Null(location);
        Assert.Single(lookup.Asked);
    }

    /// <summary>A locator answering with the given settings, and remembering what it was asked.</summary>
    private static IpGeoLocator Locator(StubLookup lookup, GeoLookupSettings settings)
    {
        var http = new HttpClient(lookup) { BaseAddress = new Uri(GeoLookupSettings.DefaultBaseUrl) };

        return new IpGeoLocator(http, settings, NullLogger<IpGeoLocator>.Instance);
    }

    /// <summary>
    /// The lookup service itself: answers every address with the same place, and keeps the paths it was asked
    /// for so a test can say what was sent and what was not.
    /// </summary>
    private sealed class StubLookup : HttpMessageHandler
    {
        private readonly string _answer;

        public StubLookup(string answer = Nagpur)
        {
            _answer = answer;
        }

        /// <summary>Every path asked of this lookup, in order - empty when it was never called.</summary>
        public List<string> Asked { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Asked.Add(request.RequestUri?.ToString() ?? string.Empty);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_answer, Encoding.UTF8, "application/json")
            });
        }
    }
}
