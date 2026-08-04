namespace PalworldManager.Models;

public enum PalworldSaveContainerKind { Unknown, PlM1, Gvas, Json }
public sealed class PalworldSaveHeader
{
    public string Path { get; set; } = "";
    public PalworldSaveContainerKind Kind { get; set; }
    public long Length { get; set; }
    public string MagicHex { get; set; } = "";
    public string MagicText { get; set; } = "";
    public bool AppearsCompressed { get; set; }
    public List<string> Warnings { get; set; } = [];
}
public sealed class RealSaveDecodeResult
{
    public PalworldSaveHeader Header { get; set; } = new();
    public SaveCodecResult Codec { get; set; } = new();
    public string JsonPath => Codec.JsonPath;
    public bool Success => Codec.Success;
}
public sealed class WorldScanStatistics
{
    public int JsonObjectsVisited { get; set; }
    public int JsonArraysVisited { get; set; }
    public int CandidatePlayers { get; set; }
    public int CandidateGuilds { get; set; }
    public int CandidateBases { get; set; }
    public int CandidatePalboxes { get; set; }
}
public sealed class LiveWorldScanResult
{
    public WorldSnapshot Snapshot { get; set; } = new();
    public WorldScanStatistics Statistics { get; set; } = new();
    public List<string> Diagnostics { get; set; } = [];
}
public sealed class BaseDiscoveryResult
{
    public IReadOnlyList<BaseDiscoveryRecord> Records { get; set; } = [];
    public IReadOnlyList<string> Diagnostics { get; set; } = [];
    public IReadOnlyList<string> Rejections { get; set; } = [];
}
public sealed class BaseDiscoveryRecord
{
    public string BaseId { get; set; } = "";
    public string GuildId { get; set; } = "";
    public string PalboxId { get; set; } = "";
    public string Name { get; set; } = "";
    public string InternalName { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public string SourcePath { get; set; } = "";
}
public sealed class GuildDiscoveryResult
{
    public IReadOnlyList<GuildDiscoveryRecord> Records { get; set; } = [];
    public IReadOnlyList<string> Diagnostics { get; set; } = [];
    public IReadOnlyList<string> CandidatePaths { get; set; } = [];
    public IReadOnlyList<string> Rejections { get; set; } = [];
}
public sealed class GuildDiscoveryRecord
{
    public string GuildId { get; set; } = "";
    public string Name { get; set; } = "";
    public string LeaderGuid { get; set; } = "";
    public List<string> MemberGuids { get; set; } = [];
    public Dictionary<string, string> MemberNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> BaseIds { get; set; } = [];
    public string SourcePath { get; set; } = "";
    public int EvidenceScore { get; set; }
    public bool HasExplicitGuildType { get; set; }
}
public sealed class RepairPreviewItem
{
    public string Category { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Field { get; set; } = "";
    public string Before { get; set; } = "";
    public string After { get; set; } = "";
    public string Reason { get; set; } = "";
    public bool IsDestructive { get; set; }
}
public sealed class RepairPreview
{
    public List<RepairPreviewItem> Items { get; set; } = [];
    public List<WorldIssue> BlockingIssues { get; set; } = [];
    public bool CanApply => BlockingIssues.Count == 0;
}
public sealed class ProductionImportResult
{
    public Guid TransactionId { get; set; }
    public string OutputWorldPath { get; set; } = "";
    public WorldSnapshot Snapshot { get; set; } = new();
    public RepairPreview Preview { get; set; } = new();
    public WorldValidationReport Validation { get; set; } = new();
    public List<string> Diagnostics { get; set; } = [];
    public bool ReadyForActivation => Validation.IsValid && Preview.CanApply;
}
