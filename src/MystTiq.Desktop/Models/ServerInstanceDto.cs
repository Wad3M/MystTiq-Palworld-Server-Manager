using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

// v0.7.44.0: mirrors MystTiq.Core.Models.ServerInstanceInfo. Distinct from ServerProcessDto
// (which only ever lists this profile's own managed processes) -- this can include Palworld
// processes belonging to a different install/profile entirely.
public sealed class ServerInstanceDto
{
    [JsonPropertyName("processId")] public int ProcessId { get; init; }
    [JsonPropertyName("parentProcessId")] public int? ParentProcessId { get; init; }
    [JsonPropertyName("processName")] public string? ProcessName { get; init; }
    [JsonPropertyName("executablePath")] public string? ExecutablePath { get; init; }
    [JsonPropertyName("responding")] public bool Responding { get; init; }
    [JsonPropertyName("managedByThisProfile")] public bool ManagedByThisProfile { get; init; }

    public string DisplayText =>
        $"{ProcessName ?? "(unknown)"} (PID {ProcessId}){(Responding ? "" : " — not responding")}";

    public string ManagedText => ManagedByThisProfile ? "Managed by this tab" : "Not managed by this tab";

    public string PathText => string.IsNullOrWhiteSpace(ExecutablePath) ? "(path unknown)" : ExecutablePath;
}

public sealed class InstanceTerminationResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("processId")] public int ProcessId { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
}
