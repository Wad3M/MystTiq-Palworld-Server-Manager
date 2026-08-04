using System.Linq;

namespace PalworldManager.Models;

public enum RepairCenterSeverity { Information, Recommendation, Warning, Critical }
public enum RepairCenterState { Detected, Selected, BackedUp, Previewed, Ready, Completed, Failed, Skipped }

public sealed class RepairCenterItem
{
    public bool Selected { get; set; }
    public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
    public RepairCenterSeverity Severity { get; set; }
    public string Category { get; set; } = "World";
    public string Action { get; set; } = "Review";
    public string Target { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Risk { get; set; } = "Low";
    public RepairCenterState State { get; set; } = RepairCenterState.Detected;
    public bool RequiresDecodedSave { get; set; }
    public bool RequiresServerStopped { get; set; } = true;
    public string StateDisplay => State.ToString();
}

public sealed class RepairCenterSession
{
    public string SessionId { get; set; } = System.Guid.NewGuid().ToString("N");
    public string WorldPath { get; set; } = "";
    public string WorldId { get; set; } = "";
    public System.DateTime CreatedUtc { get; set; } = System.DateTime.UtcNow;
    public string SourceHash { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public string PreviewPath { get; set; } = "";
    public System.Collections.Generic.List<RepairCenterItem> Items { get; set; } = [];
    public int SelectedCount => Items.Count(x => x.Selected);
    public int CriticalCount => Items.Count(x => x.Severity == RepairCenterSeverity.Critical);
    public int WarningCount => Items.Count(x => x.Severity == RepairCenterSeverity.Warning);
    public int RecommendationCount => Items.Count(x => x.Severity == RepairCenterSeverity.Recommendation);
    public bool HasBackup => !string.IsNullOrWhiteSpace(BackupPath) && System.IO.Directory.Exists(BackupPath);
    public bool CanPrepare => SelectedCount > 0 && HasBackup;
}

public sealed class RepairCenterLogRow
{
    public System.DateTime TimestampUtc { get; set; } = System.DateTime.UtcNow;
    public string Stage { get; set; } = "";
    public string Message { get; set; } = "";
    public string Result { get; set; } = "";
    public string TimestampDisplay => TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
