using HC.Adapters.Messaging;
using HC.Business.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HC.Tests;

/// <summary>
/// Who loads the shop's SMS sender, and with what. The host adds the business layer and the settings; the
/// business layer is what names the provider, so this pins that a container an application builds from
/// HC.Business can hand out an <see cref="ISmsSender"/> without HC.Services referencing an adapter - and
/// that a container built with no settings at all still answers with a sender that refuses a message
/// instead of failing.
/// </summary>
public class MessagingRegistrationTests
{
    private const string AccountSid = "AC00000000000000000000000000000000";
    private const string AuthToken = "test-token";
    private const string FromNumber = "+17372508034";

    /// <summary>The business layer is what says 'Twilio': the host only added services and settings, and
    /// still gets a ready sender out of the container.</summary>
    [Fact]
    public void TheBusinessLayerRegistersTheTwilioSender()
    {
        using var provider = Container(
            ("Twilio:AccountSid", AccountSid),
            ("Twilio:AuthToken", AuthToken),
            ("Twilio:FromNumber", FromNumber));

        var sender = provider.GetRequiredService<ISmsSender>();

        Assert.IsType<TwilioSmsSender>(sender);
        Assert.True(sender.IsConfigured);
        Assert.Equal(FromNumber, sender.Describe().From);
    }

    /// <summary>The client the sender holds is the one Twilio asks to be reused, so one application is
    /// one sender - not a new account, a new HTTP stack and a new timeout per text message.</summary>
    [Fact]
    public void TheSenderIsRegisteredOnceForTheWholeApplication()
    {
        using var provider = Container(
            ("Twilio:AccountSid", AccountSid),
            ("Twilio:AuthToken", AuthToken),
            ("Twilio:FromNumber", FromNumber));

        Assert.Same(
            provider.GetRequiredService<ISmsSender>(),
            provider.GetRequiredService<ISmsSender>());
    }

    /// <summary>A machine with no 'Twilio' section - the state of a fresh checkout - is still a working
    /// application: the sender reports that it is not set up and refuses the message in words, which is
    /// what lets the booking or the dispatch that asked for it carry on.</summary>
    [Fact]
    public async Task AContainerWithoutSettingsStillHandsBackASenderThatRefuses()
    {
        using var provider = Container();

        var sender = provider.GetRequiredService<ISmsSender>();

        Assert.False(sender.IsConfigured);

        var result = await sender.SendAsync(FromNumber, "Your order is on its way.");

        Assert.False(result.Succeeded);
        Assert.Equal(string.Empty, result.MessageSid);
        Assert.NotEmpty(result.Message);
    }

    /// <summary>
    /// A container holding what the host supplies - its configuration (appsettings and the environment)
    /// and a logger - and then the business layer's own registration, and nothing else. Resolving a sender
    /// from it is what a real HC.Services does at startup and on every send.
    /// </summary>
    private static ServiceProvider Container(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(
                setting => setting.Key, setting => (string?)setting.Value))
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ILogger<TwilioSmsSender>>(NullLogger<TwilioSmsSender>.Instance);

        services.AddBusinessMessaging();

        return services.BuildServiceProvider();
    }
}
