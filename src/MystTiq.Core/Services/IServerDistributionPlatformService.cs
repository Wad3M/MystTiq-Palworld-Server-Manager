using System.Diagnostics;

namespace MystTiq.Core.Services;

public interface IServerDistributionPlatformService
{
    string PlatformId { get; }
    Uri SteamCmdPackageUri { get; }
    string SteamCmdExecutableName { get; }
    IReadOnlyList<string> BuildSteamCmdSelfUpdateArguments();
    IReadOnlyList<string> BuildPalworldServerInstallArguments(string serverRoot, bool validate);
    ProcessStartInfo CreateSteamCmdStartInfo(string executablePath, string workingDirectory, IEnumerable<string> arguments);
    void ExtractSteamCmdPackage(string packagePath, string destinationDirectory);
    string GetDefaultPalworldInstallRoot(string steamCmdDirectory);
}
