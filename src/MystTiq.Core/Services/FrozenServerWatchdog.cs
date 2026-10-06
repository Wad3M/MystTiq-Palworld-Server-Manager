// MystTiq v1.0.3.0: file reviewed for this release (2026-10-05).
namespace MystTiq.Core.Services;

// v1.0.2.0 (roadmap R-1, frozen-server watchdog): what one look at the running server's REST API said.
public enum ResponsivenessProbeResult
{
    // It answered (any HTTP answer: a frozen process gives none).
    Answered,
    // It did not answer in time, or refused the connection.
    NoAnswer,
    // There is nothing to ask (the REST API is off or has no password): never a reason to restart.
    CannotTell
}

public interface IServerResponsivenessProbe
{
    Task<ResponsivenessProbeResult> ProbeAsync(CancellationToken cancellationToken);
}

/// <summary>
/// v1.0.2.0 (roadmap R-1): decides when a server that is running but no longer answering is frozen. A process
/// that stays alive while the game is stuck kept its game port, so crash recovery (which only reacts to a missing
/// process) never saw it. The rule never restarts a healthy server:
///   - only a process that has answered at least once since it started can be judged (a REST API that never
///     answered proves nothing about this process);
///   - only "no answer" counts against it; "cannot tell" (REST off, no password) never does;
///   - only while the server reports ready, and only after it has been silent for the whole limit.
/// A new process (another process id) starts with a clean record. Pure: the supervisor supplies the clock.
/// </summary>
public sealed class FrozenServerWatchdog
{
    public static readonly TimeSpan DefaultLimit = TimeSpan.FromMinutes(3);
    // A configured limit below this is refused (HeadlessConfigurationService): a world save can hold the REST API
    // for a little while.
    public const int MinimumLimitSeconds = 60;

    private readonly TimeSpan probeInterval;
    private int? processId;
    private DateTimeOffset? lastAnswer;
    private DateTimeOffset? lastProbe;

    public FrozenServerWatchdog(TimeSpan limit)
    {
        Limit = limit;
        // About six looks per limit, at most every 30 seconds.
        probeInterval = TimeSpan.FromTicks(Math.Clamp(limit.Ticks / 6, 1, TimeSpan.FromSeconds(30).Ticks));
    }

    public TimeSpan Limit { get; }
    public bool Enabled => Limit > TimeSpan.Zero;

    public bool IsDue(int? pid, DateTimeOffset now)
    {
        Track(pid);
        return Enabled && (lastProbe is null || now - lastProbe >= probeInterval);
    }

    /// <summary>Records a look; returns how long the server has been silent when that makes it frozen, else null.</summary>
    public TimeSpan? Record(int? pid, bool ready, ResponsivenessProbeResult result, DateTimeOffset now)
    {
        Track(pid);
        lastProbe = now;
        if (!Enabled || !ready || result == ResponsivenessProbeResult.CannotTell) return null;
        if (result == ResponsivenessProbeResult.Answered) { lastAnswer = now; return null; }
        if (lastAnswer is not { } last) return null;
        var silent = now - last;
        return silent >= Limit ? silent : null;
    }

    public void Forget()
    {
        processId = null;
        lastAnswer = null;
        lastProbe = null;
    }

    private void Track(int? pid)
    {
        if (pid == processId) return;
        processId = pid;
        lastAnswer = null;
        lastProbe = null;
    }
}
