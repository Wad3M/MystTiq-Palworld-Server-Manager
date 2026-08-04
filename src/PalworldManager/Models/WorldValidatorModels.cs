namespace PalworldManager.Models;

public enum WorldValidationSeverity
{
    Healthy,
    Information,
    Warning,
    Critical
}

public sealed class WorldValidationFindingRow
{
    public string Category { get; set; } = "World";
    public string Check { get; set; } = "";
    public WorldValidationSeverity Severity { get; set; }
    public string Status => Severity.ToString();
    public string Message { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string RecommendedAction { get; set; } = "Review";
    public bool RepairAvailable { get; set; }
}

public sealed class WorldValidatorReport
{
    public string WorldId { get; set; } = "";
    public string WorldPath { get; set; } = "";
    public DateTime ScannedUtc { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
    public int PlayerCount { get; set; }
    public int GuildCount { get; set; }
    public int BaseCount { get; set; }
    public List<WorldValidationFindingRow> Findings { get; set; } = [];
    public int CriticalCount => Findings.Count(x => x.Severity == WorldValidationSeverity.Critical);
    public int WarningCount => Findings.Count(x => x.Severity == WorldValidationSeverity.Warning);
    public int HealthyCount => Findings.Count(x => x.Severity == WorldValidationSeverity.Healthy);
    public int RepairableCount => Findings.Count(x => x.RepairAvailable);
    public int HealthScore
    {
        get
        {
            if (Findings.Count == 0) return 0;
            var deductions = (CriticalCount * 25) + (WarningCount * 8);
            return Math.Clamp(100 - deductions, 0, 100);
        }
    }
    public string OverallStatus => CriticalCount > 0 ? "Critical" : WarningCount > 0 ? "Warning" : "Healthy";
}
