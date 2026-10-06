// MystTiq v1.0.4.0: file reviewed for this release (2026-10-05).
namespace MystTiq.Core.Models;

public enum ServerLifecyclePhase
{
    Unknown = 0,
    Stopped = 1,
    Starting = 2,
    Running = 3,
    Stopping = 4,
    Crashed = 5
}

public enum HeadlessExitCode
{
    Success = 0,
    InvalidArguments = 2,
    AlreadyRunning = 10,
    NotRunning = 11,
    ServerExecutableMissing = 12,
    LaunchFailed = 13,
    StartupTimeout = 14,
    StopTimeout = 15,
    CrashDetected = 16,
    // v0.7.60.0: Port Conflict Prevention -- the game's configured UDP port is already bound by
    // something else (another process, another server profile, a leftover from an unclean stop)
    // at the moment a start was attempted. Distinct from StartupTimeout: this is caught BEFORE the
    // process is even launched, not after waiting out the full startup window for a port that was
    // never going to bind.
    PortConflict = 17,
    UnsupportedPlatform = 20
}

public sealed record ServerLifecycleSnapshot(
    ServerLifecyclePhase Phase,
    int? NativeProcessId,
    IReadOnlyList<ServerSessionProcessInfo> Processes,
    IReadOnlyList<int> GuardedListeningPorts,
    bool Ready,
    bool CrashDetected,
    DateTimeOffset ObservedAt,
    DateTimeOffset? LastTransitionAt,
    string Detail)
{
    // v1.0.0.1: when the server's process started (null when unknown), and whether it has been starting for longer than
    // StartupWatch.StuckAfter without opening its game port. A stuck start is what the stuck-start test is for.
    public DateTimeOffset? NativeStartedAt { get; init; }
    public bool StartupStuck { get; init; }

    // v1.0.4.0: a PalServer process is alive right now, or its game port is open. NativeProcessId alone is not that: after
    // a server ends outside MystTiq (a crash, a kill) the Crashed and Stopped snapshots carry its last-known id, and the
    // guards that refused on NativeProcessId blocked backup restores and save edits until the next start (found by the
    // v1.0.4.0 inventory smoke). Every "stop PalServer first" guard uses this.
    public bool ServerMayBeRunning => Processes.Count > 0 || Ready;
}

public sealed record ServerLifecycleOperationResult(
    HeadlessExitCode ExitCode,
    ServerLifecycleSnapshot Snapshot,
    bool Forced,
    string Message)
{
    public bool Success => ExitCode == HeadlessExitCode.Success;
}

public sealed record PersistedServerLifecycleState(
    ServerLifecyclePhase Phase,
    int? LastKnownProcessId,
    DateTimeOffset LastTransitionAt,
    bool StopRequested,
    string Detail);

// v0.7.44.0: a machine-wide, path-agnostic process match -- unlike ServerLifecycleSnapshot.Processes
// (which only ever lists processes already confirmed to sit under this profile's own ServerRoot),
// this can also include Palworld processes belonging to a different install/profile entirely.
// ManagedByThisProfile is deliberately conservative: an unknown/inaccessible ExecutablePath is
// reported as NOT managed by this profile (unlike the permissive path.Length == 0 => true rule
// FindManagedServerProcesses uses for status display), because this flag gates whether the UI can
// offer the safe, crash-recovery-aware stop path versus the disclosed-risk raw kill.
public sealed record ServerInstanceInfo(
    int ProcessId,
    int? ParentProcessId,
    string ProcessName,
    string? ExecutablePath,
    bool Responding,
    bool ManagedByThisProfile);

// v0.7.44.0: result of a raw, unmanaged-process termination -- distinct from
// ServerLifecycleOperationResult because a raw kill by PID doesn't touch this profile's own
// PersistedServerLifecycleState/crash-detection bookkeeping and has no lifecycle snapshot of its own.
public sealed record InstanceTerminationResult(bool Success, int ProcessId, string Message);
