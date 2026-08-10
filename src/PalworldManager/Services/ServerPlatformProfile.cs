namespace PalworldManager.Services;

/// <summary>
/// Immutable platform naming/path profile consumed by platform-neutral server services.
/// It centralizes executable/process conventions so discovery and monitoring do not
/// need Windows names embedded in their policy code.
/// </summary>
public sealed record ServerPlatformProfile(
    string PlatformId,
    IReadOnlyList<string> ProcessNames,
    IReadOnlyList<string> ServerExecutableRelativePaths,
    string SteamCmdExecutableName,
    IReadOnlyList<int> GuardedPorts)
{
    public string RootServerExecutableName =>
        ServerExecutableRelativePaths
            .FirstOrDefault(path => string.IsNullOrWhiteSpace(Path.GetDirectoryName(path)))
        ?? Path.GetFileName(ServerExecutableRelativePaths.First());

    public static ServerPlatformProfile Windows { get; } = new(
        "windows",
        [
            "PalServer",
            "PalServer-Win64-Shipping",
            "PalServer-Win64-Shipping-Cmd",
            "PalServer-Win64-Test",
            "PalServer-Win64-Test-Cmd"
        ],
        [
            Path.Combine("Pal", "Binaries", "Win64", "PalServer-Win64-Shipping-Cmd.exe"),
            Path.Combine("Pal", "Binaries", "Win64", "PalServer-Win64-Test-Cmd.exe"),
            Path.Combine("Pal", "Binaries", "Win64", "PalServer-Win64-Shipping.exe"),
            Path.Combine("Pal", "Binaries", "Win64", "PalServer-Win64-Test.exe"),
            "PalServer.exe"
        ],
        "steamcmd.exe",
        [8211, 8212, 25575]);

    public static ServerPlatformProfile ForCurrentPlatform() =>
        OperatingSystem.IsWindows()
            ? Windows
            : throw new PlatformNotSupportedException(
                "MystTiq server platform naming is implemented for Windows in the v0.2.16 series. Linux implementation begins in v0.3.");
}
