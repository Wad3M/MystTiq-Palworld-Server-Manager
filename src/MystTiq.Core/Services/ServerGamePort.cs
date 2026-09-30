// MystTiq v0.9.9.0: file reviewed for this release (2026-09-29).
namespace MystTiq.Core.Services;

// v0.9.5.0: the UDP port a server really listens on, which is what readiness waits for. PalServer binds the port its
// `-port=` launch argument names, or 8211 without one; PalWorldSettings.ini's PublicPort only says which port the server
// advertises and binds nothing. Readiness used to wait for PublicPort, so a profile launched with `-port=8211` whose ini
// said 8219 never became ready and was restarted over and over. Unusable arguments (a bad value, or two different ports)
// fall back to PublicPort, as before.
public static class ServerGamePort
{
    public static int Expected(IServerPathProfile paths, IReadOnlyList<string> launchArguments)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var (port, _, invalid) = NetworkDiagnosticsService.ResolveGamePort(launchArguments ?? []);
        return invalid ? new PalworldSettingsConfigurationService(paths).GetConfiguredGamePort() : port;
    }
}
