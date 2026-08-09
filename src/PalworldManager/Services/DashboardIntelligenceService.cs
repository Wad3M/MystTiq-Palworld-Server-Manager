using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class DashboardIntelligenceService
{
    public DashboardIntelligenceSnapshot Build(
        int onlinePlayers, int knownPlayers, int playerSaveFiles,
        int guilds, int orphanedGuilds, ModPlatformHealthSnapshot modPlatform,
        bool hasRecentBackup, bool worldAvailable, ServerLifecycleState lifecycle)
    {
        ArgumentNullException.ThrowIfNull(modPlatform);

        var signals = new List<DashboardHealthSignal>();
        Add(signals, "Operational state", LifecycleLabel(lifecycle), LifecycleDetail(lifecycle),
            lifecycle is ServerLifecycleState.Hung or ServerLifecycleState.Crashed
                ? DashboardHealthSeverity.Critical
                : DashboardHealthSeverity.Healthy);
        Add(signals, "World", worldAvailable ? "Available" : "Unavailable",
            worldAvailable ? "An active world save was detected." : "No active world save is currently available.",
            worldAvailable ? DashboardHealthSeverity.Healthy : DashboardHealthSeverity.Warning);
        Add(signals, "Backup", hasRecentBackup ? "Protected" : "Attention",
            hasRecentBackup ? "A backup was created within the last 24 hours." : "No verified recent backup was found.",
            hasRecentBackup ? DashboardHealthSeverity.Healthy : DashboardHealthSeverity.Warning);
        Add(signals, "Guild ownership", orphanedGuilds == 0 ? "Healthy" : "Attention",
            orphanedGuilds == 0 ? "No orphaned guilds were detected." : $"{orphanedGuilds} orphaned guild(s) require review.",
            orphanedGuilds == 0 ? DashboardHealthSeverity.Healthy : DashboardHealthSeverity.Warning);

        var modStatus = modPlatform.Severity switch
        {
            DashboardHealthSeverity.Error or DashboardHealthSeverity.Critical => "Attention",
            DashboardHealthSeverity.Informational => "Informational",
            _ => "Healthy"
        };
        Add(signals, "MOD platform", modStatus, modPlatform.Summary, modPlatform.Severity);

        // Overall Health is now severity-driven. Informational states (for example
        // Disabled or Active / Unverified MODs) never deduct from the server score.
        // Only confirmed warning/error/critical conditions reduce health.
        var score = 100;
        foreach (var signal in signals)
        {
            score -= signal.Severity switch
            {
                DashboardHealthSeverity.Warning => 10,
                DashboardHealthSeverity.Error => 10,
                DashboardHealthSeverity.Critical => 25,
                _ => 0
            };
        }

        // Each additional enabled MOD with a confirmed failure/error is a distinct
        // health issue, but informational MOD state never creates a deduction.
        if (modPlatform.ConfirmedIssueCount > 1)
            score -= (modPlatform.ConfirmedIssueCount - 1) * 10;

        score = Math.Clamp(score, 0, 100);
        var confirmedIssues = signals.Count(x => x.IsWarning) + Math.Max(0, modPlatform.ConfirmedIssueCount - 1);
        var label = lifecycle switch
        {
            ServerLifecycleState.Hung or ServerLifecycleState.Crashed => "Critical",
            _ when confirmedIssues == 0 => lifecycle is ServerLifecycleState.Stopped ? "Idle" : "Excellent",
            _ when score >= 80 => "Good",
            _ when score >= 60 => "Warning",
            _ => "Critical"
        };

        return new DashboardIntelligenceSnapshot
        {
            OnlinePlayers = onlinePlayers,
            KnownPlayers = knownPlayers,
            PlayerSaveFiles = playerSaveFiles,
            Guilds = guilds,
            OrphanedGuilds = orphanedGuilds,
            InstalledMods = modPlatform.Installed,
            HealthyMods = modPlatform.Healthy,
            WarningCount = confirmedIssues,
            HealthScore = score,
            HealthLabel = label,
            OperationalState = LifecycleLabel(lifecycle),
            ModPlatform = modPlatform,
            Signals = signals
        };
    }

    private static void Add(
        List<DashboardHealthSignal> signals,
        string name,
        string status,
        string detail,
        DashboardHealthSeverity severity) =>
        signals.Add(new DashboardHealthSignal
        {
            Name = name,
            Status = status,
            Detail = detail,
            Severity = severity
        });

    private static string LifecycleLabel(ServerLifecycleState lifecycle) => lifecycle switch
    {
        ServerLifecycleState.Running => "Running",
        ServerLifecycleState.Starting => "Starting",
        ServerLifecycleState.Stopping => "Stopping",
        ServerLifecycleState.Stopped => "Stopped (intentional)",
        ServerLifecycleState.Hung => "Unresponsive",
        ServerLifecycleState.Crashed => "Crashed",
        ServerLifecycleState.NotInstalled => "Not installed",
        _ => "Unknown"
    };

    private static string LifecycleDetail(ServerLifecycleState lifecycle) => lifecycle switch
    {
        ServerLifecycleState.Stopped => "The server is intentionally stopped; planned downtime does not reduce health.",
        ServerLifecycleState.NotInstalled => "Server components are not installed; install readiness is tracked separately.",
        ServerLifecycleState.Running => "The dedicated server process is running.",
        ServerLifecycleState.Starting => "The server is progressing through startup.",
        ServerLifecycleState.Stopping => "A managed shutdown is in progress.",
        ServerLifecycleState.Hung => "The server process is present but is not responding normally.",
        ServerLifecycleState.Crashed => "An abnormal server termination was detected.",
        _ => "The current operational state could not be determined."
    };
}
