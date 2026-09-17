using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class ServerStatusDto
{
    [JsonPropertyName("phase")] public int Phase { get; init; }
    [JsonPropertyName("ready")] public bool Ready { get; init; }
    [JsonPropertyName("nativeProcessId")] public int? NativeProcessId { get; init; }
    [JsonPropertyName("processes")] public IReadOnlyList<ServerProcessDto> Processes { get; init; } = [];
    [JsonPropertyName("guardedListeningPorts")] public IReadOnlyList<int> GuardedListeningPorts { get; init; } = [];
    [JsonPropertyName("detail")] public string? Detail { get; init; }
    [JsonPropertyName("crashDetected")] public bool CrashDetected { get; init; }
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("lastTransitionAt")] public DateTimeOffset? LastTransitionAt { get; init; }

    // v0.7.88.0 bug fix: NativeProcessId alone is not "a process is alive right now" -- both
    // WindowsServerLifecycleService and LinuxServerLifecycleService (MystTiq.Core) deliberately keep
    // returning the last-known PID even once Phase has genuinely gone to Stopped/Crashed, purely for
    // reference display. Confirmed live: a stopped server kept reporting its previous run's PID with
    // Phase==1 (Stopped) and Detail=="PalServer is not running.", permanently stuck showing
    // "Starting / Not Ready" with Start greyed out. Phase 3 mirrors
    // MystTiq.Core.Models.ServerLifecyclePhase.Running -- the only phase where a native process is
    // actually confirmed alive right now (including the genuine mid-launch window: the OS process
    // exists and is found immediately once Start spawns it, well before Ready flips true).
    [JsonIgnore] public bool IsProcessLive => Phase == 3;
}

public sealed class ServerProcessDto
{
    [JsonPropertyName("processId")] public int ProcessId { get; init; }
    [JsonPropertyName("parentProcessId")] public int ParentProcessId { get; init; }
    [JsonPropertyName("processName")] public string? ProcessName { get; init; }
    [JsonPropertyName("executablePath")] public string? ExecutablePath { get; init; }
    [JsonPropertyName("responding")] public bool Responding { get; init; }

    // v0.7.28.0: client-side display only, not part of the wire contract -- the Server Doctor
    // page's process list binds directly to this instead of formatting per-row in XAML.
    public string DisplayText =>
        $"{ProcessName ?? "(unknown)"} (PID {ProcessId}){(Responding ? "" : " — not responding")}";
}
