// MystTiq v1.0.1.0: file reviewed for this release (2026-10-05).
namespace MystTiq.Core.Models;

/// <summary>
/// Minimal platform-neutral configuration consumed by the headless core.
/// (The legacy WPF app's separate AppSettings model went with that app in v0.8.26.0.)
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
