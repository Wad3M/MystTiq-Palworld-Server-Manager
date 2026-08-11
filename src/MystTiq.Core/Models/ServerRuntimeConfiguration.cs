namespace MystTiq.Core.Models;

/// <summary>
/// Minimal platform-neutral configuration consumed by the headless core.
/// The WPF AppSettings model remains separate while shared state migrates
/// incrementally into the cross-platform core.
/// </summary>
public sealed record ServerRuntimeConfiguration(
    string ServerRoot,
    string SteamCmdPath,
    string BackupRoot,
    string RuntimeRoot)
{
    public static ServerRuntimeConfiguration CreateDefault()
    {
        if (OperatingSystem.IsLinux())
        {
            return new ServerRuntimeConfiguration(
                "/opt/mysttiq/palserver",
                "/opt/mysttiq/steamcmd/steamcmd.sh",
                "/opt/mysttiq/backups",
                "/opt/mysttiq/runtime");
        }

        if (OperatingSystem.IsWindows())
        {
            return new ServerRuntimeConfiguration(
                @"C:\GameServers\Palworld\Server",
                @"C:\GameServers\Palworld\SteamCMD\steamcmd.exe",
                @"C:\GameServers\Palworld\Backups",
                @"C:\GameServers\Palworld\Runtime");
        }

        throw new PlatformNotSupportedException(
            "MystTiq headless platform defaults are currently implemented for Windows and Linux.");
    }
}
