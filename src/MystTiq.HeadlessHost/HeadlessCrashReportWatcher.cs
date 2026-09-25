using System.Text.Json;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public enum CrashReportCheckOutcome { NothingNew, Baseline, Muted, Disabled, NoNewCritical, CoveredByCrashAlert, Alerted }

public sealed record CrashReportCheck(CrashReportCheckOutcome Outcome, IReadOnlyList<string> NewReports, string Detail);

// v0.8.9.0: alerts when Unreal writes a new crash report and the analysis finds a new critical problem in it. This covers
// what the crash-recovery alerts cannot: a crash while MystTiq was not running or not supervising (the report is found at
// the next check), and the exact engine error the report names. Checked on the automation tick.
//   - The first check ever records the reports already on disk without alerting, so an upgrade does not announce old crashes.
//   - While alerts are muted nothing is marked seen, so a report found during the mute still alerts once the mute ends.
//   - With crash alerts switched off, reports are marked seen and only the Activity log records them.
//   - A crash the recovery loop alerted about in the last few minutes is not announced a second time: that alert already
//     carried this same analysis.
public sealed class HeadlessCrashReportWatcher
{
    public static readonly TimeSpan RecentCrashAlertWindow = TimeSpan.FromMinutes(10);

    private readonly string serverName;
    private readonly HeadlessCrashAndSaveToolsService crashTools;
    private readonly HeadlessNotificationService notifications;
    private readonly Func<CancellationToken, Task<IReadOnlyCollection<string>>> modNames;
    private readonly Func<AlertRuleSet> rules;
    private readonly Func<DateTimeOffset?> lastCrashAlertUtc;
    private readonly HeadlessActivityLogService activity;
    private readonly string seenPath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public HeadlessCrashReportWatcher(
        IServerPathProfile paths,
        string serverName,
        HeadlessCrashAndSaveToolsService crashTools,
        HeadlessNotificationService notifications,
        Func<CancellationToken, Task<IReadOnlyCollection<string>>> modNames,
        Func<AlertRuleSet> rules,
        Func<DateTimeOffset?> lastCrashAlertUtc,
        HeadlessActivityLogService activity)
    {
        this.serverName = string.IsNullOrWhiteSpace(serverName) ? "Palworld server" : serverName.Trim();
        this.crashTools = crashTools;
        this.notifications = notifications;
        this.modNames = modNames;
        this.rules = rules;
        this.lastCrashAlertUtc = lastCrashAlertUtc;
        this.activity = activity;
        var root = Path.Combine(paths.ManagerRuntimeRoot, "crash-analyzer");
        Directory.CreateDirectory(root);
        seenPath = Path.Combine(root, "seen-reports.json");
    }

    public async Task<CrashReportCheck> CheckAsync(DateTimeOffset now, CancellationToken token)
    {
        if (!await gate.WaitAsync(0, token)) return new(CrashReportCheckOutcome.NothingNew, [], "A check is already running.");
        try
        {
            var onDisk = crashTools.ReadCrashReports().Select(r => r.Folder).ToArray();
            var seen = LoadSeen();
            if (seen is null)
            {
                SaveSeen(onDisk);
                return new(CrashReportCheckOutcome.Baseline, [], $"Recorded {onDisk.Length} existing crash report(s) without alerting.");
            }

            var fresh = onDisk.Where(name => !seen.Contains(name)).ToArray();
            if (fresh.Length == 0) return new(CrashReportCheckOutcome.NothingNew, [], "No new crash report.");

            var ruleSet = rules();
            if (AlertMutePolicy.IsMuted(ruleSet, now))
                return new(CrashReportCheckOutcome.Muted, fresh, "Alerts are muted; the new report is announced when the mute ends.");

            SaveSeen(seen.Concat(fresh));
            var list = string.Join(", ", fresh);
            if (!(ruleSet.CrashAlerts ?? new CrashAlertRule(true, true)).Enabled)
            {
                activity.Record("Information", "Crash Analyzer", "New crash report (crash alerts are switched off)", list);
                return new(CrashReportCheckOutcome.Disabled, fresh, "Crash alerts are switched off for this server.");
            }

            IReadOnlyCollection<string> names = [];
            try { names = await modNames(token); }
            catch (Exception ex) when (ex is not OperationCanceledException) { }
            var analysis = crashTools.Analyze(names);
            // Only findings that come from the new reports count: an older report's finding that was simply never analysed
            // before must not be presented as the cause of this crash.
            var critical = RelatedCriticalFindings(analysis, fresh);
            if (critical.Count == 0)
            {
                activity.Record("Information", "Crash Analyzer", "New crash report, no new critical finding", list);
                return new(CrashReportCheckOutcome.NoNewCritical, fresh, "The new report adds no new critical finding.");
            }

            if (lastCrashAlertUtc() is { } alerted && now - alerted < RecentCrashAlertWindow)
            {
                activity.Record("Information", "Crash Analyzer", "New crash report already covered by the crash alert", list);
                return new(CrashReportCheckOutcome.CoveredByCrashAlert, fresh, "The crash-recovery alert already announced this crash.");
            }

            notifications.Create("Critical", $"{serverName}: new crash report",
                $"The game wrote {fresh.Length} new crash report(s) ({list})." + CrashAlertText.Describe(analysis with { Findings = critical }) +
                " Open the Crash Analyzer for the full evidence.");
            return new(CrashReportCheckOutcome.Alerted, fresh, $"Alerted: {critical[0].Title}.");
        }
        finally { gate.Release(); }
    }

    // Pure (logic harness): new critical findings whose evidence names one of the new report folders.
    public static IReadOnlyList<HeadlessCrashFinding> RelatedCriticalFindings(HeadlessCrashAnalysisSnapshot analysis, IReadOnlyCollection<string> newReports) =>
        analysis.Findings.Where(f => f.IsNew && f.Severity == "Critical" &&
            f.Evidence.Any(line => newReports.Any(report => line.Contains($"crash report {report}:", StringComparison.OrdinalIgnoreCase)))).ToArray();

    private HashSet<string>? LoadSeen()
    {
        try { return File.Exists(seenPath) ? (JsonSerializer.Deserialize<List<string>>(File.ReadAllText(seenPath)) ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase) : null; }
        // An unreadable record is treated like a first run (record, do not alert): the alternative would announce every old report.
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    private void SaveSeen(IEnumerable<string> names)
    {
        try
        {
            var partial = seenPath + ".partial";
            File.WriteAllText(partial, JsonSerializer.Serialize(names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
            File.Move(partial, seenPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
