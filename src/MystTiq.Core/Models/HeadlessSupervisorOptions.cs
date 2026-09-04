namespace MystTiq.Core.Models;

// v0.6.3.0: platform-neutral -- was LinuxServiceSupervisorOptions, but nothing about these fields
// is Linux-specific. Shared by both Windows and Linux `service-run` supervisor loops (HeadlessSupervisor).
public sealed record HeadlessSupervisorOptions(
    TimeSpan PollInterval,
    TimeSpan StartupTimeout,
    TimeSpan StopTimeout,
    TimeSpan RestartBackoff,
    int MaximumRestartAttempts,
    TimeSpan RestartWindow);
