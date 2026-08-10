using System.Diagnostics;
using System.IO.Compression;

namespace PalworldManager.Services;

/// <summary>
/// Windows implementation of the SteamCMD distribution boundary.
/// This preserves MystTiq's validated Windows install/update behavior.
/// </summary>
public sealed class WindowsServerDistributionPlatformService : IServerDistributionPlatformService
{
    private const string PalworldDedicatedServerAppId = "2394010";
    private readonly ServerPlatformProfile profile;

    public WindowsServerDistributionPlatformService(ServerPlatformProfile? profile = null)
    {
        this.profile = profile ?? ServerPlatformProfile.Windows;
    }

    public string PlatformId => "windows";
    public Uri SteamCmdPackageUri { get; } =
        new("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip");
    public string SteamCmdExecutableName => profile.SteamCmdExecutableName;

    public IReadOnlyList<string> BuildSteamCmdSelfUpdateArguments() => ["+quit"];

    public IReadOnlyList<string> BuildPalworldServerInstallArguments(string serverRoot, bool validate)
    {
        var arguments = new List<string>
        {
            "+force_install_dir", serverRoot,
            "+login", "anonymous",
            "+app_update", PalworldDedicatedServerAppId
        };

        if (validate)
            arguments.Add("validate");

        arguments.Add("+quit");
        return arguments;
    }

    public ProcessStartInfo CreateSteamCmdStartInfo(
        string executablePath,
        string workingDirectory,
        IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }

    public void ExtractSteamCmdPackage(string packagePath, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        ZipFile.ExtractToDirectory(packagePath, destinationDirectory, overwriteFiles: true);
    }

    public string GetDefaultPalworldInstallRoot(string steamCmdDirectory) =>
        Path.Combine(steamCmdDirectory, "steamapps", "common", "PalServer");
}
