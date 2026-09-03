namespace MystTiq.Desktop.Models;

public sealed class WorldValidationFindingDto
{
    public string Severity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Check { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool RepairAvailable { get; set; }
}

public sealed class WorldValidationReportDto
{
    public bool Healthy { get; set; }
    public string? WorldId { get; set; }
    public List<WorldValidationFindingDto> Findings { get; set; } = [];
    public DateTimeOffset CheckedAt { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public sealed class WorldImportPreviewDto
{
    public bool Accepted { get; set; }
    public string PreviewToken { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public int EntryCount { get; set; }
    public long ArchiveBytes { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public List<string> Steps { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
}

public sealed record WorldTransactionApplyRequestDto(string PreviewToken, bool Confirmed);

public sealed class WorldTransactionResultDto
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string State { get; set; } = string.Empty;
    public string? SafetyBackup { get; set; }
    public bool RolledBack { get; set; }
    public string? JournalPath { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class WorldTransactionStageDto
{
    public string State { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
}

public sealed class WorldTransactionJournalDto
{
    public string TransactionId { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string WorldId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? SafetyBackup { get; set; }
    public bool RolledBack { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public string JournalPath { get; set; } = string.Empty;
    public List<WorldTransactionStageDto> Stages { get; set; } = [];
    public string Summary => $"{Mode} · {State} · {UpdatedUtc.ToLocalTime():g}";
}
