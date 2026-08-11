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

    public LinuxServerLifecycleService(
        ServerPlatformProfile platform,
        IServerPathProfile paths,
        IServerSessionInspector sessionInspector,
        IProcessSignalService? signals = null,
        ServerLifecycleStateStore? stateStore = null)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Linux lifecycle control requires Linux.");

        this.platform = platform ?? throw new ArgumentNullException(nameof(platform));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.sessionInspector = sessionInspector ?? throw new ArgumentNullException(nameof(sessionInspector));
        this.signals = signals ?? new LinuxProcessSignalService();
        this.stateStore = stateStore ?? new ServerLifecycleStateStore(paths.ManagerRuntimeRoot);
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
            var ready = ports.Contains(8211);
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
                    ? "PalServer process and UDP 8211 are active."
                    : "PalServer process is active; UDP 8211 has not been confirmed.");

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
                    "PalServer started and UDP 8211 was verified.");
            }
        }

        lastSnapshot ??= await GetStatusAsync(cancellationToken);
        if (lastSnapshot.Processes.Count > 0)
        {
            return new ServerLifecycleOperationResult(
                HeadlessExitCode.StartupTimeout,
                lastSnapshot,
                false,
                "PalServer process is running, but UDP 8211 was not confirmed before the startup timeout. The process was left running.");
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

    private void LaunchDetached(IReadOnlyList<string> serverArguments)
    {
        var launchScript = Path.Combine(paths.ManagerRuntimeRoot, "launch-palserver.sh");
        var consoleLog = Path.Combine(paths.ManagerRuntimeRoot, "palserver-console.log");

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
