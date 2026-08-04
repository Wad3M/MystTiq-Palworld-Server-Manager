using PalworldManager.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PalworldManager.Services;

public sealed class HistoricalAnalyticsService
{
    private readonly string historyPath;
    private readonly object sync = new();
    private readonly List<HistoricalMetricSample> samples = [];
    private DateTime lastPersistedUtc = DateTime.MinValue;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public HistoricalAnalyticsService(string root, AppSettings settings)
    {
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(settings.ServerExe ?? string.Empty)))[..12];
        var folder = Path.Combine(root, "History", identity);
        Directory.CreateDirectory(folder);
        historyPath = Path.Combine(folder, "metrics.json");
        Load();
    }

    public void Record(ServerResourceUsage resources, int onlinePlayers, int knownPlayers, int backupCount,
        string? worldPath, TimeSpan uptime)
    {
        lock (sync)
        {
            var now = DateTime.UtcNow;
            if (samples.Count > 0 && now - samples[^1].TimestampUtc < TimeSpan.FromSeconds(50))
                return;

            samples.Add(new HistoricalMetricSample
            {
                TimestampUtc = now,
                CpuPercent = Math.Round(resources.CpuPercent, 2),
                MemoryMb = Math.Round(resources.MemoryMb, 2),
                OnlinePlayers = Math.Max(0, onlinePlayers),
                KnownPlayers = Math.Max(0, knownPlayers),
                BackupCount = Math.Max(0, backupCount),
                WorldSizeBytes = GetDirectorySize(worldPath),
                UptimeMinutes = Math.Max(0, uptime.TotalMinutes)
            });

            var cutoff = now.AddDays(-30);
            samples.RemoveAll(x => x.TimestampUtc < cutoff);
            if (now - lastPersistedUtc >= TimeSpan.FromMinutes(1))
                Save();
        }
    }

    public HistoricalAnalyticsSnapshot Snapshot(TimeSpan range)
    {
        lock (sync)
        {
            var cutoff = DateTime.UtcNow - range;
            var selected = samples.Where(x => x.TimestampUtc >= cutoff).OrderBy(x => x.TimestampUtc).ToList();
            if (selected.Count == 0 && samples.Count > 0)
                selected.Add(samples[^1]);

            return new HistoricalAnalyticsSnapshot
            {
                Samples = selected,
                AverageCpu = selected.Count == 0 ? 0 : selected.Average(x => x.CpuPercent),
                PeakCpu = selected.Count == 0 ? 0 : selected.Max(x => x.CpuPercent),
                AverageMemoryMb = selected.Count == 0 ? 0 : selected.Average(x => x.MemoryMb),
                PeakMemoryMb = selected.Count == 0 ? 0 : selected.Max(x => x.MemoryMb),
                PeakPlayers = selected.Count == 0 ? 0 : selected.Max(x => x.OnlinePlayers),
                WorldGrowthBytes = selected.Count < 2 ? 0 : selected[^1].WorldSizeBytes - selected[0].WorldSizeBytes,
                CpuTrend = Trend(selected.Select(x => x.CpuPercent)),
                MemoryTrend = Trend(selected.Select(x => x.MemoryMb)),
                PlayerTrend = Trend(selected.Select(x => (double)x.OnlinePlayers)),
                WorldTrend = Trend(selected.Select(x => (double)x.WorldSizeBytes))
            };
        }
    }

    public string ExportCsv(TimeSpan range)
    {
        var snapshot = Snapshot(range);
        var exportFolder = Path.Combine(Path.GetDirectoryName(historyPath)!, "Exports");
        Directory.CreateDirectory(exportFolder);
        var path = Path.Combine(exportFolder, $"MystTiq-History-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        var lines = new List<string> { "TimestampLocal,CPUPercent,MemoryMB,OnlinePlayers,KnownPlayers,BackupCount,WorldSizeBytes,UptimeMinutes" };
        lines.AddRange(snapshot.Samples.Select(x => string.Join(',',
            x.TimestampUtc.ToLocalTime().ToString("O"),
            x.CpuPercent.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            x.MemoryMb.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            x.OnlinePlayers, x.KnownPlayers, x.BackupCount, x.WorldSizeBytes,
            x.UptimeMinutes.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))));
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
        return path;
    }

    public void Flush()
    {
        lock (sync) Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(historyPath)) return;
            var loaded = JsonSerializer.Deserialize<List<HistoricalMetricSample>>(File.ReadAllText(historyPath));
            if (loaded is not null) samples.AddRange(loaded.OrderBy(x => x.TimestampUtc));
        }
        catch
        {
            // A damaged history file must never prevent MystTiq from starting.
        }
    }

    private void Save()
    {
        var temp = historyPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(samples, JsonOptions), new UTF8Encoding(false));
        File.Move(temp, historyPath, true);
        lastPersistedUtc = DateTime.UtcNow;
    }

    private static string Trend(IEnumerable<double> values)
    {
        var data = values.ToArray();
        if (data.Length < 2) return "→";
        var firstCount = Math.Max(1, data.Length / 4);
        var first = data.Take(firstCount).Average();
        var last = data.TakeLast(firstCount).Average();
        var tolerance = Math.Max(0.01, Math.Abs(first) * 0.03);
        return last > first + tolerance ? "↑" : last < first - tolerance ? "↓" : "→";
    }

    private static long GetDirectorySize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return 0;
        try
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Select(file => { try { return new FileInfo(file).Length; } catch { return 0L; } })
                .Sum();
        }
        catch { return 0; }
    }
}
