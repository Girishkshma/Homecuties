using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Http;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace HC.Adapters.Messaging;

/// <summary>
/// Sends one text message through Twilio: the shop's account, its Twilio number (or Messaging
/// Service), and the message. It is the only place in the app that knows Twilio's configuration
/// keys, its sender rules and its error codes; everything above it works with
/// <see cref="SmsSendResult"/> - this is one <see cref="ISmsSender"/> among however many the shop
/// configures.
///
/// The configuration section is 'Twilio':
///
///  * 'AccountSid' ('AC...') names the account a message is sent through, and is always needed: the
///    request path is the account ('/Accounts/AC.../Messages.json'), whoever is allowed to send
///    through it.
///  * 'AuthToken' - or, preferably, 'ApiKeySid' ('SK...') with 'ApiKeySecret' - is what allows the
///    send. An API Key is used whenever both of its halves are set, because a key can be revoked on
///    its own, without touching the account's auth token. All of them are secrets and belong in the
///    git-ignored 'appsettings.Local.json' or in the environment variables ('Twilio__AccountSid',
///    'Twilio__ApiKeySid', 'Twilio__ApiKeySecret', 'Twilio__AuthToken'), never in the committed
///    'appsettings.json'.
///  * 'FromNumber' is the Twilio number the shop texts from (e.g. '+17372508034') - what the
///    integration sample passes as <c>from</c>. Without it, a send is refused: there is no number to
///    send from.
///  * 'MessagingServiceSid' ('MG...') is the alternative to 'FromNumber': Twilio then picks the
///    sender from the shop's Messaging Service, which is how a sender pool, sticky senders and
///    per-country numbers are used. When both are set the Messaging Service wins, because that is
///    the sender the shop set up on purpose.
///  * 'Enabled' false keeps the credentials but stops every send - useful on a development machine.
///  * 'DefaultCountryCode' is the country a bare 10-digit number is read in (see
///    <see cref="SmsPhoneNumber"/>).
///
/// A send never throws (the one exception is a cancellation the caller asked for). Twilio being
/// down, the credentials being wrong and the number being one Twilio will not text all come back as
/// <see cref="SmsSendResult.Succeeded"/> false with a sentence for the person waiting, and the
/// detail - Twilio's error code and message - in the log.
///
/// The adapter is registered once (see <see cref="TwilioServiceCollectionExtensions"/>) because the
/// <see cref="TwilioRestClient"/> it holds is thread-safe: a new client per request would rebuild
/// the HTTP stack for every text message.
/// </summary>
public class TwilioSmsSender : ISmsSender
{
    /// <summary>The id this adapter is logged and listed under.</summary>
    public const string SenderName = "Twilio";

    /// <summary>What the shop team reads in a list of senders.</summary>
    private const string SenderDisplayName = "Twilio SMS";

    /// <summary>The longest text Twilio accepts in one message (longer ones must be sent in parts).</summary>
    public const int MaxBodyLength = 1600;

    /// <summary>
    /// How long a send may wait on Twilio. Short on purpose: an SMS is normally sent while somebody
    /// is waiting for the action that caused it (a booking, a dispatch), and a send that hangs
    /// holds that action with it.
    /// </summary>
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(30);

    private const string SwitchedOffMessage =
        "Text messages are switched off on the shop right now.";

    private const string NotConfiguredMessage =
        "Text messages are not set up on the shop right now.";

    private const string InvalidNumberMessage =
        "That mobile number is not one a text message can be sent to.";

    private const string EmptyBodyMessage =
        "There is nothing to send - the text message is empty.";

    private const string TooLongMessage =
        "The text message is longer than the 1600 characters Twilio accepts.";

    private const string RefusedMessage =
        "Twilio would not accept the text message.";

    private const string UnreachableMessage =
        "The text message could not be sent just now. Please try again in a moment.";

    private readonly ILogger<TwilioSmsSender> _logger;
    private readonly string _accountSid;
    private readonly string _messagingServiceSid;
    private readonly string _fromNumber;
    private readonly string _defaultCountryCode;
    private readonly bool _enabled;
    private readonly ITwilioRestClient? _client;

    public TwilioSmsSender(IConfiguration configuration, ILogger<TwilioSmsSender> logger)
    {
        _logger = logger;
        _accountSid = (configuration["Twilio:AccountSid"] ?? string.Empty).Trim();
        _messagingServiceSid = (configuration["Twilio:MessagingServiceSid"] ?? string.Empty).Trim();
        _fromNumber = (configuration["Twilio:FromNumber"] ?? string.Empty).Trim();
        _defaultCountryCode = SmsPhoneNumber.CountryCode(configuration["Twilio:DefaultCountryCode"]);

        _enabled = !string.Equals(
            configuration["Twilio:Enabled"], "false", StringComparison.OrdinalIgnoreCase);

        var authToken = (configuration["Twilio:AuthToken"] ?? string.Empty).Trim();
        var apiKeySid = (configuration["Twilio:ApiKeySid"] ?? string.Empty).Trim();
        var apiKeySecret = (configuration["Twilio:ApiKeySecret"] ?? string.Empty).Trim();

        // Twilio's two kinds of credential - the account's auth token, and an API Key (a SID and a
        // secret) - are both 'username and password' to the client, but neither one takes the
        // account's place in the request path. The prefixes are checked because the mistake they
        // catch is silent otherwise: an API Key SID pasted into 'Twilio:AccountSid' builds a client
        // that asks Twilio for '/Accounts/SK.../Messages.json', which no account answers.
        var accountSidUsable = HasPrefix(_accountSid, "AC");
        var apiKeyUsable = HasPrefix(apiKeySid, "SK") && apiKeySecret.Length > 0;

        if (_accountSid.Length > 0 && !accountSidUsable)
        {
            _logger.LogWarning(
                "'Twilio:AccountSid' does not look like an account SID, which starts with 'AC'. An " +
                "API Key SID ('SK...') goes in 'Twilio:ApiKeySid', beside 'Twilio:ApiKeySecret' - " +
                "the account SID is still needed, because the message is sent through the account. " +
                "No text message will be sent until this is fixed.");
        }

        if (apiKeySid.Length > 0 && !apiKeyUsable)
        {
            _logger.LogWarning(
                "'Twilio:ApiKeySid' is set but cannot be used: an API Key SID starts with 'SK' and " +
                "needs 'Twilio:ApiKeySecret' with the same key's secret beside it. No text message " +
                "will be sent until this is fixed.");
        }

        // The client is built only when there is something to build it with, and it never talks to
        // Twilio here - no request goes out until a message is actually sent. That keeps startup
        // working on a machine with no credentials, exactly like the shipping adapters.
        if (accountSidUsable && (apiKeyUsable || authToken.Length > 0))
        {
            // Twilio's client takes its own HTTP abstraction (not a System.Net.Http.HttpClient), so the
            // transport is wrapped once here - that is also where the timeout lives, since a send must
            // not hold the action that caused it for longer than SendTimeout.
            _client = new TwilioRestClient(
                username: apiKeyUsable ? apiKeySid : _accountSid,
                password: apiKeyUsable ? apiKeySecret : authToken,
                accountSid: _accountSid,
                httpClient: new SystemNetHttpClient(
                    new System.Net.Http.HttpClient { Timeout = SendTimeout }));
        }
    }

    /// <summary>
    /// True when there is an account to send through, a sender to send from (a number or a Messaging
    /// Service) and the integration is not switched off. A send through an adapter that is not
    /// configured is refused before Twilio is called.
    /// </summary>
    public bool IsConfigured =>
        _enabled &&
        _client != null &&
        (_messagingServiceSid.Length > 0 || _fromNumber.Length > 0);

    /// <summary>Who this adapter is, whether it can be used and where it sends from (never secrets).</summary>
    public SmsSenderInfo Describe() => new()
    {
        Name = SenderName,
        DisplayName = SenderDisplayName,
        Configured = IsConfigured,

        // A Messaging Service picks the sender itself, so there is no fixed number to report - only
        // the fact that one is used (SmsSenderInfo's own rule: 'From' is "" then).
        From = _messagingServiceSid.Length > 0 ? string.Empty : _fromNumber,
        UsesMessagingService = _messagingServiceSid.Length > 0,
        DefaultCountryCode = _defaultCountryCode
    };

    /// <summary>
    /// Sends <paramref name="body"/> to <paramref name="to"/>, or explains why it could not be sent.
    /// The checks come first, in the order that saves the most work: a number that cannot be texted
    /// and an empty message are refused before Twilio is called, and so is an adapter that is
    /// switched off or not set up.
    /// </summary>
    public async Task<SmsSendResult> SendAsync(
        string to, string body, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!SmsPhoneNumber.TryNormalize(to, _defaultCountryCode, out var number))
        {
            _logger.LogWarning(
                "A text message was not sent because '{Number}' could not be read as a mobile number.",
                Mask(to));

            return SmsSendResult.Failed(to ?? string.Empty, InvalidNumberMessage);
        }

        var text = (body ?? string.Empty).Trim();

        if (text.Length == 0)
            return SmsSendResult.Failed(number, EmptyBodyMessage);

        if (text.Length > MaxBodyLength)
        {
            _logger.LogWarning(
                "A text message to '{Number}' was not sent because it is {Length} characters long " +
                "(Twilio accepts at most {Max}).",
                Mask(number), text.Length, MaxBodyLength);

            return SmsSendResult.Failed(number, TooLongMessage);
        }

        if (!_enabled)
        {
            _logger.LogWarning(
                "A text message to '{Number}' was not sent because 'Twilio:Enabled' is false.",
                Mask(number));

            return SmsSendResult.Failed(number, SwitchedOffMessage);
        }

        if (_client is null || (_messagingServiceSid.Length == 0 && _fromNumber.Length == 0))
        {
            _logger.LogWarning(
                "A text message to '{Number}' was not sent because the Twilio integration is not " +
                "configured. Set 'Twilio:AccountSid' ('AC...') with either 'Twilio:AuthToken' or " +
                "'Twilio:ApiKeySid' and 'Twilio:ApiKeySecret', plus either 'Twilio:FromNumber' or " +
                "'Twilio:MessagingServiceSid', in appsettings.Local.json (or the environment " +
                "variables 'Twilio__AccountSid' / 'Twilio__ApiKeySid' / 'Twilio__ApiKeySecret' / " +
                "'Twilio__AuthToken' / 'Twilio__FromNumber' / 'Twilio__MessagingServiceSid'), and " +
                "leave 'Twilio:Enabled' unset or 'true'.",
                Mask(number));

            return SmsSendResult.Failed(number, NotConfiguredMessage);
        }

        try
        {
            // The same call as the integration sample - body, to, and a from - through a client held
            // by this adapter rather than the process-wide static TwilioClient, so a test or a second
            // account never shares it. A Messaging Service, when the shop set one up, replaces the
            // fixed sender: Twilio picks the number itself there, and a 'from' sent beside it would
            // make Twilio refuse the message.
            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(number),
                from: _messagingServiceSid.Length > 0 ? null : new PhoneNumber(_fromNumber),
                messagingServiceSid: _messagingServiceSid.Length > 0 ? _messagingServiceSid : null,
                body: text,
                client: _client);

            var status = message.Status.ToString();
            var sid = message.Sid ?? string.Empty;

            _logger.LogInformation(
                "Twilio accepted a text message for '{Number}' (SID {Sid}, status {Status}).",
                Mask(number), sid, status);

            return SmsSendResult.Sent(number, sid, status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller that asked is gone - not this adapter's answer to give (the rule
            // ShiprocketShipmentProvider follows).
            throw;
        }
        catch (TwilioException exception) when (Refusal(exception) is { } refusal)
        {
            // Twilio answered and refused the message (an unverified number on a trial account, a
            // sender that is not the account's, a destination it will not text). The code is what the
            // shop team can look up in Twilio's error dictionary, so it goes back with the answer and
            // into the log.
            _logger.LogError(
                exception,
                "Twilio refused a text message for '{Number}' (code {Code}, status {Status}): {Message}",
                Mask(number), refusal.Code, refusal.Status, exception.Message);

            return SmsSendResult.Failed(number, RefusedMessage, refusal.Code);
        }
        catch (Exception exception)
        {
            // A timeout, DNS, TLS, or an answer that is not the shape the SDK expected: all the same
            // to the person waiting. The reason goes to the log, where the shop team can see it.
            _logger.LogWarning(
                exception, "Sending a text message to '{Number}' through Twilio failed.", Mask(number));

            return SmsSendResult.Failed(number, UnreachableMessage);
        }
    }

    /// <summary>
    /// The code and status of a refusal, when <paramref name="exception"/> is one. Twilio answers a
    /// refused request in more than one JSON shape - the long-standing '{code, message, status,
    /// more_info}' and the newer problem-details one - and the SDK raises a different exception for
    /// each; a trial account's "trial accounts can only use predefined SMS templates", for one, comes
    /// back as an <see cref="ApiException"/>. They all mean what the shop cares about: Twilio answered
    /// and said no, so the code goes back with the answer. Null means it was never a refusal - a
    /// timeout, a TLS failure, credentials Twilio would not accept - and the caller hears the
    /// unreachable sentence instead.
    /// </summary>
    private static (int Code, int Status)? Refusal(TwilioException exception) => exception switch
    {
        RestException refusal => (refusal.Code, refusal.Status),
        ApiException refusal => (refusal.Code, refusal.Status),
        ApiStandardException refusal => (refusal.Code, refusal.Status),
        RestApiStandardException refusal => (refusal.Code, refusal.Status),
        _ => null
    };

    /// <summary>
    /// Twilio's SIDs say what they are: 'AC...' an account, 'SK...' an API Key, 'MG...' a Messaging
    /// Service. Reading the prefix is what turns a credential pasted in the wrong setting into a
    /// sentence in the log instead of a refused send nobody can explain.
    /// </summary>
    private static bool HasPrefix(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A number as it may be written in a log line: the last four digits only, so a support question
    /// can be matched to a send without a customer's number being kept in a log file.
    /// </summary>
    private static string Mask(string? number)
    {
        var text = (number ?? string.Empty).Trim();

        if (text.Length <= 4)
            return new string('*', Math.Max(text.Length, 4));

        return new string('*', text.Length - 4) + text[^4..];
    }
}

