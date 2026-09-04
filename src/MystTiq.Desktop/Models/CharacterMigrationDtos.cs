namespace MystTiq.Desktop.Models;

// v0.6.3.0 character/account migration.
public sealed class CharacterMigrationPreviewDto
{
    public bool CanApply { get; set; }
    public string PreviewToken { get; set; } = string.Empty;
    public string SourcePlayerId { get; set; } = string.Empty;
    public string SourcePlayerName { get; set; } = string.Empty;
    public string DestinationPlayerId { get; set; } = string.Empty;
    public string DestinationPlayerName { get; set; } = string.Empty;
    public string? MatchMethod { get; set; }
    public double MatchConfidence { get; set; }
    public List<string> Findings { get; set; } = [];
    public DateTimeOffset ExpiresUtc { get; set; }
}

public sealed record CharacterMigrationPreviewRequestDto(string SourcePlayerId, string DestinationPlayerId);
public sealed record CharacterMigrationApplyRequestDto(string PreviewToken, bool Confirmed);
public sealed record CharacterDispositionRequestDto(string Disposition);

public sealed class CharacterMigrationResultDto
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string State { get; set; } = string.Empty;
    public string? SafetyBackup { get; set; }
    public bool RolledBack { get; set; }
    public string? JournalPath { get; set; }
    public string? SourcePlayerId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class CharacterDispositionResultDto
{
    public bool Success { get; set; }
    public string State { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
