namespace PalworldManager.Services;

public sealed class ApplicationPathService
{
    public const string PortableMarkerFileName = "portable.mode";
    public static ApplicationPathService Current { get; } = new();

    public string ExecutableRoot { get; }
    public bool IsPortable { get; }
    public string DataRoot { get; }
    public string LocalDataRoot { get; }
    public string SettingsRoot => IsPortable ? Path.Combine(DataRoot, "Settings") : DataRoot;
    public string LogsRoot => Path.Combine(DataRoot, "Logs");
    public string CacheRoot => Path.Combine(LocalDataRoot, "Cache");
    public string DiagnosticsRoot => Path.Combine(LocalDataRoot, "Diagnostics");
    public string ActivityRoot => LocalDataRoot;
    public string ExportsRoot => IsPortable ? Path.Combine(WorkspaceRoot, "Exports") : Path.Combine(DataRoot, "Exports");
    public string WorkspaceRoot { get; }
    public string ServersRoot => Path.Combine(WorkspaceRoot, "Servers");
    public string SteamCmdRoot => Path.Combine(WorkspaceRoot, "SteamCMD");
    public string BackupsRoot => IsPortable
        ? Path.Combine(WorkspaceRoot, "Backups")
        : @"C:\GameServers\Palworld\Backups";
    public string DownloadsRoot => Path.Combine(WorkspaceRoot, "Downloads");
    public string DefaultServerRoot => IsPortable
        ? Path.Combine(ServersRoot, "Palworld")
        : @"C:\GameServers\Palworld\Server";
    public string DefaultSteamCmdPath => IsPortable
        ? Path.Combine(SteamCmdRoot, "steamcmd.exe")
        : @"C:\GameServers\Palworld\SteamCMD\steamcmd.exe";

    private ApplicationPathService()
    {
        ExecutableRoot = Path.GetFullPath(AppContext.BaseDirectory);
        IsPortable = File.Exists(Path.Combine(ExecutableRoot, PortableMarkerFileName));

        if (IsPortable)
        {
            DataRoot = Path.Combine(ExecutableRoot, "Data");
            LocalDataRoot = DataRoot;
            WorkspaceRoot = Path.Combine(ExecutableRoot, "Workspace");
        }
        else
        {
            DataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                BrandingMigrationService.ProductFolder);
            LocalDataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                BrandingMigrationService.ProductFolder);
            WorkspaceRoot = Path.Combine(DataRoot, "Workspace");
        }
    }

    public void EnsureApplicationDirectories()
    {
        var applicationDirectories = new List<string>
        {
            DataRoot, LocalDataRoot, SettingsRoot, LogsRoot, CacheRoot, DiagnosticsRoot, ActivityRoot, ExportsRoot
        };

        if (IsPortable)
        {
            applicationDirectories.AddRange(new[]
            {
                WorkspaceRoot, ServersRoot, DefaultServerRoot, SteamCmdRoot, BackupsRoot, DownloadsRoot
            });
        }

        foreach (var directory in applicationDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
            Directory.CreateDirectory(directory);

        VerifyWritable(DataRoot);
        if (IsPortable) VerifyWritable(WorkspaceRoot);
    }

    public void ApplyWorkspaceDefaults(Models.AppSettings settings)
    {
        if (!IsPortable) return;

        if (string.IsNullOrWhiteSpace(settings.BackupRoot) || IsLegacyDefault(settings.BackupRoot, @"C:\GameServers\Palworld\Backups"))
            settings.BackupRoot = BackupsRoot;

        if (!IsConfiguredServerValid(settings.ServerRoot))
        {
            var detected = FindServerRoot();
            if (!string.IsNullOrWhiteSpace(detected)) settings.ServerRoot = detected;
            else if (string.IsNullOrWhiteSpace(settings.ServerRoot) || IsLegacyDefault(settings.ServerRoot, @"C:\GameServers\Palworld\Server"))
                settings.ServerRoot = DefaultServerRoot;
        }

        if (!File.Exists(settings.SteamCmdPath))
        {
            var detected = FindSteamCmdPath();
            if (!string.IsNullOrWhiteSpace(detected)) settings.SteamCmdPath = detected;
            else if (string.IsNullOrWhiteSpace(settings.SteamCmdPath) || IsLegacyDefault(settings.SteamCmdPath, @"C:\GameServers\Palworld\SteamCMD\steamcmd.exe"))
                settings.SteamCmdPath = DefaultSteamCmdPath;
        }
    }

    public string? FindServerRoot()
    {
        return FindFirstFile(ServersRoot, "PalServer.exe") is { } serverExe
            ? Path.GetDirectoryName(serverExe)
            : null;
    }

    public string? FindSteamCmdPath() => FindFirstFile(SteamCmdRoot, "steamcmd.exe")
                                         ?? FindFirstFile(WorkspaceRoot, "steamcmd.exe");

    private static bool IsConfiguredServerValid(string? serverRoot) =>
        !string.IsNullOrWhiteSpace(serverRoot) && File.Exists(Path.Combine(serverRoot, "PalServer.exe"));

    private static bool IsLegacyDefault(string value, string legacyDefault)
    {
        try
        {
            return string.Equals(Path.GetFullPath(value), Path.GetFullPath(legacyDefault), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(value, legacyDefault, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string? FindFirstFile(string root, string fileName)
    {
        try
        {
            if (!Directory.Exists(root)) return null;
            return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                .OrderBy(path => path.Count(character => character == Path.DirectorySeparatorChar))
                .FirstOrDefault();
        }
        catch (UnauthorizedAccessException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (IOException) { return null; }
    }

    private static void VerifyWritable(string directory)
    {
        var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probe, "MystTiq workspace write test");
        }
        catch (Exception exception)
        {
            throw new IOException(
                $"MystTiq cannot write to its working directory '{directory}'. " +
                "Move the portable folder to a writable location or adjust folder permissions.", exception);
        }
        finally
        {
            try { if (File.Exists(probe)) File.Delete(probe); } catch { }
        }
    }
}
