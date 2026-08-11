using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;

namespace MystTiq.Core.Services;

/// <summary>
/// Linux SteamCMD distribution policy validated against the MystTiq Ubuntu test host.
/// Palworld requires the SteamCMD platform override in this environment; omitting it
/// produced "Missing configuration" even though Linux depot 2394012 was published.
/// </summary>
public sealed class LinuxServerDistributionPlatformService : IServerDistributionPlatformService
{
    private const string PalworldDedicatedServerAppId = "2394010";
    private readonly ServerPlatformProfile profile;

    public LinuxServerDistributionPlatformService(ServerPlatformProfile? profile = null)
    {
        this.profile = profile ?? ServerPlatformProfile.Linux;
    }

    public string PlatformId => "linux";
    public Uri SteamCmdPackageUri { get; } =
        new("https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz");
    public string SteamCmdExecutableName => profile.SteamCmdExecutableName;

    public IReadOnlyList<string> BuildSteamCmdSelfUpdateArguments() => ["+quit"];

    public IReadOnlyList<string> BuildPalworldServerInstallArguments(string serverRoot, bool validate)
    {
        var arguments = new List<string>
        {
            "+@sSteamCmdForcePlatformType", "linux",
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
        using var package = File.OpenRead(packagePath);
        using var gzip = new GZipStream(package, CompressionMode.Decompress);
        TarFile.ExtractToDirectory(gzip, destinationDirectory, overwriteFiles: true);
    }

    public string GetDefaultPalworldInstallRoot(string steamCmdDirectory) =>
        Path.Combine(steamCmdDirectory, "steamapps", "common", "PalServer");
}
