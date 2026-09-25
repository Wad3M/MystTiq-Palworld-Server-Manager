using System.Text.Json;

namespace MystTiq.HeadlessHost;

// v0.8.20.0: the HOST tab's history -- the machine's processor, memory and busiest network adapter, one reading a minute,
// kept for 7 days in the fleet folder (the same machine for every server, so one history for the fleet). Readings come
// from the same HeadlessHostMonitor the page uses.
public sealed class HeadlessHostHistoryService : IAsyncDisposable
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false, PropertyNameCaseInsensitive = true };
    private readonly HeadlessHostMonitor monitor;
    private readonly string historyPath;
    private readonly TimeSpan interval;
    private readonly Func<DateTimeOffset> clock;
    private readonly object gate = new();
    private readonly List<HostHistorySample> samples = [];
    private DateTimeOffset lastSaved = DateTimeOffset.MinValue;
    private CancellationTokenSource? loopCts;
    private Task? loopTask;

    public HeadlessHostHistoryService(string historyRoot, HeadlessHostMonitor monitor, TimeSpan? interval = null, Func<DateTimeOffset>? clock = null)
    {
        this.monitor = monitor;
        this.interval = interval ?? TimeSpan.FromMinutes(1);
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        historyPath = Path.Combine(historyRoot, "history.json");
        Load();
    }

    public string HistoryPath => historyPath;

    public Task StartAsync(CancellationToken hostShutdown)
    {
        loopCts = CancellationTokenSource.CreateLinkedTokenSource(hostShutdown);
        loopTask = Task.Run(() => RunAsync(loopCts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (loopCts is null) { lock (gate) Save(); return; }
        loopCts.Cancel();
        if (loopTask is not null)
        {
            try { await loopTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
        }
        lock (gate) Save();
    }

    public async ValueTask DisposeAsync() { await StopAsync(CancellationToken.None); loopCts?.Dispose(); }

    private async Task RunAsync(CancellationToken token)
    {
        // The first reading at once, so a fresh install shows something within seconds.
        await SampleOnceAsync(token);
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(token))
                await SampleOnceAsync(token);
        }
        catch (OperationCanceledException) { }
    }

    public async Task SampleOnceAsync(CancellationToken token)
    {
        try
        {
            var snapshot = await monitor.GetSnapshotAsync([], token);
            Add(HostHistoryMath.FromSnapshot(snapshot, clock()));
        }
        catch (OperationCanceledException) { throw; }
        catch { /* a failed reading is skipped; the next minute tries again */ }
    }

    public void Add(HostHistorySample sample)
    {
        lock (gate)
        {
            samples.Add(sample);
            samples.RemoveAll(s => s.ObservedAt < sample.ObservedAt - Retention);
            // Written at the first reading and then every five minutes.
            if (sample.ObservedAt - lastSaved >= TimeSpan.FromMinutes(5)) Save();
        }
    }

    public HostHistorySnapshot Snapshot(double hours, int maximumPoints = 600)
    {
        var range = TimeSpan.FromHours(Math.Clamp(double.IsNaN(hours) ? 24 : hours, 1d, Retention.TotalHours));
        lock (gate)
        {
            var now = clock();
            var inRange = samples.Where(s => s.ObservedAt >= now - range).OrderBy(s => s.ObservedAt).ToArray();
            return HostHistoryMath.Summarize(inRange, range, maximumPoints, now);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(historyPath)) return;
            var loaded = JsonSerializer.Deserialize<List<HostHistorySample>>(File.ReadAllText(historyPath), JsonOptions);
            if (loaded is null) return;
            var cutoff = clock() - Retention;
            samples.AddRange(loaded.Where(s => s.ObservedAt >= cutoff).OrderBy(s => s.ObservedAt));
        }
        catch { /* an unreadable history starts empty rather than stopping MystTiq */ }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(historyPath)!);
            File.WriteAllText(historyPath + ".tmp", JsonSerializer.Serialize(samples, JsonOptions));
            File.Move(historyPath + ".tmp", historyPath, overwrite: true);
            lastSaved = samples.Count > 0 ? samples[^1].ObservedAt : clock();
        }
        catch { /* the in-memory history still serves the page; the next save tries again */ }
    }
}

public sealed record HostHistorySample(
    DateTimeOffset ObservedAt,
    double? CpuPercent,
    double MemoryUsedPercent,
    double? SentBytesPerSecond,
    double? ReceivedBytesPerSecond,
    string? Adapter);

public sealed record HostHistorySnapshot(
    IReadOnlyList<HostHistorySample> Samples,
    double RangeHours,
    int ReadingsInRange,
    double? AverageCpuPercent,
    double? PeakCpuPercent,
    double? AverageMemoryUsedPercent,
    double? PeakMemoryUsedPercent,
    double? PeakSentBytesPerSecond,
    double? PeakReceivedBytesPerSecond,
    DateTimeOffset? FirstReadingAt,
    DateTimeOffset ObservedAt);

// Pure, so the logic harness can check it.
public static class HostHistoryMath
{
    public static HostHistorySample FromSnapshot(HeadlessHostSnapshot snapshot, DateTimeOffset at)
    {
        var memory = snapshot.MemoryTotalBytes > 0
            ? 100d * (snapshot.MemoryTotalBytes - snapshot.MemoryAvailableBytes) / snapshot.MemoryTotalBytes : 0d;
        // The adapters come busiest first; that one stands for the machine (on a virtual switch the physical adapter and
        // the switch carry the same traffic, so adding them up would count it twice).
        var busiest = snapshot.Network.FirstOrDefault();
        return new HostHistorySample(at, snapshot.CpuPercent, Math.Clamp(memory, 0, 100),
            busiest?.SentBytesPerSecond, busiest?.ReceivedBytesPerSecond, busiest?.Name);
    }

    // At most maximumPoints, by averaging equal time slices (peaks are reported from all readings, not the averages).
    public static HostHistorySnapshot Summarize(IReadOnlyList<HostHistorySample> inRange, TimeSpan range, int maximumPoints, DateTimeOffset now)
    {
        maximumPoints = Math.Clamp(maximumPoints, 30, 1200);
        var points = inRange.Count <= maximumPoints ? inRange.ToArray() : Thin(inRange, maximumPoints);
        double? Avg(Func<HostHistorySample, double?> f) { var v = inRange.Select(f).Where(x => x.HasValue).Select(x => x!.Value).ToArray(); return v.Length == 0 ? null : v.Average(); }
        double? Max(Func<HostHistorySample, double?> f) { var v = inRange.Select(f).Where(x => x.HasValue).Select(x => x!.Value).ToArray(); return v.Length == 0 ? null : v.Max(); }
        return new HostHistorySnapshot(points, range.TotalHours, inRange.Count,
            Avg(s => s.CpuPercent), Max(s => s.CpuPercent), Avg(s => s.MemoryUsedPercent), Max(s => s.MemoryUsedPercent),
            Max(s => s.SentBytesPerSecond), Max(s => s.ReceivedBytesPerSecond), inRange.Count > 0 ? inRange[0].ObservedAt : null, now);
    }

    private static HostHistorySample[] Thin(IReadOnlyList<HostHistorySample> samples, int maximumPoints)
    {
        var size = (int)Math.Ceiling(samples.Count / (double)maximumPoints);
        var result = new List<HostHistorySample>();
        for (var i = 0; i < samples.Count; i += size)
        {
            var slice = samples.Skip(i).Take(size).ToArray();
            double? Mean(Func<HostHistorySample, double?> f) { var v = slice.Select(f).Where(x => x.HasValue).Select(x => x!.Value).ToArray(); return v.Length == 0 ? null : v.Average(); }
            result.Add(new HostHistorySample(slice[^1].ObservedAt, Mean(s => s.CpuPercent), slice.Average(s => s.MemoryUsedPercent),
                Mean(s => s.SentBytesPerSecond), Mean(s => s.ReceivedBytesPerSecond), slice[^1].Adapter));
        }
        return result.ToArray();
    }
}
