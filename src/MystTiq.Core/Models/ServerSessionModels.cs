namespace MystTiq.Core.Models;

public sealed record ServerSessionProcessInfo(
    int ProcessId,
    int ParentProcessId,
    string ProcessName,
    string ExecutablePath,
    bool Responding);

public sealed record ServerSessionSnapshot(
    long SessionId,
    int RootProcessId,
    DateTime CapturedAt,
    IReadOnlyList<ServerSessionProcessInfo> Processes,
    IReadOnlyList<string> LoadedModules,
    IReadOnlyList<int> GuardedListeningPorts);
