using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class BackupItemDto
{
    [JsonPropertyName("fileName")] public string FileName { get; init; } = string.Empty;
    [JsonPropertyName("sizeBytes")] public long SizeBytes { get; init; }
    [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; init; }
    [JsonPropertyName("verified")] public bool Verified { get; init; }

    public string SizeText => $"{SizeBytes / 1024d / 1024d:F2} MB";
    public string CreatedText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string VerificationText => Verified ? "Verified" : "Unreadable";
}

public sealed class BackupInventoryDto
{
    [JsonPropertyName("count")] public int Count { get; init; }
    [JsonPropertyName("totalSizeBytes")] public long TotalSizeBytes { get; init; }
    [JsonPropertyName("items")] public IReadOnlyList<BackupItemDto> Items { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("rootPath")] public string RootPath { get; init; } = string.Empty;
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}

public sealed class BackupOperationResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("fileName")] public string? FileName { get; init; }
    [JsonPropertyName("safetyBackupFileName")] public string? SafetyBackupFileName { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

public sealed class BackupVerificationResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("fileName")] public string FileName { get; init; } = string.Empty;
    [JsonPropertyName("sha256")] public string? Sha256 { get; init; }
    [JsonPropertyName("entryCount")] public int EntryCount { get; init; }
    [JsonPropertyName("sizeBytes")] public long SizeBytes { get; init; }
    [JsonPropertyName("verifiedAt")] public DateTimeOffset VerifiedAt { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

public sealed class BackupVerificationBatchDto
{
    [JsonPropertyName("results")] public IReadOnlyList<BackupVerificationResultDto> Results { get; init; } = [];
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

public sealed class BackupRetentionItemDto
{
    [JsonPropertyName("fileName")] public string FileName { get; init; } = string.Empty;
    [JsonPropertyName("sizeBytes")] public long SizeBytes { get; init; }
    [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; init; }
    public string DisplayText => $"{FileName} · {SizeBytes / 1024d / 1024d:F2} MB";
}

public sealed class BackupRetentionPreviewDto
{
    [JsonPropertyName("token")] public string Token { get; init; } = string.Empty;
    [JsonPropertyName("keepLatest")] public int KeepLatest { get; init; }
    [JsonPropertyName("maxAgeDays")] public int MaxAgeDays { get; init; }
    [JsonPropertyName("items")] public IReadOnlyList<BackupRetentionItemDto> Items { get; init; } = [];
    [JsonPropertyName("reclaimBytes")] public long ReclaimBytes { get; init; }
    [JsonPropertyName("expiresAt")] public DateTimeOffset ExpiresAt { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

public sealed class BackupRetentionApplyResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("deletedCount")] public int DeletedCount { get; init; }
    [JsonPropertyName("reclaimedBytes")] public long ReclaimedBytes { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

public sealed class EditableApiConfigurationDto
{
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("bindAddress")] public string BindAddress { get; init; } = string.Empty;
    [JsonPropertyName("port")] public int Port { get; init; }
}

public sealed class EditableLifecycleConfigurationDto
{
    [JsonPropertyName("startupTimeoutSeconds")] public int StartupTimeoutSeconds { get; init; }
    [JsonPropertyName("stopTimeoutSeconds")] public int StopTimeoutSeconds { get; init; }
    [JsonPropertyName("servicePollSeconds")] public int ServicePollSeconds { get; init; }
    [JsonPropertyName("recoveryBackoffSeconds")] public int RecoveryBackoffSeconds { get; init; }
    [JsonPropertyName("maximumRecoveryAttempts")] public int MaximumRecoveryAttempts { get; init; }
    [JsonPropertyName("recoveryWindowSeconds")] public int RecoveryWindowSeconds { get; init; }
}

public sealed class EditableServerConfigurationDto
{
    [JsonPropertyName("serverRoot")] public string ServerRoot { get; init; } = string.Empty;
    [JsonPropertyName("steamCmdPath")] public string SteamCmdPath { get; init; } = string.Empty;
    [JsonPropertyName("backupRoot")] public string BackupRoot { get; init; } = string.Empty;
    [JsonPropertyName("runtimeRoot")] public string RuntimeRoot { get; init; } = string.Empty;
    [JsonPropertyName("launchArguments")] public IReadOnlyList<string> LaunchArguments { get; init; } = [];
}

public sealed class EditableConfigurationDto
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; }
    [JsonPropertyName("configurationPath")] public string ConfigurationPath { get; init; } = string.Empty;
    [JsonPropertyName("api")] public EditableApiConfigurationDto Api { get; init; } = new();
    [JsonPropertyName("lifecycle")] public EditableLifecycleConfigurationDto Lifecycle { get; init; } = new();
    [JsonPropertyName("server")] public EditableServerConfigurationDto Server { get; init; } = new();
    [JsonPropertyName("authenticationEnabled")] public bool AuthenticationEnabled { get; init; }
    [JsonPropertyName("tlsEnabled")] public bool TlsEnabled { get; init; }
    [JsonPropertyName("restartRequired")] public bool RestartRequired { get; init; }
    [JsonPropertyName("validationErrors")] public IReadOnlyList<string> ValidationErrors { get; init; } = [];
}

public sealed class ConfigurationSaveResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("restartRequired")] public bool RestartRequired { get; init; }
    [JsonPropertyName("rollbackPath")] public string? RollbackPath { get; init; }
    [JsonPropertyName("validationErrors")] public IReadOnlyList<string> ValidationErrors { get; init; } = [];
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}
