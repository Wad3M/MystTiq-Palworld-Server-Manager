using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

// v0.7.48.0: UE4SS Release Catalog -- mirrors MystTiq.HeadlessHost's Ue4ssReleaseInfo.
public sealed class Ue4ssReleaseDto
{
    [JsonPropertyName("source")] public string Source { get; init; } = string.Empty;
    [JsonPropertyName("tagName")] public string TagName { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("publishedAt")] public DateTimeOffset PublishedAt { get; init; }
    [JsonPropertyName("prerelease")] public bool Prerelease { get; init; }
    [JsonPropertyName("htmlUrl")] public string HtmlUrl { get; init; } = string.Empty;
    [JsonPropertyName("recommendedAssetName")] public string? RecommendedAssetName { get; init; }
    [JsonPropertyName("recommendedAssetSizeBytes")] public long? RecommendedAssetSizeBytes { get; init; }
    [JsonPropertyName("recommendedAssetDownloadUrl")] public string? RecommendedAssetDownloadUrl { get; init; }

    public string PublishedAtText => PublishedAt == default ? "Unknown date" : PublishedAt.ToLocalTime().ToString("yyyy-MM-dd");
    public string ChannelText => Prerelease ? "Prerelease" : "Stable";
    public string AssetText => RecommendedAssetName is { Length: > 0 }
        ? $"{RecommendedAssetName}{(RecommendedAssetSizeBytes is { } bytes ? $" ({bytes / 1024.0 / 1024.0:F1} MB)" : string.Empty)}"
        : "No clear primary asset -- open the release page to choose one manually.";
}

public sealed class Ue4ssReleaseCatalogDto
{
    [JsonPropertyName("palworldForkReleases")] public IReadOnlyList<Ue4ssReleaseDto> PalworldForkReleases { get; init; } = [];
    [JsonPropertyName("officialUpstreamReleases")] public IReadOnlyList<Ue4ssReleaseDto> OfficialUpstreamReleases { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
}

// v0.7.49.0: UE4SS Install/Rollback -- mirrors MystTiq.HeadlessHost's Ue4ssInstallPreview/Result.
public sealed class Ue4ssInstallPreviewDto
{
    [JsonPropertyName("token")] public string Token { get; init; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; init; } = string.Empty;
    [JsonPropertyName("tagName")] public string TagName { get; init; } = string.Empty;
    [JsonPropertyName("assetName")] public string? AssetName { get; init; }
    [JsonPropertyName("assetSizeBytes")] public long? AssetSizeBytes { get; init; }
    [JsonPropertyName("assetDownloadUrl")] public string AssetDownloadUrl { get; init; } = string.Empty;
    [JsonPropertyName("currentLayout")] public string CurrentLayout { get; init; } = string.Empty;
    [JsonPropertyName("currentVersion")] public string CurrentVersion { get; init; } = string.Empty;
    [JsonPropertyName("previewedAt")] public DateTimeOffset PreviewedAt { get; init; }
    [JsonPropertyName("expiresAt")] public DateTimeOffset ExpiresAt { get; init; }
    [JsonPropertyName("summary")] public string Summary { get; init; } = string.Empty;
}

public sealed class Ue4ssInstallResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("installedVersion")] public string? InstalledVersion { get; init; }
    [JsonPropertyName("targetRoot")] public string? TargetRoot { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

public sealed class Ue4ssInstallStatusDto
{
    [JsonPropertyName("rollbackAvailable")] public bool RollbackAvailable { get; init; }
}
