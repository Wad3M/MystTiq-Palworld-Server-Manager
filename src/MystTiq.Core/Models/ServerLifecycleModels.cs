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
    string Detail);

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
