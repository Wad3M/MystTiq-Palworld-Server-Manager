using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class ServerDoctorService(AppSettings settings)
{
    public async Task<IReadOnlyList<DoctorCheckRow>> RunAsync(ServerService server, CancellationToken token)
    {
        var rows = new List<DoctorCheckRow>();
        void Add(string component, bool ok, string detail, string recommendation = "") => rows.Add(new DoctorCheckRow
        {
            Component = component, Status = ok ? "Healthy" : "Attention", Detail = detail, Recommendation = recommendation
        });

        Add("Server files", File.Exists(settings.ServerExe),
            File.Exists(settings.ServerExe) ? "PalServer.exe is present." : $"Missing: {settings.ServerExe}",
            "Use Server Setup to install or verify Palworld Dedicated Server.");
        Add("SteamCMD", File.Exists(settings.SteamCmdPath),
            File.Exists(settings.SteamCmdPath) ? "SteamCMD is available." : $"Missing: {settings.SteamCmdPath}",
            "Install SteamCMD or correct the configured path.");
        Add("Configuration", File.Exists(settings.ConfigFile),
            File.Exists(settings.ConfigFile) ? "PalWorldSettings.ini is present." : "PalWorldSettings.ini was not found.",
            "Start the server once or restore the configuration file.");

        var health = server.GetHealthSnapshot();
        var processHealthy = health.State is not ServerLifecycleState.Hung and not ServerLifecycleState.Crashed;
        Add("Process state", processHealthy, $"{health.State}: {health.Detail}",
            "Use Scan Processes / Force Cleanup if Palworld is hung or orphaned.");

        var win64 = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64");
        var ue4ssResolver = new Ue4ssRuntimeResolver(settings);
        var ue4ssInfo = ue4ssResolver.Resolve();
        var ue4ss = File.Exists(Path.Combine(win64, "UE4SS.dll")) ||
                    File.Exists(Path.Combine(ue4ssInfo.Ue4ssRoot, "UE4SS.dll")) ||
                    Directory.Exists(ue4ssInfo.Ue4ssRoot);
        Add("UE4SS", ue4ss && !ue4ssInfo.HasPathMismatch,
            !ue4ss ? "UE4SS is not installed or could not be detected."
            : ue4ssInfo.HasPathMismatch ? ue4ssInfo.WarningMessage
            : $"UE4SS runtime detected. Active Mods Root: {ue4ssInfo.ActiveModsRoot}",
            ue4ssInfo.HasPathMismatch ? "Resolve the UE4SS Mods Root mismatch before managing runtime mods." : "Install UE4SS only if your enabled mods require it.");

        var modService = new ModService(settings, ue4ssResolver);
        var installedMods = modService.Scan();
        var enabledMods = installedMods.Count(m => m.Enabled);
        var compatibility = new ModCompatibilityService(settings).Scan(installedMods);
        var modHealthy = compatibility.Conflicts == 0 && compatibility.MissingDependencies == 0;
        Add("Mods", modHealthy, $"{installedMods.Count} installed, {enabledMods} enabled, {compatibility.Conflicts} conflicts, {compatibility.MissingDependencies} missing dependencies.",
            "Open MOD Dashboard and run Verify All / Scan Compatibility.");

        var backupFolderAvailable = Directory.Exists(settings.BackupRoot);
        if (!backupFolderAvailable)
        {
            Add("Backups", false, "Backup folder does not exist.",
                "Create the backup folder or update Manager Settings.");
        }
        else
        {
            var backupRows = new BackupService(settings).List();
            var latestBackup = backupRows.FirstOrDefault();
            var verifiedCount = backupRows.Count(row => row.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase));
            var backupHealthy = latestBackup is not null &&
                                latestBackup.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase);
            Add("Backups", backupHealthy,
                latestBackup is null
                    ? "Backup storage is writable, but no world backup exists yet."
                    : $"{backupRows.Count} backup(s), {verifiedCount} verified. Latest: {latestBackup.Created:g} ({latestBackup.Status}).",
                backupHealthy
                    ? "Latest backup has a verification manifest."
                    : "Open Backups and verify the latest archive or create a new verified backup.");
        }

        var worldOverride = FindWorldOptionOverride(settings.SaveRoot);
        Add("World settings override", worldOverride is null,
            worldOverride is null
                ? "No WorldOption.sav override was detected in the active SaveGames tree."
                : "WorldOption.sav detected: " + worldOverride,
            worldOverride is null
                ? "PalWorldSettings.ini should be authoritative for normal dedicated-server settings."
                : "This is common with imported/downloaded worlds and can override passwords and other INI settings. Stop PalServer, then use Backups > CHECK WORLD OVERRIDE or RCON Doctor to back up and disable it.");

        var apiReachable = await CanConnectToApiAsync(settings.ApiBaseUrl, token);
        Add("REST API port", !server.IsRunning() || apiReachable,
            server.IsRunning() ? (apiReachable ? "REST API TCP endpoint is reachable." : "Server is running but the REST API TCP endpoint is not reachable.") : "Server is stopped; REST port check deferred.",
            "Verify RESTAPIEnabled, RESTAPIPort, Windows Firewall, and the configured API URL, then restart the server.");

        if (server.IsRunning() && apiReachable)
        {
            var iniPassword = new ConfigService(settings).TryReadAdminPassword();
            var managerPassword = settings.GetPassword();
            try
            {
                using var api = new ApiClient(settings);
                using var info = await api.GetAsync("info", token);
                Add("REST authentication", true,
                    $"Authenticated successfully. PalWorldSettings.ini password is {(string.IsNullOrWhiteSpace(iniPassword) ? "EMPTY" : "SET")}; MystTiq credential is {(string.IsNullOrWhiteSpace(managerPassword) ? "EMPTY" : "SET")}.",
                    "REST credentials are accepted by the running server.");
            }
            catch (Exception ex)
            {
                var authFailure = IsRestAuthenticationFailure(ex);
                var serverSaysEmpty = ex.ToString().Contains("AdminPassword is empty", StringComparison.OrdinalIgnoreCase);
                var detail = authFailure
                    ? $"REST endpoint is reachable but authentication failed{(serverSaysEmpty ? ": the running server reports AdminPassword is empty" : string.Empty)}. INI password: {(string.IsNullOrWhiteSpace(iniPassword) ? "EMPTY" : "SET")}; MystTiq credential: {(string.IsNullOrWhiteSpace(managerPassword) ? "EMPTY" : "SET")}; WorldOption.sav: {(worldOverride is null ? "not detected" : "DETECTED")}."
                    : "REST endpoint is reachable but the authenticated info request failed: " + ex.Message;
                Add("REST authentication", false, detail,
                    worldOverride is not null
                        ? "Stop PalServer and back up/disable WorldOption.sav, then restart and retest."
                        : "Synchronize the MystTiq REST password with AdminPassword in PalWorldSettings.ini, restart PalServer, and retest.");
            }
        }
        else if (server.IsRunning())
        {
            Add("REST authentication", false, "REST authentication could not be tested because the REST TCP endpoint is not reachable.", "Fix REST API reachability first, then rerun Server Doctor.");
        }
        else
        {
            Add("REST authentication", true, "Server is stopped; authenticated REST check deferred.", "Start PalServer to test the effective runtime credential.");
        }

        return rows;
    }

    private static string? FindWorldOptionOverride(string saveRoot)
    {
        try
        {
            if (!Directory.Exists(saveRoot)) return null;
            return Directory.EnumerateFiles(saveRoot, "WorldOption.sav", SearchOption.AllDirectories).FirstOrDefault();
        }
        catch { return null; }
    }

    private static bool IsRestAuthenticationFailure(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("401", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("AdminPassword is empty", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> CanConnectToApiAsync(string url, CancellationToken token)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(800));
            await client.ConnectAsync(uri.Host, uri.Port, timeout.Token);
            return client.Connected;
        }
        catch { return false; }
    }
}
