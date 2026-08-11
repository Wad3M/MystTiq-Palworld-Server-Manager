using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using MystTiq.Core.Models;
using MystTiq.Core.Services;

var knownCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "probe", "status", "install-plan", "start", "stop", "restart",
    "service-status", "service-install", "service-uninstall", "service-run",
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

var defaults = ServerRuntimeConfiguration.CreateDefault();
var configuration = defaults with
{
    ServerRoot = GetOption("--server-root") ?? defaults.ServerRoot,
    SteamCmdPath = GetOption("--steamcmd") ?? defaults.SteamCmdPath,
    BackupRoot = GetOption("--backup-root") ?? defaults.BackupRoot,
    RuntimeRoot = GetOption("--runtime-root") ?? defaults.RuntimeRoot
};

var platform = ServerPlatformProfile.ForCurrentPlatform();
var paths = ServerPathProfile.ForCurrentPlatform(configuration);
var distribution = ServerDistributionPlatformService.ForCurrentPlatform(platform);

var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

switch (command.ToLowerInvariant())
{
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
            Console.WriteLine("This command remains informational in v0.3.0.2; it does not install or update the server.");
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
                "Lifecycle authority in v0.3.0.2 is implemented only for the experimental Linux headless host.");
            return (int)HeadlessExitCode.UnsupportedPlatform;
        }

        var sessionInspector = new LinuxServerSessionInspector(platform.GuardedPorts);
        var lifecycle = new LinuxServerLifecycleService(platform, paths, sessionInspector);
        var startupTimeout = TimeSpan.FromSeconds(GetIntOption("--startup-timeout-seconds", 90));
        var stopTimeout = TimeSpan.FromSeconds(GetIntOption("--stop-timeout-seconds", 30));

        var defaultServerArguments = new[]
        {
            "EpicApp=PalServer",
            "-useperfthreads",
            "-NoAsyncLoadingThread",
            "-UseMultithreadForDS"
        };

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
                var result = await serviceManager.InstallAsync(executable, serviceUser, startNow, cancellation.Token);
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
            TimeSpan.FromSeconds(GetIntOption("--service-poll-seconds", 5)),
            TimeSpan.FromSeconds(GetIntOption("--startup-timeout-seconds", 90)),
            TimeSpan.FromSeconds(GetIntOption("--stop-timeout-seconds", 30)),
            TimeSpan.FromSeconds(GetIntOption("--recovery-backoff-seconds", 10)),
            GetIntOption("--max-recovery-attempts", 5),
            TimeSpan.FromSeconds(GetIntOption("--recovery-window-seconds", 300)));

        var serverArguments = new[]
        {
            "EpicApp=PalServer",
            "-useperfthreads",
            "-NoAsyncLoadingThread",
            "-UseMultithreadForDS"
        };

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

        try
        {
            return await supervisor.RunAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await supervisor.StopManagedServerAsync(shutdownTimeout.Token);
            return 0;
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
    Console.WriteLine("MystTiq Headless Host v0.3.0.2");
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
    Console.WriteLine();
    Console.WriteLine("Options:");
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
