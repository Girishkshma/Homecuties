using HC.Business;
using HC.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HC.Services;

/// <summary>
/// The rolling settlement pull, running by itself: every interval it asks Razorpay what it settled to the shop's
/// bank account over the last few days and writes the answer into the two settlement tables (see
/// <see cref="RazorpaySettlements.SyncAsync"/>, where the pull lives - nothing about the money is decided here;
/// this is only what makes the pull happen with nobody opening a screen).
///
/// Why it runs at all: the charge a payment was recorded with is an estimate until the settlement report says
/// what the gateway really kept, and the money that reached the bank is in these tables and nowhere else. So the
/// shop's books are exactly as old as the last pull, and remembering to press a button should not be anyone's
/// job. It covers the window a plain pull covers - yesterday and the seven days before it - and re-reads it every
/// pass, which is what catches a settlement created late, put on hold or corrected after the transaction it
/// covers; the pull is idempotent, so re-reading a day costs one request and changes nothing else. When it runs,
/// how often, and how long it waits after a restart are configuration (<see cref="SettlementSyncSchedule"/>), and
/// switching it off leaves the admin screen's 'Pull now' ('POST api/admin/settlements/sync') running the very same
/// pull.
///
/// It is deliberately small, and it never throws: a gateway that cannot be reached, a payload this code cannot
/// read or a database that refuses a write is a pass that failed - logged here, tried again by the next pass, and
/// nothing else. An exception escaping a hosted service stops the whole API (the host's default), and the shop's
/// books being a few hours behind is not worth the shop's shopfront going down for.
///
/// Nothing here runs before the app is serving either: the first thing it does is wait, so starting up - or
/// restarting in a loop - is not itself a pull.
/// </summary>
public sealed class SettlementSyncJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SettlementSyncJob> _logger;
    private readonly SettlementSyncSchedule _schedule;
    private readonly string _keyId;
    private readonly string _keySecret;

    public SettlementSyncJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SettlementSyncJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        // Read once, like every other setting in this app: the startup line below says what the pull is running
        // with, and a change to a setting is a restart. The two credentials are the ones every Razorpay caller
        // reads (see AdminDashboardService and OrderService).
        _schedule = SettlementSyncSchedule.From(configuration);
        _keyId = configuration["Razorpay:KeyId"] ?? string.Empty;
        _keySecret = configuration["Razorpay:KeySecret"] ?? string.Empty;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_schedule.Enabled)
        {
            _logger.LogInformation(
                "The rolling settlement pull is switched off ('{Key}' is false), so the shop's books are only " +
                "brought up to date when the admin area asks for a pull (POST api/admin/settlements/sync).",
                SettlementSyncSchedule.EnabledKey);

            return;
        }

        // Without credentials every pass could only fail, once an hour, forever - so it is not started at all and
        // said plainly instead (the admin button still answers in words when the shop team asks it by hand).
        if (!RazorpaySettlements.IsConfigured(_keyId, _keySecret))
        {
            _logger.LogWarning(
                "The rolling settlement pull is registered but NOT configured ('Razorpay:KeyId' / " +
                "'Razorpay:KeySecret' are empty), so the shop's books are never brought up to date on their own. " +
                "Set the credentials (the git-ignored 'appsettings.Local.json', or the environment variables " +
                "'Razorpay__KeyId' / 'Razorpay__KeySecret').");

            return;
        }

        _logger.LogInformation(
            "The rolling settlement pull runs every {IntervalMinutes:0.#} minute(s), the first pass " +
            "{StartupDelaySeconds:0.#} second(s) after startup: each pass re-reads yesterday and the " +
            "{LookbackDays} days before it from Razorpay, which is what catches a settlement created late, put " +
            "on hold or corrected after the fact. Change '{IntervalKey}' / '{DelayKey}', or set '{EnabledKey}' " +
            "to false to stop it and pull only from the admin screen.",
            _schedule.Interval.TotalMinutes,
            _schedule.StartupDelay.TotalSeconds,
            RazorpaySettlements.LookbackDays,
            SettlementSyncSchedule.IntervalMinutesKey,
            SettlementSyncSchedule.StartupDelaySecondsKey,
            SettlementSyncSchedule.EnabledKey);

        try
        {
            await Task.Delay(_schedule.StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await PullAsync();

                await Task.Delay(_schedule.Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The host is shutting down (the delay between passes is the only thing left to cancel - a pull in
            // flight is never cut short, because half a pull is still a valid pull: it writes as it goes and is
            // idempotent, so the next process finishes the same days). Nothing to report, nothing to finish.
        }
    }

    /// <summary>
    /// One pass: the pull, and the log line that says what it did - in the pull's own words
    /// (<see cref="SettlementSyncOutcome.Summary"/>), which are the words the admin screen answers 'Pull now'
    /// with, so a pass in the log and a button press on the screen can never describe the same pull differently.
    ///
    /// The context comes from a scope of its own because this runs outside any request: the pull needs a
    /// <see cref="HomecutiesDbContext"/> and the scope is what disposes it - and everything it has tracked -
    /// afterwards, so an hour of the shop's orders is not held in memory between passes.
    /// </summary>
    private async Task PullAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<HomecutiesDbContext>();

            // No window: the plain rolling one (yesterday and the seven days before it).
            var outcome = await RazorpaySettlements.SyncAsync(context, _keyId, _keySecret);

            // One severity for the whole pass: a pass that could not read a day it asked for, or that found
            // something a person has to look at, is a warning; a pass that wrote down what the gateway had and
            // found nothing wrong is information.
            var level = !outcome.Succeeded ||
                outcome.PaymentsNotFound > 0 ||
                outcome.AmountMismatches > 0 ||
                outcome.CreditMismatches > 0
                    ? LogLevel.Warning
                    : LogLevel.Information;

            _logger.Log(level, "Settlement pull finished. {Summary}", outcome.Summary);

            // The findings themselves, in the pull's own wording: what a person has to look at is worth its own
            // line on a server nobody is watching.
            foreach (var message in outcome.Messages)
            {
                _logger.Log(level, "{Message}", message);
            }
        }
        catch (Exception exception)
        {
            // Nothing is done about it here on purpose: whatever was read and written down before the failure
            // stays written (the pull saves as it goes), so the next pass re-reads the same days and carries on
            // from there. Only a failure that keeps happening is worth a person's time.
            _logger.LogError(
                exception,
                "The settlement pull failed before it finished. What it had already read and written down is " +
                "kept, and the next pass re-reads the same days - nothing needs doing unless this keeps happening.");
        }
    }
}
