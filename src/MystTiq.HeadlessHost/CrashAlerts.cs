using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed record CrashAlert(string Severity, string Title, string Message, bool Pinned);

// v0.7.101.0: turns a crash-recovery event plus the latest crash analysis into the one notification
// an admin actually wants: that the server crashed, what MystTiq is doing about it, what the logs say
// it was, and the first thing to try. Pure, so the logic harness covers every case.
public static class CrashAlertText
{
    public static CrashAlert Build(string serverName, SupervisorEvent e, HeadlessCrashAnalysisSnapshot? analysis)
    {
        var name = string.IsNullOrWhiteSpace(serverName) ? "Palworld server" : serverName.Trim();
        switch (e.Kind)
        {
            case SupervisorEventKind.CrashDetected:
                return new("Critical",
                    $"{name}: server crashed, restarting (attempt {e.Attempt} of {e.MaximumAttempts})",
                    $"PalServer stopped unexpectedly. MystTiq is restarting it in {FormatBackoff(e.RestartBackoff)}." + Describe(analysis),
                    false);

            case SupervisorEventKind.RecoverySucceeded:
                return new("Success",
                    $"{name}: server is back up",
                    $"{e.Detail} That was restart attempt {e.Attempt} of {e.MaximumAttempts} in the last {FormatBackoff(e.RestartWindow)}.",
                    false);

            case SupervisorEventKind.RecoveryFailed:
                var remaining = e.MaximumAttempts - e.Attempt;
                return new("Warning",
                    $"{name}: restart attempt {e.Attempt} of {e.MaximumAttempts} failed",
                    $"{e.Detail} " + (remaining > 0
                        ? $"MystTiq will try again ({remaining} attempt(s) left in this window)."
                        : "That was the last attempt in this window."),
                    false);

            // v0.7.110.0: automatic recovery had given up and pinned a "server is DOWN" notice; the
            // server is running again now, started by something other than the loop itself (an admin,
            // or Automation). Not pinned -- CrashAlertObserver unpins the original notice separately.
            case SupervisorEventKind.ManualRecovery:
                return new("Success",
                    $"{name}: server is back up",
                    $"{e.Detail} Automatic recovery had given up earlier and was not the cause -- crash-detect-and-restart monitoring has resumed.",
                    false);

            default:
                return new("Critical",
                    $"{name}: server is DOWN, automatic recovery gave up",
                    $"{e.Detail} The server will not restart on its own now. Fix the cause, then start it manually." + Describe(analysis),
                    true);
        }
    }

    // What the logs say, in one short paragraph. New findings come first (the analysis sorts them that
    // way); a finding already reported earlier is not presented as the cause of this crash.
    public static string Describe(HeadlessCrashAnalysisSnapshot? analysis)
    {
        if (analysis is null) return string.Empty;
        if (analysis.Findings.Count == 0)
            return " The logs contain no known crash signature, so the cause is not in them. Open the Crash Analyzer to review them.";

        var top = analysis.Findings[0];
        if (!top.IsNew)
            return $" The logs show no new crash evidence (the newest finding, \"{top.Title}\", was already reported earlier), so this crash left nothing new to read. Open the Crash Analyzer for the history.";

        var text = $" Likely cause from the logs: {top.Title}. {FirstSentence(top.Cause)}";
        if (top.Fixes is { Count: > 0 }) text += $" First thing to try: {top.Fixes[0]}";
        if (top.MentionedMods is { Count: > 0 }) text += $" Mod(s) named in the evidence: {string.Join(", ", top.MentionedMods)} (a lead, not proof).";
        var others = analysis.Findings.Count(f => f.IsNew) - 1;
        if (others > 0) text += $" {others} other new finding(s) are in the Crash Analyzer.";
        return text;
    }

    private static string FirstSentence(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var end = text.IndexOf(". ", StringComparison.Ordinal);
        return end > 0 ? text[..(end + 1)] : text.Trim();
    }

    private static string FormatBackoff(TimeSpan span) =>
        span.TotalMinutes >= 1 ? $"{span.TotalMinutes:0.#} minute(s)" : $"{Math.Max(1, (int)Math.Round(span.TotalSeconds))} second(s)";
}

// Sends the alert through the normal notification pipeline, so it reaches whatever routes the admin
// has set up (Discord, email, webhooks) and appears in the Notifications page.
public sealed class CrashAlertObserver : ISupervisorObserver
{
    private readonly string serverName;
    private readonly HeadlessNotificationService notifications;
    private readonly HeadlessCrashAndSaveToolsService crashTools;
    private readonly Func<CancellationToken, Task<IReadOnlyCollection<string>>> modNames;
    // v0.7.110.0: the id of the pinned "server is DOWN, automatic recovery gave up" notice, so a later
    // ManualRecovery event can unpin the same one it caused -- mirrors AlertEpisodeTracker's own
    // AlertNotificationId pattern (v0.7.104.0), just without a tracker object since a crash-recovery
    // give-up/recovery pair is a single in-memory cycle, not a persisted episode.
    // v0.7.115.0 (deficiency report): kept in the profile's SupervisorRecoveryStateStore when one is given,
    // so a DOWN notice pinned before a MystTiq restart can still be unpinned after it. Memory-only otherwise.
    private string? pinnedGiveUpNotificationIdInMemory;
    private string? PinnedGiveUpNotificationId
    {
        get => stateStore is null ? pinnedGiveUpNotificationIdInMemory : stateStore.Read().PinnedNotificationId;
        set { pinnedGiveUpNotificationIdInMemory = value; stateStore?.Update(s => s with { PinnedNotificationId = value }); }
    }
    // v0.7.111.0: this profile's own alert rules (crash alerts on/off, "back up" notices, mute), read on
    // every event so a change on the Alert Center page applies to the very next crash.
    private readonly Func<AlertRuleSet> rules;
    private readonly HeadlessActivityLogService? activity;
    private readonly SupervisorRecoveryStateStore? stateStore;

    public CrashAlertObserver(
        string serverName,
        HeadlessNotificationService notifications,
        HeadlessCrashAndSaveToolsService crashTools,
        Func<CancellationToken, Task<IReadOnlyCollection<string>>>? modNames = null,
        Func<AlertRuleSet>? rules = null,
        HeadlessActivityLogService? activity = null,
        SupervisorRecoveryStateStore? stateStore = null)
    {
        this.serverName = serverName;
        this.notifications = notifications;
        this.crashTools = crashTools;
        this.modNames = modNames ?? (_ => Task.FromResult<IReadOnlyCollection<string>>([]));
        this.rules = rules ?? (() => new AlertRuleSet());
        this.activity = activity;
        this.stateStore = stateStore;
    }

    // v0.8.9.0: when this observer last handled a crash, so the crash-report watcher does not announce the same crash again.
    public DateTimeOffset? LastCrashDetectedUtc { get; private set; }

    public async Task OnEventAsync(SupervisorEvent supervisorEvent, CancellationToken cancellationToken)
    {
        if (supervisorEvent.Kind == SupervisorEventKind.CrashDetected) LastCrashDetectedUtc = DateTimeOffset.UtcNow;
        HeadlessCrashAnalysisSnapshot? analysis = null;
        try
        {
            if (supervisorEvent.Kind == SupervisorEventKind.CrashDetected)
            {
                // The crash just happened, so the logs are fresh: analyse now (this also records the
                // report, which is what marks these findings as "already reported" next time).
                IReadOnlyCollection<string> names = [];
                try { names = await modNames(cancellationToken); }
                catch (Exception ex) when (ex is not OperationCanceledException) { }
                analysis = crashTools.Analyze(names);
            }
            else if (supervisorEvent.Kind == SupervisorEventKind.RecoverySuppressed)
            {
                // Reuse the analysis from the last crash instead of re-reading and re-marking it.
                analysis = crashTools.History(1).FirstOrDefault();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // An unreadable log must not stop the alert itself: send it without the analysis.
        }

        var alert = CrashAlertText.Build(serverName, supervisorEvent, analysis);
        var send = AlertMutePolicy.ShouldSendCrashAlert(rules(), supervisorEvent.Kind, DateTimeOffset.UtcNow);
        if (!send)
        {
            // Muted or switched off: no notification, but the Activity log still records what happened.
            activity?.Record("Information", "Alerts", "Crash alert not sent (muted or switched off)", alert.Title);
        }

        if (supervisorEvent.Kind == SupervisorEventKind.ManualRecovery)
        {
            // The unpin below runs even when this notice is not sent: a DOWN notice pinned before the mute
            // must not stay pinned after the server is back.
            if (send) notifications.Create(alert.Severity, alert.Title, alert.Message, alert.Pinned);
            if (PinnedGiveUpNotificationId is { } id)
            {
                try { notifications.SetPinned(id, false); }
                catch (KeyNotFoundException) { /* already dismissed; nothing to unpin */ }
                PinnedGiveUpNotificationId = null;
            }
        }
        else if (send && alert.Pinned)
        {
            // Only the give-up notice is ever pinned (see CrashAlertText.Build's default case): capture
            // its id so a later ManualRecovery can find and unpin this exact notification.
            // v0.8.2.0: service-run can give up again after the OS restarts it; the new notice replaces the old one
            // as the pinned one, so repeated cycles never leave several DOWN notices pinned.
            if (PinnedGiveUpNotificationId is { } previous)
            {
                try { notifications.SetPinned(previous, false); }
                catch (KeyNotFoundException) { /* already dismissed */ }
            }
            notifications.Create(alert.Severity, alert.Title, alert.Message, true, out var newId);
            PinnedGiveUpNotificationId = newId;
        }
        else if (send)
        {
            notifications.Create(alert.Severity, alert.Title, alert.Message, false);
        }
    }
}
