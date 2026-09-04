using System.Text.Json;
using MystTiq.Core.Automation;
using MystTiq.Core.Models;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// The one background loop in the whole app -- everything else here is purely request-driven.
// Introduces resource key "lifecycle" (distinct from "world-mutation") on the OperationCoordinator:
// StopServer acquires ["lifecycle"] only; StartServer/RestartServer acquire ["lifecycle","world-mutation"]
// together so they correctly refuse to run while a world/guild/base Apply is mid-flight.
public sealed class HeadlessAutomationService : IAsyncDisposable
{
    private const int MaximumRuns = 500;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(15);

    private readonly IServerPathProfile paths;
    private readonly HeadlessConfiguration configuration;
    private readonly HeadlessServerProfileConfiguration serverProfile;
    private readonly IServerLifecycleService lifecycle;
    private readonly HeadlessBackupService backups;
    private readonly HeadlessNotificationService notifications;
    private readonly HeadlessNotificationRoutingService notificationRouting;
    private readonly PalworldRconService rcon;
    private readonly IOperationCoordinator coordinator;
    private readonly HeadlessActivityLogService activity;
    private readonly HeadlessAlertCenterService? alertCenter;
    private readonly HeadlessMonitoringService monitoring;

    private readonly object gate = new();
    private readonly string rulesPath;
    private readonly string runsPath;
    private readonly Dictionary<AutomationRuleId, AutomationRule> rules;
    private readonly List<AutomationRunRecord> runs;
    private readonly Dictionary<string, CancellationTokenSource> activeRuns = new(StringComparer.Ordinal);

    private CancellationTokenSource? loopCts;
    private Task? loopTask;

    public HeadlessAutomationService(
        IServerPathProfile paths,
        HeadlessConfiguration configuration,
        HeadlessServerProfileConfiguration serverProfile,
        IServerLifecycleService lifecycle,
        HeadlessBackupService backups,
        HeadlessNotificationService notifications,
        HeadlessNotificationRoutingService notificationRouting,
        PalworldRconService rcon,
        IOperationCoordinator coordinator,
        HeadlessActivityLogService activity,
        HeadlessAlertCenterService? alertCenter,
        HeadlessMonitoringService monitoring)
    {
        this.paths = paths;
        this.configuration = configuration;
        this.serverProfile = serverProfile;
        this.lifecycle = lifecycle;
        this.backups = backups;
        this.notifications = notifications;
        this.notificationRouting = notificationRouting;
        this.rcon = rcon;
        this.coordinator = coordinator;
        this.activity = activity;
        this.alertCenter = alertCenter;
        this.monitoring = monitoring;

        var stateRoot = Path.Combine(paths.ManagerRuntimeRoot, "automation");
        Directory.CreateDirectory(stateRoot);
        rulesPath = Path.Combine(stateRoot, "rules.json");
        runsPath = Path.Combine(stateRoot, "runs.json");
        rules = LoadRules();
        runs = LoadRuns();

        foreach (var rule in rules.Values.Where(r => r.Enabled && r.NextDueUtc is null && r.Trigger.Kind != AutomationTriggerKind.IdleEmpty))
            rule.NextDueUtc = ComputeNextDue(rule.Trigger, DateTimeOffset.UtcNow);
    }

    public Task StartAsync(CancellationToken hostShutdownToken)
    {
        loopCts = CancellationTokenSource.CreateLinkedTokenSource(hostShutdownToken);
        loopTask = Task.Run(() => RunLoopAsync(loopCts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (loopCts is null) return;
        loopCts.Cancel();
        if (loopTask is not null)
        {
            try { await loopTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        loopCts?.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TickInterval);
        while (await SafeWaitAsync(timer, token))
        {
            EvaluateDueRules(token);
            await EvaluateIdleRulesAsync(token);
            if (alertCenter is not null)
            {
                try { await alertCenter.EvaluateThrottledAsync(token); }
                catch (Exception ex) { activity.Record("Warning", "Alerts", "Alert evaluation failed", ex.Message); }
            }
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    // v0.6.9.0 "Idle auto-stop with warning and final player recheck": unlike every other trigger
    // kind, IdleEmpty is continuously-observed live state (0 online players for N minutes), not a
    // computable future NextDueUtc, so it's evaluated on its own path each tick rather than folded
    // into EvaluateDueRules. Reuses ExecuteRuleAsync/RunLifecycleActionAsync unchanged for the
    // actual action -- including the already-existing warning-countdown broadcast mechanic -- so
    // this only adds the idle-detection layer on top of proven execution machinery.
    private async Task EvaluateIdleRulesAsync(CancellationToken hostToken)
    {
        List<AutomationRule> idleRules;
        lock (gate)
            idleRules = rules.Values.Where(r => r.Enabled && r.Trigger.Kind == AutomationTriggerKind.IdleEmpty).ToList();
        if (idleRules.Count == 0) return;

        HeadlessPlayersSnapshot players;
        try { players = await monitoring.GetPlayersAsync(hostToken); }
        catch { return; }
        if (!players.Available) return;

        var status = await lifecycle.GetStatusAsync(hostToken);
        var now = DateTimeOffset.UtcNow;

        foreach (var rule in idleRules)
        {
            if (status.Phase != ServerLifecyclePhase.Running || players.OnlineCount > 0)
            {
                lock (gate) rule.IdleSinceUtc = null;
                continue;
            }

            DateTimeOffset idleSince;
            lock (gate) { rule.IdleSinceUtc ??= now; idleSince = rule.IdleSinceUtc.Value; }

            var thresholdMinutes = Math.Max(1, rule.Trigger.IdleThresholdMinutes ?? 30);
            if (now - idleSince < TimeSpan.FromMinutes(thresholdMinutes)) continue;

            // Reset immediately so a slow-running warning countdown can't cause this same rule to
            // be picked up again on the next 15s tick while it's still executing.
            lock (gate) rule.IdleSinceUtc = null;
            _ = Task.Run(() => ExecuteRuleAsync(rule, now, hostToken), hostToken);
        }
    }

    private void EvaluateDueRules(CancellationToken hostToken)
    {
        List<AutomationRule> due;
        var now = DateTimeOffset.UtcNow;
        lock (gate)
        {
            due = rules.Values.Where(r => r.Enabled && r.NextDueUtc.HasValue && r.NextDueUtc.Value <= now).ToList();
        }

        foreach (var rule in due)
        {
            var wasOverdue = now - rule.NextDueUtc!.Value > TimeSpan.FromSeconds(rule.MissedGraceSeconds);
            lock (gate) rule.NextDueUtc = ComputeNextDue(rule.Trigger, now);

            if (wasOverdue)
            {
                RecordRun(rule, now, AutomationRunState.Missed, "Rule was overdue beyond its missed-grace window; rescheduled from now.", null, null);
                continue;
            }

            _ = Task.Run(() => ExecuteRuleAsync(rule, now, hostToken), hostToken);
        }
    }

    private async Task ExecuteRuleAsync(AutomationRule rule, DateTimeOffset dueUtc, CancellationToken hostToken)
    {
        var runId = Guid.NewGuid().ToString("N");
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(hostToken);
        lock (gate) activeRuns[runId] = runCts;
        var token = runCts.Token;
        var startedUtc = DateTimeOffset.UtcNow;

        try
        {
            var status = await lifecycle.GetStatusAsync(token);
            if (rule.Condition.RequireServerRunning && status.Phase != ServerLifecyclePhase.Running)
            {
                RecordRun(rule, dueUtc, AutomationRunState.Skipped, "Condition not met: server is not running.", runId, startedUtc);
                return;
            }
            if (rule.Condition.RequireServerStopped && status.Phase != ServerLifecyclePhase.Stopped)
            {
                RecordRun(rule, dueUtc, AutomationRunState.Skipped, "Condition not met: server is not stopped.", runId, startedUtc);
                return;
            }

            var (success, detail, operationId) = await RunActionAsync(rule, token);
            RecordRun(rule, dueUtc, success ? AutomationRunState.Completed : AutomationRunState.Failed, detail, runId, startedUtc, operationId);
        }
        catch (OperationCanceledException)
        {
            RecordRun(rule, dueUtc, AutomationRunState.Cancelled, "Run was cancelled.", runId, startedUtc);
        }
        catch (Exception ex)
        {
            RecordRun(rule, dueUtc, AutomationRunState.Failed, ex.Message, runId, startedUtc);
        }
        finally
        {
            lock (gate) activeRuns.Remove(runId);
            lock (gate) rule.LastRunUtc = DateTimeOffset.UtcNow;
        }
    }

    private async Task<(bool Success, string Detail, string? OperationId)> RunActionAsync(AutomationRule rule, CancellationToken token)
    {
        var ruleName = rule.Name;
        var action = rule.Action;
        switch (action.Kind)
        {
            case AutomationActionKind.CreateBackup:
            {
                var result = await backups.CreateAsync(BackupClass.Scheduled, token);
                return (result.Success, result.Message, null);
            }
            case AutomationActionKind.SendNotification:
            {
                var (severity, title, message) = ResolveNotificationContent(ruleName, action);
                notifications.Create(severity, title, message);
                return (true, "Notification sent.", null);
            }
            case AutomationActionKind.SendRconCommand:
            {
                var result = await rcon.ExecuteAsync(action.RconCommand ?? string.Empty, token);
                return (result.Success, result.Message, null);
            }
            case AutomationActionKind.StopServer:
            {
                // Idle-triggered stops get one more live player recheck immediately before the
                // actual stop, after any warning countdown has finished broadcasting -- a player
                // who joined during the countdown must abort the stop, not just delay it.
                Func<Task<bool>>? abortIfPlayerPresent = rule.Trigger.Kind == AutomationTriggerKind.IdleEmpty
                    ? async () =>
                    {
                        try { var recheck = await monitoring.GetPlayersAsync(token); return recheck.Available && recheck.OnlineCount > 0; }
                        catch { return false; }
                    }
                    : null;
                return await RunLifecycleActionAsync("automation-stop", ["lifecycle"], action, token,
                    () => lifecycle.StopAsync(TimeSpan.FromSeconds(configuration.Lifecycle.StopTimeoutSeconds), token), abortIfPlayerPresent);
            }
            case AutomationActionKind.StartServer:
                return await RunLifecycleActionAsync("automation-start", ["lifecycle", "world-mutation"], action, token,
                    () => lifecycle.StartAsync(serverProfile.LaunchArguments, TimeSpan.FromSeconds(configuration.Lifecycle.StartupTimeoutSeconds), token));
            case AutomationActionKind.RestartServer:
                return await RunLifecycleActionAsync("automation-restart", ["lifecycle", "world-mutation"], action, token,
                    () => lifecycle.RestartAsync(serverProfile.LaunchArguments, TimeSpan.FromSeconds(configuration.Lifecycle.StartupTimeoutSeconds), TimeSpan.FromSeconds(configuration.Lifecycle.StopTimeoutSeconds), token));
            default:
                return (false, $"Unsupported action kind: {action.Kind}", null);
        }
    }

    private (string Severity, string Title, string Message) ResolveNotificationContent(string ruleName, AutomationAction action)
    {
        var placeholders = new Dictionary<string, string> { ["ruleName"] = ruleName };
        if (!string.IsNullOrWhiteSpace(action.NotificationTemplateId))
        {
            var template = notificationRouting.ResolveTemplate(action.NotificationTemplateId);
            if (template is not null)
            {
                return (
                    action.NotificationSeverity ?? template.DefaultSeverity,
                    HeadlessNotificationRoutingService.Apply(template.TitleFormat, placeholders),
                    HeadlessNotificationRoutingService.Apply(template.MessageFormat, placeholders));
            }
        }
        return (action.NotificationSeverity ?? "Information", action.NotificationTitle ?? "Automation", action.NotificationMessage ?? string.Empty);
    }

    private async Task<(bool, string, string?)> RunLifecycleActionAsync(
        string kind, IReadOnlyList<string> resourceKeys, AutomationAction action, CancellationToken token,
        Func<Task<ServerLifecycleOperationResult>> operation, Func<Task<bool>>? abortIfTrue = null)
    {
        // Warning countdown broadcasts happen before the lock is acquired, mirroring how a human
        // admin would announce a restart before actually stopping the server.
        if (action.WarningCountdownSecondsBeforeAction is { Count: > 0 } offsets && rcon.GetStatus().Enabled)
        {
            foreach (var seconds in offsets.OrderDescending())
            {
                var message = (action.WarningMessageTemplate ?? "Server action in {seconds} seconds.").Replace("{seconds}", seconds.ToString());
                await rcon.ExecuteAsync($"Broadcast {message}", token);
                if (seconds > 0) await Task.Delay(TimeSpan.FromSeconds(seconds), token);
            }
        }

        if (abortIfTrue is not null && await abortIfTrue())
            return (false, "Idle auto-stop was aborted: a player was online at the final recheck.", null);

        OperationHandle handle;
        try
        {
            handle = await coordinator.BeginAsync(new ServerProfileId(serverProfile.Id), kind, "HeadlessAutomationService", resourceKeys, token);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message, null);
        }

        try
        {
            var result = await operation();
            if (result.Success) coordinator.Complete(handle.Id, result.Message);
            else coordinator.Fail(handle.Id, result.Message);
            return (result.Success, result.Message, handle.Id.ToString());
        }
        catch (Exception ex)
        {
            coordinator.Fail(handle.Id, ex.Message);
            return (false, ex.Message, handle.Id.ToString());
        }
        finally { handle.Dispose(); }
    }

    private void RecordRun(AutomationRule rule, DateTimeOffset dueUtc, AutomationRunState state, string detail, string? runId, DateTimeOffset? startedUtc, string? operationId = null)
    {
        var record = new AutomationRunRecord
        {
            Id = runId ?? Guid.NewGuid().ToString("N"),
            RuleId = rule.Id,
            RuleName = rule.Name,
            State = state,
            DueUtc = dueUtc,
            StartedUtc = startedUtc,
            CompletedUtc = state is AutomationRunState.Completed or AutomationRunState.Failed or AutomationRunState.Skipped or AutomationRunState.Missed or AutomationRunState.Cancelled ? DateTimeOffset.UtcNow : null,
            Detail = detail,
            OperationId = operationId
        };
        lock (gate)
        {
            runs.Add(record);
            if (runs.Count > MaximumRuns) runs.RemoveRange(0, runs.Count - MaximumRuns);
            PersistRuns();
        }
        activity.Record(state == AutomationRunState.Failed ? "Warning" : "Information", "Automation", $"Rule '{rule.Name}' {state}", detail);
    }

    private static DateTimeOffset ComputeNextDue(AutomationTrigger trigger, DateTimeOffset fromUtc)
    {
        var jitter = trigger.JitterSeconds > 0 ? TimeSpan.FromSeconds(Random.Shared.Next(0, trigger.JitterSeconds + 1)) : TimeSpan.Zero;

        if (trigger.Kind == AutomationTriggerKind.Interval)
        {
            var interval = trigger.Interval is { } i && i > TimeSpan.Zero ? i : TimeSpan.FromHours(1);
            return fromUtc + interval + jitter;
        }

        var timeOfDay = trigger.TimeOfDayUtc ?? TimeOnly.MinValue;
        var days = trigger.DaysOfWeek == AutomationDayOfWeekMask.None ? AutomationDayOfWeekMask.All : trigger.DaysOfWeek;
        for (var offset = 0; offset <= 7; offset++)
        {
            var candidateDate = DateOnly.FromDateTime(fromUtc.UtcDateTime.Date).AddDays(offset);
            var candidate = new DateTimeOffset(candidateDate.ToDateTime(timeOfDay), TimeSpan.Zero);
            if (candidate <= fromUtc) continue;
            if (!DayMaskIncludes(days, candidateDate.DayOfWeek)) continue;
            return candidate + jitter;
        }
        return fromUtc.AddDays(1) + jitter;
    }

    private static bool DayMaskIncludes(AutomationDayOfWeekMask mask, DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => mask.HasFlag(AutomationDayOfWeekMask.Sunday),
        DayOfWeek.Monday => mask.HasFlag(AutomationDayOfWeekMask.Monday),
        DayOfWeek.Tuesday => mask.HasFlag(AutomationDayOfWeekMask.Tuesday),
        DayOfWeek.Wednesday => mask.HasFlag(AutomationDayOfWeekMask.Wednesday),
        DayOfWeek.Thursday => mask.HasFlag(AutomationDayOfWeekMask.Thursday),
        DayOfWeek.Friday => mask.HasFlag(AutomationDayOfWeekMask.Friday),
        DayOfWeek.Saturday => mask.HasFlag(AutomationDayOfWeekMask.Saturday),
        _ => false
    };

    public IReadOnlyList<AutomationRule> ListRules() { lock (gate) return rules.Values.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList(); }

    public AutomationRule CreateRule(string name, AutomationTrigger trigger, AutomationCondition condition, AutomationAction action)
    {
        var rule = new AutomationRule
        {
            Id = AutomationRuleId.New(),
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled rule" : name.Trim(),
            Trigger = trigger,
            Condition = condition,
            Action = action,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        rule.NextDueUtc = trigger.Kind == AutomationTriggerKind.IdleEmpty ? null : ComputeNextDue(trigger, DateTimeOffset.UtcNow);
        lock (gate) { rules[rule.Id] = rule; PersistRules(); }
        activity.Record("Information", "Automation", "Created automation rule", $"id={rule.Id}; name={rule.Name}");
        return rule;
    }

    public AutomationRule UpdateRule(AutomationRuleId id, string name, AutomationTrigger trigger, AutomationCondition condition, AutomationAction action)
    {
        lock (gate)
        {
            var rule = rules.TryGetValue(id, out var existing) ? existing : throw new KeyNotFoundException("Automation rule was not found.");
            rule.Name = string.IsNullOrWhiteSpace(name) ? rule.Name : name.Trim();
            rule.Trigger = trigger;
            rule.Condition = condition;
            rule.Action = action;
            rule.NextDueUtc = trigger.Kind == AutomationTriggerKind.IdleEmpty ? null : ComputeNextDue(trigger, DateTimeOffset.UtcNow);
            if (trigger.Kind != AutomationTriggerKind.IdleEmpty) rule.IdleSinceUtc = null;
            PersistRules();
            return rule;
        }
    }

    public bool DeleteRule(AutomationRuleId id)
    {
        lock (gate)
        {
            var removed = rules.Remove(id);
            if (removed) PersistRules();
            return removed;
        }
    }

    public AutomationRule SetEnabled(AutomationRuleId id, bool enabled)
    {
        lock (gate)
        {
            var rule = rules.TryGetValue(id, out var existing) ? existing : throw new KeyNotFoundException("Automation rule was not found.");
            rule.Enabled = enabled;
            if (enabled && rule.NextDueUtc is null && rule.Trigger.Kind != AutomationTriggerKind.IdleEmpty)
                rule.NextDueUtc = ComputeNextDue(rule.Trigger, DateTimeOffset.UtcNow);
            if (!enabled) rule.IdleSinceUtc = null;
            PersistRules();
            return rule;
        }
    }

    public void RunNow(AutomationRuleId id, CancellationToken hostToken)
    {
        AutomationRule rule;
        lock (gate) rule = rules.TryGetValue(id, out var existing) ? existing : throw new KeyNotFoundException("Automation rule was not found.");
        _ = Task.Run(() => ExecuteRuleAsync(rule, DateTimeOffset.UtcNow, hostToken), hostToken);
    }

    public bool CancelRun(string runId)
    {
        lock (gate)
        {
            if (!activeRuns.TryGetValue(runId, out var cts)) return false;
            cts.Cancel();
            return true;
        }
    }

    public IReadOnlyList<AutomationRunRecord> ListRuns(int max = 100)
    {
        lock (gate) return runs.AsEnumerable().Reverse().Take(Math.Clamp(max, 1, MaximumRuns)).ToList();
    }

    private Dictionary<AutomationRuleId, AutomationRule> LoadRules()
    {
        try
        {
            if (!File.Exists(rulesPath)) return [];
            var list = JsonSerializer.Deserialize<List<AutomationRule>>(File.ReadAllText(rulesPath)) ?? [];
            return list.ToDictionary(r => r.Id);
        }
        catch { return []; }
    }

    // Caller must already hold gate.
    private void PersistRules()
    {
        var partial = rulesPath + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(rules.Values.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(partial, rulesPath, true);
    }

    private List<AutomationRunRecord> LoadRuns()
    {
        try { return File.Exists(runsPath) ? JsonSerializer.Deserialize<List<AutomationRunRecord>>(File.ReadAllText(runsPath)) ?? [] : []; }
        catch { return []; }
    }

    // Caller must already hold gate.
    private void PersistRuns()
    {
        var partial = runsPath + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(runs, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(partial, runsPath, true);
    }
}

public sealed record AutomationRuleRequest(string Name, AutomationTrigger Trigger, AutomationCondition Condition, AutomationAction Action);
public sealed record AutomationEnabledRequest(bool Enabled);
