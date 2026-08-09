namespace PalworldManager.Services;

/// <summary>
/// Platform boundary for observational PalServer session/process inspection.
/// Windows currently uses ServerSessionInspector; Linux can provide a separate
/// implementation without changing ServerService or native MOD evidence consumers.
/// </summary>
public interface IServerSessionInspector
{
    ServerSessionSnapshot Capture(long sessionId, int rootPid);
    IReadOnlySet<int> GetDescendantProcessIds(int rootPid);
    IReadOnlyList<int> GetGuardedListeningPorts();
}
