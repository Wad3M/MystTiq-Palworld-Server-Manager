using System.Text.Json;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// Reuses HeadlessHistoricalMetricsService's existing samples as evidence and
// HeadlessNotificationService.Create(...) as the sink -- no parallel notification pipeline.
// Evaluated on HeadlessAutomationService's existing background tick (self-throttled here to
// ~60s), not a second timer.
public sealed class HeadlessAlertCenterService
{
    // 60 seconds. MYSTTIQ_ALERT_EVAL_SECONDS (1 to 600) shortens or lengthens it; the live smoke tests use it so they
    // do not have to wait minutes per evaluation. Unset or invalid means the default.
    private static readonly TimeSpan EvaluationInterval = ReadEvaluationInterval();

    private static TimeSpan ReadEvaluationInterval() =>
        int.TryParse(Environment.GetEnvironmentVariable("MYSTTIQ_ALERT_EVAL_SECONDS"), out var seconds) && seconds is >= 1 and <= 600
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(30);

    // v0.7.107.0: a condition that stayed true for days used to alert once and then say nothing else until
    // it cleared. v0.7.108.0: the cadence is now the user's own AlertRuleSet.ReminderMinutes (editable on
    // the Alert Center page), not a fixed constant. MYSTTIQ_ALERT_REMINDER_MINUTES (0 to 10080, one week)
    // still overrides it when set, exactly as before, so the live smoke does not have to wait a day; unset
    // means "use whatever the user configured." Either way 0 disables reminders entirely.
    private TimeSpan? GetReminderInterval()
    {
        var raw = Environment.GetEnvironmentVariable("MYSTTIQ_ALERT_REMINDER_MINUTES");
        if (raw is not null && int.TryParse(raw, out var overrideMinutes) && overrideMinutes is >= 0 and <= 10080)
            return overrideMinutes == 0 ? null : TimeSpan.FromMinutes(overrideMinutes);

        var configured = GetRules().ReminderMinutes;
        return configured <= 0 ? null : TimeSpan.FromMinutes(Math.Min(configured, 10080));
    }

    // A near-zero (but positive) growthPerDay from two close-together/near-identical samples can
    // make daysRemaining astronomically large; DateTimeOffset.AddDays only accepts a range up to
    // roughly year 9999, so anything past this cap can't be projected as a real date. 100 years is
    // already far beyond a meaningful exhaustion warning.
    private const double MaxProjectableDays = 36500;

    private readonly IServerPathProfile paths;
    private readonly HeadlessHistoricalMetricsService history;
    private readonly HeadlessNotificationService notifications;
    private readonly HeadlessModManagementService modManagement;
    private readonly object gate = new();
    private readonly string rulesPath;
    private AlertRuleSet rules;
    private DateTimeOffset lastEvaluatedUtc = DateTimeOffset.MinValue;
    private readonly AlertEpisodeTracker episodes;

    public HeadlessAlertCenterService(IServerPathProfile paths, HeadlessHistoricalMetricsService history, HeadlessNotificationService notifications, HeadlessModManagementService modManagement)
    {
        this.paths = paths;
        this.history = history;
        this.notifications = notifications;
        this.modManagement = modManagement;
        var stateRoot = Path.Combine(paths.ManagerRuntimeRoot, "alerts");
        Directory.CreateDirectory(stateRoot);
        rulesPath = Path.Combine(stateRoot, "rules.json");
        episodes = new AlertEpisodeTracker(Path.Combine(stateRoot, "episodes.json"));
        rules = Load();
    }

    public AlertRuleSet GetRules() { lock (gate) return rules; }

    public AlertRuleSet SaveRules(AlertRuleSet updated)
    {
        lock (gate)
        {
            rules = updated;
            var partial = rulesPath + ".partial";
            File.WriteAllText(partial, JsonSerializer.Serialize(rules, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(partial, rulesPath, true);
            return rules;
        }
    }

    // v0.7.111.0: the end time is computed here, on the host's clock, not by whichever client asked.
    public AlertRuleSet Mute(int minutes)
    {
        lock (gate)
        {
            var updated = new AlertRuleSet
            {
                HighCpuSustained = rules.HighCpuSustained,
                HighMemorySustained = rules.HighMemorySustained,
                LowDiskSpace = rules.LowDiskSpace,
                DiskSpaceExhaustionPredicted = rules.DiskSpaceExhaustionPredicted,
                ModHealthDegraded = rules.ModHealthDegraded,
                ReminderMinutes = rules.ReminderMinutes,
                CrashAlerts = rules.CrashAlerts ?? new CrashAlertRule(true, true),
                MutedUntilUtc = AlertMutePolicy.MuteUntil(minutes, DateTimeOffset.UtcNow)
            };
            return SaveRules(updated);
        }
    }

    public DiskSpacePrediction GetDiskSpacePrediction()
    {
        var free = FreeBytes();
        var (growthPerDay, _) = ComputeGrowthPerDay();
        // v0.7.82.0 bug fix, round 2: the MaxProjectableDays cap alone did not actually stop the
        // crash live (confirmed: it kept recurring every ~60-75s across many rebuilds). A NaN
        // daysRemaining -- possible if growthPerDay itself is somehow non-finite -- compares false
        // against every bound (NaN > X and NaN <= 0 are both false), so it silently slipped past both
        // guards and reached AddDays. !double.IsFinite catches NaN and +/-Infinity explicitly, and
        // AddDays is now wrapped as a last-resort belt-and-suspenders: this is a best-effort
        // informational prediction, never worth crashing the shared background evaluation loop over.
        if (!double.IsFinite(growthPerDay) || growthPerDay <= 0)
            return new DiskSpacePrediction(free, growthPerDay, null, null, "Storage is not growing; no exhaustion projected from current trend.");

        var daysRemaining = free / growthPerDay;
        if (!double.IsFinite(daysRemaining) || daysRemaining > MaxProjectableDays)
            return new DiskSpacePrediction(free, growthPerDay, null, daysRemaining,
                "Storage is growing too slowly to project a meaningful exhaustion date.");

        try
        {
            var projected = DateTimeOffset.UtcNow.AddDays(daysRemaining);
            return new DiskSpacePrediction(free, growthPerDay, projected, daysRemaining,
                $"At the current growth rate, the backup volume has about {daysRemaining:F1} day(s) of free space remaining.");
        }
        catch (ArgumentOutOfRangeException)
        {
            return new DiskSpacePrediction(free, growthPerDay, null, daysRemaining,
                "Storage is growing too slowly to project a meaningful exhaustion date.");
        }
    }

    public async Task EvaluateThrottledAsync(CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        lock (gate)
        {
            if (now - lastEvaluatedUtc < EvaluationInterval) return;
            lastEvaluatedUtc = now;
        }

        var set = GetRules();
        // v0.7.111.0: while muted, nothing is evaluated, so no episode opens or closes. When the mute ends,
        // a condition that is still true opens its episode then and alerts normally, and one that cleared
        // during the mute sends its "Resolved" notice and unpins, instead of either being silently lost.
        if (AlertMutePolicy.IsMuted(set, now)) return;
        var snapshot = history.Snapshot(TimeSpan.FromHours(1));

        // v0.7.102.0: every rule is tracked as an episode (see AlertEpisodeTracker): one alert when the
        // condition starts, silence while it stays true, one recovery notice when it clears.
        if (set.HighCpuSustained.Enabled)
            Track("high-cpu", "Warning", "Sustained high CPU", snapshot.AverageCpu >= set.HighCpuSustained.ThresholdPercent,
                $"Average CPU over the last hour is {snapshot.AverageCpu:F1}% (threshold {set.HighCpuSustained.ThresholdPercent:F0}%).",
                $"Average CPU over the last hour is back to {snapshot.AverageCpu:F1}%.", set.HighCpuSustained.CooldownMinutes);
        else Unpin(episodes.Close("high-cpu"));

        if (set.HighMemorySustained.Enabled)
            Track("high-memory", "Warning", "Sustained high memory", snapshot.AverageMemoryMb >= set.HighMemorySustained.ThresholdMb,
                $"Average memory over the last hour is {snapshot.AverageMemoryMb:F0} MB (threshold {set.HighMemorySustained.ThresholdMb:F0} MB).",
                $"Average memory over the last hour is back to {snapshot.AverageMemoryMb:F0} MB.", set.HighMemorySustained.CooldownMinutes);
        else Unpin(episodes.Close("high-memory"));

        if (set.LowDiskSpace.Enabled)
        {
            var (free, total) = BackupVolumeSpace();
            var freeGb = free / 1024d / 1024d / 1024d;
            var percent = DiskSpaceRules.FreePercent(free, total);
            // The same rule the Doctor applies (DiskSpaceRules): under 2 GiB free, or at or under the configured percentage.
            Track("low-disk", "Critical", "Low disk space", DiskSpaceRules.Evaluate(free, total, set.LowDiskSpace.ThresholdPercent) == DiskLevel.Critical,
                $"Free space is {percent:F1}% ({freeGb:F1} GB) on the backup volume.",
                $"Free space on the backup volume is back to {percent:F1}% ({freeGb:F1} GB).", set.LowDiskSpace.CooldownMinutes);
        }
        else Unpin(episodes.Close("low-disk"));

        if (set.DiskSpaceExhaustionPredicted.Enabled)
        {
            var prediction = GetDiskSpacePrediction();
            Track("disk-exhaustion-predicted", "Warning", "Disk space exhaustion predicted",
                prediction.DaysRemaining is { } days && days < set.DiskSpaceExhaustionPredicted.ThresholdDays,
                prediction.Detail, "The backup volume is no longer projected to run out of space soon.", set.DiskSpaceExhaustionPredicted.CooldownMinutes);
        }
        else Unpin(episodes.Close("disk-exhaustion-predicted"));

        // v0.6.8.0 "Alert Center integration for MOD/UE4SS-specific health": the named roadmap
        // bullet -- before this, a MOD entering "Failed"/"Misconfigured"/"Attention" was only ever
        // visible if an operator happened to check the MOD Dashboard page themselves.
        if (set.ModHealthDegraded.Enabled)
        {
            var inventory = await modManagement.GetInventoryAsync(token);
            var degraded = inventory.OverallHealth == "Degraded";
            var issues = inventory.Mods.Where(m => m.Health is "Failed" or "Missing" or "Misconfigured" or "Attention").ToArray();
            var names = string.Join(", ", issues.Select(m => $"{m.Name} ({m.Health})").Take(5));
            var suffix = issues.Length > 5 ? $", and {issues.Length - 5} more" : "";
            Track("mod-health-degraded", "Warning", "MOD/UE4SS health needs attention", degraded,
                $"{issues.Length} enabled MOD(s) have an issue: {names}{suffix}.", "MOD/UE4SS health is back to normal.", set.ModHealthDegraded.CooldownMinutes);
        }
        else Unpin(episodes.Close("mod-health-degraded"));
    }

    // The low-disk percentage the Doctor should use so both agree, or null when the rule is switched off.
    public double? LowDiskCriticalPercent()
    {
        var rule = GetRules().LowDiskSpace;
        return rule.Enabled ? rule.ThresholdPercent : null;
    }

    private void Track(string ruleKey, string severity, string title, bool active, string message, string resolvedMessage, int cooldownMinutes)
    {
        // The configured cooldown is now the flapping guard: a condition that clears and returns inside it
        // does not alert a second time.
        var flapGuard = cooldownMinutes > 0 ? TimeSpan.FromMinutes(cooldownMinutes) : DefaultCooldown;
        switch (episodes.Observe(ruleKey, active, DateTimeOffset.UtcNow, flapGuard, out var recoveredNotificationId, GetReminderInterval()))
        {
            case EpisodeAction.Alert:
                notifications.Create(severity, title, message, pinned: severity == "Critical", out var id);
                episodes.SetAlertNotificationId(ruleKey, id);
                break;
            case EpisodeAction.Recovered:
                notifications.Create("Success", $"Resolved: {title}", resolvedMessage);
                Unpin(recoveredNotificationId);
                break;
            case EpisodeAction.Reminder:
                // Not pinned: the original alert is still pinned (or already isn't, for a Warning), and a
                // pinned reminder every interval would grow the pinned count without bound. This is a
                // plain follow-up so a long-running Critical is not forgotten between the alert and recovery.
                notifications.Create(severity, $"Still active: {title}", message);
                break;
        }
    }

    // v0.7.104.0: a pinned alert used to stay pinned forever once its "Resolved" notice had already been
    // sent -- or, worse, once the rule was switched off, which sends no notice at all (Close is silent by
    // design). A no-op when there was nothing pinned (id is null) or the notification was already
    // dismissed by the time the episode ended.
    private void Unpin(string? notificationId)
    {
        if (notificationId is null) return;
        try { notifications.SetPinned(notificationId, false); }
        catch (KeyNotFoundException) { }
    }

    private (long Free, long Total) BackupVolumeSpace()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(paths.BackupRoot)!);
            return (drive.AvailableFreeSpace, drive.TotalSize);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or NotSupportedException)
        {
            // Unknown space is "plenty": an unreadable volume must not raise a false low-disk alert.
            return (long.MaxValue, 0);
        }
    }
    private long FreeBytes()
    {
        try { return new DriveInfo(Path.GetPathRoot(paths.BackupRoot)!).AvailableFreeSpace; }
        catch { return long.MaxValue; }
    }

    // Linear projection of world/backup growth from historical samples.
    private (double BytesPerDay, int SampleCount) ComputeGrowthPerDay()
    {
        var snapshot = history.Snapshot(TimeSpan.FromDays(7), maximumSamples: 200);
        if (snapshot.Samples.Count < 2) return (0, snapshot.Samples.Count);
        var first = snapshot.Samples[0];
        var last = snapshot.Samples[^1];
        var days = (last.ObservedAt - first.ObservedAt).TotalDays;
        if (days <= 0) return (0, snapshot.Samples.Count);
        return ((last.WorldSizeBytes - first.WorldSizeBytes) / days, snapshot.Samples.Count);
    }

    private AlertRuleSet Load()
    {
        try { return File.Exists(rulesPath) ? JsonSerializer.Deserialize<AlertRuleSet>(File.ReadAllText(rulesPath)) ?? new() : new(); }
        catch { return new(); }
    }
}

public sealed record AlertThresholdRule(bool Enabled, double ThresholdPercent, int CooldownMinutes);
public sealed record AlertMemoryRule(bool Enabled, double ThresholdMb, int CooldownMinutes);
public sealed record AlertDiskDaysRule(bool Enabled, double ThresholdDays, int CooldownMinutes);
public sealed record AlertSimpleRule(bool Enabled, int CooldownMinutes);

public sealed class AlertRuleSet
{
    public AlertThresholdRule HighCpuSustained { get; init; } = new(false, 90, 30);
    public AlertMemoryRule HighMemorySustained { get; init; } = new(false, 8192, 30);
    public AlertThresholdRule LowDiskSpace { get; init; } = new(true, 10, 60);
    public AlertDiskDaysRule DiskSpaceExhaustionPredicted { get; init; } = new(true, 7, 1440);
    public AlertSimpleRule ModHealthDegraded { get; init; } = new(true, 60);
    // v0.7.108.0: how often a still-active condition's reminder repeats (see AlertEpisodeTracker's
    // reminderInterval, added in v0.7.107.0 as an env-var-only live-testing knob). 1440 minutes (24
    // hours) by default; 0 turns reminders off entirely. A per-rule setting was considered and rejected --
    // one global cadence, same as the evaluation interval, keeps this simple.
    public int ReminderMinutes { get; init; } = 1440;
    // v0.7.111.0: crash-recovery alerts (CrashAlertObserver, v0.7.101.0) had no settings at all. Both of
    // these live in this profile's own rules.json, so every server profile has its own.
    public CrashAlertRule CrashAlerts { get; init; } = new(true, true);
    // v0.7.111.0: while set and in the future, this profile sends no alerts at all (threshold rules and
    // crash alerts alike). Stored as an end time rather than a flag so a mute can never be forgotten on.
    public DateTimeOffset? MutedUntilUtc { get; init; }
}

// Enabled: send crash-recovery alerts at all. RecoveryNotices: also send the "server is back up" notices.
public sealed record CrashAlertRule(bool Enabled, bool RecoveryNotices);

public sealed record AlertMuteRequest(int Minutes);

// v0.7.111.0: pure, so the logic harness covers every case.
public static class AlertMutePolicy
{
    // 30 days. A longer silence is what switching a rule off is for.
    public const int MaximumMuteMinutes = 43200;

    public static bool IsMuted(AlertRuleSet rules, DateTimeOffset now) => rules.MutedUntilUtc is { } until && until > now;

    // minutes <= 0 unmutes; anything else is capped at MaximumMuteMinutes.
    public static DateTimeOffset? MuteUntil(int minutes, DateTimeOffset now) =>
        minutes <= 0 ? null : now.AddMinutes(Math.Min(minutes, MaximumMuteMinutes));

    public static bool ShouldSendCrashAlert(AlertRuleSet rules, SupervisorEventKind kind, DateTimeOffset now)
    {
        // A client that sends "crashAlerts": null gets the defaults, not a crash in the recovery loop.
        var crash = rules.CrashAlerts ?? new CrashAlertRule(true, true);
        if (IsMuted(rules, now) || !crash.Enabled) return false;
        return kind is not (SupervisorEventKind.RecoverySucceeded or SupervisorEventKind.ManualRecovery) || crash.RecoveryNotices;
    }
}

public sealed record DiskSpacePrediction(long FreeBytes, double GrowthBytesPerDay, DateTimeOffset? ProjectedFullUtc, double? DaysRemaining, string Detail);
