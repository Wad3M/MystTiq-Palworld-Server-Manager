namespace MystTiq.Core.Services;

// v0.7.101.0: what the crash-recovery loop tells the outside world. Until now it restarted a crashed
// server and wrote only to the console, so nobody was told the server had crashed, that it was back,
// or that recovery had given up and the server was down. An observer turns those moments into events.
public enum SupervisorEventKind
{
    // The server vanished or crashed and an automatic restart is about to be attempted.
    CrashDetected,
    // The restart worked and the server is running again.
    RecoverySucceeded,
    // The restart was attempted and failed; the loop will try again if attempts remain.
    RecoveryFailed,
    // Too many restarts inside the window: the loop stops and the server stays down.
    RecoverySuppressed,
    // v0.7.110.0: automatic recovery had given up (RecoverySuppressed) and the server is running again
    // anyway -- started by an admin, not by the loop, since the loop itself stopped trying. Appended so
    // existing callers' numbering is unaffected.
    ManualRecovery
}

public sealed record SupervisorEvent(
    SupervisorEventKind Kind,
    int Attempt,
    int MaximumAttempts,
    TimeSpan RestartBackoff,
    TimeSpan RestartWindow,
    string Detail,
    DateTimeOffset At);

public interface ISupervisorObserver
{
    // The supervisor never lets an observer failure stop crash recovery: an exception here is logged
    // and swallowed.
    Task OnEventAsync(SupervisorEvent supervisorEvent, CancellationToken cancellationToken);
}
