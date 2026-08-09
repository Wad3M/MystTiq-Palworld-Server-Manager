namespace PalworldManager.Models;

public enum DashboardHealthSeverity
{
    Healthy,
    Informational,
    Warning,
    Error,
    Critical
}

public sealed class ModPlatformHealthSnapshot
{
    public int Installed { get; init; }
    public int Healthy { get; init; }
    public int Disabled { get; init; }
    public int RuntimeUnverified { get; init; }
    public int ActiveOrUnknown { get; init; }
    public int ConfirmedIssueCount { get; init; }
    public int Failed { get; init; }
    public int Missing { get; init; }
    public int Misconfigured { get; init; }
    public int Attention { get; init; }
    public int RuntimeErrors { get; init; }
    public int Conflicts { get; init; }
    public int MissingDependencies { get; init; }
    public DashboardHealthSeverity Severity { get; init; } = DashboardHealthSeverity.Healthy;
    public string Summary { get; init; } = "Vanilla server profile.";
    public string HealthLine { get; init; } = "Mods: None";
    public bool HasConfirmedIssues => ConfirmedIssueCount > 0;
}

public sealed class DashboardHealthSignal
{
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public DashboardHealthSeverity Severity { get; init; } = DashboardHealthSeverity.Healthy;
    public bool IsWarning => Severity is DashboardHealthSeverity.Warning or DashboardHealthSeverity.Error or DashboardHealthSeverity.Critical;
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
    public ModPlatformHealthSnapshot ModPlatform { get; init; } = new();
    public IReadOnlyList<DashboardHealthSignal> Signals { get; init; } = Array.Empty<DashboardHealthSignal>();
    public DateTime RefreshedUtc { get; init; } = DateTime.UtcNow;
}
