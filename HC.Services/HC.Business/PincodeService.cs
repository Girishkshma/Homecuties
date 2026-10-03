using System.Collections.Concurrent;
using System.Text.Json;
using HC.Business.Dtos;

namespace HC.Business;

/// <summary>
/// Answers "which city, state and areas does this PIN code cover?" from the public India Post
/// directory the shop has chosen to use (see 'Pincode:BaseUrl').
///
/// Two things about that directory shape this class: it is a free public service with no promise of
/// uptime, and its data changes a few times a year at most. So every answer is remembered, a slow or
/// failed call is answered with a plain message rather than an error, and nothing here throws. The
/// address forms cannot fill the city and the state in without an answer, so what went wrong has to
/// reach the customer as words they can act on.
/// </summary>
public class PincodeService : IPincodeService
{
    /// <summary>An Indian PIN code is six digits - anything else is a typo, not a lookup.</summary>
    private const int PincodeLength = 6;

    /// <summary>
    /// Shown when the directory has no such PIN, which is nearly always a typo. The city and the state
    /// are not typed into - the PIN code is the only thing that fills them - so checking the PIN code is
    /// the only way on, and this says so rather than sending the customer looking for a field to type.
    /// (The web forms carry the same wording; see 'pincode-lookup.ts'.)
    /// </summary>
    private const string NotFoundMessage =
        "We could not find that PIN code, so the city and the state cannot be filled in. " +
        "Please check the PIN code.";

    /// <summary>Shown when the directory could not be asked at all - nothing was wrong with the PIN itself.</summary>
    private const string DirectoryUnavailableMessage =
        "We could not look that PIN code up right now, so the city and the state cannot be filled in. " +
        "Please try again in a moment.";

    /// <summary>
    /// How long an answer from the directory is reused: long for a PIN that exists (the directory
    /// changes a few times a year at most) and short for one that does not, because PIN codes are added.
    /// </summary>
    private static readonly TimeSpan FoundLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan NotFoundLifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// How many PIN codes are remembered at once. Typing a PIN code is the only way in, so this is just
    /// a ceiling that stops made-up numbers from filling memory - expired entries are dropped first.
    /// </summary>
    private const int CacheLimit = 20000;

    /// <summary>Answers already fetched, shared by every request (this service is created per request).</summary>
    private static readonly ConcurrentDictionary<string, CachedAnswer> Answers = new();

    private readonly HttpClient _http;

    /// <summary>The HttpClient comes from the container, so its base address and timeout are configured once.</summary>
    public PincodeService(HttpClient http)
    {
        _http = http;
    }

    public async Task<PincodeLookupResultDto> LookupAsync(string? pincode, CancellationToken cancellationToken = default)
    {
        var pin = (pincode ?? "").Trim();

        if (pin.Length != PincodeLength || !pin.All(char.IsAsciiDigit))
            return Failed(pin, NotFoundMessage);

        if (Answers.TryGetValue(pin, out var cached) && cached.ExpiresOn > DateTimeOffset.UtcNow)
            return cached.Answer;

        var (answer, fromDirectory) = await FetchAsync(pin, cancellationToken);

        // Only what the directory actually said is worth remembering: a directory that could not be
        // reached would otherwise be remembered as "there is no such PIN" for the next hour.
        if (fromDirectory)
            Remember(pin, answer);

        return answer;
    }

    /// <summary>
    /// Asks the directory about <paramref name="pin"/>. The flag says whether the directory answered at
    /// all, which is what decides whether the answer may be remembered.
    /// </summary>
    private async Task<(PincodeLookupResultDto Answer, bool FromDirectory)> FetchAsync(
        string pin, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync($"pincode/{pin}", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return (Failed(pin, DirectoryUnavailableMessage), false);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return (Parse(pin, json), true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The customer closed the page or the call was aborted - not this service's answer to give.
            throw;
        }
        catch (Exception)
        {
            // A timeout, DNS, TLS, a 500, or an answer that is not the JSON expected below: all the same
            // to the customer, who is told the city and the state could not be filled in from their PIN
            // code and asked to try again.
            return (Failed(pin, DirectoryUnavailableMessage), false);
        }
    }

    /// <summary>
    /// Reads the directory's answer, which is an array of one object holding 'Status' and 'PostOffice'.
    /// </summary>
    private static PincodeLookupResultDto Parse(string pin, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            return Failed(pin, NotFoundMessage);

        var answer = root[0];

        if (!string.Equals(Text(answer, "Status"), "Success", StringComparison.OrdinalIgnoreCase) ||
            !answer.TryGetProperty("PostOffice", out var offices) ||
            offices.ValueKind != JsonValueKind.Array)
        {
            return Failed(pin, NotFoundMessage);
        }

        // Every post office the PIN covers is offered, as the public directory lists them - the site a
        // customer would otherwise have to check themselves. The delivering office is put first rather
        // than the others being dropped: for 600001 the office that delivers is the GPO, while the six
        // places a customer would name in their address ("Sowcarpet", "Flower Bazaar") do not deliver it.
        var areas = offices
            .EnumerateArray()
            .Select(ToArea)
            .Where(area => area != null)
            .Select(area => area!)
            .GroupBy(a => a.Area, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(a => a.Delivers)
            .ToList();

        if (areas.Count == 0)
            return Failed(pin, NotFoundMessage);

        return new PincodeLookupResultDto
        {
            Result = 1,
            Pincode = pin,
            Areas = areas,
            Messages = new[]
            {
                areas.Count == 1
                    ? $"PIN code {pin}: {Describe(areas[0])}."
                    : $"PIN code {pin} covers {areas.Count} post offices."
            }
        };
    }

    /// <summary>One post office as an area, or null when the entry names no place at all.</summary>
    private static PincodeAreaDto? ToArea(JsonElement office)
    {
        var area = Place(office, "Name");
        if (area.Length == 0)
            return null;

        // The district is what the directory calls a city. Entries without one usually name the block or
        // the postal division instead, which places the customer just as well.
        var city = Place(office, "District");
        if (city.Length == 0)
            city = Place(office, "Block");
        if (city.Length == 0)
            city = Place(office, "Division");

        return new PincodeAreaDto
        {
            Area = area,
            City = city,
            State = Place(office, "State"),
            BranchType = Text(office, "BranchType"),
            Delivers = string.Equals(Text(office, "DeliveryStatus"), "Delivery", StringComparison.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    /// A place name from the directory. The directory writes "NA" where it has nothing, which must not be
    /// shown to a customer as a city ("Flower Bazaar" serves 600001 with a block of "NA").
    /// </summary>
    private static string Place(JsonElement element, string propertyName)
    {
        var value = Text(element, propertyName);
        return string.Equals(value, "NA", StringComparison.OrdinalIgnoreCase) ? "" : value;
    }

    /// <summary>A property of the directory's JSON as trimmed text - empty when missing or not a string.</summary>
    private static string Text(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? (value.GetString() ?? "").Trim()
            : "";
    }

    /// <summary>"Chennai, Tamil Nadu" - the place an area sits in, falling back to the area itself.</summary>
    private static string Describe(PincodeAreaDto area)
    {
        var place = string.Join(", ", new[] { area.City, area.State }.Where(part => part.Length > 0));
        return place.Length > 0 ? place : area.Area;
    }

    private static PincodeLookupResultDto Failed(string pin, string message)
    {
        return new PincodeLookupResultDto
        {
            Result = 0,
            Pincode = pin,
            Areas = new List<PincodeAreaDto>(),
            Messages = new[] { message }
        };
    }

    private static void Remember(string pin, PincodeLookupResultDto answer)
    {
        if (Answers.Count >= CacheLimit)
            DropExpired();

        var lifetime = answer.Result == 1 ? FoundLifetime : NotFoundLifetime;
        Answers[pin] = new CachedAnswer(answer, DateTimeOffset.UtcNow.Add(lifetime));
    }

    private static void DropExpired()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in Answers)
        {
            if (entry.Value.ExpiresOn <= now)
                Answers.TryRemove(entry.Key, out _);
        }
    }

    /// <summary>An answer from the directory and the moment it stops being reused.</summary>
    private sealed record CachedAnswer(PincodeLookupResultDto Answer, DateTimeOffset ExpiresOn);
}
