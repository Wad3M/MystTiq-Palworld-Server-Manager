using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class WorldCandidateDto
{
    [JsonPropertyName("worldId")] public string WorldId { get; init; } = string.Empty;
    [JsonPropertyName("worldPath")] public string WorldPath { get; init; } = string.Empty;
    [JsonPropertyName("levelSaveExists")] public bool LevelSaveExists { get; init; }
    [JsonPropertyName("levelSizeBytes")] public long LevelSizeBytes { get; init; }
    [JsonPropertyName("levelLastWriteUtc")] public DateTimeOffset LevelLastWriteUtc { get; init; }
    [JsonPropertyName("playersDirectoryExists")] public bool PlayersDirectoryExists { get; init; }

    public string Nickname => string.IsNullOrWhiteSpace(WorldId) ? "World" : $"World {WorldId[..Math.Min(8, WorldId.Length)]}";
    public string UpdatedText => LevelLastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string LevelSizeText => $"{LevelSizeBytes / 1024d / 1024d:F2} MB";
}

public sealed class WorldFileDto
{
    [JsonPropertyName("relativePath")] public string RelativePath { get; init; } = string.Empty;
    [JsonPropertyName("category")] public string Category { get; init; } = string.Empty;
    [JsonPropertyName("sizeBytes")] public long SizeBytes { get; init; }
    [JsonPropertyName("lastWriteUtc")] public DateTimeOffset LastWriteUtc { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;

    public string SizeText => SizeBytes < 1024 * 1024
        ? $"{SizeBytes / 1024d:F1} KB"
        : $"{SizeBytes / 1024d / 1024d:F2} MB";
    public string UpdatedText => LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}

public sealed class WorldExplorerSnapshotDto
{
    [JsonPropertyName("available")] public bool Available { get; init; }
    [JsonPropertyName("saveRoot")] public string SaveRoot { get; init; } = string.Empty;
    [JsonPropertyName("activeWorldId")] public string? ActiveWorldId { get; init; }
    [JsonPropertyName("activeWorldPath")] public string? ActiveWorldPath { get; init; }
    [JsonPropertyName("worldCount")] public int WorldCount { get; init; }
    [JsonPropertyName("fileCount")] public int FileCount { get; init; }
    [JsonPropertyName("playerSaveCount")] public int PlayerSaveCount { get; init; }
    [JsonPropertyName("totalSizeBytes")] public long TotalSizeBytes { get; init; }
    [JsonPropertyName("lastWorldSaveUtc")] public DateTimeOffset? LastWorldSaveUtc { get; init; }
    [JsonPropertyName("worldDayNumber")] public long? WorldDayNumber { get; init; }
    [JsonPropertyName("worldTimeText")] public string? WorldTimeText { get; init; }
    [JsonPropertyName("worlds")] public IReadOnlyList<WorldCandidateDto> Worlds { get; init; } = [];
    [JsonPropertyName("files")] public IReadOnlyList<WorldFileDto> Files { get; init; } = [];
    [JsonPropertyName("statistics")] public WorldStatisticsDto Statistics { get; init; } = new();
    [JsonPropertyName("integrity")] public WorldIntegrityDto Integrity { get; init; } = new();
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}

public sealed class WorldStatisticsDto
{
    [JsonPropertyName("saveDataFiles")] public int SaveDataFiles { get; init; }
    [JsonPropertyName("playerFiles")] public int PlayerFiles { get; init; }
    [JsonPropertyName("diagnosticFiles")] public int DiagnosticFiles { get; init; }
    [JsonPropertyName("otherFiles")] public int OtherFiles { get; init; }
    [JsonPropertyName("emptyFiles")] public int EmptyFiles { get; init; }
    [JsonPropertyName("oldestFileUtc")] public DateTimeOffset? OldestFileUtc { get; init; }
    [JsonPropertyName("newestFileUtc")] public DateTimeOffset? NewestFileUtc { get; init; }
    public string AgeRangeText => OldestFileUtc is null ? "No files" : $"{OldestFileUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm} → {NewestFileUtc?.ToLocalTime():yyyy-MM-dd HH:mm}";
}

public sealed class WorldIntegrityDto
{
    [JsonPropertyName("state")] public string State { get; init; } = "Unknown";
    [JsonPropertyName("findings")] public IReadOnlyList<string> Findings { get; init; } = [];
    [JsonPropertyName("requiredFilesPresent")] public bool RequiredFilesPresent { get; init; }
}
