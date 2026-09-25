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
    private readonly PalworldRconService rcon;
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
        // v0.7.68.0: see the long comment on StopAsync -- cheap/stateless, matches how
        // PalworldSettingsConfigurationService is already constructed ad hoc elsewhere (e.g. the
        // api-run LifecycleFactory reads the configured game port the same way).
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

        if (persisted?.Phase != ServerLifecyclePhase.Crashed)
        {
            // v0.7.29.0 bug fix: before reporting "not running," check whether a real PalServer
            // process actually is running, just at a path that doesn't match the currently
            // configured ServerRoot -- e.g. a stale/edited path on this profile, or a mismatch
            // between two tabs pointed at what's actually the same physical server. That previously
            // collapsed into this exact same generic message, indistinguishable from a genuine stop.
            var mismatched = FindProcessesWithMismatchedPath();
            if (mismatched.Count > 0)
            {
                var found = string.Join("; ", mismatched.Select(p => $"{p.ProcessName} (PID {p.ProcessId}) at {p.ExecutablePath}"));
                return Task.FromResult(new ServerLifecycleSnapshot(
                    ServerLifecyclePhase.Stopped, null, [], ports, false, false, now, persisted?.LastTransitionAt,
                    $"A PalServer process is running, but not at the configured path. Expected: {paths.ServerRoot}. Found: {found}."));
            }
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

        // v0.7.60.0: Port Conflict Prevention. The check above only catches a duplicate start of
        // THIS profile's own tracked process -- it says nothing about another profile, an unmanaged
        // process, or a not-yet-cleaned-up leftover from an unclean stop already holding the same
        // UDP port. Launching anyway in that case doesn't fail cleanly: PalServer starts, spins up
        // its full engine thread pool, and then sits forever unable to bind -- indistinguishable
        // from a genuine hang without deep diagnosis (see the real live investigation this fix is
        // grounded in). Catching it here, before the process is even created, turns that into an
        // immediate, clear, actionable error instead.
        if (sessionInspector.GetGuardedListeningPorts().Contains(expectedGamePort))
            return new ServerLifecycleOperationResult(HeadlessExitCode.PortConflict, await GetStatusAsync(cancellationToken), false,
                $"UDP port {expectedGamePort} is already in use by another process on this machine. Stop whatever's using it, or change this server's configured port, before starting -- launching anyway would leave PalServer running but unable to bind its game port, indistinguishable from a hang.");

        Directory.CreateDirectory(paths.ManagerRuntimeRoot);
        // v0.8.18.0: the server's bandwidth policy goes into Engine.ini just before it starts (never blocks the start).
        var networkLine = EngineNetworkSettings.ApplyBeforeStart(paths);
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
            if (networkLine is not null) AppendLifecycleConsoleLine(networkLine);
            AppendLifecycleConsoleLine($"PalServer bootstrap PID {ownedProcess.Id} created; waiting for UDP {expectedGamePort} readiness.");
            // v0.7.83.0 bugfix: reported live -- this previously ran for a fixed startupTimeout+30s
            // window, then stopped for good. On a host where new console allocations are hosted by
            // Windows Terminal (this project's own reference machine's default on Windows Server
            // 2025, confirmed live) rather than conhost.exe, HideManagedProcessWindows' own
            // process-name matching can't catch it at all (the top-level window belongs to
            // WindowsTerminal.exe, never one of platform.ProcessNames) -- see that method's own
            // updated comment for the title-matching fallback this pass added for that case. Even
            // with that fallback, a fixed window risked missing a console that only appears later
            // (e.g. after this method's own catch-up sweep gives up). consoleCaptureCancellation
            // already lives for the managed process' full lifetime (only cancelled on the next
            // launch/stop), so reusing it here for the sweep loop itself -- instead of a fixed
            // duration -- means "ideally this window would never popup at all" for as long as
            // MystTiq is managing this server, not just its first couple of minutes.
            _ = ApplyPostLaunchWindowPolicyAsync(consoleCaptureCancellation.Token);
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
        {
            // v0.7.69.0 bugfix: LinuxServerLifecycleService's equivalent branch already writes a
            // fresh Stopped/StopRequested state here; this one never did, so on Windows a previously
            // persisted Crashed state (GetStatusAsync's crashDetected check requires
            // persisted.Phase is Running or Starting to fire again, which is false once it's already
            // Crashed) stayed stuck indefinitely -- confirmed reachable only through an explicit
            // Start afterward, never through Stop, exactly matching a previously-disclosed,
            // never-diagnosed observation from v0.6.10.0's own session notes. An admin's natural
            // reaction to seeing "Crashed" is often to press Stop (to acknowledge/reset it), not
            // Start -- that should actually clear it.
            var snapshot = await GetStatusAsync(cancellationToken);
            stateStore.Write(new PersistedServerLifecycleState(
                ServerLifecyclePhase.Stopped, snapshot.NativeProcessId, DateTimeOffset.UtcNow, true,
                "Stop was requested while PalServer was already stopped."));
            snapshot = snapshot with
            {
                Phase = ServerLifecyclePhase.Stopped,
                CrashDetected = false,
                LastTransitionAt = DateTimeOffset.UtcNow,
                Detail = "PalServer is already stopped."
            };
            return new ServerLifecycleOperationResult(HeadlessExitCode.NotRunning, snapshot, false, "PalServer is already stopped.");
        }

        stateStore.Write(new PersistedServerLifecycleState(ServerLifecyclePhase.Stopping, SelectNativeProcess(processes)?.ProcessId,
            DateTimeOffset.UtcNow, true, "Windows shutdown requested."));

        // v0.7.68.0 bugfix: found live-testing this same session's own tray-toast fix, then traced
        // properly -- CloseMainWindow() below silently does NOTHING (returns false, no exception,
        // no effect) once ApplyPostLaunchWindowPolicyAsync has hidden PalServer's window, which it
        // does on a 500ms loop for startupTimeout+30s after every single launch (MystTiq is
        // headless-first by design -- HideManagedProcessWindows explicitly ShowWindow(SW_HIDE)s
        // every visible top-level window belonging to a managed process). Process.MainWindowHandle,
        // which CloseMainWindow relies on internally, only resolves a handle for a window that is
        // currently VISIBLE -- a real HWND existing isn't enough once .NET's own window-enumeration
        // filters it out for not being visible. So MystTiq's own "stay headless" feature was
        // silently defeating its own graceful-shutdown feature on effectively every real stop,
        // exactly matching a previously-disclosed, never-diagnosed observation ("graceful shutdown
        // fell back to forced termination on EVERY stop," v0.6.10.0's own session notes). RCON's
        // native Shutdown command is Palworld's own real graceful-exit path (world save, then clean
        // exit) and has no dependency on window visibility at all -- try it first when RCON is
        // actually configured and reachable. This is purely additive: if RCON isn't configured, or
        // the Shutdown command doesn't result in the process actually exiting within
        // gracefulTimeout, every existing step below (CloseMainWindow, then force-kill escalation)
        // still runs completely unchanged, so this can only improve the outcome, never regress it.
        var rconStatus = rcon.GetStatus();
        if (rconStatus is { Enabled: true, PasswordConfigured: true })
        {
            try { await rcon.ExecuteAsync("Shutdown 1 MystTiq requested a graceful shutdown.", cancellationToken); }
            catch { /* best-effort -- the existing CloseMainWindow/kill escalation below is still the safety net */ }
        }

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
        var root = NormalizedServerRoot();
        return sessionInspector.FindProcessesByName(platform.ProcessNames).Where(process =>
        {
            if (string.IsNullOrWhiteSpace(process.ExecutablePath)) return true;
            try { return Path.GetFullPath(process.ExecutablePath).StartsWith(root, StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }).OrderBy(process => process.ProcessId).ToArray();
    }

    // v0.7.29.0 bug fix: the inverse of FindManagedServerProcesses' path filter -- a process whose
    // name matches (e.g. PalServer.exe) but whose executable path does NOT start with the currently
    // configured ServerRoot. GetStatusAsync uses this only when FindManagedServerProcesses found
    // nothing, to distinguish "genuinely not running" from "running, but MystTiq is looking at the
    // wrong install path" -- those used to collapse into the identical generic "PalServer is not
    // running." message with no way to tell them apart.
    private IReadOnlyList<ServerSessionProcessInfo> FindProcessesWithMismatchedPath()
    {
        var root = NormalizedServerRoot();
        return sessionInspector.FindProcessesByName(platform.ProcessNames).Where(process =>
        {
            if (string.IsNullOrWhiteSpace(process.ExecutablePath)) return false;
            try { return !Path.GetFullPath(process.ExecutablePath).StartsWith(root, StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }).OrderBy(process => process.ProcessId).ToArray();
    }

    private string NormalizedServerRoot() =>
        Path.GetFullPath(paths.ServerRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;



    // v0.7.83.0: no longer takes a fixed duration -- runs for as long as token stays alive, which
    // is the managed process' full lifetime (see the call site's own updated comment).
    private async Task ApplyPostLaunchWindowPolicyAsync(CancellationToken token)
    {
        // PalServer/UE wrappers can allocate a child console after Process.Start, and PalServer.exe's
        // own grandchild (PalServer-Win64-Shipping-Cmd.exe) is not the process MystTiq directly launched,
        // so its window is never covered by that launch's CreateNoWindow/WindowStyle settings. Relying on
        // Process.MainWindowHandle alone also misses windows on hosts where the default console terminal
        // (e.g. Windows Terminal) owns the visible top-level window instead of the target process, so this
        // enumerates every top-level window belonging to any managed process name directly via Win32.
        while (true)
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

        // v0.7.83.0 bugfix: reported live -- on Windows Server 2025 (this project's own reference
        // machine), a console a background process allocates gets hosted by Windows Terminal, a
        // completely separate top-level window owned by WindowsTerminal.exe, not by the managed
        // process itself -- GetWindowThreadProcessId for it never matches managedProcessIds above,
        // no matter how long this sweep runs, which is exactly the gap the OLDER version of this
        // method's own comment already flagged as a risk without actually covering it. Windows
        // Terminal's own tab title reflects the underlying console's title (which Win32 consoles
        // default to their executable path unless the app sets its own), so this second pass
        // catches that case by matching window text against this server's own ServerRoot -- not
        // just the direct-child ServerExecutable path -- since the grandchild that actually
        // allocates the console (PalServer-Win64-Shipping-Cmd.exe) lives under the same root but
        // at a different subpath. ServerRoot also correctly distinguishes this specific server
        // instance from any other one MystTiq is managing (e.g. a local clone on a different path).
        var titleNeedle = paths.ServerRoot;

        EnumWindows((handle, _) =>
        {
            try
            {
                if (!IsWindowVisible(handle)) return true;
                GetWindowThreadProcessId(handle, out var windowProcessId);
                if (managedProcessIds.Contains((int)windowProcessId))
                {
                    ShowWindow(handle, SwHide);
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(titleNeedle))
                {
                    var length = GetWindowTextLength(handle);
                    if (length > 0)
                    {
                        var buffer = new System.Text.StringBuilder(length + 1);
                        GetWindowText(handle, buffer, buffer.Capacity);
                        if (buffer.ToString().Contains(titleNeedle, StringComparison.OrdinalIgnoreCase))
                            ShowWindow(handle, SwHide);
                    }
                }
            }
            catch { /* window may have been destroyed mid-enumeration */ }
            return true;
        }, IntPtr.Zero);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

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

    // v0.7.51.0 bug fix: found live, diagnosing a real stuck-server incident. The reader tasks below
    // MUST start unconditionally, regardless of whether the log directory/file is writable.
    // RedirectStandardOutput/RedirectStandardError back this process with small anonymous pipes --
    // if nothing ever reads them (because an earlier exception here aborted before scheduling the
    // Task.Run calls), PalServer itself blocks solid the moment its own output fills that pipe
    // buffer: alive, 0% CPU, "responding" (the block is in a worker thread), and no further progress
    // -- including never reaching the point where it binds its UDP game port. Confirmed live: a
    // restrictive ACL on MystTiq-PalServer-Console.log (SYSTEM/Administrators-only write, no access
    // for the account actually running the manager) threw UnauthorizedAccessException on the very
    // first banner-line write, which used to abort this whole method before the two Task.Run calls
    // ever ran. A logging failure must never prevent draining the pipes -- so directory/file writes
    // are now individually best-effort (TryAppendConsoleLine) and the capture tasks always start.
    private void StartConsoleCapture(Process process, CancellationToken token)
    {
        var logDirectory = paths.LogsRoot;
        try { Directory.CreateDirectory(logDirectory); }
        catch
        {
            logDirectory = Path.Combine(paths.ManagerRuntimeRoot, "logs");
            try { Directory.CreateDirectory(logDirectory); } catch { }
        }
        var logPath = Path.Combine(logDirectory, "MystTiq-PalServer-Console.log");
        TryAppendConsoleLine(logPath, $"===== MystTiq PalServer redirected console session started {DateTimeOffset.Now:O} PID {process.Id} =====");
        _ = Task.Run(() => CaptureStreamAsync(process.StandardOutput, "OUT", logPath, token));
        _ = Task.Run(() => CaptureStreamAsync(process.StandardError, "ERR", logPath, token));
    }

    private async Task CaptureStreamAsync(StreamReader reader, string streamName, string logPath, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(token);
                if (line is null) break;
                // The read above is what actually drains the pipe and keeps PalServer unblocked --
                // whether the line successfully makes it to disk is a separate, non-critical concern.
                TryAppendConsoleLine(logPath, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] [{streamName}] {line}");
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (IOException ex) { TryAppendConsoleLine(logPath, $"[CAPTURE] {streamName} reader stopped: {ex.Message}"); }
    }

    private void TryAppendConsoleLine(string path, string line)
    {
        try { AppendConsoleLine(path, line); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void AppendLifecycleConsoleLine(string message)
    {
        var logDirectory = paths.LogsRoot;
        try { Directory.CreateDirectory(logDirectory); }
        catch
        {
            logDirectory = Path.Combine(paths.ManagerRuntimeRoot, "logs");
            try { Directory.CreateDirectory(logDirectory); } catch { }
        }
        TryAppendConsoleLine(Path.Combine(logDirectory, "MystTiq-PalServer-Console.log"), $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] [MYSTTIQ] {message}");
    }

    private void AppendConsoleLine(string path, string line)
    {
        lock (consoleLogGate)
        {
            ConsoleLogRotation.RotateIfNeeded(path);
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }

    private static ServerSessionProcessInfo? SelectNativeProcess(IReadOnlyList<ServerSessionProcessInfo> processes) =>
        processes.FirstOrDefault(process => process.ProcessName.Contains("Shipping", StringComparison.OrdinalIgnoreCase)) ?? processes.FirstOrDefault();

    // v0.7.44.0: machine-wide, path-agnostic scan -- reuses the same raw FindProcessesByName primitive
    // FindManagedServerProcesses/FindProcessesWithMismatchedPath already filter down from, but returns
    // every match unfiltered, tagged with whether it belongs to this profile's own ServerRoot.
    public Task<IReadOnlyList<ServerInstanceInfo>> FindAllInstancesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = NormalizedServerRoot();
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

    // v0.7.44.0: raw termination by PID for a process this profile does NOT track as its own managed
    // server. Deliberately bypasses stateStore entirely -- writing StopRequested here would be false
    // bookkeeping for a process this instance never started or owns, and the process's actual owning
    // session (if any) needs to see this as an unrequested stop so ITS OWN crash-recovery still applies
    // its own real policy rather than being silently suppressed by an unrelated tab's kill.
    public Task<InstanceTerminationResult> TerminateUnmanagedInstanceAsync(int processId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var process = Process.GetProcessById(processId);
            var name = process.ProcessName;
            process.Kill(entireProcessTree: true);
            return Task.FromResult(new InstanceTerminationResult(true, processId, $"Sent a forced termination signal to {name} (PID {processId})."));
        }
        catch (ArgumentException)
        {
            return Task.FromResult(new InstanceTerminationResult(false, processId, $"No process with PID {processId} was found; it may have already exited."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new InstanceTerminationResult(false, processId, $"Failed to terminate PID {processId}: {ex.Message}"));
        }
    }

    private static bool IsUnderRoot(string executablePath, string root)
    {
        try { return Path.GetFullPath(executablePath).StartsWith(root, StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
}
