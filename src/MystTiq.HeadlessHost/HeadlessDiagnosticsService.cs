using MystTiq.Core.Automation;
using MystTiq.Core.Models;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.6.4.0 "Troubleshooting & Diagnostics Platform": a thin unification layer over the two
// already-correct, already-covered-by-runtime-smoke services (HeadlessDoctorService,
// HeadlessEnvironmentChecklistService) -- composition, not rewrite, same move ServerProfileHost
// was for the fleet milestone. Maps both into one DiagnosticFinding list so "no health deduction
// exists without a corresponding visible Doctor finding" per the roadmap, de-duplicating the 3
// facts both services independently check (SteamCMD/PalServer-executable/BackupRoot existence)
// into a single merged finding each.
public sealed class HeadlessDiagnosticsService
{
    // Doctor component name -> Environment component name, for the 3 known-overlapping facts.
    // Both read the exact same IServerPathProfile property; merging avoids showing the same fact
    // twice with two different labels/verdicts.
    private static readonly Dictionary<string, string> OverlapMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SteamCMD"] = "SteamCMD",
        ["PalServer entry"] = "Palworld Dedicated Server",
        ["Backup root"] = "Backup Storage",
    };

    private readonly HeadlessDoctorService doctor;
    private readonly HeadlessEnvironmentChecklistService environment;
    private readonly IServerLifecycleService lifecycle;
    private readonly IServerPathProfile paths;
    private readonly HeadlessServerDistributionService serverDistribution;
    private readonly PalworldSettingsConfigurationService palworldConfiguration;
    private readonly HeadlessPlayerRegistryService playerRegistry;
    private readonly HeadlessPlayerGuildExplorerService playerGuildExplorer;
    private readonly HeadlessBackupService? backups;
    private readonly HeadlessCrashAndSaveToolsService? crashTools;
    private readonly Func<double?>? lowDiskPercent;
    private readonly HeadlessAutomationService? automation;
    private readonly object backupRuleGate = new();

    public HeadlessDiagnosticsService(
        HeadlessDoctorService doctor,
        HeadlessEnvironmentChecklistService environment,
        IServerLifecycleService lifecycle,
        IServerPathProfile paths,
        HeadlessServerDistributionService serverDistribution,
        PalworldSettingsConfigurationService palworldConfiguration,
        HeadlessPlayerRegistryService playerRegistry,
        HeadlessPlayerGuildExplorerService playerGuildExplorer,
        HeadlessBackupService? backups = null,
        HeadlessCrashAndSaveToolsService? crashTools = null,
        Func<double?>? lowDiskPercent = null,
        HeadlessAutomationService? automation = null)
    {
        this.automation = automation;
        this.lowDiskPercent = lowDiskPercent;
        this.backups = backups;
        this.crashTools = crashTools;
        this.doctor = doctor;
        this.environment = environment;
        this.lifecycle = lifecycle;
        this.paths = paths;
        this.serverDistribution = serverDistribution;
        this.palworldConfiguration = palworldConfiguration;
        this.playerRegistry = playerRegistry;
        this.playerGuildExplorer = playerGuildExplorer;
    }

    public async Task<DiagnosticsReport> GetUnifiedReportAsync(CancellationToken cancellationToken)
    {
        var doctorReport = await doctor.RunAsync(cancellationToken);
        var envSnapshot = environment.GetSnapshot();
        var status = await lifecycle.GetStatusAsync(cancellationToken);

        var findings = new List<DiagnosticFinding>();
        var mergedEnvComponents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var check in doctorReport.Checks)
        {
            var state = MapDoctorState(check.State);
            var location = check.Evidence;
            bool actionSupported = false;
            string? actionKind = null;
            string? unavailableReason = null;

            if (OverlapMap.TryGetValue(check.Component, out var envComponent))
            {
                var envItem = envSnapshot.Items.FirstOrDefault(i => i.Component.Equals(envComponent, StringComparison.OrdinalIgnoreCase));
                if (envItem is not null)
                {
                    mergedEnvComponents.Add(envItem.Component);
                    location = envItem.Location;
                    actionSupported = envItem.ActionSupported;
                    unavailableReason = envItem.UnavailableReason;
                    if (state != DiagnosticState.Pass)
                    {
                        actionKind = check.Component.Equals("Backup root", StringComparison.OrdinalIgnoreCase)
                            ? "create-backup-root"
                            : "install-distribution";
                    }
                }
            }

            findings.Add(new DiagnosticFinding(
                Id: Slug("health", check.Component),
                Category: "Health",
                Component: check.Component,
                State: state,
                Location: location,
                Evidence: check.Evidence,
                Recommendation: check.Recommendation,
                ActionKind: actionKind,
                ActionSupported: actionKind is not null || actionSupported,
                UnavailableReason: unavailableReason,
                ObservedAt: doctorReport.CheckedAt,
                Duration: TimeSpan.Zero));
        }

        foreach (var item in envSnapshot.Items)
        {
            if (mergedEnvComponents.Contains(item.Component)) continue;

            findings.Add(new DiagnosticFinding(
                Id: Slug("setup", item.Component),
                Category: "Setup",
                Component: item.Component,
                State: MapEnvironmentState(item.Status),
                Location: item.Location,
                Evidence: item.Details,
                Recommendation: item.Details,
                ActionKind: null,
                ActionSupported: item.ActionSupported,
                UnavailableReason: item.UnavailableReason,
                ObservedAt: envSnapshot.ObservedAt,
                Duration: TimeSpan.Zero));
        }

        findings.AddRange(BuildConfigurationFindings());
        findings.AddRange(BuildCrashRiskFindings());
        findings.AddRange(BuildResourceFindings());
        findings.AddRange(BuildBackupFindings());
        findings.AddRange(BuildSecurityFindings());
        findings.AddRange(BuildRecentCrashFindings());
        findings.AddRange(await BuildIdentityFindingsAsync(cancellationToken));

        return BuildReport(findings, status.Ready);
    }

    // v0.6.7.0 "Player identity mismatch detection" -- the read-only diagnostic foundation the
    // roadmap explicitly frames as what a future UID remapping wizard builds on, not the wizard
    // itself. Reuses v0.6.6.0's Player Registry (real Steam ID/UserId capture) cross-referenced
    // against the existing guild explorer's player-save discovery -- no new save-decode work.
    private async Task<IReadOnlyList<DiagnosticFinding>> BuildIdentityFindingsAsync(CancellationToken cancellationToken)
    {
        var findings = new List<DiagnosticFinding>();
        var observedAt = DateTimeOffset.UtcNow;
        var registry = playerRegistry.Snapshot();
        if (registry.Count == 0) return findings;

        var duplicateSteamIds = registry
            .Where(r => !string.IsNullOrWhiteSpace(r.SteamId))
            .GroupBy(r => r.SteamId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(r => r.PlayerId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);

        foreach (var group in duplicateSteamIds)
        {
            var identities = string.Join(", ", group.Select(r => $"{r.LastKnownName} ({r.PlayerId})"));
            findings.Add(new DiagnosticFinding(
                Id: Slug("identity-steam-collision", group.Key),
                Category: "Identity",
                Component: "Steam ID Collision",
                State: DiagnosticState.Warning,
                Location: "Player Registry",
                Evidence: $"Steam ID {group.Key} is associated with multiple distinct player identities: {identities}.",
                Recommendation: "This can indicate a platform account switch or a duplicate identity. Review before using Character Migration or Remove Broken Member.",
                ActionKind: null,
                ActionSupported: false,
                UnavailableReason: "No automatic fix is available for this finding yet.",
                ObservedAt: observedAt,
                Duration: TimeSpan.Zero));
        }

        HeadlessPlayerGuildSnapshot guildSnapshot;
        try { guildSnapshot = await playerGuildExplorer.ExploreAsync(cancellationToken); }
        catch { return findings; }

        if (guildSnapshot.Available)
        {
            var saveExistsById = guildSnapshot.Players.ToDictionary(p => p.PlayerId, p => p.SaveExists, StringComparer.OrdinalIgnoreCase);
            foreach (var record in registry)
            {
                if (saveExistsById.TryGetValue(record.PlayerId, out var saveExists) && !saveExists)
                {
                    findings.Add(new DiagnosticFinding(
                        Id: Slug("identity-missing-save", record.PlayerId),
                        Category: "Identity",
                        Component: "Missing Save File",
                        State: DiagnosticState.Warning,
                        Location: "Player Registry vs. World Explorer",
                        Evidence: $"{record.LastKnownName} ({record.PlayerId}) was observed online via REST but has no matching player save file.",
                        Recommendation: "Normal for a player who just joined for the first time before their save writes. If persistent, it may indicate an identity mismatch worth investigating.",
                        ActionKind: null,
                        ActionSupported: false,
                        UnavailableReason: "No automatic fix is available for this finding yet.",
                        ObservedAt: observedAt,
                        Duration: TimeSpan.Zero));
                }
            }
        }

        return findings;
    }

    // v0.6.5.0 "Configuration Intelligence" (first real slice): a cross-setting check no single
    // existing service performs -- Doctor and Environment each validate individual settings exist,
    // but neither checks whether the Game/REST/RCON ports configured in PalWorldSettings.ini
    // actually collide with each other. A collision here is a real, silent-until-it-fails
    // misconfiguration (whichever service binds second either fails to start or never becomes
    // reachable), so it is surfaced as its own "Configuration" category finding through the same
    // unified report the Dashboard/Doctor page already consume.
    private IReadOnlyList<DiagnosticFinding> BuildConfigurationFindings()
    {
        var snapshot = palworldConfiguration.Load();
        var observedAt = DateTimeOffset.UtcNow;
        if (!snapshot.Exists)
        {
            return
            [
                new DiagnosticFinding(
                    Id: "configuration-port-conflicts",
                    Category: "Configuration",
                    Component: "Port Conflicts",
                    State: DiagnosticState.Skipped,
                    Location: palworldConfiguration.ConfigurationPath,
                    Evidence: snapshot.Detail,
                    Recommendation: "PalWorldSettings.ini was not found; port-conflict checking requires it to exist.",
                    ActionKind: null,
                    ActionSupported: false,
                    UnavailableReason: null,
                    ObservedAt: observedAt,
                    Duration: TimeSpan.Zero)
            ];
        }

        var ports = new List<(string Label, int Port)>();
        var gamePort = GetInt(snapshot, "PublicPort");
        if (gamePort is > 0) ports.Add(("Public Game Port", gamePort.Value));
        if (GetBool(snapshot, "RESTAPIEnabled") is true)
        {
            var restPort = GetInt(snapshot, "RESTAPIPort");
            if (restPort is > 0) ports.Add(("REST API Port", restPort.Value));
        }
        if (GetBool(snapshot, "RCONEnabled") is true)
        {
            var rconPort = GetInt(snapshot, "RCONPort");
            if (rconPort is > 0) ports.Add(("RCON Port", rconPort.Value));
        }

        var collisions = ports
            .GroupBy(x => x.Port)
            .Where(g => g.Count() > 1)
            .Select(g => $"{string.Join(" and ", g.Select(x => x.Label))} are both set to port {g.Key}.")
            .ToList();

        var state = collisions.Count > 0 ? DiagnosticState.Fail : DiagnosticState.Pass;
        var evidence = collisions.Count > 0
            ? string.Join(" ", collisions)
            : $"Checked {ports.Count} enabled port setting(s); no collisions found.";
        var recommendation = collisions.Count > 0
            ? "Assign each enabled interface (game, REST API, RCON) a distinct port in PalWorldSettings.ini, then restart the server."
            : "No action needed.";

        return
        [
            new DiagnosticFinding(
                Id: "configuration-port-conflicts",
                Category: "Configuration",
                Component: "Port Conflicts",
                State: state,
                Location: palworldConfiguration.ConfigurationPath,
                Evidence: evidence,
                Recommendation: recommendation,
                ActionKind: null,
                ActionSupported: false,
                UnavailableReason: collisions.Count > 0 ? null : "No automatic fix is available for this finding yet.",
                ObservedAt: observedAt,
                Duration: TimeSpan.Zero)
        ];
    }

    // v0.7.91.0 "Crash-Risk Configuration Detection": direct follow-up to a competitive feature
    // review against other Palworld server managers -- multiple independent community sources
    // flag BuildObjectDeteriorationDamageRate = 0 as a known cause of long-running server
    // instability: it disables automatic cleanup of decayed base structures, letting entity/object
    // counts grow unbounded over time. Confirmed live: two of MystTiq's own bundled QoL presets
    // (PalworldConfigPresets, MainWindowViewModel.cs) set this exact value to 0 -- a real,
    // self-inflicted risk worth surfacing in Doctor rather than only in the preset picker's own
    // text. Scoped to just this one well-evidenced, non-default value rather than also flagging
    // things like bEnableInvaderEnemy, which is a vanilla-default-enabled setting community
    // sources describe as a risk only at higher player/base counts, not universally -- flagging a
    // default-on setting unconditionally would be a wall of false positives for ordinary servers.
    private IReadOnlyList<DiagnosticFinding> BuildCrashRiskFindings()
    {
        var snapshot = palworldConfiguration.Load();
        var observedAt = DateTimeOffset.UtcNow;
        if (!snapshot.Exists)
        {
            return
            [
                new DiagnosticFinding(
                    Id: "configuration-crash-risk",
                    Category: "Configuration",
                    Component: "Crash-Risk Settings",
                    State: DiagnosticState.Skipped,
                    Location: palworldConfiguration.ConfigurationPath,
                    Evidence: snapshot.Detail,
                    Recommendation: "PalWorldSettings.ini was not found; crash-risk checking requires it to exist.",
                    ActionKind: null,
                    ActionSupported: false,
                    UnavailableReason: null,
                    ObservedAt: observedAt,
                    Duration: TimeSpan.Zero)
            ];
        }

        var decayRate = GetDouble(snapshot, "BuildObjectDeteriorationDamageRate");
        var isKnownRisk = decayRate == 0;
        var state = isKnownRisk ? DiagnosticState.Warning : DiagnosticState.Pass;
        var evidence = isKnownRisk
            ? "BuildObjectDeteriorationDamageRate is set to 0, disabling automatic cleanup of decayed base structures."
            : decayRate.HasValue
                ? $"BuildObjectDeteriorationDamageRate is {decayRate.Value:0.##}x; structure decay cleanup is active."
                : "BuildObjectDeteriorationDamageRate was not set to a readable numeric value.";
        var recommendation = isKnownRisk
            ? "A value of 0 is a community-reported cause of long-running server instability, since structures and their objects never clean up, letting entity counts grow unbounded. If base decay was disabled specifically to protect builds, consider a small non-zero value (e.g. 0.5-1.0) instead of 0 -- MystTiq's own QoL/Relaxed presets currently set this to 0."
            : "No action needed.";

        return
        [
            new DiagnosticFinding(
                Id: "configuration-crash-risk",
                Category: "Configuration",
                Component: "Crash-Risk Settings",
                State: state,
                Location: palworldConfiguration.ConfigurationPath,
                Evidence: evidence,
                Recommendation: recommendation,
                ActionKind: null,
                ActionSupported: false,
                UnavailableReason: "No automatic fix is available for this finding yet.",
                ObservedAt: observedAt,
                Duration: TimeSpan.Zero)
        ];
    }

    // ---- v0.7.98.0: resource, backup, security and stability findings (rules in DoctorHealthRules) ----

    private static DiagnosticFinding RuleFinding(string id, string category, string component, string location, DoctorRuleResult result) =>
        new(id, category, component, result.State, location, result.Evidence, result.Recommendation,
            ActionKind: null, ActionSupported: false, UnavailableReason: "No automatic fix is available for this finding yet.",
            ObservedAt: DateTimeOffset.UtcNow, Duration: TimeSpan.Zero);

    // The drive whose mount point is the longest prefix of the path, so a folder on D: or a second
    // mount is measured on its own drive. Null when it cannot be determined (for example a network path).
    private static DriveInfo? ResolveDrive(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            DriveInfo? best = null;
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                var root = drive.RootDirectory.FullName;
                if (full.StartsWith(root, comparison) && (best is null || root.Length > best.RootDirectory.FullName.Length)) best = drive;
            }
            return best;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or NotSupportedException) { return null; }
    }

    private long LargestBackupBytes()
    {
        try { return backups?.GetInventory().Items.Select(i => i.SizeBytes).DefaultIfEmpty(0).Max() ?? 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0; }
    }

    // Newest Level.sav that is not one of Palworld's own rolling backups, or null if there is no world yet.
    private DateTimeOffset? WorldLastWriteAt()
    {
        try
        {
            if (!Directory.Exists(paths.SaveRoot)) return null;
            var root = Path.GetFullPath(paths.SaveRoot);
            DateTimeOffset? newest = null;
            foreach (var file in Directory.EnumerateFiles(root, "Level.sav", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file);
                if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(s => s.Equals("backup", StringComparison.OrdinalIgnoreCase))) continue;
                var written = new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero);
                if (newest is null || written > newest) newest = written;
            }
            return newest;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    private IReadOnlyList<DiagnosticFinding> BuildResourceFindings()
    {
        var findings = new List<DiagnosticFinding>();
        var largestBackup = LargestBackupBytes();
        var serverDrive = ResolveDrive(paths.ServerRoot);
        var backupDrive = ResolveDrive(paths.BackupRoot);

        if (serverDrive is not null && backupDrive is not null && serverDrive.RootDirectory.FullName == backupDrive.RootDirectory.FullName)
        {
            findings.Add(RuleFinding("resources-disk-space-server-and-backups", "Resources", "Disk space (server and backups)", serverDrive.Name,
                DoctorHealthRules.DiskSpace($"the server and its backups ({serverDrive.Name})", serverDrive.AvailableFreeSpace, serverDrive.TotalSize, largestBackup, lowDiskPercent?.Invoke())));
        }
        else
        {
            // The Linux Doctor already checks the server drive, so only add it elsewhere.
            if (serverDrive is not null && !OperatingSystem.IsLinux())
                findings.Add(RuleFinding("resources-disk-space-server", "Resources", "Disk space (server)", serverDrive.Name,
                    DoctorHealthRules.DiskSpace($"the server ({serverDrive.Name})", serverDrive.AvailableFreeSpace, serverDrive.TotalSize, 0, lowDiskPercent?.Invoke())));
            if (backupDrive is not null)
                findings.Add(RuleFinding("resources-disk-space-backups", "Resources", "Disk space (backups)", backupDrive.Name,
                    DoctorHealthRules.DiskSpace($"the backups ({backupDrive.Name})", backupDrive.AvailableFreeSpace, backupDrive.TotalSize, largestBackup, lowDiskPercent?.Invoke())));
        }

        var memory = GC.GetGCMemoryInfo();
        var total = memory.TotalAvailableMemoryBytes;
        // MemoryLoadBytes is from the last collection and reads 0 before the first one; treat that as
        // "unknown, assume free" rather than raising a false warning.
        var available = memory.MemoryLoadBytes > 0 ? total - memory.MemoryLoadBytes : total;
        findings.Add(RuleFinding("resources-memory", "Resources", "Memory", "This machine", DoctorHealthRules.Memory(total, available)));
        return findings;
    }

    private IReadOnlyList<DiagnosticFinding> BuildBackupFindings()
    {
        if (backups is null) return [];
        try
        {
            var inventory = backups.GetInventory();
            var latest = inventory.Items.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
            var result = DoctorHealthRules.BackupFreshness(inventory.Items.Count, latest?.CreatedAt, WorldLastWriteAt(), DateTimeOffset.UtcNow);
            var findings = new List<DiagnosticFinding> { RuleFinding("backups-freshness", "Backups", "Backup freshness", inventory.RootPath, result) };
            findings.AddRange(BuildScheduledBackupFindings());
            return findings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [RuleFinding("backups-freshness", "Backups", "Backup freshness", paths.BackupRoot,
                new DoctorRuleResult(DiagnosticState.Skipped, $"Backups could not be read: {ex.Message}", "Check that the backup folder is readable."))];
        }
    }

    // v0.7.103.0: is anything scheduled to make the NEXT backup happen? Offers a one-click nightly rule only when no
    // backup rule exists at all.
    private IReadOnlyList<BackupScheduleRule> ReadBackupScheduleRules()
    {
        if (automation is null) return [];
        var latestRun = automation.ListRuns(200)
            .GroupBy(r => r.RuleId.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.DueUtc).First(), StringComparer.OrdinalIgnoreCase);
        return automation.ListRules()
            .Where(r => r.Action.Kind == AutomationActionKind.CreateBackup)
            .Select(r =>
            {
                latestRun.TryGetValue(r.Id.Value, out var run);
                return new BackupScheduleRule(r.Name, r.Enabled, r.Trigger.Kind, r.Trigger.Interval, r.Trigger.DaysOfWeek,
                    r.Trigger.TimeOfDayUtc, run?.State.ToString(), run?.Detail);
            })
            .ToArray();
    }

    private IReadOnlyList<DiagnosticFinding> BuildScheduledBackupFindings()
    {
        if (automation is null) return [];
        var verdict = DoctorHealthRules.ScheduledBackups(ReadBackupScheduleRules());
        var finding = RuleFinding("backups-schedule", "Backups", "Scheduled backups", "Automation", verdict.Result);
        return [verdict.CanCreateRule
            ? finding with { ActionKind = "create-backup-rule", ActionSupported = true, UnavailableReason = null }
            : finding];
    }

    private IReadOnlyList<DiagnosticFinding> BuildSecurityFindings()
    {
        var snapshot = palworldConfiguration.Load();
        var result = !snapshot.Exists
            ? new DoctorRuleResult(DiagnosticState.Skipped, snapshot.Detail, "PalWorldSettings.ini was not found; the admin password check needs it.")
            : DoctorHealthRules.AdminSecurity(Get(snapshot, "AdminPassword"), GetBool(snapshot, "RESTAPIEnabled") ?? false, GetBool(snapshot, "RCONEnabled") ?? false);
        return [RuleFinding("security-admin-access", "Security", "Admin access", palworldConfiguration.ConfigurationPath, result)];
    }

    private IReadOnlyList<DiagnosticFinding> BuildRecentCrashFindings()
    {
        if (crashTools is null) return [];
        var latest = crashTools.History(1).FirstOrDefault();
        return [RuleFinding("stability-recent-crashes", "Stability", "Recent crashes", "Crash Analyzer", DoctorHealthRules.RecentCrashes(latest, DateTimeOffset.UtcNow))];
    }

    private static bool? GetBool(PalworldConfigurationSnapshot snapshot, string name)
    {
        var value = Get(snapshot, name);
        return bool.TryParse(value?.Trim().Trim('"'), out var parsed) ? parsed : null;
    }

    private static int? GetInt(PalworldConfigurationSnapshot snapshot, string name)
    {
        var value = Get(snapshot, name);
        return int.TryParse(value?.Trim().Trim('"'), out var parsed) && parsed is > 0 and <= 65535 ? parsed : null;
    }

    private static double? GetDouble(PalworldConfigurationSnapshot snapshot, string name)
    {
        var value = Get(snapshot, name);
        return double.TryParse(value?.Trim().Trim('"'), out var parsed) ? parsed : null;
    }

    private static string? Get(PalworldConfigurationSnapshot snapshot, string name) =>
        snapshot.Settings.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

    public async Task<DiagnosticFinding?> RecheckAsync(string id, CancellationToken cancellationToken)
    {
        // Pragmatically scoped: re-runs the whole owning source (both scans are cheap
        // filesystem/config checks) and returns just the one matching finding, rather than
        // decomposing Doctor/Environment into ~25 individually callable single-check methods.
        // Honest "recheck this one thing" from the user's perspective, without that larger refactor.
        var report = await GetUnifiedReportAsync(cancellationToken);
        return report.Findings.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    // canAdminister: creating an automation rule is an Admin action everywhere else, while the fix route itself only
    // needs Operator, so the route says whether the caller is an admin.
    public async Task<HeadlessDiagnosticFixResult> FixAsync(string id, CancellationToken cancellationToken, bool canAdminister = true)
    {
        var report = await GetUnifiedReportAsync(cancellationToken);
        var finding = report.Findings.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (finding is null)
            return HeadlessDiagnosticFixResult.Failure("Finding not found. Refresh the diagnostics report and try again.");

        switch (finding.ActionKind)
        {
            case "create-backup-root":
                try
                {
                    Directory.CreateDirectory(paths.BackupRoot);
                    return new HeadlessDiagnosticFixResult(true, $"Created backup directory: {paths.BackupRoot}");
                }
                catch (Exception ex)
                {
                    return HeadlessDiagnosticFixResult.Failure($"Could not create backup directory: {ex.Message}");
                }

            case "create-backup-rule":
                if (!canAdminister)
                    return HeadlessDiagnosticFixResult.Failure("Creating an automation rule needs the Admin role. Ask an admin, or add the schedule yourself in Automation.");
                if (automation is null)
                    return HeadlessDiagnosticFixResult.Failure("Automation is not available on this server.");
                lock (backupRuleGate)
                {
                    // Re-checked under a lock so two clicks (or two clients) can never create two rules.
                    var existing = ReadBackupScheduleRules();
                    if (existing.Count > 0)
                        return new HeadlessDiagnosticFixResult(true, $"A backup rule already exists (\"{existing[0].Name}\"), so nothing was created.");
                    var rule = automation.CreateRule("Nightly backup (added by Doctor)",
                        new AutomationTrigger { Kind = AutomationTriggerKind.DailyTime, TimeOfDayUtc = new TimeOnly(3, 0), DaysOfWeek = AutomationDayOfWeekMask.All },
                        new AutomationCondition(), new AutomationAction { Kind = AutomationActionKind.CreateBackup });
                    return new HeadlessDiagnosticFixResult(true, $"Created the rule \"{rule.Name}\": a backup every day at 03:00 UTC. You can change or delete it in Automation.");
                }

            case "install-distribution":
                var result = await serverDistribution.UpdateAsync(validate: true, cancellationToken);
                return new HeadlessDiagnosticFixResult(result.Success, result.Message);

            default:
                return HeadlessDiagnosticFixResult.Failure(
                    finding.UnavailableReason ?? "No automatic fix is available for this finding yet.");
        }
    }

    private static DiagnosticsReport BuildReport(IReadOnlyList<DiagnosticFinding> findings, bool serverReady)
    {
        // "Identity" findings (v0.6.7.0) are analytical/advisory -- a Steam ID observed under two
        // player identities, or a freshly-joined player whose save hasn't written yet -- not a
        // server-operational problem, so they must not degrade Overall Health or the Dashboard
        // badge the way a real Health/Setup/Configuration finding does. They still appear in the
        // full findings list so the Doctor page shows them.
        var healthRelevant = findings.Where(f => f.Category != "Identity").ToArray();
        var passed = healthRelevant.Count(f => f.State == DiagnosticState.Pass);
        var warnings = healthRelevant.Count(f => f.State == DiagnosticState.Warning);
        var failures = healthRelevant.Count(f => f.State == DiagnosticState.Fail);

        // Reused, not re-derived: ServerHealthState (MystTiq.Core/Operations/ServerHealthState.cs)
        // has existed since v0.6.0.0 as an explicitly-framed formalization seam for exactly this,
        // but nothing consumed it until now. Unknown covers "not currently running, nothing else
        // wrong" -- the Desktop keeps its own existing neutral "STOPPED" label for that case rather
        // than forcing it into Ready/Degraded/Attention, none of which fit.
        var overall = failures > 0 ? ServerHealthState.Attention
            : warnings > 0 && serverReady ? ServerHealthState.Degraded
            : serverReady ? ServerHealthState.Ready
            : ServerHealthState.Unknown;

        var detail = failures > 0 ? $"{failures} finding(s) need attention."
            : warnings > 0 ? $"{warnings} finding(s) are informational warnings."
            : serverReady ? "All diagnostics passed."
            : "Server is not running; no health issues detected.";

        return new DiagnosticsReport(overall, detail, passed, warnings, failures, DateTimeOffset.UtcNow, findings);
    }

    private static DiagnosticState MapDoctorState(string state) => state switch
    {
        "PASS" => DiagnosticState.Pass,
        "WARNING" => DiagnosticState.Warning,
        "FAIL" => DiagnosticState.Fail,
        _ => DiagnosticState.Unknown
    };

    private static DiagnosticState MapEnvironmentState(string status) => status switch
    {
        "READY" => DiagnosticState.Pass,
        "MISSING" or "DISABLED" or "OPTIONAL" => DiagnosticState.Warning,
        _ => DiagnosticState.Unknown
    };

    private static string Slug(string prefix, string component)
    {
        var cleaned = new string(component.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");
        return $"{prefix}-{cleaned.Trim('-')}";
    }
}

public sealed record HeadlessDiagnosticFixResult(bool Success, string Message)
{
    public static HeadlessDiagnosticFixResult Failure(string message) => new(false, message);
}
