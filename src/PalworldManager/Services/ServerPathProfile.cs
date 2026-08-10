using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Platform-aware deployment paths for a Palworld dedicated-server installation.
/// Consumers use this contract instead of rebuilding OS/runtime-specific paths.
/// </summary>
public interface IServerPathProfile
{
    string PlatformId { get; }
    string ServerRoot { get; }
    string ServerExecutable { get; }
    string RuntimeBinaryRoot { get; }
    string Ue4ssRoot { get; }
    string Ue4ssModsRoot { get; }
    string LegacyUe4ssModsRoot { get; }
    string SaveRoot { get; }
    string ConfigRoot { get; }
    string LogsRoot { get; }
    string SteamCmdExecutable { get; }
}

/// <summary>Current Windows deployment layout. Values intentionally preserve v0.2.15 behavior.</summary>
public sealed class WindowsServerPathProfile : IServerPathProfile
{
    public WindowsServerPathProfile(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ServerRoot = Path.GetFullPath(settings.ServerRoot);
        ServerExecutable = Path.Combine(ServerRoot, "PalServer.exe");
        RuntimeBinaryRoot = Path.Combine(ServerRoot, "Pal", "Binaries", "Win64");
        Ue4ssRoot = Path.Combine(RuntimeBinaryRoot, "ue4ss");
        Ue4ssModsRoot = Path.Combine(Ue4ssRoot, "Mods");
        LegacyUe4ssModsRoot = Path.Combine(RuntimeBinaryRoot, "Mods");
        SaveRoot = Path.Combine(ServerRoot, "Pal", "Saved", "SaveGames");
        ConfigRoot = Path.Combine(ServerRoot, "Pal", "Saved", "Config", "WindowsServer");
        LogsRoot = Path.Combine(ServerRoot, "Pal", "Saved", "Logs");
        SteamCmdExecutable = settings.SteamCmdPath;
    }

    public string PlatformId => "windows";
    public string ServerRoot { get; }
    public string ServerExecutable { get; }
    public string RuntimeBinaryRoot { get; }
    public string Ue4ssRoot { get; }
    public string Ue4ssModsRoot { get; }
    public string LegacyUe4ssModsRoot { get; }
    public string SaveRoot { get; }
    public string ConfigRoot { get; }
    public string LogsRoot { get; }
    public string SteamCmdExecutable { get; }
}

public static class ServerPathProfile
{
    public static IServerPathProfile ForCurrentPlatform(AppSettings settings) =>
        OperatingSystem.IsWindows()
            ? new WindowsServerPathProfile(settings)
            : throw new PlatformNotSupportedException("MystTiq server deployment paths are implemented for Windows in the v0.2.16 series. Linux implementation begins in the v0.3 series.");
}
