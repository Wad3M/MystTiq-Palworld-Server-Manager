using System.Diagnostics;
using MystTiq.Core.Operations;

namespace MystTiq.Core.Services;

// Extends ICapabilityProvider (v0.6.0.0) via default interface members keyed
// off the existing PlatformId, so the Windows/Linux implementations become
// real, discoverable capability providers with no code changes of their own --
// proving the marker interface isn't just decorative scaffolding.
public interface IServerDistributionPlatformService : ICapabilityProvider
{
    string PlatformId { get; }
    Uri SteamCmdPackageUri { get; }
    string SteamCmdExecutableName { get; }
    IReadOnlyList<string> BuildSteamCmdSelfUpdateArguments();
    IReadOnlyList<string> BuildPalworldServerInstallArguments(string serverRoot, bool validate);
    ProcessStartInfo CreateSteamCmdStartInfo(string executablePath, string workingDirectory, IEnumerable<string> arguments);
    void ExtractSteamCmdPackage(string packagePath, string destinationDirectory);
    string GetDefaultPalworldInstallRoot(string steamCmdDirectory);

    string ICapabilityProvider.ProviderId => PlatformId;
    bool ICapabilityProvider.IsAvailable => true;
}
