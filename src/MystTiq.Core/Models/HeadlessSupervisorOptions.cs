// MystTiq v1.0.3.0: file reviewed for this release (2026-10-05).
namespace MystTiq.Core.Models;

// v0.6.3.0: platform-neutral -- was LinuxServiceSupervisorOptions, but nothing about these fields
// is Linux-specific. Shared by both Windows and Linux `service-run` supervisor loops (HeadlessSupervisor).
public sealed record HeadlessSupervisorOptions(
    TimeSpan PollInterval,
    TimeSpan StartupTimeout,
    TimeSpan StopTimeout,
    TimeSpan RestartBackoff,
    int MaximumRestartAttempts,
    TimeSpan RestartWindow,
    // v1.0.2.0 (roadmap R-1): how long a running server may go without answering its REST API before it is
    // restarted as frozen. Null: FrozenServerWatchdog.DefaultLimit; zero: never.
    TimeSpan? UnresponsiveLimit = null);
