// MystTiq v0.9.5.0: file reviewed for this release (2026-09-28).
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsServerSessionInspector : IServerSessionInspector
{
    private readonly IReadOnlyList<int> guardedPorts;

    public WindowsServerSessionInspector(IEnumerable<int> guardedPorts)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows session inspection requires Windows.");
        this.guardedPorts = guardedPorts.Distinct().OrderBy(port => port).ToArray();
    }

    public ServerSessionSnapshot Capture(long sessionId, int rootPid)
    {
        var processes = FindProcessesByName(ServerPlatformProfile.Windows.ProcessNames);
        return new ServerSessionSnapshot(
            sessionId,
            rootPid,
            DateTime.UtcNow,
            processes,
            Array.Empty<string>(),
            GetGuardedListeningPorts());
    }

    public IReadOnlySet<int> GetDescendantProcessIds(int rootPid) => new HashSet<int>();

    public IReadOnlyList<ServerSessionProcessInfo> FindProcessesByName(IEnumerable<string> names)
    {
        var wanted = names.ToArray();
        var result = new List<ServerSessionProcessInfo>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (!wanted.Any(name => process.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // v0.9.5.0: MainModule needs full access and fails while a process starts or exits, which left the path
                // empty; the lifecycle then took any PalServer for its own (see WindowsServerLifecycleService). The image
                // name needs only limited query access and is readable for the whole life of the process.
                var path = ImagePath(process.Id);
                if (path.Length == 0)
                    try { path = process.MainModule?.FileName ?? string.Empty; } catch { }
                result.Add(new ServerSessionProcessInfo(process.Id, 0, process.ProcessName, path, process.Responding));
            }
            catch { }
            finally { process.Dispose(); }
        }
        return result.OrderBy(item => item.ProcessId).ToArray();
    }

    private static string ImagePath(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero) return string.Empty;
        try
        {
            var buffer = new System.Text.StringBuilder(1024);
            var size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString(0, size) : string.Empty;
        }
        finally { CloseHandle(handle); }
    }

    private const uint ProcessQueryLimitedInformation = 0x1000;

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, System.Text.StringBuilder name, ref int size);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    public IReadOnlyList<int> GetGuardedListeningPorts()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netstat.exe",
                Arguments = "-ano -p udp",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (process is null) return Array.Empty<int>();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            var found = new HashSet<int>();
            foreach (Match match in Regex.Matches(output, @"UDP\s+\S+:(\d+)\s+\*:\*\s+\d+", RegexOptions.IgnoreCase))
                if (int.TryParse(match.Groups[1].Value, out var port) && guardedPorts.Contains(port)) found.Add(port);
            return found.OrderBy(port => port).ToArray();
        }
        catch { return Array.Empty<int>(); }
    }
}
