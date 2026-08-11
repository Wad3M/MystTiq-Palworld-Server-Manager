using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

public interface IServerSessionInspector
{
    ServerSessionSnapshot Capture(long sessionId, int rootPid);
    IReadOnlySet<int> GetDescendantProcessIds(int rootPid);
    IReadOnlyList<ServerSessionProcessInfo> FindProcessesByName(IEnumerable<string> names);
    IReadOnlyList<int> GetGuardedListeningPorts();
}
