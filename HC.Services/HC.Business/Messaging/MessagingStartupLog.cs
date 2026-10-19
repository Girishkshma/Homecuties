using HC.Adapters.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HC.Business.Messaging;

/// <summary>
/// What the host writes once at startup about text messages, kept here so the host never names a
/// provider: an adapter that is registered but not set up is a silent failure otherwise - the booking
/// succeeds, nothing arrives, and nobody knows until a customer says so.
/// </summary>
public static class MessagingStartupLog
{
    /// <summary>
    /// Logs which sender the shop's text messages go out through and whether it can actually send.
    /// Never the credentials, only the sender: the account SID, the token and the auth token stay in
    /// configuration, and a number is the shop's own, not a customer's.
    /// </summary>
    public static void LogSmsSenderStatus(this IServiceProvider services, ILogger logger)
    {
        var info = services.GetRequiredService<ISmsSender>().Describe();

        if (info.Configured)
        {
            logger.LogInformation(
                "SMS sender '{Sender}' is ready ({From}). Bare numbers are read as +{CountryCode}.",
                info.Name,
                info.UsesMessagingService
                    ? "through a Messaging Service"
                    : $"from {info.From}",
                info.DefaultCountryCode);

            return;
        }

        logger.LogWarning(
            "SMS sender '{Sender}' is registered but NOT configured, so no text message is sent. Set " +
            "'Twilio:AccountSid' ('AC...') with either 'Twilio:AuthToken' or 'Twilio:ApiKeySid' and " +
            "'Twilio:ApiKeySecret', plus either 'Twilio:FromNumber' or 'Twilio:MessagingServiceSid', " +
            "in the git-ignored 'appsettings.Local.json' (or the environment variables " +
            "'Twilio__AccountSid' / 'Twilio__ApiKeySid' / 'Twilio__ApiKeySecret' / " +
            "'Twilio__FromNumber' / 'Twilio__MessagingServiceSid'), and leave 'Twilio:Enabled' " +
            "unset or 'true'.",
            info.Name);
    }
}
