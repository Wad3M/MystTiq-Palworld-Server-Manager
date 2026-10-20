// MystTiq v1.1.4.0: file reviewed for this release (2026-10-20).
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

/// <summary>
/// v1.1.4.0 (roadmap F-21, found 2026-10-20 taking the README screenshots): a stopped server's status said "A PalServer
/// process is running, but not at the configured path" whenever any other PalServer ran on the computer, so with several
/// servers (the owner has five profiles) every stopped one reported the running one as its own misconfiguration. A server
/// process that a MystTiq service started (its parent, or its PalServer.exe launcher's parent, is mysttiq-server) is that
/// service's server: some other profile's, not this one at the wrong path. One started by hand from another folder is
/// still reported, which is what the message is for.
/// </summary>
public static class ForeignServerProcess
{
    private const int MaximumHops = 4;

    public static bool StartedByMystTiq(ServerSessionProcessInfo process, IReadOnlyList<ServerSessionProcessInfo> serverProcesses, Func<int, string?> processName)
    {
        var parent = process.ParentProcessId;
        for (var hop = 0; hop < MaximumHops && parent > 0; hop++)
        {
            // Shipping-Cmd's parent is the PalServer.exe launcher (itself a server process); look past it.
            if (serverProcesses.FirstOrDefault(p => p.ProcessId == parent) is { } launcher)
            {
                parent = launcher.ParentProcessId;
                continue;
            }
            var name = processName(parent);
            return name is not null && name.StartsWith("mysttiq-server", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    /// <summary>The running process's name, or null when it has exited or cannot be read.</summary>
    public static string? NameOf(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch { return null; }
    }
}
