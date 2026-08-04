namespace PalworldManager.Models;

public sealed class DashboardHealthSignal
{
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public bool IsWarning { get; init; }
}

public sealed class DashboardIntelligenceSnapshot
{
    public int OnlinePlayers { get; init; }
    public int KnownPlayers { get; init; }
    public int PlayerSaveFiles { get; init; }
    public int Guilds { get; init; }
    public int OrphanedGuilds { get; init; }
    public int InstalledMods { get; init; }
    public int HealthyMods { get; init; }
    public int WarningCount { get; init; }
    public int HealthScore { get; init; }
    public string HealthLabel { get; init; } = "Checking";
    public string OperationalState { get; init; } = "Unknown";
    public IReadOnlyList<DashboardHealthSignal> Signals { get; init; } = Array.Empty<DashboardHealthSignal>();
    public DateTime RefreshedUtc { get; init; } = DateTime.UtcNow;
}
