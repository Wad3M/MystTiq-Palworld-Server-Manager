using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MystTiq.Core.Services;

// v0.8.17.0: reads and sets a process's priority and efficiency mode. v0.8.24.0: and which processor cores it runs on.
// - Windows: the priority class, and EcoQoS through SetProcessInformation(ProcessPowerThrottling), the same switch
//   Task Manager's "Efficiency mode" uses (MystTiq does not also drop the priority to Idle as Task Manager does).
// - Linux: niceness, set on every thread (/proc/<pid>/task), because Linux niceness is per thread and a process's
//   existing threads keep theirs. There is no efficiency mode. An unprivileged user can raise niceness but not lower
//   it again, so returning to normal needs root or CAP_SYS_NICE; the error says so.
public enum EfficiencyState { Unknown, Default, On, Off }

public interface IProcessResourceControl
{
    bool SupportsEfficiencyMode { get; }
    ProcessPriorityClass? GetPriority(int processId);
    EfficiencyState GetEfficiency(int processId);
    void SetPriority(int processId, ProcessPriorityClass priority);
    // true = efficiency mode on, false = back to the system default.
    void SetEfficiency(int processId, bool on);
    // v0.8.24.0: the cores a process may run on, as a bit mask (core 0 = bit 0); null when it cannot be read.
    ulong? GetAffinity(int processId);
    void SetAffinity(int processId, ulong mask);
}

public static class ProcessResourceControl
{
    public static IProcessResourceControl ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsProcessResourceControl() : new LinuxProcessResourceControl();

    // .NET's own niceness mapping for the priority classes on Unix.
    public static int Niceness(ProcessPriorityClass priority) => priority switch
    {
        ProcessPriorityClass.Idle => 19,
        ProcessPriorityClass.BelowNormal => 10,
        ProcessPriorityClass.AboveNormal => -5,
        ProcessPriorityClass.High => -11,
        ProcessPriorityClass.RealTime => -20,
        _ => 0,
    };

    public static ProcessPriorityClass FromNiceness(int nice) => nice switch
    {
        >= 15 => ProcessPriorityClass.Idle,
        >= 5 => ProcessPriorityClass.BelowNormal,
        > -3 => ProcessPriorityClass.Normal,
        > -8 => ProcessPriorityClass.AboveNormal,
        > -18 => ProcessPriorityClass.High,
        _ => ProcessPriorityClass.RealTime,
    };
}

public sealed class WindowsProcessResourceControl : IProcessResourceControl
{
    private const int ProcessPowerThrottling = 4;
    private const uint ExecutionSpeed = 0x1;
    private const uint ProcessSetInformation = 0x0200;
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public bool SupportsEfficiencyMode => true;

    public ProcessPriorityClass? GetPriority(int processId)
    {
        try { using var process = Process.GetProcessById(processId); return process.PriorityClass; }
        catch { return null; }
    }

    public void SetPriority(int processId, ProcessPriorityClass priority)
    {
        using var process = Process.GetProcessById(processId);
        process.PriorityClass = priority;
    }

    // v0.8.24.0: the process's affinity within its processor group (at most 64 cores; CoreSelection keeps to that).
    public ulong? GetAffinity(int processId)
    {
        try { using var process = Process.GetProcessById(processId); return (ulong)process.ProcessorAffinity.ToInt64(); }
        catch { return null; }
    }

    public void SetAffinity(int processId, ulong mask)
    {
        using var process = Process.GetProcessById(processId);
        process.ProcessorAffinity = new IntPtr(unchecked((long)mask));
    }

    public EfficiencyState GetEfficiency(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero) return EfficiencyState.Unknown;
        try
        {
            var state = new PowerThrottlingState { Version = 1 };
            if (!GetProcessInformation(handle, ProcessPowerThrottling, ref state, Marshal.SizeOf<PowerThrottlingState>()))
                return EfficiencyState.Unknown;
            if ((state.ControlMask & ExecutionSpeed) == 0) return EfficiencyState.Default;
            return (state.StateMask & ExecutionSpeed) != 0 ? EfficiencyState.On : EfficiencyState.Off;
        }
        finally { CloseHandle(handle); }
    }

    public void SetEfficiency(int processId, bool on)
    {
        var handle = OpenProcess(ProcessSetInformation | ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            // On: MystTiq controls execution speed and asks for throttling. Off: MystTiq lets go, and the system decides.
            var state = new PowerThrottlingState { Version = 1, ControlMask = on ? ExecutionSpeed : 0, StateMask = on ? ExecutionSpeed : 0 };
            if (!SetProcessInformation(handle, ProcessPowerThrottling, ref state, Marshal.SizeOf<PowerThrottlingState>()))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { CloseHandle(handle); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr process, int informationClass, ref PowerThrottlingState information, int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessInformation(IntPtr process, int informationClass, ref PowerThrottlingState information, int size);
}

public sealed class LinuxProcessResourceControl : IProcessResourceControl
{
    private const int PrioProcess = 0;
    private const int PermissionDenied = 13; // EACCES
    private const int NotPermitted = 1;      // EPERM

    public bool SupportsEfficiencyMode => false;

    public ProcessPriorityClass? GetPriority(int processId) =>
        ReadNiceness(processId) is { } nice ? ProcessResourceControl.FromNiceness(nice) : null;

    public EfficiencyState GetEfficiency(int processId) => EfficiencyState.Unknown;

    public void SetEfficiency(int processId, bool on) =>
        throw new PlatformNotSupportedException("Efficiency mode is a Windows feature; on Linux eco mode lowers the priority only.");

    public void SetPriority(int processId, ProcessPriorityClass priority)
    {
        var nice = ProcessResourceControl.Niceness(priority);
        var threads = ThreadIds(processId);
        if (threads.Count == 0) throw new InvalidOperationException($"Process {processId} was not found.");
        foreach (var thread in threads)
        {
            if (setpriority(PrioProcess, thread, nice) == 0) continue;
            var error = Marshal.GetLastPInvokeError();
            if (error is PermissionDenied or NotPermitted)
                throw new UnauthorizedAccessException(
                    $"Linux refused niceness {nice} for process {processId}: raising a priority again needs root or CAP_SYS_NICE, or the MystTiq service installed by v0.8.21.0 or later (its unit sets LimitNICE=-11; run service-install again).");
            if (error != 3) // ESRCH: the thread ended meanwhile
                throw new Win32Exception(error);
        }
    }

    // v0.8.24.0: affinity, like niceness, is per thread on Linux, and a thread keeps its own when the process's first
    // thread changes; so it is read from the first thread and set on every thread. Unlike niceness, a process's owner
    // may widen it again without any privilege.
    public ulong? GetAffinity(int processId)
    {
        var set = new byte[CpuSetBytes];
        if (sched_getaffinity(processId, (IntPtr)set.Length, set) != 0) return null;
        return BitConverter.ToUInt64(set, 0);
    }

    public void SetAffinity(int processId, ulong mask)
    {
        var threads = ThreadIds(processId);
        if (threads.Count == 0) throw new InvalidOperationException($"Process {processId} was not found.");
        var set = new byte[CpuSetBytes];
        BitConverter.GetBytes(mask).CopyTo(set, 0);
        foreach (var thread in threads)
        {
            if (sched_setaffinity(thread, (IntPtr)set.Length, set) == 0) continue;
            var error = Marshal.GetLastPInvokeError();
            if (error is PermissionDenied or NotPermitted)
                throw new UnauthorizedAccessException($"Linux refused to change the cores of process {processId}: it belongs to another user.");
            if (error != 3) // ESRCH: the thread ended meanwhile
                throw new Win32Exception(error);
        }
    }

    // glibc's cpu_set_t: 1024 bits.
    private const int CpuSetBytes = 128;

    [DllImport("libc", SetLastError = true)]
    private static extern int sched_getaffinity(int pid, IntPtr size, byte[] mask);

    [DllImport("libc", SetLastError = true)]
    private static extern int sched_setaffinity(int pid, IntPtr size, byte[] mask);

    public static int? ReadNiceness(int processId)
    {
        try
        {
            // Field 19 of /proc/<pid>/stat; the command name (field 2) may contain spaces, so count from its closing ')'.
            var stat = File.ReadAllText($"/proc/{processId}/stat");
            var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
            return int.Parse(fields[16]);
        }
        catch { return null; }
    }

    private static IReadOnlyList<int> ThreadIds(int processId)
    {
        try
        {
            return Directory.EnumerateDirectories($"/proc/{processId}/task")
                .Select(d => int.TryParse(Path.GetFileName(d), out var id) ? id : 0).Where(id => id > 0).ToArray();
        }
        catch { return []; }
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int setpriority(int which, int who, int priority);
}
