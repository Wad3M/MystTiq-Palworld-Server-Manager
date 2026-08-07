namespace PalworldManager.Models;

public sealed class DiagnosticResultRow
{
    public string Category { get; set; } = "";
    public string Check { get; set; } = "";
    public string Status { get; set; } = "Not Run";
    public string Detail { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public int Weight { get; set; } = 1;
    public bool IsPassed => Status.Equals("Passed", StringComparison.OrdinalIgnoreCase);
    public bool IsWarning => Status.Equals("Warning", StringComparison.OrdinalIgnoreCase);
    public bool IsFailed => Status.Equals("Failed", StringComparison.OrdinalIgnoreCase);
}

public sealed class DiagnosticsSnapshot
{
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime CompletedUtc { get; set; }
    public List<DiagnosticResultRow> Results { get; set; } = [];
    public int Passed => Results.Count(x => x.IsPassed);
    public int Warnings => Results.Count(x => x.IsWarning);
    public int Failed => Results.Count(x => x.IsFailed);
    public int Score
    {
        get
        {
            var total = Results.Sum(x => Math.Max(1, x.Weight));
            if (total == 0) return 0;
            var earned = Results.Sum(x => x.IsPassed ? Math.Max(1, x.Weight) : x.IsWarning ? Math.Max(1, x.Weight) * 0.5 : 0);
            return (int)Math.Round(earned / total * 100d, MidpointRounding.AwayFromZero);
        }
    }
    public string OverallStatus => Failed > 0 ? "Attention Required" : Warnings > 0 ? "Healthy with Warnings" : "Healthy";
}
