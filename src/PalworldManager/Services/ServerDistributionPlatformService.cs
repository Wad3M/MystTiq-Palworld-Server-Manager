namespace PalworldManager.Services;

/// <summary>
/// Selects the current operating-system implementation of the dedicated-server
/// distribution/install/update boundary.
/// </summary>
public static class ServerDistributionPlatformService
{
    public static IServerDistributionPlatformService ForCurrentPlatform(
        ServerPlatformProfile? platformProfile = null)
    {
        platformProfile ??= ServerPlatformProfile.ForCurrentPlatform();

        return OperatingSystem.IsWindows()
            ? new WindowsServerDistributionPlatformService(platformProfile)
            : throw new PlatformNotSupportedException(
                "MystTiq server distribution operations are implemented for Windows in the v0.2.16 series. Linux distribution support begins in v0.3.");
    }
}
