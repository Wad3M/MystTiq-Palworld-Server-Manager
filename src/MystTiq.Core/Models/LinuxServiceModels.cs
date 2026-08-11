namespace MystTiq.Core.Models;

public enum LinuxServiceState
{
    Unknown = 0,
    NotInstalled = 1,
    Inactive = 2,
    Activating = 3,
    Active = 4,
    Failed = 5,
    Deactivating = 6
}

public sealed record LinuxServiceStatus(
    string UnitName,
    bool Installed,
    bool Enabled,
    LinuxServiceState State,
    string ActiveState,
    string SubState,
    int? MainProcessId,
    string Detail);

public sealed record LinuxServiceInstallResult(
    bool Success,
    string UnitName,
    string InstalledExecutable,
    string UnitPath,
    bool Enabled,
    bool Started,
    string Message);

public sealed record LinuxServiceSupervisorOptions(
    TimeSpan PollInterval,
    TimeSpan StartupTimeout,
    TimeSpan StopTimeout,
    TimeSpan RestartBackoff,
    int MaximumRestartAttempts,
    TimeSpan RestartWindow);
