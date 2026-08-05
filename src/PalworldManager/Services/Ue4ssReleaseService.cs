using PalworldManager.Models;
using PalworldManager.Services.Infrastructure;

namespace PalworldManager.Services;

public sealed class Ue4ssReleaseService : IDisposable
{
    public const string PalworldReleasesPage = "https://github.com/Okaetsu/RE-UE4SS/releases";
    public const string UpstreamReleasesPage = "https://github.com/UE4SS-RE/RE-UE4SS/releases";
    public const string ReleasesPage = PalworldReleasesPage;
    private readonly HttpClient client;
    private readonly string cacheRoot;
    private static readonly JsonSerializerOptions CacheJsonOptions = new() { WriteIndented = true };

    public Ue4ssReleaseService()
    {
        client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(ApplicationVersion.UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        cacheRoot = Path.Combine(ApplicationPathService.Current.CacheRoot, "UE4SS");
    }

    /// <summary>
    /// Returns the persistent catalog immediately when one exists. The first use of a
    /// source seeds the cache from GitHub. Use RefreshReleasesAsync for an explicit
    /// online refresh. Cached entries are intentionally retained so a previously known
    /// rollback build does not disappear just because GitHub later stops returning it.
    /// </summary>
    public async Task<IReadOnlyList<Ue4ssReleaseInfo>> GetReleasesAsync(string source = "Palworld Fork", CancellationToken token = default)
    {
        var cached = GetCachedReleases(source);
        if (cached.Count > 0) return cached;
        return await RefreshReleasesAsync(source, token);
    }

    public IReadOnlyList<Ue4ssReleaseInfo> GetCachedReleases(string source = "Palworld Fork")
    {
        var path = GetCachePath(source);
        if (!File.Exists(path)) return Array.Empty<Ue4ssReleaseInfo>();
        try
        {
            var payload = JsonSerializer.Deserialize<Ue4ssReleaseCache>(File.ReadAllText(path), CacheJsonOptions);
            if (payload?.Releases is null) return Array.Empty<Ue4ssReleaseInfo>();
            return Sort(payload.Releases);
        }
        catch
        {
            // A corrupt cache must never prevent runtime management. Refresh will
            // simply recreate it on demand.
            return Array.Empty<Ue4ssReleaseInfo>();
        }
    }

    public DateTime? GetCacheUpdatedAt(string source = "Palworld Fork")
    {
        var path = GetCachePath(source);
        if (!File.Exists(path)) return null;
        try
        {
            var payload = JsonSerializer.Deserialize<Ue4ssReleaseCache>(File.ReadAllText(path), CacheJsonOptions);
            return payload?.UpdatedAt;
        }
        catch { return null; }
    }

    /// <summary>
    /// Explicit GitHub refresh. Online results are merged into the persistent catalog;
    /// older cached releases are retained for version comparison and rollback history.
    /// </summary>
    public async Task<IReadOnlyList<Ue4ssReleaseInfo>> RefreshReleasesAsync(string source = "Palworld Fork", CancellationToken token = default)
    {
        var online = await FetchOnlineReleasesAsync(source, token);
        var cached = GetCachedReleases(source);

        var merged = cached
            .Concat(online)
            .GroupBy(r => r.ReleaseKey, StringComparer.OrdinalIgnoreCase)
            // Prefer the freshly fetched entry when GitHub returned the same release.
            .Select(g => online.FirstOrDefault(o => string.Equals(o.ReleaseKey, g.Key, StringComparison.OrdinalIgnoreCase)) ?? g.First())
            .ToList();

        var sorted = Sort(merged);
        Directory.CreateDirectory(cacheRoot);
        var payload = new Ue4ssReleaseCache
        {
            Source = NormalizeSource(source),
            UpdatedAt = DateTime.Now,
            Releases = sorted.ToList()
        };
        AtomicFile.Write(GetCachePath(source), JsonSerializer.Serialize(payload, CacheJsonOptions));
        return sorted;
    }

    private async Task<IReadOnlyList<Ue4ssReleaseInfo>> FetchOnlineReleasesAsync(string source, CancellationToken token)
    {
        var upstream = IsUpstream(source);
        var repo = upstream ? "UE4SS-RE/RE-UE4SS" : "Okaetsu/RE-UE4SS";
        var page = upstream ? UpstreamReleasesPage : PalworldReleasesPage;
        var sourceLabel = upstream ? "Official Upstream" : "Palworld Fork";
        var result = new List<Ue4ssReleaseInfo>();

        for (var pageNumber = 1; pageNumber <= 10; pageNumber++)
        {
            var api = $"https://api.github.com/repos/{repo}/releases?per_page=100&page={pageNumber}";
            using var response = await client.GetAsync(api, token);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            var releaseNodes = document.RootElement.EnumerateArray().ToList();
            if (releaseNodes.Count == 0) break;

            foreach (var release in releaseNodes)
            {
                var tag = release.TryGetProperty("tag_name", out var tagNode) ? tagNode.GetString() ?? "" : "";
                var name = release.TryGetProperty("name", out var nameNode) ? nameNode.GetString() ?? tag : tag;
                var html = release.TryGetProperty("html_url", out var htmlNode) ? htmlNode.GetString() ?? page : page;
                var pre = release.TryGetProperty("prerelease", out var preNode) && preNode.GetBoolean();
                var published = DateTime.MinValue;
                if (release.TryGetProperty("published_at", out var publishedNode)) DateTime.TryParse(publishedNode.GetString(), out published);

                var zipAssets = new List<(string Name, string Url)>();
                if (release.TryGetProperty("assets", out var assetsNode))
                {
                    foreach (var asset in assetsNode.EnumerateArray())
                    {
                        var assetName = asset.TryGetProperty("name", out var nNode) ? nNode.GetString() ?? "" : "";
                        var assetUrl = asset.TryGetProperty("browser_download_url", out var uNode) ? uNode.GetString() ?? "" : "";
                        if (!assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                        zipAssets.Add((assetName, assetUrl));
                    }
                }

                if (zipAssets.Count > 0)
                {
                    foreach (var asset in zipAssets.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Add(new Ue4ssReleaseInfo
                        {
                            Source = sourceLabel,
                            Tag = tag,
                            Name = name,
                            PublishedAt = published,
                            Prerelease = pre,
                            HtmlUrl = html,
                            AssetName = asset.Name,
                            AssetUrl = asset.Url
                        });
                    }
                }
                else
                {
                    result.Add(new Ue4ssReleaseInfo
                    {
                        Source = sourceLabel,
                        Tag = tag,
                        Name = name,
                        PublishedAt = published,
                        Prerelease = pre,
                        HtmlUrl = html
                    });
                }
            }

            if (releaseNodes.Count < 100) break;
        }

        // The Palworld fork has historically used rolling/pre-release assets. GitHub's
        // releases endpoint may expose only the current assets, so also retain repository
        // tags as historical catalog entries. Tags without a downloadable release remain
        // visible for diagnostics and rollback history instead of disappearing.
        for (var tagPage = 1; tagPage <= 10; tagPage++)
        {
            var tagsApi = $"https://api.github.com/repos/{repo}/tags?per_page=100&page={tagPage}";
            using var tagResponse = await client.GetAsync(tagsApi, token);
            tagResponse.EnsureSuccessStatusCode();
            await using var tagStream = await tagResponse.Content.ReadAsStreamAsync(token);
            using var tagDocument = await JsonDocument.ParseAsync(tagStream, cancellationToken: token);
            var tagNodes = tagDocument.RootElement.EnumerateArray().ToList();
            if (tagNodes.Count == 0) break;
            foreach (var tagNode in tagNodes)
            {
                var tagName = tagNode.TryGetProperty("name", out var tagNameNode) ? tagNameNode.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(tagName) || result.Any(r => string.Equals(r.Tag, tagName, StringComparison.OrdinalIgnoreCase))) continue;
                var tagHtml = $"https://github.com/{repo}/tree/{Uri.EscapeDataString(tagName)}";
                result.Add(new Ue4ssReleaseInfo
                {
                    Source = sourceLabel,
                    Tag = tagName,
                    Name = "Historical tag",
                    PublishedAt = DateTime.MinValue,
                    Prerelease = true,
                    HtmlUrl = tagHtml
                });
            }
            if (tagNodes.Count < 100) break;
        }

        return Sort(result
            .GroupBy(r => r.ReleaseKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()));
    }

    private static IReadOnlyList<Ue4ssReleaseInfo> Sort(IEnumerable<Ue4ssReleaseInfo> releases)
        => releases
            .OrderByDescending(r => r.PublishedAt)
            .ThenBy(r => r.Tag, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.AssetName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private string GetCachePath(string source)
        => Path.Combine(cacheRoot, IsUpstream(source) ? "OfficialUpstream.releases.json" : "PalworldFork.releases.json");

    private static bool IsUpstream(string source)
        => source.Contains("Upstream", StringComparison.OrdinalIgnoreCase) ||
           source.Contains("Official", StringComparison.OrdinalIgnoreCase) ||
           source.Contains("Standard", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSource(string source) => IsUpstream(source) ? "Official Upstream" : "Palworld Fork";

    public async Task<string> DownloadAsync(Ue4ssReleaseInfo release, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(release.AssetUrl)) throw new InvalidOperationException("The selected GitHub release does not expose a downloadable ZIP asset. Open GitHub Releases and use Import Runtime ZIP instead.");
        var root = Path.Combine(Path.GetTempPath(), "MystTiqPalworldServer", "UE4SS"); Directory.CreateDirectory(root);
        var fileName = string.IsNullOrWhiteSpace(release.AssetName) ? $"UE4SS_{release.Tag}.zip" : release.AssetName;
        var output = Path.Combine(root, fileName);
        using var response = await client.GetAsync(release.AssetUrl, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var file = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await input.CopyToAsync(file, token); return output;
    }

    public void Dispose() => client.Dispose();

    private sealed class Ue4ssReleaseCache
    {
        public string Source { get; set; } = "Palworld Fork";
        public DateTime UpdatedAt { get; set; }
        public List<Ue4ssReleaseInfo> Releases { get; set; } = new();
    }
}
