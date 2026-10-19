using HC.Adapters.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace HC.Business.Messaging;

/// <summary>
/// Wires the shop's text-message sender into the container on the business layer's terms: everything
/// that decides a customer should be texted - an order, a dispatch, a reminder - asks the container for
/// <see cref="ISmsSender"/> and never for a provider, so the layer that sends the message owns the
/// choice of who sends it.
///
/// The host does not register the adapter itself. It adds this layer, and the provider is loaded with
/// it, which keeps the provider's SDK, its classes and its error codes behind one boundary. What the
/// host owns is the configuration the adapter reads - the 'Twilio' section in HC.Services'
/// 'appsettings.json' and its git-ignored 'appsettings.Local.json', or the environment variables -
/// because a credential belongs with the process that runs, not with a library.
/// </summary>
public static class BusinessMessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shop's <see cref="ISmsSender"/>. Twilio is the sender today; a second provider is
    /// one more adapter and one more line here, with no caller, controller or rule touched.
    ///
    /// Nothing is called to Twilio while the container is built: the sender is created when the
    /// container is first asked for one, which is when a text message is actually being sent - so a
    /// machine with no credentials starts exactly like one with them, and the startup log reports the
    /// sender as not configured instead of failing.
    /// </summary>
    public static IServiceCollection AddBusinessMessaging(this IServiceCollection services)
    {
        // The adapter registers itself (see TwilioServiceCollectionExtensions): this method is the seam
        // where the shop names its provider, and the only place a second one would be added.
        services.AddTwilioMessaging();

        return services;
    }
}
