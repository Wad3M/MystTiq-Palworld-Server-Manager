using PalworldManager.Models;
using PalworldManager.Services.Infrastructure;

namespace PalworldManager.Services;

public interface IDiagnosticsProvider
{
    string Category { get; }
    Task<IReadOnlyList<DiagnosticResultRow>> RunAsync(CancellationToken cancellationToken = default);
}

public sealed class DiagnosticsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly AppSettings settings;
    private readonly ApplicationPathService paths = ApplicationPathService.Current;
    private readonly List<IDiagnosticsProvider> providers;

    public DiagnosticsService(AppSettings settings)
    {
        this.settings = settings;
        providers =
        [
            new ApplicationDiagnosticsProvider(settings, paths),
            new WorkspaceDiagnosticsProvider(settings, paths),
            new ServerDiagnosticsProvider(settings),
            new WorldDiagnosticsProvider(settings),
            new BackupDiagnosticsProvider(settings),
            new ModDiagnosticsProvider(settings),
            new TransactionDiagnosticsProvider(settings),
            new NotificationDiagnosticsProvider(paths)
        ];
    }

    public async Task<DiagnosticsSnapshot> RunAllAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var snapshot = new DiagnosticsSnapshot();
        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Running {provider.Category} diagnostics...");
            try
            {
                snapshot.Results.AddRange(await provider.RunAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                snapshot.Results.Add(Fail(provider.Category, "Provider execution", ex.Message, "Review the session log and retry the diagnostic."));
            }
        }
        snapshot.CompletedUtc = DateTime.UtcNow;
        progress?.Report($"Diagnostics complete: {snapshot.Score}% ({snapshot.OverallStatus}).");
        return snapshot;
    }

    public (string JsonPath, string TextPath) Export(DiagnosticsSnapshot snapshot)
    {
        paths.EnsureApplicationDirectories();
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var root = paths.DiagnosticsRoot;
        Directory.CreateDirectory(root);
        var jsonPath = Path.Combine(root, $"DiagnosticsReport_{stamp}.json");
        var textPath = Path.Combine(root, $"DiagnosticsReport_{stamp}.txt");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(snapshot, JsonOptions));
        File.WriteAllText(textPath, BuildTextReport(snapshot));
        return (jsonPath, textPath);
    }

    public string CreateSupportPackage(DiagnosticsSnapshot snapshot)
    {
        var exported = Export(snapshot);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var staging = Path.Combine(paths.CacheRoot, "SupportPackages", stamp);
        Directory.CreateDirectory(staging);
        File.Copy(exported.JsonPath, Path.Combine(staging, Path.GetFileName(exported.JsonPath)), true);
        File.Copy(exported.TextPath, Path.Combine(staging, Path.GetFileName(exported.TextPath)), true);

        var redacted = new
        {
            Version = ApplicationVersion.Version,
            Mode = paths.IsPortable ? "Portable" : "Installed",
            settings.ServerRoot,
            settings.SteamCmdPath,
            settings.BackupRoot,
            settings.ApiBaseUrl,
            settings.ApiUser,
            PasswordConfigured = !string.IsNullOrWhiteSpace(settings.ProtectedPassword),
            settings.BackupRetention,
            settings.AutoCrashRecovery,
            settings.ScheduledRestartEnabled,
            settings.ScheduledRestartTime,
            settings.PalworldSaveToolsPath,
            settings.PythonExecutable
        };
        File.WriteAllText(Path.Combine(staging, "Configuration-REDACTED.json"), JsonSerializer.Serialize(redacted, JsonOptions));

        var logDestination = Path.Combine(staging, "RecentLogs");
        Directory.CreateDirectory(logDestination);
        try
        {
            var recentLogs = Directory.Exists(paths.LogsRoot)
                ? Directory.EnumerateFiles(paths.LogsRoot, "*.*", SearchOption.TopDirectoryOnly)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(x => x.LastWriteTimeUtc)
                    .Take(5)
                    .ToList()
                : new List<FileInfo>();
            foreach (var log in recentLogs)
            {
                if (log.Length > 5 * 1024 * 1024) continue;
                var extension = log.Extension.ToLowerInvariant();
                if (extension is not (".log" or ".txt" or ".json" or ".jsonl")) continue;
                var text = File.ReadAllText(log.FullName);
                File.WriteAllText(Path.Combine(logDestination, log.Name), RedactSensitiveText(text));
            }
        }
        catch { }

        var transactionRoot = Path.Combine(settings.BackupRoot, "Transactions");
        File.WriteAllText(Path.Combine(staging, "TransactionLocation.txt"), transactionRoot);
        var zipPath = Path.Combine(paths.DiagnosticsRoot, $"MystTiqSupportPackage_{stamp}.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(staging, zipPath, CompressionLevel.Optimal, false);
        try { Directory.Delete(staging, true); } catch { }
        return zipPath;
    }

    private static string RedactSensitiveText(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var redacted = Regex.Replace(value, @"(?im)(password|passwd|token|secret|api[_ -]?key|authorization)(\s*[:=]\s*)([^\r\n,;]+)", "$1$2[REDACTED]");
        redacted = Regex.Replace(redacted, @"(?im)(Basic\s+)[A-Za-z0-9+/=]+", "$1[REDACTED]");
        redacted = Regex.Replace(redacted, @"(?im)(Bearer\s+)[A-Za-z0-9._~+/-]+", "$1[REDACTED]");
        return redacted;
    }

    public static string BuildTextReport(DiagnosticsSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("MystTiq Palworld Server Manager Diagnostics");
        builder.AppendLine($"Generated: {snapshot.CompletedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Overall: {snapshot.Score}% - {snapshot.OverallStatus}");
        builder.AppendLine($"Passed: {snapshot.Passed}  Warnings: {snapshot.Warnings}  Failed: {snapshot.Failed}");
        builder.AppendLine(new string('-', 72));
        foreach (var row in snapshot.Results)
        {
            builder.AppendLine($"[{row.Status}] {row.Category} / {row.Check}");
            builder.AppendLine($"  {row.Detail}");
            if (!string.IsNullOrWhiteSpace(row.Recommendation)) builder.AppendLine($"  Recommendation: {row.Recommendation}");
        }
        return builder.ToString();
    }

    internal static DiagnosticResultRow Pass(string category, string check, string detail) => new() { Category = category, Check = check, Status = "Passed", Detail = detail };
    internal static DiagnosticResultRow Warn(string category, string check, string detail, string recommendation) => new() { Category = category, Check = check, Status = "Warning", Detail = detail, Recommendation = recommendation };
    internal static DiagnosticResultRow Fail(string category, string check, string detail, string recommendation) => new() { Category = category, Check = check, Status = "Failed", Detail = detail, Recommendation = recommendation, Weight = 2 };

    private sealed class ApplicationDiagnosticsProvider(AppSettings settings, ApplicationPathService paths) : IDiagnosticsProvider
    {
        public string Category => "Application";
        public Task<IReadOnlyList<DiagnosticResultRow>> RunAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DiagnosticResultRow> rows =
            [
                Pass(Category, "Version", $"MystTiq {ApplicationVersion.DisplayVersion} is running."),
                Pass(Category, "Deployment mode", paths.IsPortable ? "Portable mode is active." : "Installed mode is active."),
                File.Exists(new SettingsStore().FilePath)
                    ? Pass(Category, "Configuration", "The settings file exists and was loaded.")
                    : Warn(Category, "Configuration", "No persisted settings file was found.", "Save Settings once to create the configuration file."),
                string.IsNullOrWhiteSpace(settings.ServerRoot)
                    ? Fail(Category, "Server configuration", "ServerRoot is empty.", "Configure the Palworld server folder in Workspace Manager.")
                    : Pass(Category, "Server configuration", $"ServerRoot is configured as '{settings.ServerRoot}'.")
            ];
            return Task.FromResult(rows);
        }
    }

    private sealed class WorkspaceDiagnosticsProvider(AppSettings settings, ApplicationPathService paths) : IDiagnosticsProvider
    {
        public string Category => "Workspace";
        public Task<IReadOnlyList<DiagnosticResultRow>> RunAsync(CancellationToken cancellationToken = default)
        {
            var rows = new List<DiagnosticResultRow>();
            CheckDirectory(rows, paths.DataRoot, "Application data", true);
            CheckDirectory(rows, paths.LogsRoot, "Logs", true);
            CheckDirectory(rows, paths.DiagnosticsRoot, "Diagnostics", true);
            CheckDirectory(rows, settings.BackupRoot, "Backups", true);
            CheckDirectory(rows, paths.ExportsRoot, "Exports", true);
            return Task.FromResult<IReadOnlyList<DiagnosticResultRow>>(rows);
        }
        private void CheckDirectory(List<DiagnosticResultRow> rows, string path, string check, bool testWrite)
        {
            try
            {
                Directory.CreateDirectory(path);
                if (testWrite)
                {
                    var probe = Path.Combine(path, $".diagnostic-{Guid.NewGuid():N}.tmp");
                    File.WriteAllText(probe, "MystTiq diagnostic write test");
                    File.Delete(probe);
                }
                rows.Add(Pass(Category, check, $"'{path}' is accessible and writable."));
            }
            catch (Exception ex) { rows.Add(Fail(Category, check, ex.Message, $"Grant write access to '{path}' or choose another folder.")); }
        }
    }

    private sealed class ServerDiagnosticsProvider(AppSettings settings) : IDiagnosticsProvider
    {
        public string Category => "Server";
        public async Task<IReadOnlyList<DiagnosticResultRow>> RunAsync(CancellationToken cancellationToken = default)
        {
            var rows = new List<DiagnosticResultRow>
            {
                File.Exists(settings.ServerExe) ? Pass(Category, "PalServer executable", settings.ServerExe) : Fail(Category, "PalServer executable", $"Missing: {settings.ServerExe}", "Select the folder containing PalServer.exe."),
                File.Exists(settings.SteamCmdPath) ? Pass(Category, "SteamCMD", settings.SteamCmdPath) : Warn(Category, "SteamCMD", $"Missing: {settings.SteamCmdPath}", "Configure steamcmd.exe to enable server updates.")
            };
            var running = Process.GetProcessesByName("PalServer-Win64-Test-Cmd").Length > 0 || Process.GetProcessesByName("PalServer").Length > 0;
            rows.Add(Pass(Category, "Process state", running ? "PalServer is currently running." : "PalServer is intentionally stopped or not running."));
            try
            {
                var uri = new Uri(settings.ApiBaseUrl);
                using var client = new TcpClient();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                await client.ConnectAsync(uri.Host, uri.Port > 0 ? uri.Port : 80, timeout.Token);
                rows.Add(Pass(Category, "REST endpoint", $"TCP connection succeeded to {uri.Host}:{uri.Port}."));
            }
            catch (Exception ex) { rows.Add(Warn(Category, "REST endpoint", ex.Message, "If the server is running, verify REST API settings and firewall access.")); }
            return rows;
        }
    }

    private sealed class WorldDiagnosticsProvider(AppSettings settings) : IDiagnosticsProvider
    {
        public string Category => "World";
        public Task<IReadOnlyList<DiagnosticResultRow>> RunAsync(CancellationToken cancellationToken = default)
        {
            var rows = new List<DiagnosticResultRow>();
            var root = Path.Combine(settings.SaveRoot, "0");
            var fileSystem = new SafeFileSystemService();
            var world = fileSystem.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly, cancellationToken)
                .Where(path => File.Exists(Path.Combine(path, "Level.sav")))
                .OrderByDescending(path => File.GetLastWriteTimeUtc(Path.Combine(path, "Level.sav")))
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(world))
            {
                rows.Add(Warn(Category, "Active world", "No world containing Level.sav was found.", "Start the server once or verify the server root and active world."));
                return Task.FromResult<IReadOnlyList<DiagnosticResultRow>>(rows);
            }
            rows.Add(Pass(Category, "Active world", world));
            var discovery = new PlayerSaveDiscoveryService(fileSystem).DiscoverFromWorld(world, cancellationToken);
            rows.Add(Pass(Category, "Player saves", $"Accepted {discovery.Accepted.Count} unique player save(s)."));
            rows.Add(discovery.Rejected.Count == 0
                ? Pass(Category, "Rejected player saves", "No malformed or transient player saves were found.")
                : Warn(Category, "Rejected player saves", $"Rejected {discovery.Rejected.Count} file(s).", "Review Player Discovery diagnostics for rejection reasons."));
            try
            {
                var guildSnapshot = new GuildService(settings).LoadSnapshot(world);
                rows.Add(guildSnapshot.Warnings.Count == 0
                    ? Pass(Category, "Guild snapshot", $"Loaded {guildSnapshot.Guilds.Count} guild(s).")
                    : Warn(Category, "Guild snapshot", string.Join(" | ", guildSnapshot.Warnings), "Run World Validator for detailed findings."));
            }
            catch (Exception ex) { rows.Add(Warn(Category, "Guild snapshot", ex.Message, "Run World Validator for detailed findings.")); }
            try
            {
                var bases = new BaseManagerService(settings).Scan(world);
                rows.Add(bases.Warnings.Count == 0
                    ? Pass(Category, "Base snapshot", $"Loaded {bases.Bases.Count} base(s); {bases.OrphanedCount} orphaned.")
                    : Warn(Category, "Base snapshot", $"Loaded {bases.Bases.Count} base(s) with {bases.Warnings.Count} warning(s).", "Open Base Manager or Repair Center for details."));
            }
            catch (Exception ex) { rows.Add(Warn(Category, "Base snapshot", ex.Message, "Open Base Manager or Repair Center for details.")); }
            return Task.FromResult<IReadOnlyList<DiagnosticResultRow>>(rows);
        }
    }

    private sealed class BackupDiagnosticsProvider(AppSettings settings) : IDiagnosticsProvider
    {
        public string Category => "Backups";
        public Task<IReadOnlyList<DiagnosticResultRow>> RunAsync(CancellationToken cancellationToken = default)
        {
            var rows = new List<DiagnosticResultRow>();
            try
            {
                Directory.CreateDirectory(settings.BackupRoot);
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(settings.BackupRoot))!);
                rows.Add(Pass(Category, "Backup destination", settings.BackupRoot));
                rows.Add(drive.AvailableFreeSpace < 2L * 1024 * 1024 * 1024
                    ? Warn(Category, "Free disk space", $"{drive.AvailableFreeSpace / 1024d / 1024d / 1024d:F1} GB available.", "Free disk space before creating additional backups.")
                    : Pass(Category, "Free disk space", $"{drive.AvailableFreeSpace / 1024d / 1024d / 1024d:F1} GB available."));
                var count = Directory.EnumerateFiles(settings.BackupRoot, "*.zip", SearchOption.AllDirectories).Take(10000).Count();
                rows.Add(Pass(Category, "Backup inventory", $"Found {count} ZIP archive(s)."));
            }
            catch (Exception ex) { rows.Add(Fail(Category, "Backup destination", ex.Message, "Choose a writable backup folder in Workspace Manager.")); }
            return Task.FromResult<IReadOnlyList<DiagnosticResultRow>>(rows);
        }
    }

    private sealed class ModDiagnosticsProvider(AppSettings settings) : IDiagnosticsProvider
    {
        public string Category => "Mods";
        public Task<IReadOnlyList<DiagnosticResultRow>> RunAsync(CancellationToken cancellationToken = default)
        {
            var rows = new List<DiagnosticResultRow>();
            try
            {
                var mods = new ModService(settings).Scan();
                rows.Add(Pass(Category, "Inventory", $"Detected {mods.Count} managed or discoverable MOD entry/entries."));
            }
            catch (Exception ex) { rows.Add(Warn(Category, "Inventory", ex.Message, "Open MOD Dashboard and refresh the inventory for more detail.")); }
            var ue4ss = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", "UE4SS.dll");
            rows.Add(File.Exists(ue4ss)
                ? Pass(Category, "UE4SS runtime", ue4ss)
                : Warn(Category, "UE4SS runtime", "UE4SS.dll was not detected in the server binary folder.", "Ignore this warning when UE4SS is not required, or install/verify the runtime from the UE4SS page."));
            return Task.FromResult<IReadOnlyList<DiagnosticResultRow>>(rows);
        }
    }

    private sealed class TransactionDiagnosticsProvider(AppSettings settings) : IDiagnosticsProvider
    {
        public string Category => "Transactions";
        public Task<IReadOnlyList<DiagnosticResultRow>> RunAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var snapshot = new TransactionHistoryService(settings).Load(cancellationToken);
                IReadOnlyList<DiagnosticResultRow> rows =
                [
                    snapshot.Diagnostics.Count == 0
                        ? Pass(Category, "Journal integrity", $"Loaded {snapshot.Rows.Count} transaction record(s) without malformed journals.")
                        : Warn(Category, "Journal integrity", $"Loaded {snapshot.Rows.Count} record(s); skipped {snapshot.Diagnostics.Count} malformed or unreadable journal(s).", "Review Transaction Center and the session log."),
                    Pass(Category, "Rollback metadata", $"{snapshot.Rows.Count(x => x.RollbackAvailable)} record(s) reference an available rollback backup.")
                ];
                return Task.FromResult(rows);
            }
            catch (Exception ex) { return Task.FromResult<IReadOnlyList<DiagnosticResultRow>>([Warn(Category, "Transaction history", ex.Message, "Open Transaction Center and refresh the history.")]); }
        }
    }

    private sealed class NotificationDiagnosticsProvider(ApplicationPathService paths) : IDiagnosticsProvider
    {
        public string Category => "Notifications";
        public Task<IReadOnlyList<DiagnosticResultRow>> RunAsync(CancellationToken cancellationToken = default)
        {
            var root = paths.LocalDataRoot;
            IReadOnlyList<DiagnosticResultRow> rows =
            [
                Directory.Exists(root) ? Pass(Category, "Storage", $"Notification storage root is accessible: {root}") : Warn(Category, "Storage", $"Storage root does not exist: {root}", "Open Notification Center once or verify application-data permissions."),
                Pass(Category, "Self-test availability", "Use Notification Center → Run Self-Test to verify visual delivery, pinning, clearing, and bell behavior.")
            ];
            return Task.FromResult(rows);
        }
    }
}
