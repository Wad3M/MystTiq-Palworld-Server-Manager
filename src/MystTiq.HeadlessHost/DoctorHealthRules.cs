using MystTiq.Core.Automation;
using MystTiq.Core.Models;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed record DoctorRuleResult(DiagnosticState State, string Evidence, string Recommendation);

// v0.7.103.0: what the Doctor needs to know about one automation rule whose action is CreateBackup.
public sealed record BackupScheduleRule(
    string Name, bool Enabled, AutomationTriggerKind Kind, TimeSpan? Interval, AutomationDayOfWeekMask Days,
    TimeOnly? TimeOfDayUtc, string? LastRunState, string? LastRunDetail);

// CanCreateRule is true only when there is no backup rule at all, so the one-click fix can never duplicate one.
public sealed record ScheduledBackupsVerdict(DoctorRuleResult Result, bool CanCreateRule);

// v0.7.98.0: pure health rules for the Doctor. The Doctor used to check that paths exist and that
// ports do not collide, but not the things that actually end a server's life: a full disk (only
// checked on Linux), no recent backup, an empty or trivially guessable admin password, too little
// memory, or an unreviewed crash. The inputs are gathered by HeadlessDiagnosticsService; these
// functions only decide, so the logic harness can test every threshold without a machine to match.
public static class DoctorHealthRules
{
    private const double GiB = 1024d * 1024d * 1024d;

    // Very common admin passwords. Compared case-insensitively and never echoed back anywhere.
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "admin123", "administrator", "password", "password1", "123456", "1234567", "12345678", "123456789",
        "1234", "12345", "0000", "1111", "qwerty", "letmein", "changeme", "default", "root", "test", "server",
        "pass", "palworld", "palserver", "pal", "welcome", "secret"
    };

    public static string FormatSpan(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span.TotalMinutes < 1) return "under a minute";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} minute(s)";
        if (span.TotalDays < 1) return $"{span.TotalHours:0.#} hour(s)";
        return $"{span.TotalDays:0.#} day(s)";
    }

    // Free space on a drive, judged by the shared DiskSpaceRules the Alert Center uses too (v0.7.102.0), so
    // the two can no longer disagree: Fail under 2 GiB free or at or under the Alert Center's low-disk
    // percentage (criticalPercent, null when that rule is off), Warning under 5 GiB. If backups live on the
    // drive, the next backup also has to fit, so free space below twice the largest backup is a warning even
    // when the absolute figure looks fine.
    public static DoctorRuleResult DiskSpace(string label, long freeBytes, long totalBytes, long largestBackupBytes = 0, double? criticalPercent = null)
    {
        var free = freeBytes / GiB;
        var total = totalBytes / GiB;
        var state = DiskSpaceRules.Evaluate(freeBytes, totalBytes, criticalPercent) switch
        {
            DiskLevel.Critical => DiagnosticState.Fail,
            DiskLevel.Warning => DiagnosticState.Warning,
            _ => DiagnosticState.Pass
        };
        var evidence = $"{free:F1} GiB free of {total:F1} GiB ({DiskSpaceRules.FreePercent(freeBytes, totalBytes):F1}%) on the drive holding {label}.";
        var recommendation = state == DiagnosticState.Pass ? "No action required." : "Free disk space before updates, backups or save maintenance: a full disk can stop the server and corrupt a save.";

        if (largestBackupBytes > 0 && freeBytes < 2 * largestBackupBytes)
        {
            if (state == DiagnosticState.Pass) state = DiagnosticState.Warning;
            evidence += $" The largest backup is {largestBackupBytes / GiB:F2} GiB, so the next backup may not fit.";
            recommendation = "Free space or prune old backups (Backup Center retention), or move the backup root to a larger drive.";
        }
        return new(state, evidence, recommendation);
    }

    // Backup freshness is judged against the world, not the clock: a server that has been stopped
    // for a week with an unchanged world is fully protected by a week-old backup. What matters is how
    // long the world's latest changes have gone unprotected.
    public static DoctorRuleResult BackupFreshness(int backupCount, DateTimeOffset? latestBackupAt, DateTimeOffset? worldLastWriteAt, DateTimeOffset now)
    {
        if (backupCount == 0 || latestBackupAt is null)
        {
            return worldLastWriteAt is null
                ? new(DiagnosticState.Skipped, "No world save and no backups exist yet, so there is nothing to protect.", "Create a backup once the world exists.")
                : new(DiagnosticState.Warning, "The world has save data but no backup exists.", "Create a backup now in Backup Center, and set up scheduled backups in Automation so it never depends on remembering.");
        }

        var backupAge = now - latestBackupAt.Value;
        if (worldLastWriteAt is null)
            return new(DiagnosticState.Pass, $"Latest backup is {FormatSpan(backupAge)} old. No world save was found to compare it with.", "No action required.");

        var exposure = worldLastWriteAt.Value - latestBackupAt.Value;
        if (exposure <= TimeSpan.FromHours(1))
            return new(DiagnosticState.Pass, $"Latest backup is {FormatSpan(backupAge)} old and the world has not changed since (or only within the hour after it).", "No action required.");
        if (exposure < TimeSpan.FromHours(24))
            return new(DiagnosticState.Pass, $"Latest backup is {FormatSpan(backupAge)} old; the world changed {FormatSpan(exposure)} after it.", "No action required.");

        var state = exposure < TimeSpan.FromDays(14) ? DiagnosticState.Warning : DiagnosticState.Fail;
        return new(state,
            $"Latest backup is {FormatSpan(backupAge)} old, and the world kept changing for {FormatSpan(exposure)} after it, so that much play is not backed up.",
            "Create a backup now, and schedule regular backups in Automation.");
    }

    // Whether a backup exists is Backup freshness's question; this one is whether anything will make the NEXT
    // one happen. With no rule creating backups, protection depends on someone pressing Backup.
    public static ScheduledBackupsVerdict ScheduledBackups(IReadOnlyList<BackupScheduleRule> rules)
    {
        if (rules.Count == 0)
            return new(new DoctorRuleResult(DiagnosticState.Warning,
                "No automation rule creates backups, so a backup only exists when someone presses Backup.",
                "Create a nightly backup rule (the button here does it), or add your own schedule in Automation."), CanCreateRule: true);

        var enabled = rules.Where(r => r.Enabled).ToArray();
        if (enabled.Length == 0)
            return new(new DoctorRuleResult(DiagnosticState.Warning,
                $"{rules.Count} backup rule(s) exist, but all of them are switched off: {string.Join(", ", rules.Select(r => $"\"{r.Name}\"").Take(3))}.",
                "Enable one in Automation. No new rule is offered, to avoid a duplicate."), CanCreateRule: false);

        var regular = enabled.Where(RunsAtLeastWeekly).ToArray();
        if (regular.Length == 0)
            return new(new DoctorRuleResult(DiagnosticState.Warning,
                $"Backup rule(s) exist and are on, but none runs on a schedule of at least weekly: {string.Join("; ", enabled.Select(r => $"\"{r.Name}\" ({Describe(r)})").Take(3))}.",
                "Change one to a daily or interval schedule in Automation. A rule that only fires when the server is idle is not a schedule."), CanCreateRule: false);

        var failing = regular.Where(r => string.Equals(r.LastRunState, "Failed", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (failing.Length == regular.Length)
            return new(new DoctorRuleResult(DiagnosticState.Warning,
                $"The scheduled backup rule \"{failing[0].Name}\" ({Describe(failing[0])}) failed on its last run: {failing[0].LastRunDetail}",
                "Open Automation to see the run, fix the cause (often a full disk or a locked file), then use Run Now."), CanCreateRule: false);

        var best = regular[0];
        return new(new DoctorRuleResult(DiagnosticState.Pass,
            $"Backups are scheduled: \"{best.Name}\" runs {Describe(best)}.", "No action required."), CanCreateRule: false);
    }

    public static bool RunsAtLeastWeekly(BackupScheduleRule rule) => rule.Kind switch
    {
        AutomationTriggerKind.DailyTime => rule.Days != AutomationDayOfWeekMask.None,
        AutomationTriggerKind.Interval => rule.Interval is { } interval && interval > TimeSpan.Zero && interval <= TimeSpan.FromDays(7),
        _ => false
    };

    private static string Describe(BackupScheduleRule rule) => rule.Kind switch
    {
        AutomationTriggerKind.DailyTime when rule.Days == AutomationDayOfWeekMask.All => $"every day at {rule.TimeOfDayUtc?.ToString("HH:mm") ?? "??:??"} UTC",
        AutomationTriggerKind.DailyTime when rule.Days == AutomationDayOfWeekMask.None => "on no days",
        AutomationTriggerKind.DailyTime => $"on selected days at {rule.TimeOfDayUtc?.ToString("HH:mm") ?? "??:??"} UTC",
        AutomationTriggerKind.Interval => $"every {FormatSpan(rule.Interval ?? TimeSpan.Zero)}",
        AutomationTriggerKind.IdleEmpty => "only when the server has been empty for a while",
        _ => "on an unknown schedule"
    };

    // The admin password is only judged when a remote-admin interface is on, and it is never echoed:
    // the evidence carries its length or the fact that it is a common value, not the value.
    public static DoctorRuleResult AdminSecurity(string? adminPassword, bool restEnabled, bool rconEnabled)
    {
        var password = (adminPassword ?? string.Empty).Trim().Trim('"');
        if (!restEnabled && !rconEnabled)
            return new(DiagnosticState.Pass, "The REST API and RCON are both off, so nothing can be administered remotely.", "No action required. MystTiq needs one of them on for kick, ban, save and broadcast.");

        var interfaces = restEnabled && rconEnabled ? "The REST API and RCON are enabled" : restEnabled ? "The REST API is enabled" : "RCON is enabled";
        if (password.Length == 0)
            return new(DiagnosticState.Warning, $"{interfaces} but AdminPassword is empty, so authenticated requests are rejected and MystTiq cannot kick, ban, save or broadcast.",
                "Set a long, random AdminPassword in Configuration and restart the server.");

        if (CommonPasswords.Contains(password))
            return new(DiagnosticState.Warning, $"{interfaces} and AdminPassword is a very common password.",
                "Replace it with a long, random one. It controls the whole server, so keep the REST and RCON ports closed to the internet.");
        if (password.Length < 8)
            return new(DiagnosticState.Warning, $"{interfaces} and AdminPassword is only {password.Length} character(s) long.",
                "Use at least 12 random characters. It controls the whole server, so keep the REST and RCON ports closed to the internet.");

        return new(DiagnosticState.Pass, $"{interfaces} and AdminPassword is set ({password.Length} characters).", "Keep the REST and RCON ports closed to the internet unless you need them.");
    }

    // The 16 GB figure is the commonly cited recommendation for Palworld dedicated servers, worded
    // as such rather than as a hard requirement.
    public static DoctorRuleResult Memory(long totalBytes, long availableBytes)
    {
        if (totalBytes <= 0)
            return new(DiagnosticState.Skipped, "The machine's memory could not be read.", "No action required.");

        var total = totalBytes / GiB;
        var available = Math.Max(0, availableBytes) / GiB;
        var evidence = $"{total:F1} GiB installed, about {available:F1} GiB free right now.";
        var problems = new List<string>();
        if (total < 8) problems.Add($"{total:F1} GiB is well below the 16 GiB commonly recommended for a Palworld dedicated server");
        if (available < 1.5) problems.Add($"only about {available:F1} GiB is free right now");
        if (problems.Count == 0) return new(DiagnosticState.Pass, evidence, "No action required.");

        return new(DiagnosticState.Warning, $"{evidence} {string.Join("; ", problems)}.",
            "Palworld servers use more memory the longer they run and the more bases exist. Add memory or swap, close other programs, and schedule regular restarts in Automation.");
    }

    // Reads the newest Crash Analyzer report. Only findings that are both new and Critical count, so
    // once the user has run the analysis again (which marks them as already reported) the warning
    // clears. A report written before v0.7.97.0 carries no keys and cannot say what is new.
    public static DoctorRuleResult RecentCrashes(HeadlessCrashAnalysisSnapshot? latest, DateTimeOffset now)
    {
        if (latest is null)
            return new(DiagnosticState.Skipped, "No crash analysis has been run yet.", "Run the Crash Analyzer if the server has been crashing or stopping unexpectedly.");

        var age = FormatSpan(now - latest.ObservedAt);
        if (latest.Findings.Any(f => string.IsNullOrEmpty(f.Key)))
            return new(DiagnosticState.Skipped, $"The latest crash analysis ({age} ago) was made by an older version and cannot say what is new.", "Run the Crash Analyzer again.");

        var fresh = latest.Findings.Where(f => f.IsNew && f.Severity == "Critical").ToArray();
        if (fresh.Length > 0)
        {
            var titles = string.Join(", ", fresh.Take(3).Select(f => f.Title));
            return new(DiagnosticState.Warning, $"The latest crash analysis ({age} ago) found {fresh.Length} new critical problem(s): {titles}.",
                "Open Crash Analyzer for the cause and what to try. Running the analysis again marks these as reviewed.");
        }

        return new(DiagnosticState.Pass, latest.Findings.Count == 0
            ? $"The latest crash analysis ({age} ago) found no known crash signature."
            : $"The latest crash analysis ({age} ago) has {latest.Findings.Count} finding(s), none of them new and critical.",
            "No action required.");
    }
}
