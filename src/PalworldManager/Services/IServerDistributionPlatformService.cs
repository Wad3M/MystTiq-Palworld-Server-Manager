using System.Diagnostics;

namespace PalworldManager.Services;

/// <summary>
/// Platform-specific distribution/install/update behavior for SteamCMD and the
/// Palworld dedicated-server package. High-level installer/update policy consumes
/// this contract rather than embedding Windows archive/process conventions.
/// </summary>
public interface IServerDistributionPlatformService
{
    string PlatformId { get; }
    Uri SteamCmdPackageUri { get; }
    string SteamCmdExecutableName { get; }
    IReadOnlyList<string> BuildSteamCmdSelfUpdateArguments();
    IReadOnlyList<string> BuildPalworldServerInstallArguments(string serverRoot, bool validate);
    ProcessStartInfo CreateSteamCmdStartInfo(
        string executablePath,
        string workingDirectory,
        IEnumerable<string> arguments);
    void ExtractSteamCmdPackage(string packagePath, string destinationDirectory);
    string GetDefaultPalworldInstallRoot(string steamCmdDirectory);
}
