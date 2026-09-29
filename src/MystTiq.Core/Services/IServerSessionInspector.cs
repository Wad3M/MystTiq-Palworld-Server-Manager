// MystTiq v0.9.8.0: file reviewed for this release (2026-09-29).
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

public interface IServerSessionInspector
{
    ServerSessionSnapshot Capture(long sessionId, int rootPid);
    IReadOnlySet<int> GetDescendantProcessIds(int rootPid);
    IReadOnlyList<ServerSessionProcessInfo> FindProcessesByName(IEnumerable<string> names);
    IReadOnlyList<int> GetGuardedListeningPorts();
}
