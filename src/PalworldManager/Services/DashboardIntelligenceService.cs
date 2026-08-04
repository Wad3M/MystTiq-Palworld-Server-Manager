using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class DashboardIntelligenceService
{
    public DashboardIntelligenceSnapshot Build(
        int onlinePlayers, int knownPlayers, int playerSaveFiles,
        int guilds, int orphanedGuilds, int installedMods, int healthyMods,
        bool hasRecentBackup, bool worldAvailable, ServerLifecycleState lifecycle)
    {
        var signals = new List<DashboardHealthSignal>();
        Add(signals, "Operational state", LifecycleLabel(lifecycle), LifecycleDetail(lifecycle),
            lifecycle is ServerLifecycleState.Hung or ServerLifecycleState.Crashed);
        Add(signals, "World", worldAvailable ? "Available" : "Unavailable",
            worldAvailable ? "An active world save was detected." : "No active world save is currently available.", !worldAvailable);
        Add(signals, "Backup", hasRecentBackup ? "Protected" : "Attention",
            hasRecentBackup ? "A backup was created within the last 24 hours." : "No verified recent backup was found.", !hasRecentBackup);
        Add(signals, "Guild ownership", orphanedGuilds == 0 ? "Healthy" : "Attention",
            orphanedGuilds == 0 ? "No orphaned guilds were detected." : $"{orphanedGuilds} orphaned guild(s) require review.", orphanedGuilds > 0);
        var unhealthyMods = Math.Max(0, installedMods - healthyMods);
        Add(signals, "MOD platform", unhealthyMods == 0 ? "Healthy" : "Attention",
            installedMods == 0 ? "Vanilla server profile." : $"{healthyMods} of {installedMods} installed MOD(s) are healthy.", unhealthyMods > 0);

        // Stopped and not-installed are operational states, not health defects. Only abnormal
        // lifecycle states reduce the health score. This prevents planned downtime from
        // presenting as a degraded server.
        var warnings = signals.Count(x => x.IsWarning);
        warnings += Math.Max(0, unhealthyMods - 1);
        var score = Math.Clamp(100 - warnings * 10, 0, 100);
        var label = lifecycle switch
        {
            ServerLifecycleState.Hung or ServerLifecycleState.Crashed => "Critical",
            _ when warnings == 0 => lifecycle is ServerLifecycleState.Stopped ? "Idle" : "Excellent",
            _ when score >= 80 => "Good",
            _ when score >= 60 => "Warning",
            _ => "Critical"
        };

        return new DashboardIntelligenceSnapshot
        {
            OnlinePlayers = onlinePlayers, KnownPlayers = knownPlayers, PlayerSaveFiles = playerSaveFiles,
            Guilds = guilds, OrphanedGuilds = orphanedGuilds, InstalledMods = installedMods,
            HealthyMods = healthyMods, WarningCount = warnings, HealthScore = score,
            HealthLabel = label, OperationalState = LifecycleLabel(lifecycle), Signals = signals
        };
    }

    private static void Add(List<DashboardHealthSignal> signals, string name, string status, string detail, bool warning) =>
        signals.Add(new DashboardHealthSignal { Name = name, Status = status, Detail = detail, IsWarning = warning });

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
