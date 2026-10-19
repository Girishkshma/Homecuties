using Microsoft.Extensions.DependencyInjection;

namespace HC.Adapters.Messaging;

/// <summary>
/// Wires the Twilio SMS adapter into the application's container, so a caller asks for
/// <see cref="ISmsSender"/> and never for Twilio.
///
/// This is the adapter's own seam, and HC.Business is what calls it
/// (<c>AddBusinessMessaging</c>): the host adds the business layer, the business layer names the
/// provider, and the provider stays out of HC.Services. A second provider is one more adapter and one
/// more line there, with no host change.
///
/// The adapter is a singleton because it is cheap and stateless apart from the
/// <see cref="Twilio.Clients.TwilioRestClient"/> it holds - a thread-safe client that is meant to be
/// reused (see <see cref="TwilioSmsSender"/>). It reads its settings from the 'Twilio' configuration
/// section, which the host owns ('appsettings.json', 'appsettings.Local.json' or the environment
/// variables), when it is first resolved - nothing here reads a configuration file of its own.
/// </summary>
public static class TwilioServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TwilioSmsSender"/> as the app's <see cref="ISmsSender"/>. Nothing is
    /// called to Twilio until a message is actually sent, so this is safe to register on a machine
    /// with no credentials - the startup log then reports the sender as not configured.
    /// </summary>
    public static IServiceCollection AddTwilioMessaging(this IServiceCollection services)
    {
        services.AddSingleton<ISmsSender, TwilioSmsSender>();

        return services;
    }
}
