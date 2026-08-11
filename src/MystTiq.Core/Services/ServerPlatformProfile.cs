namespace MystTiq.Core.Services;

/// <summary>
/// Cross-platform PalServer process/executable conventions used by the headless core.
/// </summary>
public sealed record ServerPlatformProfile(
    string PlatformId,
    IReadOnlyList<string> ProcessNames,
    IReadOnlyList<string> ServerExecutableRelativePaths,
    string SteamCmdExecutableName,
    IReadOnlyList<int> GuardedPorts)
{
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

    public static ServerPlatformProfile Linux { get; } = new(
        "linux",
        [
            "PalServer-Linux-Shipping",
            "PalServer-Linux-", // procfs comm fallback when executable-link resolution is restricted
            "PalServer"
        ],
        [
            "PalServer.sh",
            Path.Combine("Pal", "Binaries", "Linux", "PalServer-Linux-Shipping")
        ],
        "steamcmd.sh",
        [8211, 8212, 25575]);

    public static ServerPlatformProfile ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? Windows :
        OperatingSystem.IsLinux() ? Linux :
        throw new PlatformNotSupportedException("MystTiq currently supports Windows and Linux platform profiles.");
}
