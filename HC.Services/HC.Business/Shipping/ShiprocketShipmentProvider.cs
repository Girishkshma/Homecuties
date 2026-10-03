using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HC.Business.Dtos;
using HC.Data.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HC.Business.Shipping;

/// <summary>
/// Talks to Shiprocket: signs in once with the shop's API credentials and asks about one parcel at a
/// time. It is the only place in the app that knows Shiprocket's URLs, its field names and its
/// vocabulary; everything above it works with <see cref="ShipmentTrackingSnapshotDto"/> - this is one
/// <see cref="IShipmentProvider"/> among however many the shop has configured (see
/// <see cref="IShipmentProviderRegistry"/>).
///
/// The provider's documentation is loose about the shape of its answers and its wording changes as its
/// couriers change, so nothing here trusts a field to be present: a missing or renamed field costs
/// that field on screen, never the call ('track this AWB' has to keep working after the next rename).
/// The status text is passed on untouched - reading it is <see cref="ShipmentStatusFlow"/>'s job.
///
/// Endpoints come from configuration ('Shiprocket:BaseUrl', 'Shiprocket:AuthPath',
/// 'Shiprocket:TrackPath') so a provider that moves an endpoint, or a test account, needs a settings
/// change rather than a release.
///
/// Credentials are an API user the shop makes in the provider's own panel (Settings > API > Configure
/// > 'Create An API User'), with an e-mail and a password of its own - not the panel login - and belong
/// in the git-ignored 'appsettings.Local.json' or in the environment variables 'Shiprocket__Email' /
/// 'Shiprocket__Password', never in the committed 'appsettings.json'. They are posted as they are
/// ('{ "email": ..., "password": ... }', which is the shape the provider asks for: it answers a body
/// without them with "The email is required." / "The password is required."), and the token that comes
/// back is what every later call carries as 'Authorization: Bearer ...' - the header the provider
/// answers 'unauthorized request' without. Its portal asks callers to generate a token per request;
/// one token really does serve the whole account, so holding it is both allowed and the only way a
/// page of parcels does not pay for a sign-in per parcel (its sign-ins are rate-limited). It is
/// renewed by the clock ('TokenLifetimeHours') and, if the provider still refuses it, by one forced
/// sign-in - safe to do, because the provider answers a URL it does not have with 404, so a 401 is
/// always about the token and never about the path.
///
/// Only tracking is wired today, and only by AWB: the shop team books the parcel in the provider's panel
/// and types the consignment number into the admin order screen. The provider's own booking surface is
/// the next step if that ever moves in here - creating the order ('v1/external/orders/create/adhoc'),
/// its AWB ('v1/external/courier/assign/awb'), the label, the pickup and the manifest
/// ('v1/external/courier/generate/label', '.../generate/pickup', 'v1/external/manifests/generate'),
/// cancelling ('v1/external/orders/cancel'), rate and serviceability by PIN code
/// ('v1/external/courier/serviceability'), the pickup locations a booking needs
/// ('v1/external/settings/company/pickup') and tracking a parcel that has no AWB yet
/// ('v1/external/courier/track/shipment/{0}'). Each of those routes is live on the API, and adding one
/// is one method here - no screen, no order rule and no column moves. The list is kept here on purpose:
/// this class is the only place allowed to know the provider's vocabulary, so it is where the vocabulary
/// the shop does not use yet belongs too.
/// </summary>
public class ShiprocketShipmentProvider : IShipmentProvider
{
    /// <summary>The provider's API, unless configured otherwise.</summary>
    private const string DefaultBaseUrl = "https://apiv2.shiprocket.in/";

    /// <summary>Login: the account's e-mail and password in, a bearer token out.</summary>
    private const string DefaultLoginPath = "v1/external/auth/login";

    /// <summary>One parcel by AWB; '{0}' is where the number goes.</summary>
    private const string DefaultTrackingPath = "v1/external/courier/track/awb/{0}";

    /// <summary>The provider's public tracking page, used when its answer names no page of its own.</summary>
    private const string PublicTrackingUrl = "https://shiprocket.co/tracking/{0}";

    private const string NotConfiguredMessage =
        "Tracking is not available from the shop right now.";

    private const string LoginFailedMessage =
        "Tracking is unavailable at the moment - the courier's system did not let us in.";

    private const string UnreachableMessage =
        "The courier could not be reached just now, so this parcel's latest status could not be fetched. Please try again in a moment.";

    private const string NoInformationMessage =
        "The courier has not reported anything about this parcel yet.";

    private const string NoAwbMessage =
        "This order has no AWB number to track yet.";

    /// <summary>How much of an unexpected answer a log line keeps (the rest is dropped).</summary>
    private const int ExcerptLength = 300;

    // One token serves the whole account, so it is kept once for the process rather than per caller: a
    // typed HttpClient hands out a new ShiprocketShipmentProvider for every request, and signing in per request
    // would burn logins (the provider rate-limits them) and add a round trip to every tracking pull.
    // The gate is what stops a 'My Orders' page with five parcels from starting five logins at once.
    private static readonly SemaphoreSlim LoginGate = new(1, 1);
    private static string _token = string.Empty;
    private static DateTimeOffset _tokenExpiresOn = DateTimeOffset.MinValue;

    private readonly HttpClient _http;
    private readonly ILogger<ShiprocketShipmentProvider> _logger;
    private readonly string _email;
    private readonly string _password;
    private readonly string _loginPath;
    private readonly string _trackingPath;
    private readonly TimeSpan _tokenLifetime;
    private readonly bool _enabled;

    public ShiprocketShipmentProvider(HttpClient http, IConfiguration configuration, ILogger<ShiprocketShipmentProvider> logger)
    {
        _http = http;
        _logger = logger;

        _email = (configuration["Shiprocket:Email"] ?? string.Empty).Trim();
        _password = configuration["Shiprocket:Password"] ?? string.Empty;

        BaseUrl = Endpoint(configuration, "Shiprocket:BaseUrl", DefaultBaseUrl);
        _loginPath = Endpoint(configuration, "Shiprocket:AuthPath", DefaultLoginPath);
        _trackingPath = Endpoint(configuration, "Shiprocket:TrackPath", DefaultTrackingPath);

        _enabled = !string.Equals(configuration["Shiprocket:Enabled"], "false", StringComparison.OrdinalIgnoreCase);

        // Renewed well before the provider retires it (its tokens are good for a day, or ten on some
        // plans): a token used up to its last minute would fail mid-page, and the 401 retry below would
        // pay for a login while a customer waits.
        var hours = int.TryParse(configuration["Shiprocket:TokenLifetimeHours"], NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var configured) && configured > 0 ? configured : 20;
        _tokenLifetime = TimeSpan.FromHours(hours);
    }

    public bool IsConfigured => _enabled && _email.Length > 0 && _password.Length > 0;

    /// <summary>
    /// The id a parcel booked here is recorded under (OrderShipments.Provider), taken from the entity's
    /// own constant so the column and the adapter can never drift apart.
    /// </summary>
    public string Name => OrderShipment.ShiprocketProvider;

    /// <summary>
    /// The AWB is the courier's own - it is read off the Shiprocket panel and typed in by the shop team -
    /// so nothing is minted here (see IShipmentProvider).
    /// </summary>
    public bool AwbGeneratedBySystem => false;

    /// <summary>
    /// This adapter is an aggregator: a parcel booked with it is asked about, rather than only written
    /// down (the opposite of OrderShipment.CustomProvider).
    /// </summary>
    public bool ReportsTracking => true;

    /// <summary>
    /// Never asked: the AWB is the courier's own and the shop types it in (see AwbGeneratedBySystem), so
    /// this adapter has no reference of its own to give.
    /// </summary>
    public string CreateAwb(long orderId, DateTime now) => string.Empty;

    /// <summary>What the shop team reads when this adapter is offered on the admin order screen.</summary>
    private const string ProviderDisplayName = "Shiprocket";

    /// <summary>Who this adapter is, whether it can be used and where it talks to (never the credentials).</summary>
    public ShipmentProviderInfo Describe() => new()
    {
        Name = Name,
        DisplayName = ProviderDisplayName,
        Configured = IsConfigured,
        ApiBaseUrl = BaseUrl,
        TrackingPath = _trackingPath
    };

    public string BaseUrl { get; }

    public string LoginPath => _loginPath;

    public string TrackingPath => _trackingPath;

    public async Task<ShipmentTrackingSnapshotDto> TrackByAwbAsync(
        string awbNumber, CancellationToken cancellationToken = default)
    {
        var awb = (awbNumber ?? string.Empty).Trim();

        if (awb.Length == 0)
            return Failed(string.Empty, NoAwbMessage);

        if (!IsConfigured)
        {
            _logger.LogWarning(
                "Shiprocket tracking was asked for AWB '{Awb}' but the integration is not configured. Set " +
                "'Shiprocket:Email' and 'Shiprocket:Password' in appsettings.Local.json (or the environment " +
                "variables 'Shiprocket__Email' / 'Shiprocket__Password') and leave 'Shiprocket:Enabled' unset " +
                "or 'true'.",
                awb);

            return Failed(awb, NotConfiguredMessage);
        }

        // Twice at most, and only for the one failure worth retrying: a token the provider has retired
        // since it was cached answers 401, so the second call goes out with a fresh one. A timeout or a
        // 500 is reported as it is - asking again would only keep the screen waiting.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await LoginAsync(forceRefresh: attempt > 0, awb, cancellationToken);

            if (token.Length == 0)
                return Failed(awb, LoginFailedMessage);

            var (answer, unauthorized) = await AskAsync(awb, token, cancellationToken);

            if (answer != null)
                return answer;

            if (!unauthorized)
                return Failed(awb, UnreachableMessage);

            _logger.LogWarning(
                "Shiprocket refused the cached token for AWB '{Awb}' (401); signing in again.", awb);
        }

        return Failed(awb, UnreachableMessage);
    }

    /// <summary>
    /// Asks the provider about <paramref name="awb"/> and reads its answer. Null comes back when the
    /// call produced no snapshot at all, and then <c>Unauthorized</c> says whether the token was the
    /// reason (worth one retry) or the provider was simply unreachable (not worth one).
    /// </summary>
    private async Task<(ShipmentTrackingSnapshotDto? Answer, bool Unauthorized)> AskAsync(
        string awb, string token, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, TrackUrl(awb));
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");

            using var response = await _http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return (null, true);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Shiprocket tracking for AWB '{Awb}' answered {StatusCode}.",
                    awb, (int)response.StatusCode);
                return (null, false);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var answer = ParseTracking(awb, json);

            if (answer.Succeeded)
            {
                _logger.LogInformation(
                    "Shiprocket tracking for AWB '{Awb}': {Status} ({Courier}).",
                    awb, answer.StatusText, answer.CourierName);
            }
            else
            {
                _logger.LogInformation(
                    "Shiprocket tracking for AWB '{Awb}' had nothing to report: {Message}",
                    awb, answer.Message);
            }

            return (answer, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The page that asked is gone - not this service's answer to give (see PincodeService).
            throw;
        }
        catch (Exception exception)
        {
            // A timeout, DNS, TLS, or a body that is not the JSON expected below: all the same to the
            // person waiting, who is told the latest status could not be fetched just now. The reason
            // goes to the log, where the shop team can see it.
            _logger.LogWarning(exception, "Shiprocket tracking for AWB '{Awb}' failed.", awb);
            return (null, false);
        }
    }

    /// <summary>
    /// The bearer token to call with, signing in when there is none (or when the cached one was
    /// refused). The gate is held only for the login itself, so the parcels of one page share whatever
    /// token the first of them fetched.
    /// </summary>
    private async Task<string> LoginAsync(bool forceRefresh, string awb, CancellationToken cancellationToken)
    {
        if (!forceRefresh && _token.Length > 0 && _tokenExpiresOn > DateTimeOffset.UtcNow)
            return _token;

        await LoginGate.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have signed in while this one waited for the gate.
            if (!forceRefresh && _token.Length > 0 && _tokenExpiresOn > DateTimeOffset.UtcNow)
                return _token;

            using var response = await _http.PostAsJsonAsync(
                _loginPath,
                new { email = _email, password = _password },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Shiprocket login was refused with {StatusCode} - check 'Shiprocket:Email' / " +
                    "'Shiprocket:Password'. (AWB '{Awb}')",
                    (int)response.StatusCode, awb);

                return string.Empty;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var token = ReadToken(json);

            if (token.Length == 0)
            {
                // The answer is worth keeping for diagnosis: the provider sometimes replies 200 with a
                // message instead of a token, and that message is the only clue there is. It can never
                // hold a token, because this branch is only reached when the answer had none.
                _logger.LogWarning(
                    "Shiprocket login answered without a token, so tracking AWB '{Awb}' is not possible. Answer: {Answer}",
                    awb, Excerpt(json));

                return string.Empty;
            }

            _token = token;
            _tokenExpiresOn = DateTimeOffset.UtcNow.Add(_tokenLifetime);
            return _token;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Shiprocket login failed; tracking AWB '{Awb}' is not possible.", awb);
            return string.Empty;
        }
        finally
        {
            LoginGate.Release();
        }
    }

    /// <summary>The access token out of the login answer ('{ "token": "..." }').</summary>
    private static string ReadToken(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var token = Text(root, "token");
            return token.Length > 0 ? token : Text(Object(root, "data"), "token");
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Reads one parcel out of the provider's answer. Every field is looked for by name (ignoring
    /// case) and every block is optional: the answer is documented only loosely, so a missing or
    /// renamed field has to cost that field on screen, never the whole call.
    /// </summary>
    private static ShipmentTrackingSnapshotDto ParseTracking(string awb, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // The parcel sits under 'tracking_data'; a mirror or a hand-made answer may put it at the root.
        var data = Object(root, "tracking_data") ?? root;
        var parcel = First(ObjectArray(data, "shipment_track"));
        var activities = ObjectArray(data, "shipment_track_activities");

        // The parcel's own summary first, then the newest line of the tracking trail (the provider
        // lists the activities newest first). Its wording is passed on exactly as it came.
        var statusText = Text(parcel, "current_status");
        var latest = First(activities);
        if (statusText.Length == 0)
            statusText = Text(latest, "status");
        if (statusText.Length == 0)
            statusText = Text(latest, "activity");

        if (statusText.Length == 0)
            return Failed(awb, NoInformationMessage);

        var reportedAwb = Text(parcel, "awb_code");
        if (reportedAwb.Length == 0)
            reportedAwb = Text(data, "awb_code");

        var trackingUrl = Text(data, "track_url");
        if (trackingUrl.Length == 0)
            trackingUrl = Text(parcel, "track_url");
        if (trackingUrl.Length == 0)
        {
            // No page from the provider: the shop can still send the customer to the provider's public
            // tracking page for this number, which is better than showing no link at all.
            trackingUrl = string.Format(
                CultureInfo.InvariantCulture, PublicTrackingUrl, Uri.EscapeDataString(awb));
        }

        return new ShipmentTrackingSnapshotDto
        {
            Succeeded = true,
            AwbNumber = reportedAwb.Length > 0 ? reportedAwb : awb,
            CourierName = Text(parcel, "courier_name"),
            TrackingUrl = trackingUrl,
            StatusText = statusText,
            StatusCode = Number(data, "shipment_status") ?? Number(parcel, "current_status_id"),
            DeliveredOn = DeliveredOnFrom(activities)
                ?? ProviderDate(Text(parcel, "delivered_date"))
                ?? ProviderDate(Text(data, "delivered_date"))
        };
    }

    /// <summary>
    /// The delivery date out of the tracking trail: the newest activity that reads as a delivery, with
    /// the date the provider put next to it.
    /// </summary>
    private static DateTime? DeliveredOnFrom(JsonElement? activities)
    {
        if (activities is not { ValueKind: JsonValueKind.Array } trail)
            return null;

        foreach (var activity in trail.EnumerateArray())
        {
            var status = Text(activity, "status");
            if (status.Length == 0)
                status = Text(activity, "activity");

            if (ShipmentStatusFlow.FromProviderText(status) != ShipmentStage.Delivered)
                continue;

            var date = ProviderDate(Text(activity, "date"));
            if (date != null)
                return date;
        }

        return null;
    }

    /// <summary>A failed snapshot, in words for the person waiting - never an exception (see the DTO).</summary>
    private static ShipmentTrackingSnapshotDto Failed(string awb, string message) => new()
    {
        Succeeded = false,
        Message = message,
        AwbNumber = awb
    };

    /// <summary>The track URL for one AWB, with the number escaped into the configured path.</summary>
    private string TrackUrl(string awb)
    {
        var escaped = Uri.EscapeDataString(awb);

        // A configured path that forgot the placeholder still works: the number is appended to it,
        // where a wrong template would otherwise throw while a customer waits.
        return _trackingPath.Contains("{0}", StringComparison.Ordinal)
            ? _trackingPath.Replace("{0}", escaped, StringComparison.Ordinal)
            : $"{_trackingPath.TrimEnd('/')}/{escaped}";
    }

    /// <summary>
    /// A configured endpoint, with the leading '/' the HttpClient's BaseAddress cannot take removed and
    /// the shipping default used when nothing was configured.
    /// </summary>
    private static string Endpoint(IConfiguration configuration, string key, string fallback)
    {
        var configured = (configuration[key] ?? string.Empty).Trim();
        return configured.Length > 0 ? configured.TrimStart('/') : fallback;
    }

    /// <summary>The first few characters of a body, for a log line (never the whole of it).</summary>
    private static string Excerpt(string body)
    {
        var text = body.Trim();
        return text.Length <= ExcerptLength ? text : text[..ExcerptLength] + "...";
    }

    // ---------------------------------------------------------------------------------------------
    // Reading the provider's answer. All of it is by name, ignoring case, and anything that is missing
    // or of the wrong shape is simply absent rather than a failure: the answer is the provider's to
    // change, and a renamed field must never take a tracking pull down with it.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A named value on <paramref name="element"/>, whatever kind it is (null when absent).</summary>
    private static JsonElement? Property(JsonElement? element, string name)
    {
        if (element is not { ValueKind: JsonValueKind.Object } parent)
            return null;

        foreach (var property in parent.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return null;
    }

    /// <summary>A named value as text ('' when it is absent or not a string - a number is not text here).</summary>
    private static string Text(JsonElement? element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.String } value
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>A named value as a whole number, whether the provider sent 7 or '7' (null when neither).</summary>
    private static int? Number(JsonElement? element, string name)
    {
        if (Property(element, name) is not { } value)
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(
                value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var text) => text,
            _ => null
        };
    }

    /// <summary>A named object (null when it is absent or is not an object).</summary>
    private static JsonElement? Object(JsonElement? element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.Object } value ? value : null;

    /// <summary>A named array (null when it is absent or is not an array).</summary>
    private static JsonElement? ObjectArray(JsonElement? element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.Array } value ? value : null;

    /// <summary>The first entry of an array (null when it is absent or empty).</summary>
    private static JsonElement? First(JsonElement? array) =>
        array is { ValueKind: JsonValueKind.Array } items && items.GetArrayLength() > 0 ? items[0] : null;

    // The provider reports Indian times ('2023-01-05 14:03:38') without saying which clock they are
    // on. They are read as India Standard Time, which is the clock an Indian courier reports in and
    // the clock the screens in front of customers show: stored as a true instant, the browser puts it
    // back to the wall clock the courier wrote down. An answer that does carry an offset ('...Z',
    // '...+05:30') is believed instead. The date only ever reaches the screen as 'Delivered on <date>',
    // and the shop's own history entry is stamped with the server clock regardless.
    private static readonly TimeSpan IndiaStandardTimeOffset = TimeSpan.FromHours(5.5);

    /// <summary>A date the provider wrote, as a UTC instant (null when it cannot be read).</summary>
    private static DateTime? ProviderDate(string text)
    {
        if (text.Length == 0)
            return null;

        if (HasOffset(text) &&
            DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var zoned))
        {
            return zoned.UtcDateTime;
        }

        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var wallClock))
            return null;

        return DateTime.SpecifyKind(wallClock - IndiaStandardTimeOffset, DateTimeKind.Utc);
    }

    /// <summary>
    /// Whether the provider's date says which offset it is on. 'DateTimeOffset.TryParse' would
    /// otherwise assume the server's own zone, which is exactly the guess this avoids.
    /// </summary>
    private static bool HasOffset(string text)
    {
        if (text.EndsWith('Z') || text.EndsWith('z'))
            return true;

        // Written after the date part only: '2023-01-05T14:03:38+05:30' / '2023-01-05 14:03:38 0530'.
        var sign = text.LastIndexOfAny(['+', '-']);
        return sign > 10 && text[(sign + 1)..].Any(char.IsAsciiDigit);
    }
}
