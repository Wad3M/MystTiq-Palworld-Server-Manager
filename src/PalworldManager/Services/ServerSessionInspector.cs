using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace PalworldManager.Services;

/// <summary>
/// Windows process/session inspection boundary for PalServer.
/// This class is observational only: it never starts, stops, injects into, or modifies a process.
/// Keeping process-tree and loaded-module inspection outside ServerService prepares the
/// runtime evidence path for a future platform-specific implementation.
/// </summary>
public sealed class ServerSessionInspector : IServerSessionInspector
{
    private const uint Th32CsSnapProcess = 0x00000002;
    private readonly int[] guardedPorts;

    public ServerSessionInspector(IEnumerable<int> guardedPorts)
    {
        this.guardedPorts = guardedPorts.Distinct().OrderBy(x => x).ToArray();
    }

    public ServerSessionSnapshot Capture(long sessionId, int rootPid)
    {
        var processEntries = EnumerateProcessTree();
        var descendants = BuildDescendantProcessIds(processEntries, rootPid);
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
                // A process can legitimately exit while a snapshot is being captured.
            }
        }

        var modules = new List<string>();
        foreach (var processInfo in processes)
        {
            try
            {
                using var runtimeProcess = Process.GetProcessById(processInfo.ProcessId);
                foreach (ProcessModule module in runtimeProcess.Modules)
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
        }

        return new ServerSessionSnapshot(
            sessionId,
            rootPid,
            DateTime.Now,
            processes,
            modules.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            GetGuardedListeningPorts());
    }

    public IReadOnlySet<int> GetDescendantProcessIds(int rootPid)
    {
        if (rootPid <= 0)
            return new HashSet<int>();

        var entries = EnumerateProcessTree();
        return BuildDescendantProcessIds(entries, rootPid);
    }

    public IReadOnlyList<int> GetGuardedListeningPorts()
    {
        try
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            return listeners.Select(x => x.Port)
                .Where(port => guardedPorts.Contains(port))
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }
        catch
        {
            return Array.Empty<int>();
        }
    }

    private static HashSet<int> BuildDescendantProcessIds(
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
}
