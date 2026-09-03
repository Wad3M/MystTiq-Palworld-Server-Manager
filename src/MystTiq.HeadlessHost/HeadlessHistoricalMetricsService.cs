using System.Text;
using System.Text.Json;

namespace MystTiq.HeadlessHost;

/// <summary>
/// Persists coarse PalServer resource samples for the dashboard history view.
/// Recording is intentionally throttled so the existing aggregate status poll remains
/// the only periodic sampling loop while history survives GUI restarts.
/// </summary>
public sealed class HeadlessHistoricalMetricsService
{
    private static readonly TimeSpan MinimumSampleInterval = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string historyPath;
    private readonly object gate = new();
    private readonly List<HeadlessHistoricalMetricSample> samples = [];
    private DateTimeOffset lastPersistedAt = DateTimeOffset.MinValue;

    public HeadlessHistoricalMetricsService(MystTiq.Core.Services.IServerPathProfile paths)
    {
        var historyRoot = Path.Combine(paths.ManagerRuntimeRoot, "History");
        Directory.CreateDirectory(historyRoot);
        historyPath = Path.Combine(historyRoot, "metrics.json");
        Load();
    }

    public void Record(
        HeadlessRuntimeMetricsSnapshot metrics,
        HeadlessPlayersSnapshot players,
        HeadlessBackupInventory backups,
        HeadlessWorldExplorerSnapshot world,
        MystTiq.Core.Models.ServerLifecycleSnapshot status)
    {
        if (!metrics.Available)
            return;

        lock (gate)
        {
            var now = metrics.ObservedAt;
            if (samples.Count > 0 && now - samples[^1].ObservedAt < MinimumSampleInterval)
                return;

            var uptimeMinutes = status.Phase == MystTiq.Core.Models.ServerLifecyclePhase.Running && status.LastTransitionAt.HasValue
                ? Math.Max(0, (now - status.LastTransitionAt.Value).TotalMinutes)
                : 0;

            samples.Add(new HeadlessHistoricalMetricSample(
                now,
                Math.Clamp(metrics.CpuPercent ?? 0d, 0d, 100d),
                Math.Max(0d, metrics.WorkingSetBytes / 1024d / 1024d),
                Math.Max(0, players.OnlineCount),
                Math.Max(players.Players.Count, world.PlayerSaveCount),
                Math.Max(0, backups.Count),
                Math.Max(0, world.TotalSizeBytes),
                uptimeMinutes));

            var cutoff = now - Retention;
            samples.RemoveAll(sample => sample.ObservedAt < cutoff);

            if (now - lastPersistedAt >= TimeSpan.FromMinutes(1))
                Save();
        }
    }

    public HeadlessHistoricalMetricsSnapshot Snapshot(TimeSpan range, int maximumSamples = 600)
    {
        range = range <= TimeSpan.Zero ? TimeSpan.FromHours(1) : range > Retention ? Retention : range;
        maximumSamples = Math.Clamp(maximumSamples, 30, 1200);

        lock (gate)
        {
            var cutoff = DateTimeOffset.UtcNow - range;
            var selected = samples.Where(sample => sample.ObservedAt >= cutoff).OrderBy(sample => sample.ObservedAt).ToList();
            if (selected.Count == 0 && samples.Count > 0)
                selected.Add(samples[^1]);

            var display = Downsample(selected, maximumSamples);
            return new HeadlessHistoricalMetricsSnapshot(
                display,
                selected.Count == 0 ? 0 : selected.Average(x => x.CpuPercent),
                selected.Count == 0 ? 0 : selected.Max(x => x.CpuPercent),
                selected.Count == 0 ? 0 : selected.Average(x => x.MemoryMb),
                selected.Count == 0 ? 0 : selected.Max(x => x.MemoryMb),
                selected.Count == 0 ? 0 : selected.Max(x => x.OnlinePlayers),
                selected.Count < 2 ? 0 : selected[^1].WorldSizeBytes - selected[0].WorldSizeBytes,
                Trend(selected.Select(x => x.CpuPercent)),
                Trend(selected.Select(x => x.MemoryMb)),
                DateTimeOffset.UtcNow,
                $"{selected.Count} sample(s) in the selected range." );
        }
    }

    private static IReadOnlyList<HeadlessHistoricalMetricSample> Downsample(IReadOnlyList<HeadlessHistoricalMetricSample> source, int max)
    {
        if (source.Count <= max)
            return source.ToArray();

        var result = new List<HeadlessHistoricalMetricSample>(max);
        for (var i = 0; i < max; i++)
        {
            var index = (int)Math.Round(i * (source.Count - 1d) / (max - 1d));
            result.Add(source[index]);
        }
        return result;
    }

    private static string Trend(IEnumerable<double> values)
    {
        var data = values.ToArray();
        if (data.Length < 2) return "→";
        var count = Math.Max(1, data.Length / 4);
        var first = data.Take(count).Average();
        var last = data.TakeLast(count).Average();
        var tolerance = Math.Max(0.01, Math.Abs(first) * 0.03);
        return last > first + tolerance ? "↑" : last < first - tolerance ? "↓" : "→";
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(historyPath)) return;
            var loaded = JsonSerializer.Deserialize<List<HeadlessHistoricalMetricSample>>(File.ReadAllText(historyPath));
            if (loaded is null) return;
            var cutoff = DateTimeOffset.UtcNow - Retention;
            samples.AddRange(loaded.Where(x => x.ObservedAt >= cutoff).OrderBy(x => x.ObservedAt));
        }
        catch
        {
            // Historical telemetry is optional and must never block management startup.
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(historyPath)!);
            var temp = historyPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(samples, JsonOptions), new UTF8Encoding(false));
            File.Move(temp, historyPath, true);
            lastPersistedAt = DateTimeOffset.UtcNow;
        }
        catch
        {
            // A telemetry persistence problem must not affect PalServer lifecycle operations.
        }
    }
}

public sealed record HeadlessHistoricalMetricSample(
    DateTimeOffset ObservedAt,
    double CpuPercent,
    double MemoryMb,
    int OnlinePlayers,
    int KnownPlayers,
    int BackupCount,
    long WorldSizeBytes,
    double UptimeMinutes);

public sealed record HeadlessHistoricalMetricsSnapshot(
    IReadOnlyList<HeadlessHistoricalMetricSample> Samples,
    double AverageCpu,
    double PeakCpu,
    double AverageMemoryMb,
    double PeakMemoryMb,
    int PeakPlayers,
    long WorldGrowthBytes,
    string CpuTrend,
    string MemoryTrend,
    DateTimeOffset ObservedAt,
    string Detail);
