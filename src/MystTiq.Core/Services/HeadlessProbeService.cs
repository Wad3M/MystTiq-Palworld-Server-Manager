using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

public sealed record HeadlessProbeResult(
    string PlatformId,
    LinuxDistributionInfo? LinuxDistribution,
    IServerPathProfile Paths,
    bool ServerExecutableExists,
    bool SteamCmdExists,
    IReadOnlyList<ServerSessionProcessInfo> ServerProcesses,
    IReadOnlyList<int> GuardedListeningPorts);

public sealed class HeadlessProbeService
{
    private readonly ServerPlatformProfile platform;
    private readonly IServerPathProfile paths;

    public HeadlessProbeService(ServerPlatformProfile platform, IServerPathProfile paths)
    {
        this.platform = platform;
        this.paths = paths;
    }

    public HeadlessProbeResult Probe()
    {
        if (OperatingSystem.IsLinux())
        {
            var inspector = new LinuxServerSessionInspector(platform.GuardedPorts);
            return new HeadlessProbeResult(
                platform.PlatformId,
                new LinuxDistributionService().Detect(),
                paths,
                File.Exists(paths.ServerExecutable),
                File.Exists(paths.SteamCmdExecutable),
                inspector.FindProcessesByName(platform.ProcessNames),
                inspector.GetGuardedListeningPorts());
        }

        return new HeadlessProbeResult(
            platform.PlatformId,
            null,
            paths,
            File.Exists(paths.ServerExecutable),
            File.Exists(paths.SteamCmdExecutable),
            Array.Empty<ServerSessionProcessInfo>(),
            Array.Empty<int>());
    }
}
