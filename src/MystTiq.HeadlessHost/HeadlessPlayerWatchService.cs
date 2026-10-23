// MystTiq v1.1.5.0: file reviewed for this release (2026-10-23).
namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.1.1.0: the player checks without a desktop. The join check, the identity guard, character moves, the player list,
/// the whitelist, the name guard, starter kits and temporary-ban expiry ran only when a desktop polled /status/poll, so a
/// service with no desktop open let a player the game gave a new ID make a fresh character unnoticed. The service now
/// runs the same checks itself every <see cref="Interval"/> once no desktop has polled for <see cref="DesktopQuiet"/>;
/// while a desktop polls, its poll runs them as before. Both go through one lock, so the checks never run twice at once.
/// </summary>
public sealed class HeadlessPlayerWatchService : IAsyncDisposable
{
    public static TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(15);
    public static TimeSpan DesktopQuiet { get; set; } = TimeSpan.FromSeconds(30);

    private readonly Func<CancellationToken, Task<HeadlessPlayersSnapshot>> readPlayers;
    private readonly Func<HeadlessPlayersSnapshot, CancellationToken, Task> checks;
    private readonly HeadlessActivityLogService? activity;
    private readonly Func<DateTimeOffset> clock;
    private readonly SemaphoreSlim running = new(1, 1);
    private readonly object gate = new();
    private DateTimeOffset lastPoll = DateTimeOffset.MinValue;
    private string? lastProblem;
    private CancellationTokenSource? loopCts;
    private Task? loopTask;

    public HeadlessPlayerWatchService(Func<CancellationToken, Task<HeadlessPlayersSnapshot>> readPlayers,
        Func<HeadlessPlayersSnapshot, CancellationToken, Task> checks, HeadlessActivityLogService? activity = null, Func<DateTimeOffset>? clock = null)
    {
        this.readPlayers = readPlayers;
        this.checks = checks;
        this.activity = activity;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>When the checks last ran, and whether a desktop's poll or the service ran them.</summary>
    public DateTimeOffset? LastRunAt { get; private set; }
    public string LastRunBy { get; private set; } = string.Empty;

    /// <summary>A desktop's /status/poll: runs the checks on the players it read and holds the service's own run back.</summary>
    public Task RunFromPollAsync(HeadlessPlayersSnapshot players, CancellationToken cancellationToken)
    {
        lock (gate) lastPoll = clock();
        return RunAsync(players, "desktop", cancellationToken);
    }

    /// <summary>One service tick: reads the players and runs the checks when no desktop polled lately. True when it ran.</summary>
    public async Task<bool> TickAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset polled;
        lock (gate) polled = lastPoll;
        if (clock() - polled < DesktopQuiet) return false;
        var players = await readPlayers(cancellationToken);
        await RunAsync(players, "service", cancellationToken);
        return true;
    }

    private async Task RunAsync(HeadlessPlayersSnapshot players, string by, CancellationToken cancellationToken)
    {
        await running.WaitAsync(cancellationToken);
        try
        {
            await checks(players, cancellationToken);
            LastRunAt = clock();
            LastRunBy = by;
        }
        finally { running.Release(); }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (loopTask is not null) return Task.CompletedTask;
        loopCts = new CancellationTokenSource();
        var token = loopCts.Token;
        loopTask = Task.Run(() => RunLoopAsync(token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (loopCts is null) return;
        loopCts.Cancel();
        if (loopTask is not null)
        {
            try { await loopTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        loopCts?.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(Interval);
        while (true)
        {
            try { if (!await timer.WaitForNextTickAsync(token)) return; }
            catch (OperationCanceledException) { return; }
            try
            {
                await TickAsync(token);
                lastProblem = null;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // Logged once per distinct problem, not every 15 seconds.
                if (ex.Message != lastProblem) activity?.Record("Warning", "Players", "Player checks", "The service's player checks failed: " + ex.Message);
                lastProblem = ex.Message;
            }
        }
    }
}
