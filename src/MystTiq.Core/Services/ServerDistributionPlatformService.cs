// MystTiq v1.0.5.0: file reviewed for this release (2026-10-06).
namespace MystTiq.Core.Services;

public static class ServerDistributionPlatformService
{
    public static IServerDistributionPlatformService ForCurrentPlatform(ServerPlatformProfile? profile = null)
    {
        profile ??= ServerPlatformProfile.ForCurrentPlatform();

        return OperatingSystem.IsWindows()
            ? new WindowsServerDistributionPlatformService(profile)
            : OperatingSystem.IsLinux()
                ? new LinuxServerDistributionPlatformService(profile)
                : throw new PlatformNotSupportedException(
                    "MystTiq distribution operations are currently implemented for Windows and Linux.");
    }
}
