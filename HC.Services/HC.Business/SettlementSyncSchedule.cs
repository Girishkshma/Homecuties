using Microsoft.Extensions.Configuration;

namespace HC.Business;

/// <summary>
/// When the shop pulls the gateway's own books by itself. What a pull covers is decided in
/// <see cref="RazorpaySettlements.SyncAsync"/>; this is only when one happens, and the timer that keeps to it
/// (<c>HC.Services/SettlementSyncJob.cs</c>) has nothing of its own to decide - every value it runs with is read
/// here, so the rules can be read and pinned in a test without a host, a gateway or a database.
///
/// Why an interval rather than a time of day: the pull does not need an hour, it needs to happen often enough.
/// Every pass re-reads yesterday and the seven days before it (<see cref="RazorpaySettlements.LookbackDays"/>)
/// and is idempotent, so a settlement created late, put on hold or corrected after the fact is picked up by the
/// next pass rather than by tomorrow's - one request a day and nothing else. A shop that would rather decide for
/// itself switches the whole thing off ('Razorpay:SettlementSync:Enabled' false) and uses the admin screen's
/// 'Pull now' (<c>POST api/admin/settlements/sync</c>), which runs the very same pull.
///
/// Every setting is a courtesy: an unset or unusable value falls back to the shipped default rather than to
/// "never run" or "run in a tight loop", because a typo in a settings file must not cost the shop its books.
/// </summary>
public readonly record struct SettlementSyncSchedule(bool Enabled, TimeSpan Interval, TimeSpan StartupDelay)
{
    /// <summary>Whether the pull runs on its own at all (anything but 'false' - an unset key means it runs).</summary>
    public const string EnabledKey = "Razorpay:SettlementSync:Enabled";

    /// <summary>How many minutes the pull waits between two passes.</summary>
    public const string IntervalMinutesKey = "Razorpay:SettlementSync:IntervalMinutes";

    /// <summary>How many seconds the pull waits after startup before its first pass.</summary>
    public const string StartupDelaySecondsKey = "Razorpay:SettlementSync:StartupDelaySeconds";

    /// <summary>An hour: a shop's day has one settlement in it, so a pass an hour catches it the same day.</summary>
    public const int DefaultIntervalMinutes = 60;

    /// <summary>
    /// Five minutes: one pass reads eight days off the gateway, so asking for the same figures again sooner than
    /// this is asking without a reason - a shorter interval is trimmed up to this rather than obeyed.
    /// </summary>
    public const int MinimumIntervalMinutes = 5;

    /// <summary>
    /// A minute: long enough that a restarting server is serving before the gateway is asked anything (the first
    /// pass of a process is otherwise a pull of its own, every time the process restarts).
    /// </summary>
    public const int DefaultStartupDelaySeconds = 60;

    /// <summary>An hour at most: a longer wait is only ever a way of postponing the pull past the day it is for.</summary>
    public const int MaximumStartupDelaySeconds = 3600;

    /// <summary>
    /// What configuration says. Read once, when the job starts - like every other setting in this app, so a change
    /// takes effect on the next restart, and the line the job logs at startup says what it is running with.
    /// </summary>
    public static SettlementSyncSchedule From(IConfiguration configuration) => From(
        configuration[EnabledKey],
        configuration[IntervalMinutesKey],
        configuration[StartupDelaySecondsKey]);

    /// <summary>
    /// The same reading of the three settings, from the values themselves - what a test pins (the configuration
    /// overload above is only this with the keys resolved).
    /// </summary>
    public static SettlementSyncSchedule From(string? enabled, string? intervalMinutes, string? startupDelaySeconds) => new(
        IsEnabled(enabled),
        TimeSpan.FromMinutes(IntervalMinutes(intervalMinutes)),
        TimeSpan.FromSeconds(StartupDelaySeconds(startupDelaySeconds)));

    /// <summary>
    /// True unless the setting says 'false' (case-insensitively, surrounding blanks ignored). Anything else - a
    /// missing setting included - leaves the pull running, because the shop's books being current is the point of
    /// it and switching them off is the deliberate act.
    /// </summary>
    public static bool IsEnabled(string? configured) =>
        !string.Equals(configured?.Trim(), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The minutes between two passes: what was configured when it is a number this code can keep to, the default
    /// when it is absent or is not a number at all, and never less than <see cref="MinimumIntervalMinutes"/>.
    /// </summary>
    public static int IntervalMinutes(string? configured) =>
        int.TryParse(configured?.Trim(), out var minutes)
            ? Math.Max(minutes, MinimumIntervalMinutes)
            : DefaultIntervalMinutes;

    /// <summary>
    /// The seconds before the first pass: 0 asks for the pull to start as soon as the app is serving (a real
    /// choice, so it is not trimmed up), and a longer wait than <see cref="MaximumStartupDelaySeconds"/> is
    /// trimmed down to it.
    /// </summary>
    public static int StartupDelaySeconds(string? configured) =>
        int.TryParse(configured?.Trim(), out var seconds)
            ? Math.Clamp(seconds, 0, MaximumStartupDelaySeconds)
            : DefaultStartupDelaySeconds;
}
