using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using MystTiq.Core.Models;
using MystTiq.Core.Services;
using MystTiq.HeadlessHost;

var knownCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "probe", "status", "install-plan", "start", "stop", "restart",
    "service-status", "service-install", "service-uninstall", "service-run",
    "config-show", "config-validate", "config-write-default", "config-migrate",
    "api-token-create", "api-tls-create", "api-remote-enable", "api-remote-disable", "api-run", "production-doctor",
    "help", "--help", "-h"
};
var command = args.FirstOrDefault(knownCommands.Contains) ?? "status";
var json = args.Any(argument => argument.Equals("--json", StringComparison.OrdinalIgnoreCase));

string? GetOption(string name)
{
    for (var index = 0; index < args.Length - 1; index++)
    {
        if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase))
            return args[index + 1];
    }
    return null;
}

int GetIntOption(string name, int fallback)
{
    var value = GetOption(name);
    return value is not null &&
           int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
           parsed > 0
        ? parsed
        : fallback;
}

if (command is "help" or "--help" or "-h")
{
    PrintHelp();
    return 0;
}

var configurationPath = GetOption("--config")
    ?? (OperatingSystem.IsLinux()
        ? HeadlessConfigurationService.LinuxDefaultPath
        : "mysttiq.json");
var configurationService = new HeadlessConfigurationService();

if (command.Equals("api-token-create", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var tokenPath = GetOption("--token-file") ?? "/etc/mysttiq/secrets/api-token";
        var overwrite = args.Any(argument => argument.Equals("--overwrite", StringComparison.OrdinalIgnoreCase));
        var secrets = new HeadlessSecretFileService();
        var token = secrets.GenerateBearerToken();
        secrets.WriteSecret(tokenPath, token, overwrite);
        Console.WriteLine($"MystTiq API bearer token written: {tokenPath}");
        Console.WriteLine("Token value is stored only in the protected token file; use the file when constructing authorized requests.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unable to create API token: {ex.Message}");
        return 1;
    }
}

if (command.Equals("api-tls-create", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var certificatePath = GetOption("--certificate-file") ?? "/etc/mysttiq/certs/mysttiq.pfx";
        var passwordFile = GetOption("--certificate-password-file")
            ?? "/etc/mysttiq/secrets/certificate-password";
        var bindAddress = GetOption("--bind-address")
            ?? throw new ArgumentException("--bind-address is required for api-tls-create.");
        var dnsName = GetOption("--dns-name");
        var overwrite = args.Any(argument =>
            argument.Equals("--overwrite", StringComparison.OrdinalIgnoreCase));

        var result = new HeadlessCertificateService().CreateSelfSignedServerCertificate(
            certificatePath,
            passwordFile,
            bindAddress,
            dnsName,
            overwrite);

        Console.WriteLine("MystTiq TLS certificate created.");
        Console.WriteLine($"Certificate     : {result.CertificatePath}");
        Console.WriteLine($"Password file   : {result.PasswordFile}");
        Console.WriteLine($"Subject         : {result.Subject}");
        Console.WriteLine($"Valid from      : {result.NotBefore:O}");
        Console.WriteLine($"Valid until     : {result.NotAfter:O}");
        Console.WriteLine($"Thumbprint      : {result.Thumbprint}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unable to create TLS certificate: {ex.Message}");
        return 1;
    }
}

if (command.Equals("config-write-default", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var overwrite = args.Any(argument => argument.Equals("--overwrite", StringComparison.OrdinalIgnoreCase));
        configurationService.WriteDefault(configurationPath, overwrite);
        Console.WriteLine($"Default MystTiq configuration written: {configurationPath}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unable to write configuration: {ex.Message}");
        return 1;
    }
}

HeadlessConfiguration headlessConfiguration;

try
{
    headlessConfiguration = configurationService.LoadOrDefault(configurationPath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Configuration load failed: {ex.Message}");
    return (int)HeadlessExitCode.InvalidArguments;
}

var configurationValidation = configurationService.Validate(headlessConfiguration);
if (!configurationValidation.Valid)
{
    Console.Error.WriteLine($"Configuration validation failed: {configurationPath}");
    foreach (var error in configurationValidation.Errors)
        Console.Error.WriteLine($"  - {error}");
    return (int)HeadlessExitCode.InvalidArguments;
}

var defaults = HeadlessConfigurationService.ToRuntimeConfiguration(headlessConfiguration);
var runtimeConfiguration = defaults with
{
    ServerRoot = GetOption("--server-root") ?? defaults.ServerRoot,
    SteamCmdPath = GetOption("--steamcmd") ?? defaults.SteamCmdPath,
    BackupRoot = GetOption("--backup-root") ?? defaults.BackupRoot,
    RuntimeRoot = GetOption("--runtime-root") ?? defaults.RuntimeRoot
};

var platform = ServerPlatformProfile.ForCurrentPlatform();
var paths = ServerPathProfile.ForCurrentPlatform(runtimeConfiguration);
var distribution = ServerDistributionPlatformService.ForCurrentPlatform(platform);

var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

switch (command.ToLowerInvariant())
{
    case "config-migrate":
    {
        try
        {
            if (!configurationService.NeedsMigration(configurationPath))
            {
                Console.WriteLine($"Configuration already uses schema {HeadlessConfiguration.CurrentSchemaVersion}: {configurationPath}");
                return 0;
            }
            var migrated = configurationService.MigrateFile(configurationPath);
            Console.WriteLine($"Configuration migrated to schema {migrated.SchemaVersion}: {configurationPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Configuration migration failed: {ex.Message}");
            return 1;
        }
    }

    case "api-remote-enable":
    {
        try
        {
            var bindAddress = GetOption("--bind-address")
                ?? throw new ArgumentException("--bind-address is required.");
            var port = GetIntOption("--api-port", headlessConfiguration.Api.Port);
            var tokenFile = GetOption("--token-file")
                ?? headlessConfiguration.Api.Authentication.TokenFile;
            var certificateFile = GetOption("--certificate-file")
                ?? headlessConfiguration.Api.Tls.CertificatePath;
            var certificatePasswordFile = GetOption("--certificate-password-file")
                ?? headlessConfiguration.Api.Tls.CertificatePasswordFile;

            var updated = new HeadlessRemoteApiEnrollmentService(configurationService)
                .EnableRemoteApi(
                    configurationPath,
                    bindAddress,
                    port,
                    tokenFile,
                    certificateFile,
                    certificatePasswordFile);

            Console.WriteLine("MystTiq remote API configuration enabled.");
            Console.WriteLine($"Bind            : {updated.Api.BindAddress}:{updated.Api.Port}");
            Console.WriteLine($"Authentication  : {updated.Api.Authentication.Enabled}");
            Console.WriteLine($"TLS             : {updated.Api.Tls.Enabled}");
            Console.WriteLine($"Config          : {configurationPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unable to enable remote API: {ex.Message}");
            return 1;
        }
    }

    case "api-remote-disable":
    {
        try
        {
            var updated = new HeadlessRemoteApiEnrollmentService(configurationService)
                .DisableRemoteApi(configurationPath);

            Console.WriteLine("MystTiq remote API configuration disabled.");
            Console.WriteLine($"Bind            : {updated.Api.BindAddress}:{updated.Api.Port}");
            Console.WriteLine($"Authentication  : {updated.Api.Authentication.Enabled}");
            Console.WriteLine($"TLS             : {updated.Api.Tls.Enabled}");
            Console.WriteLine($"Config          : {configurationPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unable to disable remote API: {ex.Message}");
            return 1;
        }
    }

    case "config-show":
    {
        Console.WriteLine(JsonSerializer.Serialize(headlessConfiguration, JsonOptions()));
        return 0;
    }

    case "config-validate":
    {
        if (configurationValidation.Valid)
        {
            Console.WriteLine($"Configuration valid: {configurationPath}");
            return 0;
        }

        foreach (var error in configurationValidation.Errors)
            Console.Error.WriteLine(error);
        return (int)HeadlessExitCode.InvalidArguments;
    }

    case "production-doctor":
    {
        var checks = new List<object>();
        var failures = 0;
        var warnings = 0;

        void Add(string component, string state, string evidence, string recommendation)
        {
            checks.Add(new { component, state, evidence, recommendation });
            if (state == "FAIL") failures++;
            else if (state == "WARNING") warnings++;
        }

        Add("Configuration", configurationValidation.Valid ? "PASS" : "FAIL",
            configurationValidation.Valid ? $"Schema {headlessConfiguration.SchemaVersion}; {configurationPath}" : string.Join("; ", configurationValidation.Errors),
            configurationValidation.Valid ? "No action required." : "Correct the reported configuration errors, then rerun production-doctor.");

        Add("Server root", Directory.Exists(paths.ServerRoot) ? "PASS" : "FAIL",
            paths.ServerRoot,
            Directory.Exists(paths.ServerRoot) ? "No action required." : "Install or restore Palworld Dedicated Server at the configured server root.");

        Add("PalServer entry", File.Exists(paths.ServerExecutable) ? "PASS" : "FAIL",
            paths.ServerExecutable,
            File.Exists(paths.ServerExecutable) ? "No action required." : "Run the SteamCMD installation/update workflow and verify ServerRoot.");

        Add("SteamCMD", File.Exists(paths.SteamCmdExecutable) ? "PASS" : "FAIL",
            paths.SteamCmdExecutable,
            File.Exists(paths.SteamCmdExecutable) ? "No action required." : "Install SteamCMD at the configured path or correct SteamCmdPath.");

        Add("Backup root", Directory.Exists(paths.BackupRoot) ? "PASS" : "WARNING",
            paths.BackupRoot,
            Directory.Exists(paths.BackupRoot) ? "No action required." : "Create the backup directory and ensure the MystTiq service account can write to it.");

        if (OperatingSystem.IsLinux())
        {
            var serviceManager = new LinuxSystemdServiceManager(paths);
            var serviceStatus = await serviceManager.GetStatusAsync(cancellation.Token);
            Add("systemd", serviceStatus.Installed && serviceStatus.Enabled && serviceStatus.State == LinuxServiceState.Active ? "PASS" : "FAIL",
                $"installed={serviceStatus.Installed}; enabled={serviceStatus.Enabled}; state={serviceStatus.ActiveState}/{serviceStatus.SubState}",
                serviceStatus.Installed && serviceStatus.Enabled && serviceStatus.State == LinuxServiceState.Active
                    ? "No action required."
                    : "Run service-install --start-now and review journalctl -u mysttiq-palworld -b.");

            var sessionInspector = new LinuxServerSessionInspector(platform.GuardedPorts);
            var lifecycleService = new LinuxServerLifecycleService(platform, paths, sessionInspector);
            var snapshot = await lifecycleService.GetStatusAsync(cancellation.Token);
            Add("PalServer readiness", snapshot.Ready ? "PASS" : "WARNING",
                snapshot.Detail,
                snapshot.Ready ? "No action required." : "If the server should be online, run start and verify UDP 8211 plus the PalServer process.");

            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(paths.ServerRoot)!);
                var freeGiB = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
                Add("Disk space", freeGiB >= 5 ? "PASS" : freeGiB >= 2 ? "WARNING" : "FAIL",
                    $"{freeGiB:F1} GiB free on {drive.Name}",
                    freeGiB >= 5 ? "No action required." : "Free disk space before updates, backups, or save maintenance.");
            }
            catch (Exception ex)
            {
                Add("Disk space", "WARNING", ex.Message, "Verify free disk space manually before maintenance.");
            }
        }

        if (headlessConfiguration.Api.Enabled)
        {
            var remote = !System.Net.IPAddress.IsLoopback(System.Net.IPAddress.Parse(headlessConfiguration.Api.BindAddress));
            Add("Management API security",
                !remote || (headlessConfiguration.Api.Authentication.Enabled && headlessConfiguration.Api.Tls.Enabled) ? "PASS" : "FAIL",
                $"bind={headlessConfiguration.Api.BindAddress}:{headlessConfiguration.Api.Port}; auth={headlessConfiguration.Api.Authentication.Enabled}; tls={headlessConfiguration.Api.Tls.Enabled}",
                !remote || (headlessConfiguration.Api.Authentication.Enabled && headlessConfiguration.Api.Tls.Enabled)
                    ? "No action required."
                    : "Remote management must use both bearer authentication and TLS.");
        }

        var result = new
        {
            version = "0.3.0.7",
            status = failures > 0 ? "FAIL" : warnings > 0 ? "WARNING" : "PASS",
            passed = checks.Count - failures - warnings,
            warnings,
            failures,
            checkedAt = DateTimeOffset.UtcNow,
            checks
        };

        if (json)
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions()));
        else
        {
            Console.WriteLine("MystTiq Production Doctor — v0.3.0.7");
            foreach (dynamic check in checks)
            {
                Console.WriteLine($"[{check.state}] {check.component}");
                Console.WriteLine($"  Evidence       : {check.evidence}");
                Console.WriteLine($"  Recommendation : {check.recommendation}");
            }
            Console.WriteLine($"Summary: {result.status}; {result.passed} passed, {warnings} warning(s), {failures} failure(s).");
        }
        return failures > 0 ? 1 : 0;
    }

    case "probe":
    {
        var result = new HeadlessProbeService(platform, paths).Probe();
        if (json)
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions()));
        else
            PrintProbe(result);
        return 0;
    }

    case "install-plan":
    {
        var installArguments = distribution.BuildPalworldServerInstallArguments(paths.ServerRoot, validate: true);
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                distribution.PlatformId,
                distribution.SteamCmdExecutableName,
                PackageUri = distribution.SteamCmdPackageUri.ToString(),
                Executable = paths.SteamCmdExecutable,
                Arguments = installArguments
            }, JsonOptions()));
        }
        else
        {
            Console.WriteLine("MystTiq headless SteamCMD install plan");
            Console.WriteLine($"Platform : {distribution.PlatformId}");
            Console.WriteLine($"SteamCMD : {paths.SteamCmdExecutable}");
            Console.WriteLine($"Package  : {distribution.SteamCmdPackageUri}");
            Console.WriteLine("Arguments:");
            foreach (var argument in installArguments)
                Console.WriteLine($"  {argument}");
            Console.WriteLine();
            Console.WriteLine("This command remains informational in v0.3.0.7; it does not install or update the server.");
        }
        return 0;
    }

    case "status":
    case "start":
    case "stop":
    case "restart":
    {
        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine(
                "Lifecycle authority in v0.3.0.7 is implemented only for the Linux headless host.");
            return (int)HeadlessExitCode.UnsupportedPlatform;
        }

        var sessionInspector = new LinuxServerSessionInspector(platform.GuardedPorts);
        var lifecycle = new LinuxServerLifecycleService(platform, paths, sessionInspector);
        var startupTimeout = TimeSpan.FromSeconds(
            GetIntOption("--startup-timeout-seconds", headlessConfiguration.Lifecycle.StartupTimeoutSeconds));
        var stopTimeout = TimeSpan.FromSeconds(
            GetIntOption("--stop-timeout-seconds", headlessConfiguration.Lifecycle.StopTimeoutSeconds));

        var defaultServerArguments = headlessConfiguration.Server.LaunchArguments;

        try
        {
            if (command.Equals("status", StringComparison.OrdinalIgnoreCase))
            {
                var snapshot = await lifecycle.GetStatusAsync(cancellation.Token);
                if (json)
                    Console.WriteLine(JsonSerializer.Serialize(snapshot, JsonOptions()));
                else
                    PrintLifecycle(snapshot);

                return snapshot.CrashDetected
                    ? (int)HeadlessExitCode.CrashDetected
                    : 0;
            }

            ServerLifecycleOperationResult result = command.ToLowerInvariant() switch
            {
                "start" => await lifecycle.StartAsync(
                    defaultServerArguments,
                    startupTimeout,
                    cancellation.Token),
                "stop" => await lifecycle.StopAsync(
                    stopTimeout,
                    cancellation.Token),
                "restart" => await lifecycle.RestartAsync(
                    defaultServerArguments,
                    startupTimeout,
                    stopTimeout,
                    cancellation.Token),
                _ => throw new InvalidOperationException("Unexpected lifecycle command.")
            };

            if (json)
                Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions()));
            else
                PrintOperation(command, result);

            return (int)result.ExitCode;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 130;
        }
    }


    case "api-run":
    {
        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine("Local API host in v0.3.0.7 requires Linux.");
            return (int)HeadlessExitCode.UnsupportedPlatform;
        }

        var sessionInspector = new LinuxServerSessionInspector(platform.GuardedPorts);
        var lifecycle = new LinuxServerLifecycleService(platform, paths, sessionInspector);
        var serviceManager = new LinuxSystemdServiceManager(paths);

        await using var apiHost = LocalManagementApiHost.Create(
            headlessConfiguration,
            lifecycle,
            serviceManager);

        using var termRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            cancellation.Cancel();
        });

        try
        {
            await apiHost.StartAsync(cancellation.Token);
            Console.WriteLine(
                $"MystTiq local management API listening on http://{headlessConfiguration.Api.BindAddress}:{headlessConfiguration.Api.Port}");
            await apiHost.WaitForShutdownAsync(cancellation.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await apiHost.StopAsync(shutdownTimeout.Token);
            return 0;
        }
    }

    case "service-status":
    case "service-install":
    case "service-uninstall":
    case "service-run":
    {
        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine("Linux service commands require the experimental Linux headless host.");
            return (int)HeadlessExitCode.UnsupportedPlatform;
        }

        var sessionInspector = new LinuxServerSessionInspector(platform.GuardedPorts);
        var lifecycle = new LinuxServerLifecycleService(platform, paths, sessionInspector);
        var serviceManager = new LinuxSystemdServiceManager(paths);

        if (command.Equals("service-status", StringComparison.OrdinalIgnoreCase))
        {
            var serviceStatus = await serviceManager.GetStatusAsync(cancellation.Token);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(serviceStatus, JsonOptions()));
            else
                PrintServiceStatus(serviceStatus);
            return serviceStatus.State == LinuxServiceState.Failed ? 1 : 0;
        }

        if (command.Equals("service-install", StringComparison.OrdinalIgnoreCase))
        {
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("Unable to determine the current headless-host executable path.");
            var serviceUser = GetOption("--service-user")
                ?? Environment.GetEnvironmentVariable("SUDO_USER")
                ?? Environment.GetEnvironmentVariable("USER")
                ?? "mystroth";
            var startNow = args.Any(argument => argument.Equals("--start-now", StringComparison.OrdinalIgnoreCase));

            try
            {
                if (!File.Exists(configurationPath))
                {
                    configurationService.WriteDefault(configurationPath);
                    Console.WriteLine($"Created default configuration: {configurationPath}");
                }
                else if (configurationService.NeedsMigration(configurationPath))
                {
                    var migrated = configurationService.MigrateFile(configurationPath);
                    Console.WriteLine($"Migrated configuration to schema {migrated.SchemaVersion}: {configurationPath}");
                }

                var result = await serviceManager.InstallAsync(
                    executable,
                    serviceUser,
                    configurationPath,
                    startNow,
                    cancellation.Token);
                if (json)
                    Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions()));
                else
                {
                    Console.WriteLine("MystTiq Headless Host — systemd install");
                    Console.WriteLine($"Unit            : {result.UnitName}");
                    Console.WriteLine($"Executable      : {result.InstalledExecutable}");
                    Console.WriteLine($"Unit path       : {result.UnitPath}");
                    Console.WriteLine($"Enabled         : {result.Enabled}");
                    Console.WriteLine($"Started         : {result.Started}");
                    Console.WriteLine($"Message         : {result.Message}");
                }
                return result.Success ? 0 : 1;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine("Run service-install with sudo.");
                return 1;
            }
        }

        if (command.Equals("service-uninstall", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var removed = await serviceManager.UninstallAsync(cancellation.Token);
                Console.WriteLine(removed
                    ? "MystTiq systemd service removed."
                    : "MystTiq systemd service could not be completely removed.");
                return removed ? 0 : 1;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine("Run service-uninstall with sudo.");
                return 1;
            }
        }

        var supervisorOptions = new LinuxServiceSupervisorOptions(
            TimeSpan.FromSeconds(GetIntOption("--service-poll-seconds", headlessConfiguration.Lifecycle.ServicePollSeconds)),
            TimeSpan.FromSeconds(GetIntOption("--startup-timeout-seconds", headlessConfiguration.Lifecycle.StartupTimeoutSeconds)),
            TimeSpan.FromSeconds(GetIntOption("--stop-timeout-seconds", headlessConfiguration.Lifecycle.StopTimeoutSeconds)),
            TimeSpan.FromSeconds(GetIntOption("--recovery-backoff-seconds", headlessConfiguration.Lifecycle.RecoveryBackoffSeconds)),
            GetIntOption("--max-recovery-attempts", headlessConfiguration.Lifecycle.MaximumRecoveryAttempts),
            TimeSpan.FromSeconds(GetIntOption("--recovery-window-seconds", headlessConfiguration.Lifecycle.RecoveryWindowSeconds)));

        var serverArguments = headlessConfiguration.Server.LaunchArguments;
        var supervisor = new LinuxHeadlessSupervisor(lifecycle, supervisorOptions, serverArguments);

        using var termRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            cancellation.Cancel();
        });
        using var intRegistration = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
        {
            context.Cancel = true;
            cancellation.Cancel();
        });

        LocalManagementApiHost? apiHost = null;
        try
        {
            if (headlessConfiguration.Api.Enabled)
            {
                apiHost = LocalManagementApiHost.Create(headlessConfiguration, lifecycle, serviceManager);
                await apiHost.StartAsync(cancellation.Token);
                Console.WriteLine(
                    $"MystTiq local management API listening on http://{headlessConfiguration.Api.BindAddress}:{headlessConfiguration.Api.Port}");
            }

            return await supervisor.RunAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            if (apiHost is not null)
                await apiHost.StopAsync(shutdownTimeout.Token);
            await supervisor.StopManagedServerAsync(shutdownTimeout.Token);
            return 0;
        }
        finally
        {
            if (apiHost is not null)
                await apiHost.DisposeAsync();
        }
    }

    default:
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return (int)HeadlessExitCode.InvalidArguments;
}

static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true };

static void PrintProbe(HeadlessProbeResult result)
{
    Console.WriteLine("MystTiq Headless Host — platform probe");
    Console.WriteLine($"Platform        : {result.PlatformId}");
    if (result.LinuxDistribution is not null)
    {
        Console.WriteLine($"Distribution    : {result.LinuxDistribution.PrettyName}");
        Console.WriteLine($"Version ID      : {result.LinuxDistribution.VersionId}");
        Console.WriteLine($"Kernel          : {result.LinuxDistribution.Kernel}");
        Console.WriteLine($"Architecture    : {result.LinuxDistribution.Architecture}");
    }
    Console.WriteLine($"Server root     : {result.Paths.ServerRoot}");
    Console.WriteLine($"Server entry    : {result.Paths.ServerExecutable}");
    Console.WriteLine($"Server exists   : {result.ServerExecutableExists}");
    Console.WriteLine($"SteamCMD        : {result.Paths.SteamCmdExecutable}");
    Console.WriteLine($"SteamCMD exists : {result.SteamCmdExists}");
    Console.WriteLine($"Save root       : {result.Paths.SaveRoot}");
    Console.WriteLine($"Config root     : {result.Paths.ConfigRoot}");
    Console.WriteLine($"Logs root       : {result.Paths.LogsRoot}");
    Console.WriteLine($"Backup root     : {result.Paths.BackupRoot}");
    Console.WriteLine($"Runtime root    : {result.Paths.ManagerRuntimeRoot}");
    Console.WriteLine($"Server processes: {result.ServerProcesses.Count}");
    foreach (var process in result.ServerProcesses)
        Console.WriteLine($"  PID {process.ProcessId} {process.ProcessName} {process.ExecutablePath}");
    Console.WriteLine($"Guarded ports   : {(result.GuardedListeningPorts.Count == 0 ? "none" : string.Join(", ", result.GuardedListeningPorts))}");
}

static void PrintLifecycle(ServerLifecycleSnapshot snapshot)
{
    Console.WriteLine("MystTiq Headless Host — server lifecycle");
    Console.WriteLine($"State           : {snapshot.Phase}");
    Console.WriteLine($"Ready           : {snapshot.Ready}");
    Console.WriteLine($"Crash detected  : {snapshot.CrashDetected}");
    Console.WriteLine($"Native PID      : {(snapshot.NativeProcessId?.ToString() ?? "none")}");
    Console.WriteLine($"Server processes: {snapshot.Processes.Count}");
    foreach (var process in snapshot.Processes)
        Console.WriteLine($"  PID {process.ProcessId} {process.ProcessName} {process.ExecutablePath}");
    Console.WriteLine($"Guarded ports   : {(snapshot.GuardedListeningPorts.Count == 0 ? "none" : string.Join(", ", snapshot.GuardedListeningPorts))}");
    Console.WriteLine($"Last transition : {(snapshot.LastTransitionAt?.ToString("O") ?? "unknown")}");
    Console.WriteLine($"Detail          : {snapshot.Detail}");
}

static void PrintOperation(string command, ServerLifecycleOperationResult result)
{
    Console.WriteLine($"MystTiq Headless Host — {command}");
    Console.WriteLine($"Result          : {result.ExitCode}");
    Console.WriteLine($"State           : {result.Snapshot.Phase}");
    Console.WriteLine($"Ready           : {result.Snapshot.Ready}");
    Console.WriteLine($"Native PID      : {(result.Snapshot.NativeProcessId?.ToString() ?? "none")}");
    Console.WriteLine($"Forced          : {result.Forced}");
    Console.WriteLine($"Guarded ports   : {(result.Snapshot.GuardedListeningPorts.Count == 0 ? "none" : string.Join(", ", result.Snapshot.GuardedListeningPorts))}");
    Console.WriteLine($"Message         : {result.Message}");
}


static void PrintServiceStatus(LinuxServiceStatus status)
{
    Console.WriteLine("MystTiq Headless Host — systemd service");
    Console.WriteLine($"Unit            : {status.UnitName}");
    Console.WriteLine($"Installed       : {status.Installed}");
    Console.WriteLine($"Enabled         : {status.Enabled}");
    Console.WriteLine($"State           : {status.State}");
    Console.WriteLine($"Active state    : {status.ActiveState}");
    Console.WriteLine($"Sub state       : {status.SubState}");
    Console.WriteLine($"Main PID        : {(status.MainProcessId?.ToString() ?? "none")}");
    Console.WriteLine($"Detail          : {status.Detail}");
}

static void PrintHelp()
{
    Console.WriteLine("MystTiq Headless Host v0.3.0.7");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  probe          Detect platform, distro, paths, PalServer processes and guarded ports.");
    Console.WriteLine("  status         Show persisted + observed Linux PalServer lifecycle state.");
    Console.WriteLine("  start          Start PalServer headlessly; blocks duplicate starts and verifies UDP 8211.");
    Console.WriteLine("  stop           Send SIGTERM first; escalate to SIGKILL only after the shutdown timeout.");
    Console.WriteLine("  restart        Stop safely when needed, then start and verify PalServer.");
    Console.WriteLine("  install-plan   Show the platform-specific SteamCMD plan without executing it.");
    Console.WriteLine("  service-status Show MystTiq systemd installation/runtime state.");
    Console.WriteLine("  service-install Install/enable MystTiq under systemd (requires sudo).");
    Console.WriteLine("  service-uninstall Stop/disable/remove MystTiq systemd unit (requires sudo).");
    Console.WriteLine("  service-run    Long-running supervisor used by systemd.");
    Console.WriteLine("  api-run        Run the management API without systemd.");
    Console.WriteLine("  config-show    Print the effective headless configuration.");
    Console.WriteLine("  config-validate Validate the effective headless configuration.");
    Console.WriteLine("  config-write-default Write a default configuration file.");
    Console.WriteLine("  config-migrate Persist an older supported config in the current schema.");
    Console.WriteLine("  api-token-create Generate a protected bearer-token file.");
    Console.WriteLine("  api-tls-create Generate a self-signed TLS server certificate.");
    Console.WriteLine("  api-remote-enable Explicitly enable authenticated + TLS remote API binding.");
    Console.WriteLine("  api-remote-disable Return API binding to the safe loopback default.");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --config <path>                  Headless JSON configuration path.");
    Console.WriteLine("  --token-file <path>              API bearer-token secret path.");
    Console.WriteLine("  --certificate-file <path>        TLS PFX certificate path.");
    Console.WriteLine("  --certificate-password-file <path> TLS certificate-password secret path.");
    Console.WriteLine("  --bind-address <ip>              Literal API/certificate bind IP.");
    Console.WriteLine("  --api-port <n>                   Management API port.");
    Console.WriteLine("  --dns-name <name>                Optional DNS SAN for generated TLS certificate.");
    Console.WriteLine("  --overwrite                      Allow config-write-default to replace an existing file.");
    Console.WriteLine("  --server-root <path>             Override PalServer root.");
    Console.WriteLine("  --steamcmd <path>                Override SteamCMD executable.");
    Console.WriteLine("  --backup-root <path>             Override backup root.");
    Console.WriteLine("  --runtime-root <path>            Override MystTiq lifecycle/log state root.");
    Console.WriteLine("  --startup-timeout-seconds <n>    Startup verification timeout (default 90).");
    Console.WriteLine("  --stop-timeout-seconds <n>       Graceful SIGTERM timeout (default 30).");
    Console.WriteLine("  --service-user <user>             User account for the installed systemd service.");
    Console.WriteLine("  --start-now                       Start/restart the service during service-install.");
    Console.WriteLine("  --service-poll-seconds <n>        Supervisor poll interval (default 5).");
    Console.WriteLine("  --recovery-backoff-seconds <n>    Delay before crash recovery (default 10).");
    Console.WriteLine("  --max-recovery-attempts <n>       Maximum recoveries per window (default 5).");
    Console.WriteLine("  --recovery-window-seconds <n>     Recovery throttle window (default 300).");
    Console.WriteLine("  --json                            Emit JSON.");
}
