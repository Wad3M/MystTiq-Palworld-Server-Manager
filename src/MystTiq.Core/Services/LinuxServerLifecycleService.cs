using System.Diagnostics;
using System.Runtime.Versioning;
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

public interface IServerLifecycleService
{
    Task<ServerLifecycleSnapshot> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<ServerLifecycleOperationResult> StartAsync(
        IReadOnlyList<string> serverArguments,
        TimeSpan startupTimeout,
        CancellationToken cancellationToken = default);
    Task<ServerLifecycleOperationResult> StopAsync(
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default);
    Task<ServerLifecycleOperationResult> RestartAsync(
        IReadOnlyList<string> serverArguments,
        TimeSpan startupTimeout,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default);

    // v0.7.44.0: machine-wide instance detection/termination, independent of this profile's own
    // managed-process tracking -- see ServerInstanceInfo for why ManagedByThisProfile is conservative.
    Task<IReadOnlyList<ServerInstanceInfo>> FindAllInstancesAsync(CancellationToken cancellationToken = default);
    Task<InstanceTerminationResult> TerminateUnmanagedInstanceAsync(int processId, CancellationToken cancellationToken = default);
}

[SupportedOSPlatform("linux")]
public sealed class LinuxServerLifecycleService : IServerLifecycleService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ForceKillWait = TimeSpan.FromSeconds(5);

    private readonly ServerPlatformProfile platform;
    private readonly IServerPathProfile paths;
    private readonly IServerSessionInspector sessionInspector;
    private readonly IProcessSignalService signals;
    private readonly ServerLifecycleStateStore stateStore;
    private readonly int expectedGamePort;
    private readonly PalworldRconService rcon;

    public LinuxServerLifecycleService(
        ServerPlatformProfile platform,
        IServerPathProfile paths,
        IServerSessionInspector sessionInspector,
        IProcessSignalService? signals = null,
        ServerLifecycleStateStore? stateStore = null,
        int expectedGamePort = 8211)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Linux lifecycle control requires Linux.");

        this.platform = platform ?? throw new ArgumentNullException(nameof(platform));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.sessionInspector = sessionInspector ?? throw new ArgumentNullException(nameof(sessionInspector));
        this.signals = signals ?? new LinuxProcessSignalService();
        this.stateStore = stateStore ?? new ServerLifecycleStateStore(paths.ManagerRuntimeRoot);
        this.expectedGamePort = expectedGamePort is > 0 and <= 65535 ? expectedGamePort : 8211;
        // v0.7.68.0: mirrors WindowsServerLifecycleService -- see its own StopAsync comment for the
        // full reasoning. SIGTERM isn't known-broken here the way CloseMainWindow is confirmed
        // broken on Windows, but RCON's native Shutdown is still Palworld's own real graceful-exit
        // path (world save, then clean exit) rather than an OS-level signal, so trying it first is
        // a genuine reliability improvement, not just parity for its own sake.
        this.rcon = new PalworldRconService(new PalworldSettingsConfigurationService(paths));
    }

    public Task<ServerLifecycleSnapshot> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var processes = FindManagedServerProcesses();
        var ports = sessionInspector.GetGuardedListeningPorts();
        var persisted = stateStore.Read();
        var now = DateTimeOffset.UtcNow;

        if (processes.Count > 0)
        {
            var native = SelectNativeProcess(processes);
            var ready = ports.Contains(expectedGamePort);
            var snapshot = new ServerLifecycleSnapshot(
                ServerLifecyclePhase.Running,
                native?.ProcessId,
                processes,
                ports,
                ready,
                false,
                now,
                persisted?.LastTransitionAt,
                ready
                    ? $"PalServer process and UDP {expectedGamePort} are active."
                    : $"PalServer process is active; UDP {expectedGamePort} has not been confirmed.");

            // Observation is allowed to repair stale state from a previous host invocation.
            if (persisted?.Phase != ServerLifecyclePhase.Running ||
                persisted.LastKnownProcessId != native?.ProcessId)
            {
                stateStore.Write(new PersistedServerLifecycleState(
                    ServerLifecyclePhase.Running,
                    native?.ProcessId,
                    now,
                    false,
                    snapshot.Detail));
                snapshot = snapshot with { LastTransitionAt = now };
            }

            return Task.FromResult(snapshot);
        }

        var crashDetected = persisted is not null &&
                            !persisted.StopRequested &&
                            persisted.Phase is ServerLifecyclePhase.Running or ServerLifecyclePhase.Starting;

        if (crashDetected)
        {
            var crashed = new PersistedServerLifecycleState(
                ServerLifecyclePhase.Crashed,
                persisted!.LastKnownProcessId,
                now,
                false,
                "Previously managed PalServer process is no longer present without a requested stop.");
            stateStore.Write(crashed);

            return Task.FromResult(new ServerLifecycleSnapshot(
                ServerLifecyclePhase.Crashed,
                persisted.LastKnownProcessId,
                [],
                ports,
                false,
                true,
                now,
                now,
                crashed.Detail));
        }

        return Task.FromResult(new ServerLifecycleSnapshot(
            persisted?.Phase == ServerLifecyclePhase.Crashed
                ? ServerLifecyclePhase.Crashed
                : ServerLifecyclePhase.Stopped,
            persisted?.LastKnownProcessId,
            [],
            ports,
            false,
            persisted?.Phase == ServerLifecyclePhase.Crashed,
            now,
            persisted?.LastTransitionAt,
            persisted?.Phase == ServerLifecyclePhase.Crashed
                ? persisted.Detail
                : "PalServer is not running."));
    }

    public async Task<ServerLifecycleOperationResult> StartAsync(
        IReadOnlyList<string> serverArguments,
        TimeSpan startupTimeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serverArguments);

        if (!File.Exists(paths.ServerExecutable))
        {
            var missing = await GetStatusAsync(cancellationToken);
            return new ServerLifecycleOperationResult(
                HeadlessExitCode.ServerExecutableMissing,
                missing,
                false,
                $"Server entry point was not found: {paths.ServerExecutable}");
        }

        var existing = FindManagedServerProcesses();
        if (existing.Count > 0)
        {
            var snapshot = await GetStatusAsync(cancellationToken);
            return new ServerLifecycleOperationResult(
                HeadlessExitCode.AlreadyRunning,
                snapshot,
                false,
                "PalServer is already running; duplicate start was blocked.");
        }

        // v0.7.60.0: Port Conflict Prevention -- mirrors WindowsServerLifecycleService's own fix.
        // The check above only catches a duplicate start of THIS profile's own tracked process; it
        // says nothing about another profile, an unmanaged process, or a not-yet-cleaned-up leftover
        // already holding the same UDP port. Catching it here, before the process is even created,
        // turns a silent hang into an immediate, clear, actionable error.
        if (sessionInspector.GetGuardedListeningPorts().Contains(expectedGamePort))
        {
            var conflictSnapshot = await GetStatusAsync(cancellationToken);
            return new ServerLifecycleOperationResult(
                HeadlessExitCode.PortConflict,
                conflictSnapshot,
                false,
                $"UDP port {expectedGamePort} is already in use by another process on this machine. Stop whatever's using it, or change this server's configured port, before starting -- launching anyway would leave PalServer running but unable to bind its game port, indistinguishable from a hang.");
        }

        Directory.CreateDirectory(paths.ManagerRuntimeRoot);
        var now = DateTimeOffset.UtcNow;
        stateStore.Write(new PersistedServerLifecycleState(
            ServerLifecyclePhase.Starting,
            null,
            now,
            false,
            "Headless host requested PalServer startup."));

        try
        {
            LaunchDetached(serverArguments);
        }
        catch (Exception ex)
        {
            var failedState = new PersistedServerLifecycleState(
                ServerLifecyclePhase.Crashed,
                null,
                DateTimeOffset.UtcNow,
                false,
                "PalServer launch failed: " + ex.Message);
            stateStore.Write(failedState);

            var snapshot = new ServerLifecycleSnapshot(
                ServerLifecyclePhase.Crashed,
                null,
                [],
                sessionInspector.GetGuardedListeningPorts(),
                false,
                true,
                DateTimeOffset.UtcNow,
                failedState.LastTransitionAt,
                failedState.Detail);

            return new ServerLifecycleOperationResult(
                HeadlessExitCode.LaunchFailed,
                snapshot,
                false,
                failedState.Detail);
        }

        var deadline = DateTimeOffset.UtcNow + startupTimeout;
        ServerLifecycleSnapshot? lastSnapshot = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(PollInterval, cancellationToken);

            lastSnapshot = await GetStatusAsync(cancellationToken);
            if (lastSnapshot.Phase == ServerLifecyclePhase.Crashed)
            {
                return new ServerLifecycleOperationResult(
                    HeadlessExitCode.LaunchFailed,
                    lastSnapshot,
                    false,
                    "PalServer exited before startup verification completed.");
            }

            if (lastSnapshot.Processes.Count > 0 && lastSnapshot.Ready)
            {
                return new ServerLifecycleOperationResult(
                    HeadlessExitCode.Success,
                    lastSnapshot,
                    false,
                    $"PalServer started and UDP {expectedGamePort} was verified.");
            }
        }

        lastSnapshot ??= await GetStatusAsync(cancellationToken);
        if (lastSnapshot.Processes.Count > 0)
        {
            return new ServerLifecycleOperationResult(
                HeadlessExitCode.StartupTimeout,
                lastSnapshot,
                false,
                $"PalServer process is running, but UDP {expectedGamePort} was not confirmed before the startup timeout. The process was left running.");
        }

        return new ServerLifecycleOperationResult(
            HeadlessExitCode.LaunchFailed,
            lastSnapshot,
            false,
            "PalServer did not remain running during startup verification.");
    }

    public async Task<ServerLifecycleOperationResult> StopAsync(
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        var processes = FindManagedServerProcesses();
        if (processes.Count == 0)
        {
            var snapshot = await GetStatusAsync(cancellationToken);
            stateStore.Write(new PersistedServerLifecycleState(
                ServerLifecyclePhase.Stopped,
                snapshot.NativeProcessId,
                DateTimeOffset.UtcNow,
                true,
                "Stop was requested while PalServer was already stopped."));

            snapshot = snapshot with
            {
                Phase = ServerLifecyclePhase.Stopped,
                CrashDetected = false,
                LastTransitionAt = DateTimeOffset.UtcNow,
                Detail = "PalServer is already stopped."
            };

            return new ServerLifecycleOperationResult(
                HeadlessExitCode.NotRunning,
                snapshot,
                false,
                "PalServer is already stopped.");
        }

        var native = SelectNativeProcess(processes);
        var transitionAt = DateTimeOffset.UtcNow;
        stateStore.Write(new PersistedServerLifecycleState(
            ServerLifecyclePhase.Stopping,
            native?.ProcessId,
            transitionAt,
            true,
            "Graceful SIGTERM shutdown requested."));

        // v0.7.68.0: try RCON's native Shutdown first when configured/reachable -- purely additive,
        // the SIGTERM below (and the SIGKILL escalation further down) still runs completely
        // unchanged regardless of whether this succeeds, so it can only help, never regress.
        var rconStatus = rcon.GetStatus();
        if (rconStatus is { Enabled: true, PasswordConfigured: true })
        {
            try { await rcon.ExecuteAsync("Shutdown 1 MystTiq requested a graceful shutdown.", cancellationToken); }
            catch { /* best-effort -- the SIGTERM/SIGKILL escalation below is still the safety net */ }
        }

        foreach (var process in processes)
            signals.TryTerminate(process.ProcessId);

        if (await WaitForExitAsync(gracefulTimeout, cancellationToken))
        {
            return CompleteStopped(false, "PalServer stopped after graceful SIGTERM.");
        }

        // Capture descendants before escalation so crash/telemetry helpers do not linger.
        var forceTargets = new HashSet<int>();
        foreach (var process in FindManagedServerProcesses())
        {
            foreach (var child in sessionInspector.GetDescendantProcessIds(process.ProcessId))
                forceTargets.Add(child);
            forceTargets.Add(process.ProcessId);
        }

        foreach (var processId in forceTargets.OrderByDescending(id => id))
            signals.TryKill(processId);

        if (await WaitForExitAsync(ForceKillWait, cancellationToken))
        {
            return CompleteStopped(true, "PalServer required SIGKILL escalation after the graceful shutdown timeout.");
        }

        var remaining = await GetStatusAsync(cancellationToken);
        return new ServerLifecycleOperationResult(
            HeadlessExitCode.StopTimeout,
            remaining,
            true,
            "PalServer remained active after SIGTERM and SIGKILL escalation.");
    }

    public async Task<ServerLifecycleOperationResult> RestartAsync(
        IReadOnlyList<string> serverArguments,
        TimeSpan startupTimeout,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);
        if (status.Processes.Count > 0)
        {
            var stop = await StopAsync(gracefulTimeout, cancellationToken);
            if (stop.ExitCode == HeadlessExitCode.StopTimeout)
                return stop;
        }
        else
        {
            // Restart from Stopped/Crashed is intentionally equivalent to Start.
            stateStore.Write(new PersistedServerLifecycleState(
                ServerLifecyclePhase.Stopped,
                status.NativeProcessId,
                DateTimeOffset.UtcNow,
                true,
                "Restart requested while server was not running; proceeding with start."));
        }

        return await StartAsync(serverArguments, startupTimeout, cancellationToken);
    }

    private ServerLifecycleOperationResult CompleteStopped(bool forced, string message)
    {
        var now = DateTimeOffset.UtcNow;
        stateStore.Write(new PersistedServerLifecycleState(
            ServerLifecyclePhase.Stopped,
            null,
            now,
            true,
            message));

        var snapshot = new ServerLifecycleSnapshot(
            ServerLifecyclePhase.Stopped,
            null,
            [],
            sessionInspector.GetGuardedListeningPorts(),
            false,
            false,
            now,
            now,
            message);

        return new ServerLifecycleOperationResult(
            HeadlessExitCode.Success,
            snapshot,
            forced,
            message);
    }

    private async Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FindManagedServerProcesses().Count == 0)
                return true;
            await Task.Delay(PollInterval, cancellationToken);
        }

        return FindManagedServerProcesses().Count == 0;
    }

    private IReadOnlyList<ServerSessionProcessInfo> FindManagedServerProcesses()
    {
        var root = Path.GetFullPath(paths.ServerRoot)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return sessionInspector.FindProcessesByName(platform.ProcessNames)
            .Where(process =>
            {
                if (string.IsNullOrWhiteSpace(process.ExecutablePath))
                    return true; // procfs name is still useful when /proc/<pid>/exe is restricted.

                var executable = Path.GetFullPath(process.ExecutablePath);
                return executable.StartsWith(root, StringComparison.Ordinal);
            })
            .OrderBy(process => process.ProcessId)
            .ToList();
    }

    private static ServerSessionProcessInfo? SelectNativeProcess(
        IReadOnlyList<ServerSessionProcessInfo> processes) =>
        processes.FirstOrDefault(process =>
            process.ProcessName.Contains("Linux-Shipping", StringComparison.OrdinalIgnoreCase))
        ?? processes.FirstOrDefault();

    public Task<IReadOnlyList<ServerInstanceInfo>> FindAllInstancesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = Path.GetFullPath(paths.ServerRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        IReadOnlyList<ServerInstanceInfo> instances = sessionInspector.FindProcessesByName(platform.ProcessNames)
            .Select(process => new ServerInstanceInfo(
                process.ProcessId,
                process.ParentProcessId,
                process.ProcessName,
                process.ExecutablePath,
                process.Responding,
                ManagedByThisProfile: !string.IsNullOrWhiteSpace(process.ExecutablePath) &&
                    IsUnderRoot(process.ExecutablePath, root)))
            .OrderBy(instance => instance.ProcessId)
            .ToArray();
        return Task.FromResult(instances);
    }

    public Task<InstanceTerminationResult> TerminateUnmanagedInstanceAsync(int processId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Raw SIGKILL by PID -- deliberately does not touch this profile's own stateStore, since the
        // target may belong to a different local MystTiq session's crash-recovery tracking entirely.
        var killed = signals.TryKill(processId);
        return Task.FromResult(killed
            ? new InstanceTerminationResult(true, processId, $"Sent SIGKILL to PID {processId}.")
            : new InstanceTerminationResult(false, processId, $"Could not signal PID {processId}; it may have already exited."));
    }

    private static bool IsUnderRoot(string executablePath, string root)
    {
        try { return Path.GetFullPath(executablePath).StartsWith(root, StringComparison.Ordinal); }
        catch { return false; }
    }

    // v0.7.65.0: PalServer's raw stdout/stderr WAS being captured here all along -- via the shell
    // redirect below, not missing entirely as first suspected while tracing v0.7.64.0's log-rotation
    // fix -- but into ManagerRuntimeRoot/palserver-console.log, a location nothing on the read side
    // (HeadlessMonitoringService.ResolveActiveLogPath, the Doctor/monitoring log-source list, the
    // Live Console page) ever looked at. Those all only ever checked
    // LogsRoot/MystTiq-PalServer-Console.log (the exact file WindowsServerLifecycleService captures
    // into) or LogsRoot/Pal.log. Renamed/relocated to match, so Linux's real capture finally reaches
    // the same consumers Windows' already does.
    private void LaunchDetached(IReadOnlyList<string> serverArguments)
    {
        var launchScript = Path.Combine(paths.ManagerRuntimeRoot, "launch-palserver.sh");
        var logDirectory = paths.LogsRoot;
        try { Directory.CreateDirectory(logDirectory); }
        catch
        {
            logDirectory = Path.Combine(paths.ManagerRuntimeRoot, "logs");
            try { Directory.CreateDirectory(logDirectory); } catch { /* best-effort, mirrors Windows */ }
        }
        var consoleLog = Path.Combine(logDirectory, "MystTiq-PalServer-Console.log");

        // Rotation only works here, right before the detached process opens its own fresh file
        // handle for the whole session -- setsid -f's shell redirect (`>>`) holds that handle open
        // for as long as PalServer runs, so renaming the file out from under it mid-session (the way
        // ConsoleLogRotation is used on Windows, where MystTiq's own C# process re-opens the file on
        // every single line) would just make the shell keep appending to the now-renamed .1
        // generation forever under its stale handle. Rotating once, here, at the one point a brand
        // new file handle is about to be opened, is the only place on this detached-process
        // architecture where rotation is both safe and effective.
        // v0.8.18.0: the server's bandwidth policy goes into Engine.ini just before it starts (never blocks the start).
        var networkLine = EngineNetworkSettings.ApplyBeforeStart(paths);
        try
        {
            ConsoleLogRotation.RotateIfNeeded(consoleLog);
            File.AppendAllText(consoleLog, $"===== MystTiq PalServer detached console session starting {DateTimeOffset.Now:O} =====" + Environment.NewLine);
            if (networkLine is not null) File.AppendAllText(consoleLog, $"[MYSTTIQ] {networkLine}" + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        var commandArguments = new[] { paths.ServerExecutable }
            .Concat(serverArguments)
            .Select(ShellQuote);

        var script = string.Join('\n',
        [
            "#!/usr/bin/env bash",
            "set -e",
            $"cd {ShellQuote(paths.ServerRoot)}",
            $"exec /usr/bin/setsid -f {string.Join(" ", commandArguments)} >> {ShellQuote(consoleLog)} 2>&1 < /dev/null"
        ]) + "\n";

        File.WriteAllText(launchScript, script);
        File.SetUnixFileMode(
            launchScript,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = launchScript,
            WorkingDirectory = paths.ServerRoot,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("The PalServer launcher process could not be created.");

        // setsid -f forks the detached server process and returns promptly.
        if (!process.WaitForExit(5000))
            throw new TimeoutException("The detached launch helper did not return within 5 seconds.");

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"The detached PalServer launch helper exited with code {process.ExitCode}.");
    }

    private static string ShellQuote(string value) =>
        "'" + value.Replace("'", "'\"'\"'") + "'";
}
