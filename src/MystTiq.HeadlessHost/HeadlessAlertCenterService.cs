using System.Text.Json;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// Reuses HeadlessHistoricalMetricsService's existing samples as evidence and
// HeadlessNotificationService.Create(...) as the sink -- no parallel notification pipeline.
// Evaluated on HeadlessAutomationService's existing background tick (self-throttled here to
// ~60s), not a second timer.
public sealed class HeadlessAlertCenterService
{
    private static readonly TimeSpan EvaluationInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(30);

    private readonly IServerPathProfile paths;
    private readonly HeadlessHistoricalMetricsService history;
    private readonly HeadlessNotificationService notifications;
    private readonly HeadlessModManagementService modManagement;
    private readonly object gate = new();
    private readonly string rulesPath;
    private AlertRuleSet rules;
    private DateTimeOffset lastEvaluatedUtc = DateTimeOffset.MinValue;
    private readonly Dictionary<string, DateTimeOffset> lastFiredUtc = new(StringComparer.Ordinal);

    public HeadlessAlertCenterService(IServerPathProfile paths, HeadlessHistoricalMetricsService history, HeadlessNotificationService notifications, HeadlessModManagementService modManagement)
    {
        this.paths = paths;
        this.history = history;
        this.notifications = notifications;
        this.modManagement = modManagement;
        var stateRoot = Path.Combine(paths.ManagerRuntimeRoot, "alerts");
        Directory.CreateDirectory(stateRoot);
        rulesPath = Path.Combine(stateRoot, "rules.json");
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

    public DiskSpacePrediction GetDiskSpacePrediction()
    {
        var free = FreeBytes();
        var (growthPerDay, _) = ComputeGrowthPerDay();
        if (growthPerDay <= 0)
            return new DiskSpacePrediction(free, growthPerDay, null, null, "Storage is not growing; no exhaustion projected from current trend.");

        var daysRemaining = free / growthPerDay;
        var projected = DateTimeOffset.UtcNow.AddDays(daysRemaining);
        return new DiskSpacePrediction(free, growthPerDay, projected, daysRemaining,
            $"At the current growth rate, the backup volume has about {daysRemaining:F1} day(s) of free space remaining.");
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
        var snapshot = history.Snapshot(TimeSpan.FromHours(1));

        if (set.HighCpuSustained.Enabled && snapshot.AverageCpu >= set.HighCpuSustained.ThresholdPercent)
            Fire("high-cpu", "Warning", "Sustained high CPU", $"Average CPU over the last hour is {snapshot.AverageCpu:F1}% (threshold {set.HighCpuSustained.ThresholdPercent:F0}%).", set.HighCpuSustained.CooldownMinutes);

        if (set.HighMemorySustained.Enabled && snapshot.AverageMemoryMb >= set.HighMemorySustained.ThresholdMb)
            Fire("high-memory", "Warning", "Sustained high memory", $"Average memory over the last hour is {snapshot.AverageMemoryMb:F0} MB (threshold {set.HighMemorySustained.ThresholdMb:F0} MB).", set.HighMemorySustained.CooldownMinutes);

        if (set.LowDiskSpace.Enabled)
        {
            var free = FreeBytes();
            var freePercent = FreePercent();
            if (freePercent.HasValue && freePercent.Value <= set.LowDiskSpace.ThresholdPercent)
                Fire("low-disk", "Critical", "Low disk space", $"Free space is {freePercent.Value:F1}% ({free / 1024d / 1024d / 1024d:F1} GB) on the backup volume.", set.LowDiskSpace.CooldownMinutes);
        }

        if (set.DiskSpaceExhaustionPredicted.Enabled)
        {
            var prediction = GetDiskSpacePrediction();
            if (prediction.DaysRemaining is { } days && days < set.DiskSpaceExhaustionPredicted.ThresholdDays)
                Fire("disk-exhaustion-predicted", "Warning", "Disk space exhaustion predicted", prediction.Detail, set.DiskSpaceExhaustionPredicted.CooldownMinutes);
        }

        // v0.6.8.0 "Alert Center integration for MOD/UE4SS-specific health": the named roadmap
        // bullet -- before this, a MOD entering "Failed"/"Misconfigured"/"Attention" was only ever
        // visible if an operator happened to check the MOD Dashboard page themselves.
        if (set.ModHealthDegraded.Enabled)
        {
            var inventory = await modManagement.GetInventoryAsync(token);
            if (inventory.OverallHealth == "Degraded")
            {
                var issues = inventory.Mods.Where(m => m.Health is "Failed" or "Missing" or "Misconfigured" or "Attention").ToArray();
                var names = string.Join(", ", issues.Select(m => $"{m.Name} ({m.Health})").Take(5));
                var suffix = issues.Length > 5 ? $", and {issues.Length - 5} more" : "";
                Fire("mod-health-degraded", "Warning", "MOD/UE4SS health needs attention",
                    $"{issues.Length} enabled MOD(s) have an issue: {names}{suffix}.", set.ModHealthDegraded.CooldownMinutes);
            }
        }
    }

    private void Fire(string ruleKey, string severity, string title, string message, int cooldownMinutes)
    {
        var cooldown = cooldownMinutes > 0 ? TimeSpan.FromMinutes(cooldownMinutes) : DefaultCooldown;
        lock (gate)
        {
            if (lastFiredUtc.TryGetValue(ruleKey, out var last) && DateTimeOffset.UtcNow - last < cooldown) return;
            lastFiredUtc[ruleKey] = DateTimeOffset.UtcNow;
        }
        notifications.Create(severity, title, message, pinned: severity == "Critical");
    }

    private long FreeBytes()
    {
        try { return new DriveInfo(Path.GetPathRoot(paths.BackupRoot)!).AvailableFreeSpace; }
        catch { return long.MaxValue; }
    }

    private double? FreePercent()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(paths.BackupRoot)!);
            return drive.TotalSize <= 0 ? null : 100d * drive.AvailableFreeSpace / drive.TotalSize;
        }
        catch { return null; }
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
}

public sealed record DiskSpacePrediction(long FreeBytes, double GrowthBytesPerDay, DateTimeOffset? ProjectedFullUtc, double? DaysRemaining, string Detail);
