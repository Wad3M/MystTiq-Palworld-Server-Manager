namespace MystTiq.Core.Models;

public enum WindowsServiceState
{
    NotInstalled = 0,
    Stopped = 1,
    StartPending = 2,
    StopPending = 3,
    Running = 4,
    ContinuePending = 5,
    PausePending = 6,
    Paused = 7,
    Unknown = 99
}

public sealed record WindowsServiceStatus(
    string ServiceName,
    bool Installed,
    WindowsServiceState State,
    int? ProcessId,
    string Detail);

public sealed record WindowsServiceInstallResult(
    bool Success,
    string ServiceName,
    string ExecutablePath,
    bool Started,
    string Message);
