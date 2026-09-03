using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class ServerDistributionStatusDto
{
    [JsonPropertyName("platform")] public string Platform { get; init; } = string.Empty;
    [JsonPropertyName("steamCmdPath")] public string SteamCmdPath { get; init; } = string.Empty;
    [JsonPropertyName("steamCmdExists")] public bool SteamCmdExists { get; init; }
    [JsonPropertyName("serverRoot")] public string ServerRoot { get; init; } = string.Empty;
    [JsonPropertyName("serverRootExists")] public bool ServerRootExists { get; init; }
    [JsonPropertyName("serverExecutable")] public string ServerExecutable { get; init; } = string.Empty;
    [JsonPropertyName("serverExecutableExists")] public bool ServerExecutableExists { get; init; }
    [JsonPropertyName("steamCmdPackageUri")] public string SteamCmdPackageUri { get; init; } = string.Empty;
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}

public sealed class ServerDistributionPlanDto
{
    [JsonPropertyName("platform")] public string Platform { get; init; } = string.Empty;
    [JsonPropertyName("steamCmdPath")] public string SteamCmdPath { get; init; } = string.Empty;
    [JsonPropertyName("serverRoot")] public string ServerRoot { get; init; } = string.Empty;
    [JsonPropertyName("validate")] public bool Validate { get; init; }
    [JsonPropertyName("arguments")] public IReadOnlyList<string> Arguments { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
}

public sealed class ServerDistributionOperationResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("exitCode")] public int ExitCode { get; init; }
    [JsonPropertyName("validate")] public bool Validate { get; init; }
    [JsonPropertyName("serverExecutableExists")] public bool ServerExecutableExists { get; init; }
    [JsonPropertyName("outputTail")] public IReadOnlyList<string> OutputTail { get; init; } = [];
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}
