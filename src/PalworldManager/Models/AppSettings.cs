using PalworldManager.Services;

namespace PalworldManager.Models;

public sealed class AppSettings
{
    public string ServerRoot { get; set; } = ApplicationPathService.Current.DefaultServerRoot;
    public string SteamCmdPath { get; set; } = ApplicationPathService.Current.DefaultSteamCmdPath;
    public string BackupRoot { get; set; } = ApplicationPathService.Current.BackupsRoot;
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:8212/v1/api";
    public string ApiUser { get; set; } = "admin";
    public string ProtectedPassword { get; set; } = "";
    public string LaunchArguments { get; set; } = "-useperfthreads -NoAsyncLoadingThread -UseMultithreadForDS -log -logformat=text";
    public int BackupRetention { get; set; } = 48;
    public int RestartWarningSeconds { get; set; } = 60;
    public bool AutoCrashRecovery { get; set; } = false;
    public int CrashRecoveryDelaySeconds { get; set; } = 30;
    public bool ScheduledRestartEnabled { get; set; } = false;
    public string ScheduledRestartTime { get; set; } = "04:00";
    public string LastRconPreset { get; set; } = "Command Library";
    public string PalworldSaveToolsPath { get; set; } = "";
    public string PythonExecutable { get; set; } = "python";

    public string ServerExe => Path.Combine(ServerRoot, "PalServer.exe");
    public string SaveRoot => Path.Combine(ServerRoot, "Pal", "Saved", "SaveGames");
    public string ConfigFile => Path.Combine(ServerRoot, "Pal", "Saved", "Config", "WindowsServer", "PalWorldSettings.ini");
    public string LogsRoot => Path.Combine(ServerRoot, "Pal", "Saved", "Logs");
    public string ModsRoot => Path.Combine(ServerRoot, "Mods");
    public string WorkshopRoot => Path.Combine(ModsRoot, "Workshop");
    public string DisabledWorkshopRoot => Path.Combine(ModsRoot, "WorkshopDisabled");
    public string ManagedModsRoot => Path.Combine(ModsRoot, "ManagedMods");
    public string ModSettingsFile => Path.Combine(ModsRoot, "PalModSettings.ini");

    public string GetPassword()
    {
        if (string.IsNullOrWhiteSpace(ProtectedPassword)) return "";
        try
        {
            var p = Convert.FromBase64String(ProtectedPassword);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(p, null, DataProtectionScope.CurrentUser));
        }
        catch { return ""; }
    }

    public void SetPassword(string password)
    {
        ProtectedPassword = string.IsNullOrEmpty(password) ? "" : Convert.ToBase64String(
            ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser));
    }
}
