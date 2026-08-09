namespace PalworldManager.Models;

public sealed record RuntimeStateSnapshot(
    string SessionId,
    bool SessionActive,
    DateTime? SessionStartedAt,
    long Revision,
    DateTime? LastObservedAt,
    string? RuntimeLogPath,
    string RuntimeHealth,
    string RuntimeWarning,
    IReadOnlyList<string> LoadedAliases,
    IReadOnlyList<string> RuntimeErrors)
{
    public int LoadedCount => LoadedAliases.Count;
    public int ErrorCount => RuntimeErrors.Count;
}
