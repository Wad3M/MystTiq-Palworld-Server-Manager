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
}

public sealed class ServerProcessDto
{
    [JsonPropertyName("processId")] public int ProcessId { get; init; }
    [JsonPropertyName("parentProcessId")] public int ParentProcessId { get; init; }
    [JsonPropertyName("processName")] public string? ProcessName { get; init; }
    [JsonPropertyName("executablePath")] public string? ExecutablePath { get; init; }
    [JsonPropertyName("responding")] public bool Responding { get; init; }
}
