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
