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

                string path = string.Empty;
                try { path = process.MainModule?.FileName ?? string.Empty; } catch { }
                result.Add(new ServerSessionProcessInfo(process.Id, 0, process.ProcessName, path, process.Responding));
            }
            catch { }
            finally { process.Dispose(); }
        }
        return result.OrderBy(item => item.ProcessId).ToArray();
    }

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
