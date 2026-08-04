namespace PalworldManager.Models;

public sealed class PlayerRecoveryRow
{
    public string PlayerGuid { get; set; } = "";
    public string DisplayName { get; set; } = "Unknown player";
    public string PlatformId { get; set; } = "";
    public string SavePath { get; set; } = "";
    public string CompanionPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime LastWriteUtc { get; set; }
    public bool IsHostCandidate { get; set; }
    public bool HasCompanion { get; set; }
    public bool HasHistoryMatch { get; set; }
    public string Status { get; set; } = "Unmapped";
    public string SizeDisplay => SizeBytes < 1024 ? $"{SizeBytes} B" : SizeBytes < 1024 * 1024 ? $"{SizeBytes / 1024d:F1} KB" : $"{SizeBytes / 1024d / 1024d:F2} MB";
    public string LastWriteDisplay => LastWriteUtc == default ? "—" : LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string HostDisplay => IsHostCandidate ? "Host candidate" : "Standard";
    public string CompanionDisplay => HasCompanion ? "Present" : "None";
}

public sealed class PlayerRecoveryPlan
{
    public string PlanId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string WorldPath { get; set; } = "";
    public string SourcePlayerGuid { get; set; } = "";
    public string DestinationPlayerGuid { get; set; } = "";
    public string SourceSavePath { get; set; } = "";
    public string DestinationSavePath { get; set; } = "";
    public string MappingMethod { get; set; } = "Manual";
    public bool SourceIsHostCandidate { get; set; }
    public bool DestinationExists { get; set; }
    public bool CodecAvailable { get; set; }
    public bool RequiresLevelSaveRewrite { get; set; } = true;
    public List<string> ValidationMessages { get; set; } = [];
}

public sealed class PlayerRecoverySummary
{
    public string WorldPath { get; set; } = "";
    public List<PlayerRecoveryRow> Players { get; set; } = [];
    public int HostCandidateCount => Players.Count(x => x.IsHostCandidate);
    public int MappedCount => Players.Count(x => x.HasHistoryMatch);
    public int UnmappedCount => Players.Count(x => !x.HasHistoryMatch);
    public long TotalBytes => Players.Sum(x => x.SizeBytes);
    public string TotalSizeDisplay => TotalBytes < 1024 * 1024 ? $"{TotalBytes / 1024d:F1} KB" : $"{TotalBytes / 1024d / 1024d:F2} MB";
}
