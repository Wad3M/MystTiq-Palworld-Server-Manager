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

    public HeadlessDiagnosticsService(
        HeadlessDoctorService doctor,
        HeadlessEnvironmentChecklistService environment,
        IServerLifecycleService lifecycle,
        IServerPathProfile paths,
        HeadlessServerDistributionService serverDistribution,
        PalworldSettingsConfigurationService palworldConfiguration,
        HeadlessPlayerRegistryService playerRegistry,
        HeadlessPlayerGuildExplorerService playerGuildExplorer)
    {
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

    public async Task<HeadlessDiagnosticFixResult> FixAsync(string id, CancellationToken cancellationToken)
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
