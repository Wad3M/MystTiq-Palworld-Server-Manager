using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

// v0.7.93.0: Nexus Mods public API v1 shapes (https://api.nexusmods.com/v1). Only the fields the
// catalog browser actually shows or needs are mapped.
public sealed class NexusUserDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("is_premium")] public bool IsPremium { get; init; }
    [JsonPropertyName("is_supporter")] public bool IsSupporter { get; init; }
}

public sealed class NexusModDto
{
    [JsonPropertyName("mod_id")] public int ModId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("summary")] public string? Summary { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("author")] public string? Author { get; init; }
    [JsonPropertyName("endorsement_count")] public int EndorsementCount { get; init; }
    [JsonPropertyName("updated_timestamp")] public long UpdatedTimestamp { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Mod {ModId}" : Name!;
    public string ByLine => $"{(string.IsNullOrWhiteSpace(Author) ? "unknown author" : Author)} · v{(string.IsNullOrWhiteSpace(Version) ? "?" : Version)} · {EndorsementCount:N0} endorsements";
}

public sealed class NexusFilesResponseDto
{
    [JsonPropertyName("files")] public IReadOnlyList<NexusFileDto> Files { get; init; } = [];
}

public sealed class NexusFileDto
{
    [JsonPropertyName("file_id")] public int FileId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("category_name")] public string? CategoryName { get; init; }
    [JsonPropertyName("is_primary")] public bool IsPrimary { get; init; }
    [JsonPropertyName("size_kb")] public long SizeKb { get; init; }
    [JsonPropertyName("file_name")] public string? FileName { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? (FileName ?? $"File {FileId}") : Name!;
    public string Detail => $"{(string.IsNullOrWhiteSpace(CategoryName) ? "FILE" : CategoryName!.ToUpperInvariant())} · {FileName} · {SizeKb / 1024d:F1} MB{(IsPrimary ? " · primary" : string.Empty)}";
}

public sealed class NexusDownloadLinkDto
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("short_name")] public string? ShortName { get; init; }
    [JsonPropertyName("URI")] public string? Uri { get; init; }
}

// Result wrapper so the UI can show rate-limit headroom and a plain-language reason for a failure
// instead of an HTTP status code.
public sealed record NexusCallResult<T>(T? Value, string? Error, int? HourlyRemaining, int? DailyRemaining)
{
    public bool Ok => Error is null;
}
