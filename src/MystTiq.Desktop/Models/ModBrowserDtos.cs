// MystTiq v1.0.5.0: file reviewed for this release (2026-10-06).
namespace MystTiq.Desktop.Models;

// v1.0.5.0 (roadmap M-1): one MOD as a repository (or a folder on this PC) lists it, and one of its files. Names,
// summaries and versions are the repository's own data and are shown as they come.
public sealed class ModListing
{
    public string Source { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Author { get; init; }
    public string? Summary { get; init; }
    public string? Version { get; init; }
    public DateTimeOffset? Updated { get; init; }
    public long? Downloads { get; init; }
    public string? PageUrl { get; init; }
    // MystTiq's own remark (translated where shown), e.g. that a package needs a loader MystTiq does not manage.
    public string? Note { get; init; }
    // Data: who made it, the version, when it changed and how often it was downloaded.
    public string ByLineVerbatim => string.Join(" · ", new[]
    {
        string.IsNullOrWhiteSpace(Author) ? null : Author,
        string.IsNullOrWhiteSpace(Version) ? null : "v" + Version,
        Updated?.ToLocalTime().ToString("yyyy-MM-dd"),
        Downloads is { } d ? $"{d:N0} ↓" : null,
    }.Where(s => s is not null));
    public string SummaryVerbatim => Summary ?? string.Empty;
    public string NoteText => Note ?? string.Empty;
    public bool HasNote => !string.IsNullOrEmpty(Note);
}

public sealed class ModListingFile
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Version { get; init; }
    public long? SizeBytes { get; init; }
    public DateTimeOffset? Updated { get; init; }
    public string? DownloadUrl { get; init; }
    // A file already on this PC (the Downloads and folders source).
    public string? LocalPath { get; init; }
    public string? Note { get; init; }
    // Data: the file's name, version, size and date.
    public string RowVerbatim => string.Join(" · ", new[]
    {
        Name,
        string.IsNullOrWhiteSpace(Version) ? null : "v" + Version,
        SizeBytes is { } s ? $"{s / 1048576d:F1} MB" : null,
        Updated?.ToLocalTime().ToString("yyyy-MM-dd"),
    }.Where(x => x is not null));
    public string NoteText => Note ?? string.Empty;
}

public sealed record ModSourceResult<T>(T? Value, string? Error)
{
    public bool Ok => Error is null;
    public static ModSourceResult<T> Fail(string error) => new(default, error);
}

public sealed class ModSourceOption
{
    public string Id { get; init; } = string.Empty;
    // English source name; shown through the translator.
    public string Name { get; init; } = string.Empty;
}
