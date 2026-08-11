using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

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
    string BackupRoot { get; }
    string ManagerRuntimeRoot { get; }
}

public sealed class LinuxServerPathProfile : IServerPathProfile
{
    public LinuxServerPathProfile(ServerRuntimeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ServerRoot = Path.GetFullPath(configuration.ServerRoot);
        ServerExecutable = Path.Combine(ServerRoot, "PalServer.sh");
        RuntimeBinaryRoot = Path.Combine(ServerRoot, "Pal", "Binaries", "Linux");
        Ue4ssRoot = Path.Combine(RuntimeBinaryRoot, "ue4ss");
        Ue4ssModsRoot = Path.Combine(Ue4ssRoot, "Mods");
        LegacyUe4ssModsRoot = Path.Combine(RuntimeBinaryRoot, "Mods");
        SaveRoot = Path.Combine(ServerRoot, "Pal", "Saved", "SaveGames");
        ConfigRoot = Path.Combine(ServerRoot, "Pal", "Saved", "Config", "LinuxServer");
        LogsRoot = Path.Combine(ServerRoot, "Pal", "Saved", "Logs");
        SteamCmdExecutable = Path.GetFullPath(configuration.SteamCmdPath);
        BackupRoot = Path.GetFullPath(configuration.BackupRoot);
        ManagerRuntimeRoot = Path.GetFullPath(configuration.RuntimeRoot);
    }

    public string PlatformId => "linux";
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
    public string BackupRoot { get; }
    public string ManagerRuntimeRoot { get; }
}

public sealed class WindowsServerPathProfile : IServerPathProfile
{
    public WindowsServerPathProfile(ServerRuntimeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ServerRoot = Path.GetFullPath(configuration.ServerRoot);
        ServerExecutable = Path.Combine(ServerRoot, "PalServer.exe");
        RuntimeBinaryRoot = Path.Combine(ServerRoot, "Pal", "Binaries", "Win64");
        Ue4ssRoot = Path.Combine(RuntimeBinaryRoot, "ue4ss");
        Ue4ssModsRoot = Path.Combine(Ue4ssRoot, "Mods");
        LegacyUe4ssModsRoot = Path.Combine(RuntimeBinaryRoot, "Mods");
        SaveRoot = Path.Combine(ServerRoot, "Pal", "Saved", "SaveGames");
        ConfigRoot = Path.Combine(ServerRoot, "Pal", "Saved", "Config", "WindowsServer");
        LogsRoot = Path.Combine(ServerRoot, "Pal", "Saved", "Logs");
        SteamCmdExecutable = Path.GetFullPath(configuration.SteamCmdPath);
        BackupRoot = Path.GetFullPath(configuration.BackupRoot);
        ManagerRuntimeRoot = Path.GetFullPath(configuration.RuntimeRoot);
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
    public string BackupRoot { get; }
    public string ManagerRuntimeRoot { get; }
}

public static class ServerPathProfile
{
    public static IServerPathProfile ForCurrentPlatform(ServerRuntimeConfiguration configuration) =>
        OperatingSystem.IsWindows() ? new WindowsServerPathProfile(configuration) :
        OperatingSystem.IsLinux() ? new LinuxServerPathProfile(configuration) :
        throw new PlatformNotSupportedException("MystTiq currently supports Windows and Linux path profiles.");
}
