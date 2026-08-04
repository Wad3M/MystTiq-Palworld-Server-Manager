using System.Diagnostics;
using System.Net.NetworkInformation;

namespace PalworldManager.Services;

/// <summary>
/// Read-only discovery for Palworld server processes and guarded network ports.
/// This service deliberately owns no lifecycle state and never terminates processes.
/// </summary>
public sealed class ServerProcessDiscoveryService
{
    private readonly string serverRoot;
    private readonly IReadOnlyList<string> processNames;
    private readonly IReadOnlySet<int> guardedPorts;

    public ServerProcessDiscoveryService(
        string serverRoot,
        IEnumerable<string> processNames,
        IEnumerable<int> guardedPorts)
    {
        this.serverRoot = serverRoot ?? string.Empty;
        this.processNames = processNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        this.guardedPorts = guardedPorts.ToHashSet();
    }

    public bool IsPortListening(int port)
    {
        if (!guardedPorts.Contains(port))
            return false;

        return GetGuardedListeningPorts().Contains(port);
    }

    public IReadOnlyList<int> GetGuardedListeningPorts()
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Select(endpoint => endpoint.Port)
                .Where(guardedPorts.Contains)
                .Distinct()
                .OrderBy(port => port)
                .ToArray();
        }
        catch
        {
            return Array.Empty<int>();
        }
    }

    public IReadOnlyList<ServerProcessInfo> Scan()
    {
        var results = new List<ServerProcessInfo>();

        foreach (var name in processNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        var path = TryGetExecutablePath(process);
                        results.Add(new ServerProcessInfo(
                            process.Id,
                            process.ProcessName,
                            path,
                            IsPathInsideServerRoot(path)));
                    }
                    catch
                    {
                        // The process may exit between enumeration and inspection.
                    }
                }
            }
        }

        return results.OrderBy(item => item.ProcessId).ToArray();
    }

    public bool IsRunning(int managedProcessId = -1)
    {
        foreach (var item in Scan())
        {
            if (item.ProcessId == managedProcessId || item.InConfiguredServerRoot)
                return true;
        }

        return false;
    }

    public bool IsPathInsideServerRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(serverRoot))
            return false;

        try
        {
            var root = Path.GetFullPath(serverRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(path);
            return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string TryGetExecutablePath(Process process)
    {
        try { return process.MainModule?.FileName ?? string.Empty; }
        catch { return string.Empty; }
    }
}
