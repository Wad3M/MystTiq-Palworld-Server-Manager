using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class PlayerSnapshotDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("userId")] public string UserId { get; init; } = string.Empty;
    [JsonPropertyName("steamId")] public string SteamId { get; init; } = string.Empty;
    [JsonPropertyName("playerId")] public string PlayerId { get; init; } = string.Empty;
    [JsonPropertyName("ip")] public string Ip { get; init; } = string.Empty;
    [JsonPropertyName("ping")] public string Ping { get; init; } = string.Empty;
    [JsonPropertyName("platform")] public string Platform { get; init; } = string.Empty;
    [JsonPropertyName("level")] public string Level { get; init; } = string.Empty;
    [JsonPropertyName("buildingCount")] public string BuildingCount { get; init; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? UserId : Name;
    public string IdentityText =>
        !string.IsNullOrWhiteSpace(SteamId) ? $"Steam {SteamId}" :
        !string.IsNullOrWhiteSpace(UserId) ? UserId :
        PlayerId;
}

public sealed class PlayersSnapshotDto
{
    [JsonPropertyName("available")] public bool Available { get; init; }
    [JsonPropertyName("onlineCount")] public int OnlineCount { get; init; }
    [JsonPropertyName("players")] public IReadOnlyList<PlayerSnapshotDto> Players { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}

public sealed class LogTailSnapshotDto
{
    [JsonPropertyName("available")] public bool Available { get; init; }
    [JsonPropertyName("fileName")] public string? FileName { get; init; }
    [JsonPropertyName("lines")] public IReadOnlyList<string> Lines { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}

public sealed class RuntimeMetricsSnapshotDto
{
    [JsonPropertyName("available")] public bool Available { get; init; }
    [JsonPropertyName("processId")] public int? ProcessId { get; init; }
    [JsonPropertyName("cpuPercent")] public double? CpuPercent { get; init; }
    [JsonPropertyName("workingSetBytes")] public long WorkingSetBytes { get; init; }
    [JsonPropertyName("threadCount")] public int ThreadCount { get; init; }
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}

public sealed record MetricHistoryPoint(
    DateTimeOffset ObservedAt,
    double? CpuPercent,
    double WorkingSetMb,
    int ThreadCount)
{
    public string DisplayText =>
        $"{ObservedAt.ToLocalTime():HH:mm:ss}  CPU {(CpuPercent.HasValue ? $"{CpuPercent.Value:F1}%" : "baseline")}  RAM {WorkingSetMb:F1} MB  Threads {ThreadCount}";
    public double CpuGraphHeight => Math.Clamp((CpuPercent ?? 0d) * 0.55d + 2d, 2d, 57d);
    public string CpuToolTip => $"{ObservedAt.ToLocalTime():HH:mm:ss} — CPU {(CpuPercent.HasValue ? $"{CpuPercent.Value:F1}%" : "baseline")}";
}

public sealed class HistoricalMetricPointDto
{
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("cpuPercent")] public double CpuPercent { get; init; }
    [JsonPropertyName("memoryMb")] public double MemoryMb { get; init; }
    [JsonPropertyName("onlinePlayers")] public int OnlinePlayers { get; init; }
    [JsonPropertyName("knownPlayers")] public int KnownPlayers { get; init; }
    [JsonPropertyName("backupCount")] public int BackupCount { get; init; }
    [JsonPropertyName("worldSizeBytes")] public long WorldSizeBytes { get; init; }
    [JsonPropertyName("uptimeMinutes")] public double UptimeMinutes { get; init; }
}

public sealed class HistoricalMetricsSnapshotDto
{
    [JsonPropertyName("samples")] public IReadOnlyList<HistoricalMetricPointDto> Samples { get; init; } = [];
    [JsonPropertyName("averageCpu")] public double AverageCpu { get; init; }
    [JsonPropertyName("peakCpu")] public double PeakCpu { get; init; }
    [JsonPropertyName("averageMemoryMb")] public double AverageMemoryMb { get; init; }
    [JsonPropertyName("peakMemoryMb")] public double PeakMemoryMb { get; init; }
    [JsonPropertyName("peakPlayers")] public int PeakPlayers { get; init; }
    [JsonPropertyName("worldGrowthBytes")] public long WorldGrowthBytes { get; init; }
    [JsonPropertyName("cpuTrend")] public string CpuTrend { get; init; } = "→";
    [JsonPropertyName("memoryTrend")] public string MemoryTrend { get; init; } = "→";
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}
