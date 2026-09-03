using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsServerLifecycleService : IServerLifecycleService
{
    private const int SwHide = 0;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ForceKillWait = TimeSpan.FromSeconds(5);
    private readonly ServerPlatformProfile platform;
    private readonly IServerPathProfile paths;
    private readonly IServerSessionInspector sessionInspector;
    private readonly ServerLifecycleStateStore stateStore;
    private readonly int expectedGamePort;
    private readonly object consoleLogGate = new();
    private Process? ownedProcess;
    private CancellationTokenSource? consoleCaptureCancellation;

    public WindowsServerLifecycleService(
        ServerPlatformProfile platform,
        IServerPathProfile paths,
        IServerSessionInspector sessionInspector,
        ServerLifecycleStateStore? stateStore = null,
        int expectedGamePort = 8211)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows lifecycle control requires Windows.");
        this.platform = platform ?? throw new ArgumentNullException(nameof(platform));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.sessionInspector = sessionInspector ?? throw new ArgumentNullException(nameof(sessionInspector));
        this.stateStore = stateStore ?? new ServerLifecycleStateStore(paths.ManagerRuntimeRoot);
        this.expectedGamePort = expectedGamePort is > 0 and <= 65535 ? expectedGamePort : 8211;
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
                ServerLifecyclePhase.Running, native?.ProcessId, processes, ports, ready, false, now,
                persisted?.LastTransitionAt,
                ready ? $"PalServer process and UDP {expectedGamePort} are active." : $"PalServer process is active; UDP {expectedGamePort} has not been confirmed.");
            if (persisted?.Phase != ServerLifecyclePhase.Running || persisted.LastKnownProcessId != native?.ProcessId)
            {
                stateStore.Write(new PersistedServerLifecycleState(ServerLifecyclePhase.Running, native?.ProcessId, now, false, snapshot.Detail));
                snapshot = snapshot with { LastTransitionAt = now };
            }
            return Task.FromResult(snapshot);
        }

        var crashDetected = persisted is not null && !persisted.StopRequested &&
                            persisted.Phase is ServerLifecyclePhase.Running or ServerLifecyclePhase.Starting;
        if (crashDetected)
        {
            var crashed = new PersistedServerLifecycleState(ServerLifecyclePhase.Crashed, persisted!.LastKnownProcessId, now, false,
                "Previously managed PalServer process is no longer present without a requested stop.");
            stateStore.Write(crashed);
            return Task.FromResult(new ServerLifecycleSnapshot(ServerLifecyclePhase.Crashed, persisted.LastKnownProcessId, [], ports,
                false, true, now, now, crashed.Detail));
        }

        return Task.FromResult(new ServerLifecycleSnapshot(
            persisted?.Phase == ServerLifecyclePhase.Crashed ? ServerLifecyclePhase.Crashed : ServerLifecyclePhase.Stopped,
            persisted?.LastKnownProcessId, [], ports, false, persisted?.Phase == ServerLifecyclePhase.Crashed,
            now, persisted?.LastTransitionAt,
            persisted?.Phase == ServerLifecyclePhase.Crashed ? persisted.Detail : "PalServer is not running."));
    }

    public async Task<ServerLifecycleOperationResult> StartAsync(IReadOnlyList<string> serverArguments, TimeSpan startupTimeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serverArguments);
        if (!File.Exists(paths.ServerExecutable))
            return new ServerLifecycleOperationResult(HeadlessExitCode.ServerExecutableMissing, await GetStatusAsync(cancellationToken), false,
                $"Server entry point was not found: {paths.ServerExecutable}");
        if (FindManagedServerProcesses().Count > 0)
            return new ServerLifecycleOperationResult(HeadlessExitCode.AlreadyRunning, await GetStatusAsync(cancellationToken), false,
                "PalServer is already running; duplicate start was blocked.");

        Directory.CreateDirectory(paths.ManagerRuntimeRoot);
        var now = DateTimeOffset.UtcNow;
        stateStore.Write(new PersistedServerLifecycleState(ServerLifecyclePhase.Starting, null, now, false, "Headless host requested PalServer startup."));
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = paths.ServerExecutable,
                WorkingDirectory = paths.ServerRoot,
                // PalServer is intentionally launched without a visible console window.
                // Unreal stdout/stderr is redirected into MystTiq-PalServer-Console.log and surfaced by Live Console.
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in BuildHiddenConsoleArguments(serverArguments))
                startInfo.ArgumentList.Add(argument);
            ownedProcess?.Dispose();
            consoleCaptureCancellation?.Cancel();
            consoleCaptureCancellation?.Dispose();
            ownedProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("PalServer process could not be created.");
            consoleCaptureCancellation = new CancellationTokenSource();
            StartConsoleCapture(ownedProcess, consoleCaptureCancellation.Token);
            AppendLifecycleConsoleLine($"Launching: {paths.ServerExecutable}");
            AppendLifecycleConsoleLine($"Working directory: {paths.ServerRoot}");
            AppendLifecycleConsoleLine("Arguments: " + string.Join(" ", startInfo.ArgumentList));
            AppendLifecycleConsoleLine($"PalServer bootstrap PID {ownedProcess.Id} created; waiting for UDP {expectedGamePort} readiness.");
            // Pad past the readiness timeout: PalServer's own child (PalServer-Win64-Shipping-Cmd.exe,
            // not the direct child MystTiq launched) can allocate its console window late during a slow
            // Unreal asset-load, well after this method's own readiness wait would otherwise give up.
            _ = ApplyPostLaunchWindowPolicyAsync(consoleCaptureCancellation.Token, startupTimeout + TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            var failed = new PersistedServerLifecycleState(ServerLifecyclePhase.Crashed, null, DateTimeOffset.UtcNow, false, "PalServer launch failed: " + ex.Message);
            stateStore.Write(failed);
            return new ServerLifecycleOperationResult(HeadlessExitCode.LaunchFailed, await GetStatusAsync(cancellationToken), false, failed.Detail);
        }

        var deadline = DateTimeOffset.UtcNow + startupTimeout;
        ServerLifecycleSnapshot? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(PollInterval, cancellationToken);
            last = await GetStatusAsync(cancellationToken);
            if (last.Processes.Count > 0 && last.Ready)
            {
                AppendLifecycleConsoleLine($"PalServer ready. Managed PID {last.NativeProcessId?.ToString() ?? "unknown"}; UDP {expectedGamePort} verified.");
                return new ServerLifecycleOperationResult(HeadlessExitCode.Success, last, false, $"PalServer started and UDP {expectedGamePort} was verified.");
            }
        }
        last ??= await GetStatusAsync(cancellationToken);
        return last.Processes.Count > 0
            ? new ServerLifecycleOperationResult(HeadlessExitCode.StartupTimeout, last, false, $"PalServer is running, but UDP {expectedGamePort} was not confirmed before the startup timeout. The process was left running.")
            : new ServerLifecycleOperationResult(HeadlessExitCode.LaunchFailed, last, false, "PalServer did not remain running during startup verification.");
    }

    public async Task<ServerLifecycleOperationResult> StopAsync(TimeSpan gracefulTimeout, CancellationToken cancellationToken = default)
    {
        var processes = FindManagedServerProcesses();
        if (processes.Count == 0)
            return new ServerLifecycleOperationResult(HeadlessExitCode.NotRunning, await GetStatusAsync(cancellationToken), false, "PalServer is already stopped.");

        stateStore.Write(new PersistedServerLifecycleState(ServerLifecyclePhase.Stopping, SelectNativeProcess(processes)?.ProcessId,
            DateTimeOffset.UtcNow, true, "Windows shutdown requested."));

        foreach (var item in processes)
        {
            try { using var process = Process.GetProcessById(item.ProcessId); process.CloseMainWindow(); } catch { }
        }
        if (await WaitForExitAsync(gracefulTimeout, cancellationToken))
        {
            AppendLifecycleConsoleLine("PalServer stopped cleanly.");
            return CompleteStopped(false, "PalServer stopped cleanly.");
        }

        foreach (var item in FindManagedServerProcesses().OrderByDescending(item => item.ProcessId))
        {
            try { using var process = Process.GetProcessById(item.ProcessId); process.Kill(entireProcessTree: true); } catch { }
        }
        if (await WaitForExitAsync(ForceKillWait, cancellationToken)) return CompleteStopped(true, "PalServer required forced termination after the graceful shutdown timeout.");
        return new ServerLifecycleOperationResult(HeadlessExitCode.StopTimeout, await GetStatusAsync(cancellationToken), true,
            "PalServer remained active after Windows shutdown escalation.");
    }

    public async Task<ServerLifecycleOperationResult> RestartAsync(IReadOnlyList<string> serverArguments, TimeSpan startupTimeout, TimeSpan gracefulTimeout, CancellationToken cancellationToken = default)
    {
        if (FindManagedServerProcesses().Count > 0)
        {
            var stop = await StopAsync(gracefulTimeout, cancellationToken);
            if (stop.ExitCode == HeadlessExitCode.StopTimeout) return stop;
        }
        return await StartAsync(serverArguments, startupTimeout, cancellationToken);
    }

    private ServerLifecycleOperationResult CompleteStopped(bool forced, string message)
    {
        consoleCaptureCancellation?.Cancel();
        consoleCaptureCancellation?.Dispose();
        consoleCaptureCancellation = null;
        ownedProcess?.Dispose();
        ownedProcess = null;
        var now = DateTimeOffset.UtcNow;
        stateStore.Write(new PersistedServerLifecycleState(ServerLifecyclePhase.Stopped, null, now, true, message));
        var snapshot = new ServerLifecycleSnapshot(ServerLifecyclePhase.Stopped, null, [], sessionInspector.GetGuardedListeningPorts(), false, false, now, now, message);
        return new ServerLifecycleOperationResult(HeadlessExitCode.Success, snapshot, forced, message);
    }

    private async Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (FindManagedServerProcesses().Count == 0) return true;
            await Task.Delay(PollInterval, cancellationToken);
        }
        return FindManagedServerProcesses().Count == 0;
    }

    private IReadOnlyList<ServerSessionProcessInfo> FindManagedServerProcesses()
    {
        var root = Path.GetFullPath(paths.ServerRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return sessionInspector.FindProcessesByName(platform.ProcessNames).Where(process =>
        {
            if (string.IsNullOrWhiteSpace(process.ExecutablePath)) return true;
            try { return Path.GetFullPath(process.ExecutablePath).StartsWith(root, StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }).OrderBy(process => process.ProcessId).ToArray();
    }



    private async Task ApplyPostLaunchWindowPolicyAsync(CancellationToken token, TimeSpan duration)
    {
        // PalServer/UE wrappers can allocate a child console after Process.Start, and PalServer.exe's
        // own grandchild (PalServer-Win64-Shipping-Cmd.exe) is not the process MystTiq directly launched,
        // so its window is never covered by that launch's CreateNoWindow/WindowStyle settings. Relying on
        // Process.MainWindowHandle alone also misses windows on hosts where the default console terminal
        // (e.g. Windows Terminal) owns the visible top-level window instead of the target process, so this
        // enumerates every top-level window belonging to any managed process name directly via Win32.
        var deadline = DateTimeOffset.UtcNow + duration;
        while (DateTimeOffset.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            HideManagedProcessWindows();
            await Task.Delay(500, token).ConfigureAwait(false);
        }
    }

    private void HideManagedProcessWindows()
    {
        var managedProcessIds = new HashSet<int>();
        foreach (var name in platform.ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process) { try { managedProcessIds.Add(process.Id); } catch { } }
            }
        }
        if (managedProcessIds.Count == 0) return;

        EnumWindows((handle, _) =>
        {
            try
            {
                if (!IsWindowVisible(handle)) return true;
                GetWindowThreadProcessId(handle, out var windowProcessId);
                if (managedProcessIds.Contains((int)windowProcessId))
                    ShowWindow(handle, SwHide);
            }
            catch { /* window may have been destroyed mid-enumeration */ }
            return true;
        }, IntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private static IReadOnlyList<string> BuildHiddenConsoleArguments(IReadOnlyList<string> serverArguments)
    {
        // Unreal's exact -log flag creates a separate logging window on Windows.
        // MystTiq is headless-first, so preserve all other custom arguments while routing log output to stdout.
        var arguments = serverArguments
            .Where(argument => !string.Equals(argument?.Trim(), "-log", StringComparison.OrdinalIgnoreCase))
            .Where(argument => !string.IsNullOrWhiteSpace(argument))
            .ToList();

        if (!arguments.Any(argument => string.Equals(argument, "-stdout", StringComparison.OrdinalIgnoreCase)))
            arguments.Add("-stdout");
        if (!arguments.Any(argument => string.Equals(argument, "-FullStdOutLogOutput", StringComparison.OrdinalIgnoreCase)))
            arguments.Add("-FullStdOutLogOutput");

        return arguments;
    }

    private void StartConsoleCapture(Process process, CancellationToken token)
    {
        try
        {
            var logDirectory = paths.LogsRoot;
            try { Directory.CreateDirectory(logDirectory); }
            catch
            {
                logDirectory = Path.Combine(paths.ManagerRuntimeRoot, "logs");
                Directory.CreateDirectory(logDirectory);
            }
            var logPath = Path.Combine(logDirectory, "MystTiq-PalServer-Console.log");
            AppendConsoleLine(logPath, $"===== MystTiq PalServer redirected console session started {DateTimeOffset.Now:O} PID {process.Id} =====");
            _ = Task.Run(() => CaptureStreamAsync(process.StandardOutput, "OUT", logPath, token));
            _ = Task.Run(() => CaptureStreamAsync(process.StandardError, "ERR", logPath, token));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private async Task CaptureStreamAsync(StreamReader reader, string streamName, string logPath, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(token);
                if (line is null) break;
                AppendConsoleLine(logPath, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] [{streamName}] {line}");
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (IOException ex) { AppendConsoleLine(logPath, $"[CAPTURE] {streamName} reader stopped: {ex.Message}"); }
    }

    private void AppendLifecycleConsoleLine(string message)
    {
        try
        {
            var logDirectory = paths.LogsRoot;
            try { Directory.CreateDirectory(logDirectory); }
            catch
            {
                logDirectory = Path.Combine(paths.ManagerRuntimeRoot, "logs");
                Directory.CreateDirectory(logDirectory);
            }
            AppendConsoleLine(Path.Combine(logDirectory, "MystTiq-PalServer-Console.log"), $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] [MYSTTIQ] {message}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void AppendConsoleLine(string path, string line)
    {
        lock (consoleLogGate)
            File.AppendAllText(path, line + Environment.NewLine);
    }

    private static ServerSessionProcessInfo? SelectNativeProcess(IReadOnlyList<ServerSessionProcessInfo> processes) =>
        processes.FirstOrDefault(process => process.ProcessName.Contains("Shipping", StringComparison.OrdinalIgnoreCase)) ?? processes.FirstOrDefault();
}
