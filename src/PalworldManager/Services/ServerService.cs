using PalworldManager.Models;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;

namespace PalworldManager.Services;

public readonly record struct ServerResourceUsage(double MemoryMb, double CpuPercent);

public sealed record ServerProcessInfo(int ProcessId, string Name, string ExecutablePath, bool InConfiguredServerRoot);


public sealed record ServerSessionProcessInfo(
    int ProcessId,
    int ParentProcessId,
    string Name,
    string ExecutablePath,
    bool Responding);

public sealed record ServerSessionSnapshot(
    long SessionId,
    int RootProcessId,
    DateTime CapturedAt,
    IReadOnlyList<ServerSessionProcessInfo> Processes,
    IReadOnlyList<string> LoadedModules,
    IReadOnlyList<int> ListeningPorts);

public sealed record ServerCleanupReport(
    long SessionId,
    int RootProcessId,
    DateTime StartedAt,
    DateTime CompletedAt,
    IReadOnlyList<int> OrphanProcessIds,
    IReadOnlyList<int> TerminatedProcessIds,
    IReadOnlyList<int> RemainingProcessIds,
    IReadOnlyList<int> RemainingListeningPorts,
    bool Clean,
    string ReportPath);


public sealed record ServerSessionIoDiagnostics(
    long SessionId,
    bool ProcessRunning,
    int StdOutReaders,
    int StdErrReaders,
    int PalLogReaders,
    int RestPollers,
    int PlayerPollers,
    bool CleanupInProgress,
    bool Clean);


public enum ServerLifecycleState
{
    NotInstalled,
    Stopped,
    Starting,
    Running,
    Stopping,
    Hung,
    Crashed
}

public sealed record ServerHealthSnapshot(
    ServerLifecycleState State,
    bool ProcessDetected,
    bool ManagedProcessDetected,
    int ProcessCount,
    string Detail,
    DateTime CheckedAt);

public enum ServerUpdateState
{
    Checking,
    UpToDate,
    Updating,
    Complete,
    Error
}

public readonly record struct ServerUpdateResult(
    ServerUpdateState State,
    int ExitCode,
    string Message);

public sealed class ServerService : IDisposable
{
    private readonly AppSettings settings;
    private readonly object processLock = new();
    private readonly ServerProcessDiscoveryService processDiscovery;
    private readonly ServerResourceMonitor resourceMonitor;
    private readonly SteamServerUpdateService updateService;
    private Process? managedProcess;
    private bool disposed;
    private volatile ServerLifecycleState lifecycleState = ServerLifecycleState.Stopped;
    private DateTime lifecycleChangedUtc = DateTime.UtcNow;
    // Shutdown intent is bound to a specific server session. A late Exited callback
    // from an older PalServer instance must never change the lifecycle state of a
    // newer session.
    private long shutdownRequestedSessionId;
    private long sessionCounter;
    private long activeSessionId;
    private int activeSessionRootPid;
    private DateTime activeSessionStartedAt;
    private ServerSessionSnapshot? activeSessionSnapshot;
    private CancellationTokenSource? sessionIoCts;
    private Task? stdoutReaderTask;
    private Task? stderrReaderTask;
    private int activeStdoutReaders;
    private int activeStderrReaders;
    private int activePalLogReaders;
    private int activeRestPollers;
    private int activePlayerPollers;
    private int cleanupInProgress;

    private const int SwHide = 0;
    private const uint Th32CsSnapProcess = 0x00000002;
    private static readonly int[] GuardedPorts = [8211, 8212, 25575];

    private static readonly string[] Names =
    [
        "PalServer",
        "PalServer-Win64-Shipping",
        "PalServer-Win64-Shipping-Cmd",
        "PalServer-Win64-Test",
        "PalServer-Win64-Test-Cmd"
    ];

    public event Action<string>? OutputReceived;
    public event Action<int>? ServerExited;

    public bool LastExitWasExpected { get; private set; }
    public DateTime? LastExitAt { get; private set; }

    public ServerService(AppSettings settings)
    {
        this.settings = settings;
        processDiscovery = new ServerProcessDiscoveryService(settings.ServerRoot, Names, GuardedPorts);
        resourceMonitor = new ServerResourceMonitor(Names);
        updateService = new SteamServerUpdateService(
            settings,
            IsRunning,
            message => OutputReceived?.Invoke(message));
    }

    public bool HasActiveSession => Volatile.Read(ref activeSessionId) > 0;

    public bool TryAdoptRunningServer()
    {
        ThrowIfDisposed();

        if (HasActiveSession)
            return true;

        foreach (var name in Names)
        {
            foreach (var candidate in Process.GetProcessesByName(name))
            {
                try
                {
                    var path = string.Empty;
                    try { path = candidate.MainModule?.FileName ?? string.Empty; } catch { }
                    if (!IsPathInsideServerRoot(path) || candidate.HasExited)
                    {
                        candidate.Dispose();
                        continue;
                    }

                    var adopted = Process.GetProcessById(candidate.Id);
                    candidate.Dispose();
                    adopted.EnableRaisingEvents = true;

                    var sessionId = Interlocked.Increment(ref sessionCounter);
                    Interlocked.Exchange(ref shutdownRequestedSessionId, 0);

                    lock (processLock)
                    {
                        if (managedProcess is not null)
                        {
                            try
                            {
                                if (!managedProcess.HasExited)
                                {
                                    adopted.Dispose();
                                    return true;
                                }
                            }
                            catch { }
                            try { managedProcess.Dispose(); } catch { }
                        }

                        managedProcess = adopted;
                        activeSessionId = sessionId;
                        activeSessionRootPid = adopted.Id;
                        try { activeSessionStartedAt = adopted.StartTime; }
                        catch { activeSessionStartedAt = DateTime.Now; }
                        sessionIoCts = null;
                        stdoutReaderTask = null;
                        stderrReaderTask = null;
                        Interlocked.Exchange(ref activeStdoutReaders, 0);
                        Interlocked.Exchange(ref activeStderrReaders, 0);
                    }

                    try
                    {
                        activeSessionSnapshot = CaptureSessionSnapshot(sessionId, adopted.Id);
                    }
                    catch
                    {
                        activeSessionSnapshot = new ServerSessionSnapshot(
                            sessionId, adopted.Id, DateTime.Now, Array.Empty<ServerSessionProcessInfo>(),
                            Array.Empty<string>(), GetGuardedListeningPorts());
                    }

                    adopted.Exited += (_, _) =>
                    {
                        var exitCode = -1;
                        try { exitCode = adopted.ExitCode; } catch { }
                        var expectedStop = Volatile.Read(ref shutdownRequestedSessionId) == sessionId;
                        var isCurrentSession = Volatile.Read(ref activeSessionId) == sessionId;

                        lock (processLock)
                        {
                            if (ReferenceEquals(managedProcess, adopted))
                            {
                                managedProcess = null;
                                sessionIoCts = null;
                                stdoutReaderTask = null;
                                stderrReaderTask = null;
                            }
                        }

                        if (!isCurrentSession)
                        {
                            try { adopted.Dispose(); } catch { }
                            return;
                        }

                        LastExitWasExpected = expectedStop;
                        LastExitAt = DateTime.Now;
                        lifecycleState = expectedStop ? ServerLifecycleState.Stopped : ServerLifecycleState.Crashed;
                        lifecycleChangedUtc = DateTime.UtcNow;
                        Interlocked.Exchange(ref activeSessionId, 0);
                        activeSessionRootPid = 0;
                        activeSessionSnapshot = null;
                        try { adopted.Dispose(); } catch { }
                        ServerExited?.Invoke(exitCode);
                    };

                    lifecycleState = ServerLifecycleState.Running;
                    lifecycleChangedUtc = DateTime.UtcNow;
                    OutputReceived?.Invoke($"[SESSION] Adopted existing PalServer process PID {adopted.Id} as server session #{sessionId}. stdout/stderr are external; Pal.log and REST monitoring will be managed by MystTiq.");
                    return true;
                }
                catch
                {
                    try { candidate.Dispose(); } catch { }
                }
            }
        }

        return false;
    }

    public ServerSessionIoDiagnostics GetSessionIoDiagnostics()
    {
        var sessionId = Volatile.Read(ref activeSessionId);
        var processRunning = false;
        try { processRunning = IsRunning(); } catch { }
        var stdout = Volatile.Read(ref activeStdoutReaders);
        var stderr = Volatile.Read(ref activeStderrReaders);
        var palLog = Volatile.Read(ref activePalLogReaders);
        var rest = Volatile.Read(ref activeRestPollers);
        var players = Volatile.Read(ref activePlayerPollers);
        var cleanup = Volatile.Read(ref cleanupInProgress) != 0;
        return new ServerSessionIoDiagnostics(
            sessionId, processRunning, stdout, stderr, palLog, rest, players, cleanup,
            !processRunning && stdout == 0 && stderr == 0 && palLog == 0 && rest == 0 && players == 0 && !cleanup);
    }

    public void SetExternalIoCounts(int palLogReaders, int restPollers, int playerPollers)
    {
        Interlocked.Exchange(ref activePalLogReaders, Math.Max(0, palLogReaders));
        Interlocked.Exchange(ref activeRestPollers, Math.Max(0, restPollers));
        Interlocked.Exchange(ref activePlayerPollers, Math.Max(0, playerPollers));
    }

    public ServerSessionSnapshot? GetActiveSessionSnapshot()
    {
        lock (processLock)
            return activeSessionSnapshot;
    }

    private ServerSessionSnapshot CaptureSessionSnapshot(long sessionId, int rootPid)
    {
        var processEntries = EnumerateProcessTree();
        var descendants = GetDescendantProcessIds(processEntries, rootPid);
        descendants.Add(rootPid);

        var processes = new List<ServerSessionProcessInfo>();
        foreach (var entry in processEntries.Where(x => descendants.Contains(x.ProcessId)))
        {
            try
            {
                using var process = Process.GetProcessById(entry.ProcessId);
                var path = string.Empty;
                try { path = process.MainModule?.FileName ?? string.Empty; } catch { }
                var responding = true;
                try { responding = process.Responding; } catch { }
                processes.Add(new ServerSessionProcessInfo(
                    entry.ProcessId, entry.ParentProcessId, process.ProcessName, path, responding));
            }
            catch
            {
                // Process may exit while the snapshot is being captured.
            }
        }

        var modules = new List<string>();
        try
        {
            using var root = Process.GetProcessById(rootPid);
            foreach (ProcessModule module in root.Modules)
            {
                try
                {
                    var value = string.IsNullOrWhiteSpace(module.FileName)
                        ? module.ModuleName
                        : module.FileName;
                    if (!string.IsNullOrWhiteSpace(value))
                        modules.Add(value);
                }
                catch { }
            }
        }
        catch { }

        return new ServerSessionSnapshot(
            sessionId, rootPid, DateTime.Now, processes,
            modules.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            GetGuardedListeningPorts());
    }

    private static HashSet<int> GetDescendantProcessIds(
        IReadOnlyList<NativeProcessEntry> entries, int rootPid)
    {
        var result = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(rootPid);
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            foreach (var child in entries.Where(x => x.ParentProcessId == parent))
            {
                if (result.Add(child.ProcessId))
                    queue.Enqueue(child.ProcessId);
            }
        }
        return result;
    }

    private static IReadOnlyList<NativeProcessEntry> EnumerateProcessTree()
    {
        var result = new List<NativeProcessEntry>();
        var snapshot = CreateToolhelp32Snapshot(Th32CsSnapProcess, 0);
        if (snapshot == new IntPtr(-1))
            return result;

        try
        {
            var entry = new ProcessEntry32 { dwSize = (uint)Marshal.SizeOf<ProcessEntry32>() };
            if (!Process32First(snapshot, ref entry))
                return result;
            do
            {
                result.Add(new NativeProcessEntry(
                    unchecked((int)entry.th32ProcessID),
                    unchecked((int)entry.th32ParentProcessID),
                    entry.szExeFile ?? string.Empty));
                entry.dwSize = (uint)Marshal.SizeOf<ProcessEntry32>();
            } while (Process32Next(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }
        return result;
    }

    private static IReadOnlyList<int> GetGuardedListeningPorts()
    {
        try
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            return listeners.Select(x => x.Port)
                .Where(port => GuardedPorts.Contains(port))
                .Distinct().OrderBy(x => x).ToList();
        }
        catch
        {
            return Array.Empty<int>();
        }
    }


    public bool IsPortListening(int port) => processDiscovery.IsPortListening(port);

    public IReadOnlyList<ServerProcessInfo> ScanServerProcesses() => processDiscovery.Scan();

    private bool IsPathInsideServerRoot(string path) => processDiscovery.IsPathInsideServerRoot(path);

    public bool IsRunning()
    {
        lock (processLock)
        {
            if (managedProcess is not null)
            {
                try
                {
                    if (!managedProcess.HasExited)
                        return true;
                }
                catch
                {
                    // Fall back to discovery when the tracked Process is stale.
                }
            }
        }

        var managedPid = -1;
        lock (processLock)
        {
            try { managedPid = managedProcess?.Id ?? -1; } catch { }
        }
        return processDiscovery.IsRunning(managedPid);
    }

    public ServerHealthSnapshot GetHealthSnapshot()
    {
        var processes = ScanServerProcesses().Where(p => p.InConfiguredServerRoot).ToList();
        var processDetected = processes.Count > 0;
        var managedDetected = false;
        lock (processLock)
        {
            try { managedDetected = managedProcess is not null && !managedProcess.HasExited; } catch { }
        }

        var state = lifecycleState;
        if (!processDetected && state is ServerLifecycleState.Running or ServerLifecycleState.Starting)
            state = ServerLifecycleState.Crashed;
        else if (!processDetected && state == ServerLifecycleState.Stopping)
            state = ServerLifecycleState.Stopped;
        else if (processDetected && state == ServerLifecycleState.Stopped)
            state = ServerLifecycleState.Running;

        if (state == ServerLifecycleState.Starting && DateTime.UtcNow - lifecycleChangedUtc > TimeSpan.FromSeconds(45) && processDetected)
            state = ServerLifecycleState.Running;
        // A normal Palworld shutdown includes the configured warning/countdown. Do not
        // declare the process hung while that countdown is still legitimately running.
        var shutdownHungThreshold = TimeSpan.FromSeconds(Math.Max(45, settings.RestartWarningSeconds + 45));
        if (state == ServerLifecycleState.Stopping && DateTime.UtcNow - lifecycleChangedUtc > shutdownHungThreshold && processDetected)
            state = ServerLifecycleState.Hung;

        lifecycleState = state;
        return new ServerHealthSnapshot(
            state, processDetected, managedDetected, processes.Count,
            state switch
            {
                ServerLifecycleState.Hung => "Palworld process remains after the shutdown timeout.",
                ServerLifecycleState.Crashed => "The managed Palworld process exited unexpectedly.",
                ServerLifecycleState.Starting => "Palworld is starting.",
                ServerLifecycleState.Stopping => "Palworld is stopping.",
                ServerLifecycleState.Running => "Palworld process is running.",
                _ => "No Palworld server process is running."
            }, DateTime.Now);
    }

    public void MarkStopping()
    {
        // Keep shutdown intent separate from the display lifecycle state. The health
        // monitor is allowed to move Stopping -> Hung without turning a requested
        // shutdown into an unexpected crash.
        var sessionId = Volatile.Read(ref activeSessionId);
        if (sessionId > 0)
            Interlocked.Exchange(ref shutdownRequestedSessionId, sessionId);
        lifecycleState = ServerLifecycleState.Stopping;
        lifecycleChangedUtc = DateTime.UtcNow;
    }

    public ServerResourceUsage GetResourceUsage() => resourceMonitor.Sample();

    public double MemoryMb() => GetResourceUsage().MemoryMb;

    public void Start(bool noMods = false)
    {
        ThrowIfDisposed();

        if (IsRunning())
            throw new InvalidOperationException("Server is already running.");

        // Clear any already-exited Process object from the previous run before a
        // completely new session is created.
        ClearExitedManagedProcess();
        activeSessionId = Interlocked.Increment(ref sessionCounter);
        var sessionId = activeSessionId;
        Interlocked.Exchange(ref shutdownRequestedSessionId, 0);
        lifecycleState = ServerLifecycleState.Starting;
        lifecycleChangedUtc = DateTime.UtcNow;

        var executable = ResolveServerExecutable();
        var arguments = EnsureLoggingArguments(settings.LaunchArguments);
        arguments = EnsureWorkshopArgument(arguments);

        if (noMods)
            arguments = AppendArgument(arguments, "-NoMods");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            // Unreal server binaries should run from their own folder. This also
            // prevents the root PalServer bootstrapper from spawning a visible
            // Shipping-Cmd child process.
            WorkingDirectory = Path.GetDirectoryName(executable) ?? settings.ServerRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        EventHandler? exitHandler = null;
        exitHandler = (_, _) =>
        {
            var exitCode = -1;

            try
            {
                exitCode = process.ExitCode;
            }
            catch
            {
                // Keep fallback exit code.
            }

            var expectedStop = Volatile.Read(ref shutdownRequestedSessionId) == sessionId;
            var isCurrentSession = Volatile.Read(ref activeSessionId) == sessionId;

            // Cancel this session's stream readers immediately. Any late reader output
            // is session-gated and cannot mutate a newer PalServer session.
            CancellationTokenSource? ioCts = null;
            Task? stdoutTask = null;
            Task? stderrTask = null;
            lock (processLock)
            {
                if (ReferenceEquals(managedProcess, process))
                {
                    managedProcess = null;
                    ioCts = sessionIoCts;
                    stdoutTask = stdoutReaderTask;
                    stderrTask = stderrReaderTask;
                    sessionIoCts = null;
                    stdoutReaderTask = null;
                    stderrReaderTask = null;
                }
            }
            try { ioCts?.Cancel(); } catch { }
            if (exitHandler is not null)
            {
                try { process.Exited -= exitHandler; } catch { }
            }

            if (isCurrentSession)
            {
                LastExitWasExpected = expectedStop;
                LastExitAt = DateTime.Now;
                lifecycleState = expectedStop ? ServerLifecycleState.Stopped : ServerLifecycleState.Crashed;
                lifecycleChangedUtc = DateTime.UtcNow;
                if (expectedStop)
                    Interlocked.CompareExchange(ref shutdownRequestedSessionId, 0, sessionId);

                OutputReceived?.Invoke(
                    $"Server session #{sessionId} process exited with code {exitCode}." +
                    (expectedStop ? "" : " Exit was not part of a requested shutdown."));

                ServerExited?.Invoke(exitCode);
            }
            else
            {
                OutputReceived?.Invoke(
                    $"Server session #{sessionId} cleanup completed after a newer session became active; late lifecycle state was ignored.");
            }

            _ = Task.Run(async () =>
            {
                await AwaitReaderShutdownAsync(sessionId, stdoutTask, stderrTask, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                try { ioCts?.Dispose(); } catch { }
                try { process.Dispose(); } catch { }
                OutputReceived?.Invoke($"Server session #{sessionId} resources released.");
            });
        };

        process.Exited += exitHandler;

        try
        {
            if (!process.Start())
                throw new InvalidOperationException(
                    "The Palworld server process could not be started.");

            lock (processLock)
            {
                managedProcess?.Dispose();
                managedProcess = process;
                activeSessionRootPid = process.Id;
                activeSessionStartedAt = DateTime.Now;
                activeSessionSnapshot = null;
            }

            // Process/module inspection can be comparatively expensive on Windows,
            // particularly while UE4SS is injecting. Never perform it on WPF's UI
            // thread. Capture once shortly after launch and again after startup settles.
            _ = Task.Run(async () =>
            {
                foreach (var delay in new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10) })
                {
                    try
                    {
                        await Task.Delay(delay).ConfigureAwait(false);
                        if (disposed || Volatile.Read(ref activeSessionId) != sessionId)
                            return;
                        var snapshot = CaptureSessionSnapshot(sessionId, process.Id);
                        lock (processLock)
                        {
                            if (activeSessionId == sessionId)
                                activeSessionSnapshot = snapshot;
                        }
                    }
                    catch { }
                }
            });

            var ioCts = new CancellationTokenSource();
            var stdoutTask = PumpProcessStreamAsync(process.StandardOutput, sessionId, isError: false, ioCts.Token);
            var stderrTask = PumpProcessStreamAsync(process.StandardError, sessionId, isError: true, ioCts.Token);
            lock (processLock)
            {
                if (activeSessionId == sessionId && ReferenceEquals(managedProcess, process))
                {
                    sessionIoCts = ioCts;
                    stdoutReaderTask = stdoutTask;
                    stderrReaderTask = stderrTask;
                }
                else
                {
                    ioCts.Cancel();
                }
            }

            OutputReceived?.Invoke($"[SESSION #{sessionId}] stdout reader started.");
            OutputReceived?.Invoke($"[SESSION #{sessionId}] stderr reader started.");

            lifecycleState = ServerLifecycleState.Running;
            lifecycleChangedUtc = DateTime.UtcNow;

            OutputReceived?.Invoke(
                $"Server session #{sessionId} started: {Path.GetFileName(executable)}");
            OutputReceived?.Invoke(
                "Standard output and Pal.log are being forwarded to the dashboard.");

            // This is a secondary safeguard. Normally no window is ever created
            // because the Shipping-Cmd executable is launched directly with
            // CreateNoWindow and redirected output.
            _ = Task.Run(HidePalworldWindowsDuringStartupAsync);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    private async Task PumpProcessStreamAsync(StreamReader reader, long sessionId, bool isError, CancellationToken token)
    {
        if (isError)
            Interlocked.Increment(ref activeStderrReaders);
        else
            Interlocked.Increment(ref activeStdoutReaders);

        try
        {
            while (!token.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (line is null)
                    break;
                if (Volatile.Read(ref activeSessionId) != sessionId)
                    break;
                if (!string.IsNullOrWhiteSpace(line))
                    OutputReceived?.Invoke(isError ? "[stderr] " + line : line);
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested && Volatile.Read(ref activeSessionId) == sessionId)
                OutputReceived?.Invoke($"[SESSION #{sessionId}] {(isError ? "stderr" : "stdout")} reader warning: {ex.Message}");
        }
        finally
        {
            if (isError)
                Interlocked.Decrement(ref activeStderrReaders);
            else
                Interlocked.Decrement(ref activeStdoutReaders);
            OutputReceived?.Invoke($"[SESSION #{sessionId}] {(isError ? "stderr" : "stdout")} reader stopped.");
        }
    }

    private async Task AwaitReaderShutdownAsync(long sessionId, Task? stdoutTask, Task? stderrTask, TimeSpan timeout)
    {
        var tasks = new[] { stdoutTask, stderrTask }.Where(task => task is not null).Cast<Task>().ToArray();
        if (tasks.Length == 0)
            return;
        try
        {
            var all = Task.WhenAll(tasks);
            var completed = await Task.WhenAny(all, Task.Delay(timeout)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, all))
                OutputReceived?.Invoke($"[SESSION #{sessionId}] stream-reader cleanup exceeded {timeout.TotalSeconds:0.#} seconds; continuing without blocking the UI.");
            else
                await all.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OutputReceived?.Invoke($"[SESSION #{sessionId}] stream-reader cleanup warning: {ex.Message}");
        }
    }

    public async Task CancelActiveSessionIoAsync(TimeSpan timeout)
    {
        var sessionId = Volatile.Read(ref activeSessionId);
        CancellationTokenSource? cts;
        Task? stdoutTask;
        Task? stderrTask;
        lock (processLock)
        {
            cts = sessionIoCts;
            stdoutTask = stdoutReaderTask;
            stderrTask = stderrReaderTask;
        }
        if (cts is null)
            return;
        OutputReceived?.Invoke($"[SESSION #{sessionId}] stream-reader cancellation requested.");
        try { cts.Cancel(); } catch { }
        await AwaitReaderShutdownAsync(sessionId, stdoutTask, stderrTask, timeout).ConfigureAwait(false);
    }

    private string ResolveServerExecutable()
    {
        var candidates = new[]
        {
            // Current dedicated-server binary. Launching this directly is the
            // important part: PalServer.exe is only a bootstrapper and spawns
            // this executable in a separate visible console window.
            Path.Combine(
                settings.ServerRoot,
                "Pal",
                "Binaries",
                "Win64",
                "PalServer-Win64-Shipping-Cmd.exe"),

            // Alternate names used by older/test server builds.
            Path.Combine(
                settings.ServerRoot,
                "Pal",
                "Binaries",
                "Win64",
                "PalServer-Win64-Test-Cmd.exe"),

            Path.Combine(
                settings.ServerRoot,
                "Pal",
                "Binaries",
                "Win64",
                "PalServer-Win64-Shipping.exe"),

            Path.Combine(
                settings.ServerRoot,
                "Pal",
                "Binaries",
                "Win64",
                "PalServer-Win64-Test.exe"),

            // Last-resort compatibility fallback. This may create a separate
            // console on installations whose real binary cannot be found.
            settings.ServerExe
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException(
                "No Palworld server executable was found.",
                string.Join(Environment.NewLine, candidates));
    }

    private async Task HidePalworldWindowsDuringStartupAsync()
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (disposed)
                return;

            foreach (var name in Names)
            {
                var processes = Process.GetProcessesByName(name);

                foreach (var process in processes)
                {
                    using (process)
                    {
                        try
                        {
                            process.Refresh();
                            var handle = process.MainWindowHandle;

                            if (handle != IntPtr.Zero)
                                ShowWindow(handle, SwHide);
                        }
                        catch
                        {
                            // The process may be starting or exiting.
                        }
                    }
                }
            }

            await Task.Delay(500);
        }
    }


    private string EnsureWorkshopArgument(string arguments)
    {
        if (ContainsArgument(arguments, "-workshopdir="))
            return arguments;

        Directory.CreateDirectory(settings.WorkshopRoot);
        var escapedPath = settings.WorkshopRoot.Replace("\"", "\\\"");
        return AppendArgument(arguments, $"-workshopdir=\"{escapedPath}\"");
    }

    private static string EnsureLoggingArguments(string arguments)
    {
        var result = arguments?.Trim() ?? string.Empty;

        if (!ContainsArgument(result, "-log"))
            result = AppendArgument(result, "-log");

        if (!ContainsArgument(result, "-logformat="))
            result = AppendArgument(result, "-logformat=text");

        return result;
    }

    private static bool ContainsArgument(string arguments, string argument)
    {
        return arguments.Contains(argument, StringComparison.OrdinalIgnoreCase);
    }

    private static string AppendArgument(string arguments, string argument)
    {
        return string.IsNullOrWhiteSpace(arguments)
            ? argument
            : arguments.TrimEnd() + " " + argument;
    }

    public Task<ServerUpdateResult> UpdateServerAsync(
        Action<ServerUpdateState, string>? statusChanged,
        CancellationToken token)
    {
        ThrowIfDisposed();
        return updateService.UpdateAsync(statusChanged, token);
    }

    public Task<ServerCleanupReport> CleanupSessionAfterShutdownAsync(CancellationToken token)
    {
        // Process/module inspection and forced orphan cleanup always run away from
        // WPF's dispatcher. Session Guardian is diagnostic/recovery work and must
        // never be able to freeze the manager UI.
        return Task.Run(async () =>
        {
            Interlocked.Exchange(ref cleanupInProgress, 1);
            try
            {
            token.ThrowIfCancellationRequested();
            var sessionId = Volatile.Read(ref activeSessionId);
            int rootPid;
            DateTime startedAt;
            ServerSessionSnapshot? startupSnapshot;
            lock (processLock)
            {
                rootPid = activeSessionRootPid;
                startedAt = activeSessionStartedAt;
                startupSnapshot = activeSessionSnapshot;
            }

            var nativeEntries = EnumerateProcessTree();
            var descendantIds = rootPid > 0
                ? GetDescendantProcessIds(nativeEntries, rootPid)
                : new HashSet<int>();

            if (startupSnapshot is not null)
            {
                foreach (var item in startupSnapshot.Processes)
                    if (item.ProcessId != rootPid)
                        descendantIds.Add(item.ProcessId);
            }

            // Also include Palworld executables from the configured server root. This
            // catches orphaned launcher/shipping processes whose original parent has
            // already exited and been re-parented by Windows.
            foreach (var item in ScanServerProcesses().Where(x => x.InConfiguredServerRoot))
                descendantIds.Add(item.ProcessId);

            descendantIds.Remove(rootPid);
            descendantIds.Remove(Environment.ProcessId);
            var orphans = descendantIds.OrderBy(x => x).ToList();
            // v2.0.1.7 safety hotfix:
            // A normal graceful shutdown must NEVER kill processes automatically based only
            // on a previously captured PID. Windows can recycle process IDs, and an orphan
            // candidate may no longer be the same process that belonged to PalServer.
            // Session Guardian is diagnostic-only on the successful shutdown path. Forced
            // termination remains available through the explicit Force Cleanup/Force Stop
            // workflow where the server root/process identity is revalidated immediately.
            var terminated = new List<int>();
            var remainingPids = orphans.Where(IsProcessAlive).OrderBy(x => x).ToList();
            var remainingPorts = GetGuardedListeningPorts().ToList();
            var clean = remainingPids.Count == 0 && remainingPorts.Count == 0 && !HasDetectedServerProcess();
            var completedAt = DateTime.Now;
            var reportPath = WriteSessionCleanupReport(
                sessionId, rootPid, startedAt, completedAt, startupSnapshot,
                orphans, terminated, remainingPids, remainingPorts, clean);

            OutputReceived?.Invoke(
                $"[SESSION GUARDIAN] Session #{sessionId} cleanup " +
                (clean ? "verified clean." : "detected leftovers requiring attention; no automatic termination was attempted.") +
                $" Report: {reportPath}");

            return new ServerCleanupReport(
                sessionId, rootPid, startedAt, completedAt, orphans, terminated,
                remainingPids, remainingPorts, clean, reportPath);
            }
            finally
            {
                Interlocked.Exchange(ref cleanupInProgress, 0);
            }
        }, token);
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch { return false; }
    }

    private string WriteSessionCleanupReport(
        long sessionId, int rootPid, DateTime startedAt, DateTime completedAt,
        ServerSessionSnapshot? startupSnapshot, IReadOnlyList<int> orphans,
        IReadOnlyList<int> terminated, IReadOnlyList<int> remainingPids,
        IReadOnlyList<int> remainingPorts, bool clean)
    {
        try
        {
            var diagnostics = Path.Combine(settings.LogsRoot, "Diagnostics");
            Directory.CreateDirectory(diagnostics);
            var stamp = completedAt.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            var basePath = Path.Combine(diagnostics, $"SessionCleanup_{stamp}");
            var jsonPath = basePath + ".json";
            var txtPath = basePath + ".txt";

            var payload = new
            {
                SessionId = sessionId,
                RootProcessId = rootPid,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Result = clean ? "Clean" : "Incomplete",
                StartupProcesses = startupSnapshot?.Processes ?? Array.Empty<ServerSessionProcessInfo>(),
                LoadedModules = startupSnapshot?.LoadedModules ?? Array.Empty<string>(),
                StartupListeningPorts = startupSnapshot?.ListeningPorts ?? Array.Empty<int>(),
                OrphanProcessIds = orphans,
                TerminatedProcessIds = terminated,
                RemainingProcessIds = remainingPids,
                RemainingListeningPorts = remainingPorts
            };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

            var text = new StringBuilder();
            text.AppendLine("MystTiq Palworld Server - Session Guardian Cleanup Report");
            text.AppendLine($"Session: #{sessionId}");
            text.AppendLine($"Root PID: {rootPid}");
            text.AppendLine($"Started: {startedAt:O}");
            text.AppendLine($"Cleanup completed: {completedAt:O}");
            text.AppendLine($"Result: {(clean ? "CLEAN" : "INCOMPLETE")}");
            text.AppendLine($"Orphans detected: {(orphans.Count == 0 ? "None" : string.Join(", ", orphans))}");
            text.AppendLine($"Processes terminated: {(terminated.Count == 0 ? "None" : string.Join(", ", terminated))}");
            text.AppendLine($"Processes remaining: {(remainingPids.Count == 0 ? "None" : string.Join(", ", remainingPids))}");
            text.AppendLine($"Guarded ports remaining: {(remainingPorts.Count == 0 ? "None" : string.Join(", ", remainingPorts))}");
            if (startupSnapshot is not null)
            {
                text.AppendLine();
                text.AppendLine("Startup process tree:");
                foreach (var item in startupSnapshot.Processes)
                    text.AppendLine($"  PID {item.ProcessId} parent {item.ParentProcessId} {item.Name} {item.ExecutablePath}");
                text.AppendLine();
                text.AppendLine("Loaded modules captured:");
                foreach (var module in startupSnapshot.LoadedModules)
                    text.AppendLine("  " + module);
            }
            File.WriteAllText(txtPath, text.ToString());
            return txtPath;
        }
        catch (Exception exception)
        {
            return "Cleanup report could not be written: " + exception.Message;
        }
    }

    public async Task ForceStopAsync()
    {
        MarkStopping();
        Process? ownedProcess;

        lock (processLock)
        {
            ownedProcess = managedProcess;
        }

        // Cancel session-owned stdout/stderr readers before terminating PalServer.
        // Cancellation is bounded; a stuck pipe reader must never block Stop/Restart.
        try { await CancelActiveSessionIoAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); } catch { }

        if (ownedProcess is not null)
        {
            try
            {
                if (!ownedProcess.HasExited)
                    ownedProcess.Kill(entireProcessTree: true);
            }
            catch
            {
                // Continue with process-name fallback.
            }
        }

        KillDetectedServerProcesses();

        // Windows can report the tracked process as alive briefly after the
        // process tree has been terminated. Verify by polling the real Palworld
        // executable names rather than relying on the stale Process object.
        var verificationDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(6);

        while (DateTime.UtcNow < verificationDeadline)
        {
            if (!HasDetectedServerProcess())
            {
                ClearExitedManagedProcess();
                lifecycleState = ServerLifecycleState.Stopped;
                lifecycleChangedUtc = DateTime.UtcNow;
                try { await CleanupSessionAfterShutdownAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
                return;
            }

            await Task.Delay(500);
            KillDetectedServerProcesses();
        }

        if (HasDetectedServerProcess())
            throw new InvalidOperationException(
                "A Palworld server process is still running after the shutdown timeout.");

        ClearExitedManagedProcess();
        lifecycleState = ServerLifecycleState.Stopped;
        lifecycleChangedUtc = DateTime.UtcNow;
        try { await CleanupSessionAfterShutdownAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
    }

    private bool HasDetectedServerProcess()
    {
        var ownedPid = -1;
        lock (processLock)
        {
            try { ownedPid = managedProcess?.Id ?? -1; } catch { }
        }

        foreach (var name in Names)
        {
            var processes = Process.GetProcessesByName(name);
            foreach (var process in processes)
            {
                using (process)
                {
                    try
                    {
                        if (process.Id == ownedPid && !process.HasExited)
                            return true;
                        var path = string.Empty;
                        try { path = process.MainModule?.FileName ?? string.Empty; } catch { }
                        if (IsPathInsideServerRoot(path) && !process.HasExited)
                            return true;
                    }
                    catch { }
                }
            }
        }
        return false;
    }

    private void KillDetectedServerProcesses()
    {
        var ownedPid = -1;
        lock (processLock)
        {
            try { ownedPid = managedProcess?.Id ?? -1; } catch { }
        }

        foreach (var name in Names)
        {
            var processes = Process.GetProcessesByName(name);
            foreach (var process in processes)
            {
                using (process)
                {
                    try
                    {
                        var path = string.Empty;
                        try { path = process.MainModule?.FileName ?? string.Empty; } catch { }
                        var belongsToThisServer = process.Id == ownedPid || IsPathInsideServerRoot(path);
                        if (belongsToThisServer && !process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // Best effort. Verification runs after kill attempts.
                    }
                }
            }
        }
    }

    private void ClearExitedManagedProcess()
    {
        lock (processLock)
        {
            if (managedProcess is null)
                return;

            try
            {
                if (!managedProcess.HasExited)
                    return;
            }
            catch
            {
                // A disposed or invalid process is no longer useful for status.
            }

            // The Exited callback may clear managedProcess between HasExited and disposal.
            // Capture the reference while holding the lock so cleanup is idempotent.
            var exitedProcess = managedProcess;
            managedProcess = null;
            try { exitedProcess?.Dispose(); } catch { /* already disposed/invalid */ }
        }
    }

    public Task WaitForExitAsync(
        TimeSpan timeout,
        CancellationToken token)
    {
        // Process/module inspection can occasionally block on Windows while Unreal
        // is tearing down. Keep the entire wait loop off the WPF dispatcher thread
        // so a slow or stuck Palworld shutdown can never freeze MystTiq's UI.
        return Task.Run(async () =>
        {
            var end = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < end)
            {
                token.ThrowIfCancellationRequested();
                if (!IsRunning())
                    return;
                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }, token);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        CancellationTokenSource? ioCts;
        lock (processLock)
        {
            ioCts = sessionIoCts;
            sessionIoCts = null;
            stdoutReaderTask = null;
            stderrReaderTask = null;
            if (managedProcess is not null)
            {
                managedProcess.Dispose();
                managedProcess = null;
            }
        }
        try { ioCts?.Cancel(); } catch { }
        try { ioCts?.Dispose(); } catch { }
        Interlocked.Exchange(ref shutdownRequestedSessionId, 0);
    }

    private readonly record struct NativeProcessEntry(int ProcessId, int ParentProcessId, string Name);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct ProcessEntry32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);
}
