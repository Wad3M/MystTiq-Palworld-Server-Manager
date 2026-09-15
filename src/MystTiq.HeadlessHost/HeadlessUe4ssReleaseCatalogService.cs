using System.Text.Json;
using System.Text.Json.Serialization;

namespace MystTiq.HeadlessHost;

// v0.7.48.0: UE4SS Release Catalog (item 45) -- the "Release source" dropdown on the UE4SS page
// has always been a client-side-only stub ("Palworld Fork"/"Official Upstream", no backend). This
// is the real headless release-catalog service the UI already flagged as missing. Deliberately
// listing-only: install/rollback is real, separate, higher-risk work (writing into the server's
// UE4SS binaries) tracked as its own future version -- verified live against real GitHub release
// data for both repos before writing this, including the exact asset-naming convention used to
// pick the correct non-dev download out of each release's multiple attached files.
public sealed class HeadlessUe4ssReleaseCatalogService
{
    private const string PalworldForkRepo = "Okaetsu/RE-UE4SS";
    private const string OfficialUpstreamRepo = "UE4SS-RE/RE-UE4SS";

    private static readonly HttpClient Http = BuildHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Ue4ssReleaseCatalog> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var palworldFork = await FetchReleasesAsync("PalworldFork", PalworldForkRepo, cancellationToken);
        var officialUpstream = await FetchReleasesAsync("OfficialUpstream", OfficialUpstreamRepo, cancellationToken);
        return new Ue4ssReleaseCatalog(palworldFork, officialUpstream, now);
    }

    private static async Task<IReadOnlyList<Ue4ssReleaseInfo>> FetchReleasesAsync(string source, string repo, CancellationToken cancellationToken)
    {
        var releases = await TryGetJsonAsync<List<GitHubReleaseDto>>($"https://api.github.com/repos/{repo}/releases", cancellationToken);
        if (releases is null) return [];

        return releases
            .Where(r => r.TagName is { Length: > 0 })
            .Select(r =>
            {
                var recommended = ChooseRecommendedAsset(r.Assets);
                return new Ue4ssReleaseInfo(
                    source,
                    r.TagName!,
                    string.IsNullOrWhiteSpace(r.Name) ? r.TagName! : r.Name!,
                    r.PublishedAt ?? DateTimeOffset.MinValue,
                    r.Prerelease,
                    r.HtmlUrl ?? $"https://github.com/{repo}/releases/tag/{r.TagName}",
                    recommended?.Name,
                    recommended?.Size,
                    recommended?.BrowserDownloadUrl);
            })
            .OrderByDescending(r => r.PublishedAt)
            .ToArray();
    }

    // Verified live against real release data for both repos before writing this: the correct
    // "install this one" asset is whichever attached file has "UE4SS" in its name and does NOT
    // have "dev" in its name (case-insensitive) -- confirmed against Okaetsu/RE-UE4SS's 2281fa31
    // release (UE4SS-Palworld-g2281fa31.zip vs the -zDev.zip developer build) and UE4SS-RE/RE-UE4SS's
    // v3.0.1 release (UE4SS_v3.0.1.zip vs zDEV-UE4SS_v3.0.1.zip, zCustomGameConfigs.zip,
    // zMapGenBP.zip -- the latter two correctly excluded for lacking "UE4SS" in their name). When no
    // asset matches this rule, the recommended asset is left null rather than guessing -- the UI
    // must disclose "no clear primary asset" rather than pick one at random.
    private static GitHubReleaseAssetDto? ChooseRecommendedAsset(List<GitHubReleaseAssetDto>? assets)
    {
        if (assets is null || assets.Count == 0) return null;
        var candidates = assets
            .Where(a => a.Name is { Length: > 0 } &&
                        a.Name.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) &&
                        !a.Name.Contains("dev", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.Size)
            .ToList();
        return candidates.FirstOrDefault();
    }

    private static async Task<T?> TryGetJsonAsync<T>(string url, CancellationToken cancellationToken) where T : class
    {
        try
        {
            using var response = await Http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        }
        catch { return null; }
    }

    private static HttpClient BuildHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MystTiq-Palworld-Server-Manager");
        return client;
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; init; }
        [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; init; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; init; }
        [JsonPropertyName("assets")] public List<GitHubReleaseAssetDto>? Assets { get; init; }
    }

    private sealed class GitHubReleaseAssetDto
    {
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("size")] public long Size { get; init; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; init; }
    }
}

public sealed record Ue4ssReleaseInfo(
    string Source,
    string TagName,
    string Name,
    DateTimeOffset PublishedAt,
    bool Prerelease,
    string HtmlUrl,
    string? RecommendedAssetName,
    long? RecommendedAssetSizeBytes,
    string? RecommendedAssetDownloadUrl);

public sealed record Ue4ssReleaseCatalog(
    IReadOnlyList<Ue4ssReleaseInfo> PalworldForkReleases,
    IReadOnlyList<Ue4ssReleaseInfo> OfficialUpstreamReleases,
    DateTimeOffset ObservedAt);
