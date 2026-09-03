using System.Diagnostics;
using System.Net;
using MystTiq.Core.Models;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed record DoctorCheck(string Component, string State, string Evidence, string Recommendation);
public sealed record DoctorReport(string Version, string Status, int Passed, int Warnings, int Failures, DateTimeOffset CheckedAt, IReadOnlyList<DoctorCheck> Checks);

public sealed class HeadlessDoctorService
{
    private readonly HeadlessConfiguration configuration;
    private readonly string configurationPath;
    private readonly IServerPathProfile paths;
    private readonly IServerLifecycleService lifecycle;
    private readonly ILinuxServiceManager? serviceManager;

    public HeadlessDoctorService(HeadlessConfiguration configuration, string configurationPath, IServerPathProfile paths, IServerLifecycleService lifecycle, ILinuxServiceManager? serviceManager)
    {
        this.configuration = configuration; this.configurationPath = configurationPath; this.paths = paths; this.lifecycle = lifecycle; this.serviceManager = serviceManager;
    }

    public async Task<DoctorReport> RunAsync(CancellationToken token)
    {
        var checks = new List<DoctorCheck>();
        void Add(string c,string s,string e,string rec) => checks.Add(new(c,s,e,rec));
        var validation = new HeadlessConfigurationService().Validate(configuration);
        Add("Configuration", validation.Valid ? "PASS" : "FAIL", validation.Valid ? $"Schema {configuration.SchemaVersion}; {configurationPath}" : string.Join("; ", validation.Errors), validation.Valid ? "No action required." : "Correct configuration errors and rerun Doctor.");
        Add("Server root", Directory.Exists(paths.ServerRoot) ? "PASS" : "FAIL", paths.ServerRoot, Directory.Exists(paths.ServerRoot) ? "No action required." : "Install or restore Palworld Dedicated Server at the configured server root.");
        Add("PalServer entry", File.Exists(paths.ServerExecutable) ? "PASS" : "FAIL", paths.ServerExecutable, File.Exists(paths.ServerExecutable) ? "No action required." : "Run the SteamCMD installation/update workflow and verify ServerRoot.");
        Add("SteamCMD", File.Exists(paths.SteamCmdExecutable) ? "PASS" : "FAIL", paths.SteamCmdExecutable, File.Exists(paths.SteamCmdExecutable) ? "No action required." : "Install SteamCMD at the configured path or correct SteamCmdPath.");
        Add("Backup root", Directory.Exists(paths.BackupRoot) ? "PASS" : "WARNING", paths.BackupRoot, Directory.Exists(paths.BackupRoot) ? "No action required." : "Create the backup directory and ensure MystTiq can write to it.");

        if (OperatingSystem.IsLinux())
        {
            if (serviceManager is null)
            {
                Add("systemd", "FAIL", "Linux service manager was not composed.", "Correct headless-host composition before production use.");
                return BuildReport(checks);
            }
            var service = await serviceManager.GetStatusAsync(token);
            var serviceOk = service.Installed && service.Enabled && service.State == LinuxServiceState.Active;
            Add("systemd", serviceOk ? "PASS" : "FAIL", $"installed={service.Installed}; enabled={service.Enabled}; state={service.ActiveState}/{service.SubState}", serviceOk ? "No action required." : "Run service-install --start-now and review journalctl -u mysttiq-palworld -b.");
            var snapshot = await lifecycle.GetStatusAsync(token);
            Add("PalServer readiness", snapshot.Ready ? "PASS" : "WARNING", snapshot.Detail, snapshot.Ready ? "No action required." : "If the server should be online, start it and verify UDP 8211 plus the PalServer process.");
            try { var drive=new DriveInfo(Path.GetPathRoot(paths.ServerRoot)!); var free=drive.AvailableFreeSpace/1024d/1024d/1024d; Add("Disk space", free>=5?"PASS":free>=2?"WARNING":"FAIL", $"{free:F1} GiB free on {drive.Name}", free>=5?"No action required.":"Free disk space before updates, backups, or save maintenance."); } catch(Exception ex) { Add("Disk space","WARNING",ex.Message,"Verify free disk space manually before maintenance."); }
            var hasFatalJournalEvents = await HasFatalJournalEventsAsync(token);
            Add("Current-boot fatal journal events", hasFatalJournalEvents ? "WARNING" : "PASS", hasFatalJournalEvents ? "Fatal/error-priority entries were returned for mysttiq-palworld.service." : "none detected", "Review journalctl -u mysttiq-palworld -b -p err if warnings are reported.");
        }

        if (configuration.Api.Enabled && IPAddress.TryParse(configuration.Api.BindAddress, out var bind))
        {
            var remote=!IPAddress.IsLoopback(bind); var secure=!remote || (configuration.Api.Authentication.Enabled && configuration.Api.Tls.Enabled);
            Add("Management API security", secure?"PASS":"FAIL", $"bind={configuration.Api.BindAddress}:{configuration.Api.Port}; auth={configuration.Api.Authentication.Enabled}; tls={configuration.Api.Tls.Enabled}", secure?"No action required.":"Remote management must use both bearer authentication and TLS.");
        }
        return BuildReport(checks);
    }

    private static DoctorReport BuildReport(IReadOnlyList<DoctorCheck> checks)
    {
        var failures=checks.Count(x=>x.State=="FAIL"); var warnings=checks.Count(x=>x.State=="WARNING");
        var reportVersion = typeof(HeadlessDoctorService).Assembly.GetName().Version?.ToString(3) ?? "unknown";
        return new DoctorReport(reportVersion, failures>0?"FAIL":warnings>0?"WARNING":"PASS", checks.Count-failures-warnings, warnings, failures, DateTimeOffset.UtcNow, checks);
    }

    private static async Task<bool> HasFatalJournalEventsAsync(CancellationToken token)
    {
        try { using var p=new Process { StartInfo=new ProcessStartInfo("journalctl", "-u mysttiq-palworld.service -b -p err --no-pager -q") { RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false } }; p.Start(); var output=await p.StandardOutput.ReadToEndAsync(token); await p.WaitForExitAsync(token); return !string.IsNullOrWhiteSpace(output); } catch { return false; }
    }
}
