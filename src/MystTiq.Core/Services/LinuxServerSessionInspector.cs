using System.Diagnostics;
using System.Globalization;
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

/// <summary>
/// Read-only Linux PalServer process/session inspection using procfs.
/// No signals are sent and no process state is modified.
/// </summary>
public sealed class LinuxServerSessionInspector : IServerSessionInspector
{
    private readonly HashSet<int> guardedPorts;

    public LinuxServerSessionInspector(IEnumerable<int> guardedPorts)
    {
        this.guardedPorts = guardedPorts.ToHashSet();
    }

    public ServerSessionSnapshot Capture(long sessionId, int rootPid)
    {
        EnsureLinux();
        var entries = EnumerateProcesses();
        var descendants = BuildDescendants(entries, rootPid);
        descendants.Add(rootPid);

        var processes = entries
            .Where(entry => descendants.Contains(entry.ProcessId))
            .Select(entry => new ServerSessionProcessInfo(
                entry.ProcessId,
                entry.ParentProcessId,
                entry.Name,
                entry.ExecutablePath,
                true))
            .OrderBy(entry => entry.ProcessId)
            .ToList();

        var modules = processes
            .SelectMany(process => ReadMappedFiles(process.ProcessId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        return new ServerSessionSnapshot(
            sessionId,
            rootPid,
            DateTime.Now,
            processes,
            modules,
            GetGuardedListeningPorts());
    }

    public IReadOnlySet<int> GetDescendantProcessIds(int rootPid)
    {
        EnsureLinux();
        if (rootPid <= 0)
            return new HashSet<int>();
        return BuildDescendants(EnumerateProcesses(), rootPid);
    }

    public IReadOnlyList<int> GetGuardedListeningPorts()
    {
        EnsureLinux();
        var found = new HashSet<int>();
        foreach (var source in new[]
        {
            (Path: "/proc/net/tcp", IsTcp: true),
            (Path: "/proc/net/tcp6", IsTcp: true),
            (Path: "/proc/net/udp", IsTcp: false),
            (Path: "/proc/net/udp6", IsTcp: false)
        })
        {
            foreach (var port in ReadListeningPorts(source.Path, source.IsTcp))
            {
                if (guardedPorts.Contains(port))
                    found.Add(port);
            }
        }
        return found.OrderBy(port => port).ToList();
    }

    public IReadOnlyList<ServerSessionProcessInfo> FindProcessesByName(IEnumerable<string> names)
    {
        EnsureLinux();
        var wanted = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return EnumerateProcesses()
            .Where(entry => wanted.Contains(entry.Name))
            .Select(entry => new ServerSessionProcessInfo(
                entry.ProcessId,
                entry.ParentProcessId,
                entry.Name,
                entry.ExecutablePath,
                true))
            .OrderBy(entry => entry.ProcessId)
            .ToList();
    }

    private static IReadOnlyList<ProcEntry> EnumerateProcesses()
    {
        var result = new List<ProcEntry>();
        foreach (var directory in Directory.EnumerateDirectories("/proc"))
        {
            var name = Path.GetFileName(directory);
            if (!int.TryParse(name, out var pid))
                continue;

            try
            {
                var stat = File.ReadAllText(Path.Combine(directory, "stat"));
                var closeParen = stat.LastIndexOf(')');
                var openParen = stat.IndexOf('(');
                if (openParen < 0 || closeParen <= openParen)
                    continue;

                var processName = stat[(openParen + 1)..closeParen];
                var remainder = stat[(closeParen + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (remainder.Length < 2 || !int.TryParse(remainder[1], out var parentPid))
                    continue;

                var executablePath = ResolveExecutablePath(pid);
                var executableName = string.IsNullOrWhiteSpace(executablePath)
                    ? processName
                    : Path.GetFileName(executablePath);

                result.Add(new ProcEntry(pid, parentPid, executableName, executablePath));
            }
            catch
            {
                // Processes can exit between directory enumeration and stat reads.
            }
        }
        return result;
    }

    private static HashSet<int> BuildDescendants(IReadOnlyList<ProcEntry> entries, int rootPid)
    {
        var result = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(rootPid);
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            foreach (var child in entries.Where(entry => entry.ParentProcessId == parent))
            {
                if (result.Add(child.ProcessId))
                    queue.Enqueue(child.ProcessId);
            }
        }
        return result;
    }

    private static string ResolveExecutablePath(int pid)
    {
        try
        {
            var target = new FileInfo($"/proc/{pid}/exe").ResolveLinkTarget(returnFinalTarget: true);
            return target?.FullName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IEnumerable<string> ReadMappedFiles(int pid)
    {
        var path = $"/proc/{pid}/maps";
        if (!File.Exists(path))
            yield break;

        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch { yield break; }

        foreach (var line in lines)
        {
            var slash = line.IndexOf('/');
            if (slash < 0)
                continue;
            var mappedPath = line[slash..].Trim();
            if (!string.IsNullOrWhiteSpace(mappedPath))
                yield return mappedPath;
        }
    }

    private static IEnumerable<int> ReadListeningPorts(string path, bool isTcp)
    {
        if (!File.Exists(path))
            yield break;

        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch { yield break; }

        foreach (var line in lines.Skip(1))
        {
            var columns = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 4)
                continue;

            var socketState = columns[3];
            if (isTcp && !socketState.Equals("0A", StringComparison.OrdinalIgnoreCase))
                continue; // TCP LISTEN
            if (!isTcp && !socketState.Equals("07", StringComparison.OrdinalIgnoreCase))
                continue; // UDP UNCONN/listener

            var localAddress = columns[1];
            var separator = localAddress.LastIndexOf(':');
            if (separator < 0)
                continue;

            var portHex = localAddress[(separator + 1)..];
            if (int.TryParse(portHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var port))
                yield return port;
        }
    }

    private static void EnsureLinux()
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("LinuxServerSessionInspector requires Linux procfs.");
    }

    private readonly record struct ProcEntry(int ProcessId, int ParentProcessId, string Name, string ExecutablePath);
}
