namespace PalworldManager.Models;

public sealed class HistoricalMetricSample
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public double CpuPercent { get; set; }
    public double MemoryMb { get; set; }
    public int OnlinePlayers { get; set; }
    public int KnownPlayers { get; set; }
    public int BackupCount { get; set; }
    public long WorldSizeBytes { get; set; }
    public double UptimeMinutes { get; set; }
}

public sealed class HistoricalAnalyticsSnapshot
{
    public IReadOnlyList<HistoricalMetricSample> Samples { get; init; } = [];
    public HistoricalMetricSample? Latest => Samples.LastOrDefault();
    public double AverageCpu { get; init; }
    public double PeakCpu { get; init; }
    public double AverageMemoryMb { get; init; }
    public double PeakMemoryMb { get; init; }
    public int PeakPlayers { get; init; }
    public long WorldGrowthBytes { get; init; }
    public string CpuTrend { get; init; } = "→";
    public string MemoryTrend { get; init; } = "→";
    public string PlayerTrend { get; init; } = "→";
    public string WorldTrend { get; init; } = "→";
}
