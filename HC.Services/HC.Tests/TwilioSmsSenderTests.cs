using HC.Adapters.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HC.Tests;

/// <summary>
/// What the Twilio adapter says about itself and what it refuses before Twilio is ever called. Every
/// case here is a decision made from configuration alone - no message is sent, no account is needed -
/// which is exactly the part the shop depends on when the credentials are missing or the sender is
/// switched off: the action that caused the text keeps working and the failure is reported.
/// </summary>
public class TwilioSmsSenderTests
{
    private const string AccountSid = "AC00000000000000000000000000000000";
    private const string AuthToken = "test-token";
    private const string ApiKeySid = "SK00000000000000000000000000000000";
    private const string ApiKeySecret = "test-api-key-secret";
    private const string FromNumber = "+17372508034";

    /// <summary>No credentials: the adapter is registered but cannot send, and it says so instead of
    /// trying. This is what a machine with an empty 'Twilio' section must look like.</summary>
    [Fact]
    public void WithoutCredentialsTheSenderIsNotConfigured()
    {
        var sender = Sender();

        Assert.False(sender.IsConfigured);

        var info = sender.Describe();

        Assert.Equal(TwilioSmsSender.SenderName, info.Name);
        Assert.False(info.Configured);
        Assert.Equal(SmsPhoneNumber.DefaultCountryCode, info.DefaultCountryCode);
    }

    /// <summary>An account, a token and a number is a sender that is ready, and what it reports is the
    /// number - never the credentials.</summary>
    [Fact]
    public void AnAccountAndANumberMakeTheSenderReady()
    {
        var sender = Sender(
            ("Twilio:AccountSid", AccountSid),
            ("Twilio:AuthToken", AuthToken),
            ("Twilio:FromNumber", FromNumber));

        Assert.True(sender.IsConfigured);

        var info = sender.Describe();

        Assert.True(info.Configured);
        Assert.Equal(FromNumber, info.From);
        Assert.False(info.UsesMessagingService);
    }

    /// <summary>
    /// An API Key is Twilio's narrower credential - a SID and its secret, made and revoked on its own -
    /// and, set beside the account it sends through, it is all a ready sender needs. It stands in for
    /// the account's auth token, so the shop's own secret never has to be on the machine at all.
    /// </summary>
    [Fact]
    public void AnApiKeyAndANumberMakeTheSenderReady()
    {
        var sender = Sender(
            ("Twilio:AccountSid", AccountSid),
            ("Twilio:ApiKeySid", ApiKeySid),
            ("Twilio:ApiKeySecret", ApiKeySecret),
            ("Twilio:FromNumber", FromNumber));

        Assert.True(sender.IsConfigured);

        var info = sender.Describe();

        Assert.True(info.Configured);
        Assert.Equal(FromNumber, info.From);

        // The credential is not something the adapter reports: only where the texts come from.
        Assert.DoesNotContain(ApiKeySecret, info.From);
        Assert.DoesNotContain(ApiKeySid, info.From);
    }

    /// <summary>
    /// The account SID is not optional beside an API Key: the request path is the account
    /// ('/Accounts/AC.../Messages.json'), and a key only says who may send through it. A key on its
    /// own is refused rather than guessed at.
    /// </summary>
    [Fact]
    public void AnApiKeyWithoutAnAccountIsNotConfigured()
    {
        var sender = Sender(
            ("Twilio:ApiKeySid", ApiKeySid),
            ("Twilio:ApiKeySecret", ApiKeySecret),
            ("Twilio:FromNumber", FromNumber));

        Assert.False(sender.IsConfigured);
        Assert.False(sender.Describe().Configured);
    }

    /// <summary>
    /// An API Key SID pasted where the account SID belongs is the quiet failure worth refusing: Twilio
    /// would be asked for '/Accounts/SK.../Messages.json', which no account answers, and the send
    /// would fail with a Twilio error nobody can read. The sender stays unconfigured instead, so the
    /// send is refused with a sentence.
    /// </summary>
    [Fact]
    public void AnApiKeySidInTheAccountSettingIsNotConfigured()
    {
        var sender = Sender(
            ("Twilio:AccountSid", ApiKeySid),
            ("Twilio:AuthToken", AuthToken),
            ("Twilio:FromNumber", FromNumber));

        Assert.False(sender.IsConfigured);
        Assert.False(sender.Describe().Configured);
    }

    /// <summary>A key SID without its secret is half a credential, so it configures nothing.</summary>
    [Fact]
    public void AnApiKeyWithoutItsSecretIsNotConfigured()
    {
        var sender = Sender(
            ("Twilio:AccountSid", AccountSid),
            ("Twilio:ApiKeySid", ApiKeySid),
            ("Twilio:FromNumber", FromNumber));

        Assert.False(sender.IsConfigured);
    }

    /// <summary>
    /// A Messaging Service is a sender of its own: Twilio picks the number from it, so the adapter
    /// reports that a service is used and no fixed 'From'. That is also why a Messaging Service alone
    /// (no number at all) is a configured adapter.
    /// </summary>
    [Fact]
    public void AMessagingServiceCanBeTheSenderInsteadOfANumber()
    {
        var sender = Sender(
            ("Twilio:AccountSid", AccountSid),
            ("Twilio:AuthToken", AuthToken),
            ("Twilio:MessagingServiceSid", "MG00000000000000000000000000000000"));

        Assert.True(sender.IsConfigured);

        var info = sender.Describe();

        Assert.True(info.UsesMessagingService);
        Assert.Equal(string.Empty, info.From);

        // With both set, the Messaging Service still wins - that is the sender the shop set up on purpose.
        var both = Sender(
            ("Twilio:AccountSid", AccountSid),
            ("Twilio:AuthToken", AuthToken),
            ("Twilio:FromNumber", FromNumber),
            ("Twilio:MessagingServiceSid", "MG00000000000000000000000000000000"));

        Assert.True(both.Describe().UsesMessagingService);
        Assert.Equal(string.Empty, both.Describe().From);
    }

    /// <summary>A number with no account behind it cannot send, and neither can an account with no
    /// number or service: both are an adapter that is not set up.</summary>
    [Fact]
    public void ANumberWithoutAnAccountIsNotAConfiguredSender()
    {
        var numberOnly = Sender(("Twilio:FromNumber", FromNumber));
        var accountOnly = Sender(
            ("Twilio:AccountSid", AccountSid),
            ("Twilio:AuthToken", AuthToken));

        Assert.False(numberOnly.IsConfigured);
        Assert.False(accountOnly.IsConfigured);
    }

    /// <summary>'Enabled' false keeps the credentials but stops every send - which is how a development
    /// machine is kept from texting (and billing) real customers.</summary>
    [Fact]
    public void SwitchingTheSenderOffStopsItEvenWithCredentials()
    {
        var sender = Sender(
            ("Twilio:AccountSid", AccountSid),
            ("Twilio:AuthToken", AuthToken),
            ("Twilio:FromNumber", FromNumber),
            ("Twilio:Enabled", "false"));

        Assert.False(sender.IsConfigured);
        Assert.False(sender.Describe().Configured);
    }

    /// <summary>A valid send through an unconfigured adapter comes back as a failed send, not an
    /// exception, and echoes the number in the E.164 form Twilio would have been given.</summary>
    [Fact]
    public async Task AnUnconfiguredSenderRefusesToSend()
    {
        var sender = Sender();

        var result = await sender.SendAsync("9845861860", "Your order is on its way.");

        Assert.False(result.Succeeded);
        Assert.Equal("+919845861860", result.To);
        Assert.Equal(string.Empty, result.MessageSid);
        Assert.Null(result.ErrorCode);
        Assert.NotEmpty(result.Message);
    }

    /// <summary>A send through a switched-off adapter is refused for that reason.</summary>
    [Fact]
    public async Task ASwitchedOffSenderRefusesToSend()
    {
        var sender = Sender(
            ("Twilio:AccountSid", AccountSid),
            ("Twilio:AuthToken", AuthToken),
            ("Twilio:FromNumber", FromNumber),
            ("Twilio:Enabled", "false"));

        var result = await sender.SendAsync("9845861860", "Your order is on its way.");

        Assert.False(result.Succeeded);
        Assert.Contains("switched off", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A number that cannot be texted is refused before Twilio is called - the cheapest failure
    /// there is, and the one a form's typo turns into.</summary>
    [Fact]
    public async Task ANumberThatCannotBeTextedIsRefusedBeforeSending()
    {
        var sender = Sender(
            ("Twilio:AccountSid", AccountSid),
            ("Twilio:AuthToken", AuthToken),
            ("Twilio:FromNumber", FromNumber));

        var result = await sender.SendAsync("not a number", "Your order is on its way.");

        Assert.False(result.Succeeded);
        Assert.Equal("not a number", result.To);
        Assert.Null(result.ErrorCode);
    }

    /// <summary>An empty (or whitespace-only) message is not worth a request, and Twilio would refuse it
    /// anyway.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyMessageIsRefusedBeforeSending(string body)
    {
        var sender = Sender(
            ("Twilio:AccountSid", AccountSid),
            ("Twilio:AuthToken", AuthToken),
            ("Twilio:FromNumber", FromNumber));

        var result = await sender.SendAsync("9845861860", body);

        Assert.False(result.Succeeded);
        Assert.Equal("+919845861860", result.To);
        Assert.Null(result.ErrorCode);
    }

    /// <summary>Twilio accepts 1600 characters in one message; a longer one is refused here rather than
    /// being cut short by the provider (a half sentence is worse than a clear refusal).</summary>
    [Fact]
    public async Task AMessageLongerThanTwilioAcceptsIsRefused()
    {
        var sender = Sender(
            ("Twilio:AccountSid", AccountSid),
            ("Twilio:AuthToken", AuthToken),
            ("Twilio:FromNumber", FromNumber));

        var result = await sender.SendAsync(
            "9845861860", new string('x', TwilioSmsSender.MaxBodyLength + 1));

        Assert.False(result.Succeeded);
        Assert.Null(result.ErrorCode);
    }

    /// <summary>A cancellation the caller asked for is the one exception the adapter passes on - the
    /// 'never throws' rule is about failures, not about a caller that has gone away.</summary>
    [Fact]
    public async Task ACancelledSendIsPassedOn()
    {
        var sender = Sender();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sender.SendAsync("9845861860", "Your order is on its way.", cancellation.Token));
    }

    /// <summary>The adapter reads its settings from the 'Twilio' section; a test builds that section in
    /// memory so no configuration file has to exist.</summary>
    private static TwilioSmsSender Sender(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(
                setting => setting.Key,
                setting => (string?)setting.Value))
            .Build();

        return new TwilioSmsSender(configuration, NullLogger<TwilioSmsSender>.Instance);
    }
}

