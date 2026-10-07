// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using MystTiq.Core.Services;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

// v1.0.5.0 (roadmap M-1; owner decision 2026-10-06): the MOD browser's sources, all called from this PC, never through
// the MystTiq server, as Nexus's API policy already requires for its key: the owner's Downloads folder and other folders,
// Thunderstore, CurseForge (with the owner's own API key), GitHub releases of repositories the owner adds (an open search
// there is full of cheat and malware lures), and Nexus Mods (the existing client and key). Every file is downloaded only
// from its source's own hosts, then checked by ModArchivePlanner, confirmed, and installed through the service's validated
// ZIP install, which applies the same rules again.
public interface IModSource
{
    string Id { get; }
    string Name { get; }
    Task<ModSourceResult<IReadOnlyList<ModListing>>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<ModSourceResult<IReadOnlyList<ModListingFile>>> GetFilesAsync(ModListing listing, CancellationToken cancellationToken = default);
    // Where to fetch a file from (a repository's link, checked against its hosts), or null with the reason.
    Task<ModSourceResult<Uri>> ResolveDownloadAsync(ModListing listing, ModListingFile file, CancellationToken cancellationToken = default);
    bool IsAllowedDownloadHost(Uri uri);
}

public static class ModSourceHttp
{
    public const long MaxDownloadBytes = 512L * 1024 * 1024;

    public static HttpClient Create(HttpMessageHandler? handler = null)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = TimeSpan.FromMinutes(10);
        var version = typeof(ModSourceHttp).Assembly.GetName().Version?.ToString(3) ?? "0";
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(NexusModsClient.ApplicationName, version));
        return client;
    }

    public static bool HostIn(Uri uri, params string[] domains) =>
        uri.Scheme == Uri.UriSchemeHttps && domains.Any(d => uri.Host.Equals(d, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));

    public static string Describe(HttpStatusCode status, string source) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"{source} refused the request (check the key, or wait if its limit was reached).",
        HttpStatusCode.NotFound => $"{source} has no such entry.",
        HttpStatusCode.TooManyRequests => $"{source} asks to slow down. Try again in a minute.",
        _ => $"{source} answered {(int)status}.",
    };

    // Downloads to a file, following redirects only within the source's hosts, up to the size limit.
    public static async Task<string?> DownloadAsync(HttpClient http, IModSource source, Uri uri, string destination, IProgress<string>? progress, CancellationToken cancellationToken = default)
    {
        if (!source.IsAllowedDownloadHost(uri)) return $"The download link points outside {source.Name} ({uri.Host}), so MystTiq will not fetch it.";
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var final = response.RequestMessage?.RequestUri ?? uri;
        if (!source.IsAllowedDownloadHost(final)) return $"The download was sent on to {final.Host}, outside {source.Name}, so MystTiq stopped.";
        if (!response.IsSuccessStatusCode) return Describe(response.StatusCode, source.Name);
        if (response.Content.Headers.ContentLength is > MaxDownloadBytes) return "The file is larger than 512 MB, the MOD install limit.";
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920]; long total = 0; int read; var lastReport = 0L;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxDownloadBytes) return "The file is larger than 512 MB, the MOD install limit.";
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            if (total - lastReport > 1048576) { lastReport = total; progress?.Report($"Downloading… {total / 1048576d:F1} MB"); }
        }
        return null;
    }

    // What a ZIP on disk holds, by the service's own rules; null when it is not a ZIP MystTiq can read.
    public static ModArchivePlan? PlanZip(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            if (zip.Entries.Count > 20000) return null;
            return ModArchivePlanner.Plan(zip.Entries.Select(e => e.FullName));
        }
        catch (InvalidDataException) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    internal static async Task<(JsonDocument? Json, string? Error)> GetJsonAsync(HttpClient http, string url, string source, Action<HttpRequestMessage>? prepare, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            prepare?.Invoke(request);
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return (null, Describe(response.StatusCode, source));
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return (await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken), null);
        }
        catch (HttpRequestException ex) { return (null, $"{source} could not be reached: {ex.Message}"); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return (null, $"{source} did not answer in time."); }
        catch (JsonException) { return (null, $"{source} answered with something other than JSON."); }
    }

    internal static string? Str(this JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    internal static long? Long(this JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : null;
    internal static bool Bool(this JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    internal static DateTimeOffset? Date(this JsonElement e, string name) => DateTimeOffset.TryParse(e.Str(name), out var d) ? d : null;
    internal static IEnumerable<JsonElement> Array(this JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : [];
    internal static bool Matches(string query, params string?[] fields) =>
        string.IsNullOrWhiteSpace(query) || query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => fields.Any(f => f?.Contains(word, StringComparison.OrdinalIgnoreCase) == true));
}

// The owner's Downloads folder and any folders they add: ZIPs that hold a MOD, newest first.
public sealed class FolderModSource(Func<IReadOnlyList<string>> folders) : IModSource
{
    public string Id => "folders";
    public string Name => "Downloads and folders";
    public int SkippedOtherArchives { get; private set; }

    public static string DefaultDownloadsFolder() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public Task<ModSourceResult<IReadOnlyList<ModListing>>> SearchAsync(string query, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var found = new List<ModListing>(); var skipped = 0;
        foreach (var folder in folders().Where(Directory.Exists))
        {
            IEnumerable<FileInfo> archives;
            try
            {
                archives = new DirectoryInfo(folder).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 1, IgnoreInaccessible = true })
                    .OrderByDescending(f => f.LastWriteTimeUtc).Take(400).ToList();
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            foreach (var file in archives)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ext = file.Extension.ToLowerInvariant();
                if (ext is ".7z" or ".rar") { skipped++; continue; }
                if (ext != ".zip" || file.Length > ModSourceHttp.MaxDownloadBytes || !ModSourceHttp.Matches(query, file.Name)) continue;
                var plan = ModSourceHttp.PlanZip(file.FullName);
                // Only archives that hold a MOD; programs, installers and unrelated ZIPs stay out of the list.
                if (plan is null || plan.Kind is ModArchiveKind.NoMod or ModArchiveKind.Executable or ModArchiveKind.UnsafePath) continue;
                found.Add(new ModListing
                {
                    Source = Id, Id = file.FullName, Name = Path.GetFileNameWithoutExtension(file.Name), Author = folder,
                    Summary = plan.Summary, Updated = file.LastWriteTimeUtc, Note = plan.Installable ? null : "MystTiq cannot install this one (see above).",
                });
            }
        }
        SkippedOtherArchives = skipped;
        return new ModSourceResult<IReadOnlyList<ModListing>>(found.OrderByDescending(l => l.Updated).ToList(), null);
    }, cancellationToken);

    public Task<ModSourceResult<IReadOnlyList<ModListingFile>>> GetFilesAsync(ModListing listing, CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(listing.Id);
        IReadOnlyList<ModListingFile> files = info.Exists
            ? [new ModListingFile { Id = info.FullName, Name = info.Name, SizeBytes = info.Length, Updated = info.LastWriteTimeUtc, LocalPath = info.FullName }]
            : [];
        return Task.FromResult(new ModSourceResult<IReadOnlyList<ModListingFile>>(files, info.Exists ? null : "The file is no longer there."));
    }

    public Task<ModSourceResult<Uri>> ResolveDownloadAsync(ModListing listing, ModListingFile file, CancellationToken cancellationToken = default) =>
        Task.FromResult(ModSourceResult<Uri>.Fail("The file is already on this PC."));
    public bool IsAllowedDownloadHost(Uri uri) => false;
}

// Thunderstore's Palworld community: its full package list (small) is read once every 10 minutes and searched here.
public sealed class ThunderstoreModSource(HttpClient http) : IModSource
{
    public const string ListUrl = "https://thunderstore.io/c/palworld/api/v1/package/";
    private JsonDocument? cache; private DateTimeOffset cachedAt;
    public string Id => "thunderstore";
    public string Name => "Thunderstore";
    public bool IsAllowedDownloadHost(Uri uri) => ModSourceHttp.HostIn(uri, "thunderstore.io");

    public async Task<ModSourceResult<IReadOnlyList<ModListing>>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (cache is null || DateTimeOffset.UtcNow - cachedAt > TimeSpan.FromMinutes(10))
        {
            var (json, error) = await ModSourceHttp.GetJsonAsync(http, ListUrl, Name, null, cancellationToken);
            if (json is null) return ModSourceResult<IReadOnlyList<ModListing>>.Fail(error!);
            cache?.Dispose(); cache = json; cachedAt = DateTimeOffset.UtcNow;
        }
        var list = new List<ModListing>();
        foreach (var p in cache.RootElement.EnumerateArray())
        {
            if (p.Bool("is_deprecated") || p.Bool("has_nsfw_content")) continue;
            var latest = p.Array("versions").FirstOrDefault();
            if (latest.ValueKind != JsonValueKind.Object) continue;
            var description = latest.Str("description");
            if (!ModSourceHttp.Matches(query, p.Str("name"), p.Str("owner"), description)) continue;
            var deps = latest.Array("dependencies").Select(d => d.GetString() ?? string.Empty).ToList();
            var categories = p.Array("categories").Select(c => c.GetString() ?? string.Empty).ToList();
            list.Add(new ModListing
            {
                Source = Id, Id = p.Str("full_name") ?? string.Empty, Name = (p.Str("name") ?? string.Empty).Replace('_', ' '), Author = p.Str("owner"),
                Summary = description, Version = latest.Str("version_number"), Updated = p.Date("date_updated"),
                Downloads = p.Array("versions").Sum(v => v.Long("downloads") ?? 0), PageUrl = p.Str("package_url"),
                Note = deps.Any(d => d.StartsWith("Thunderstore-unreal_shimloader", StringComparison.OrdinalIgnoreCase)) ? "Needs unreal_shimloader, which MystTiq does not manage."
                     : categories.Contains("Tools") && !categories.Contains("Mods") ? "A tool, not a MOD." : null,
            });
        }
        return new(list.OrderBy(l => l.Note is null ? 0 : 1).ThenByDescending(l => l.Updated).ToList(), null);
    }

    public Task<ModSourceResult<IReadOnlyList<ModListingFile>>> GetFilesAsync(ModListing listing, CancellationToken cancellationToken = default)
    {
        var package = cache?.RootElement.EnumerateArray().FirstOrDefault(p => p.Str("full_name") == listing.Id);
        if (package is not { ValueKind: JsonValueKind.Object } found) return Task.FromResult(ModSourceResult<IReadOnlyList<ModListingFile>>.Fail("Search again; the list was refreshed."));
        IReadOnlyList<ModListingFile> files = found.Array("versions").Take(15).Select(v => new ModListingFile
        {
            Id = v.Str("full_name") ?? string.Empty, Name = (v.Str("full_name") ?? listing.Name) + ".zip", Version = v.Str("version_number"),
            SizeBytes = v.Long("file_size"), Updated = v.Date("date_created"), DownloadUrl = v.Str("download_url"),
        }).ToList();
        return Task.FromResult(new ModSourceResult<IReadOnlyList<ModListingFile>>(files, null));
    }

    public Task<ModSourceResult<Uri>> ResolveDownloadAsync(ModListing listing, ModListingFile file, CancellationToken cancellationToken = default) =>
        Task.FromResult(Uri.TryCreate(file.DownloadUrl, UriKind.Absolute, out var uri) ? new ModSourceResult<Uri>(uri, null) : ModSourceResult<Uri>.Fail("Thunderstore gave no download link."));
}

// CurseForge, with the owner's own API key (from console.curseforge.com). Palworld's game id is looked up by its slug.
public sealed class CurseForgeModSource(HttpClient http, Func<string> key) : IModSource
{
    public const string ApiBase = "https://api.curseforge.com";
    private long? gameId;
    public string Id => "curseforge";
    public string Name => "CurseForge";
    public bool IsAllowedDownloadHost(Uri uri) => ModSourceHttp.HostIn(uri, "forgecdn.net", "curseforge.com");
    private void Key(HttpRequestMessage request) => request.Headers.TryAddWithoutValidation("x-api-key", key());

    private async Task<string?> EnsureGameAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key())) return "Enter your CurseForge API key first (console.curseforge.com > API keys).";
        if (gameId is not null) return null;
        for (var index = 0; index < 500; index += 50)
        {
            var (json, error) = await ModSourceHttp.GetJsonAsync(http, $"{ApiBase}/v1/games?index={index}&pageSize=50", Name, Key, cancellationToken);
            if (json is null) return error;
            using (json)
            {
                var games = json.RootElement.Array("data").ToList();
                var palworld = games.FirstOrDefault(g => string.Equals(g.Str("slug"), "palworld", StringComparison.OrdinalIgnoreCase));
                if (palworld.ValueKind == JsonValueKind.Object) { gameId = palworld.Long("id"); return null; }
                if (games.Count < 50) break;
            }
        }
        return "CurseForge does not list Palworld for this key.";
    }

    public async Task<ModSourceResult<IReadOnlyList<ModListing>>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (await EnsureGameAsync(cancellationToken) is { } problem) return ModSourceResult<IReadOnlyList<ModListing>>.Fail(problem);
        var url = $"{ApiBase}/v1/mods/search?gameId={gameId}&searchFilter={Uri.EscapeDataString(query.Trim())}&sortField=2&sortOrder=desc&pageSize=30";
        var (json, error) = await ModSourceHttp.GetJsonAsync(http, url, Name, Key, cancellationToken);
        if (json is null) return ModSourceResult<IReadOnlyList<ModListing>>.Fail(error!);
        using (json)
            return new(json.RootElement.Array("data").Select(m => new ModListing
            {
                Source = Id, Id = (m.Long("id") ?? 0).ToString(), Name = m.Str("name") ?? string.Empty,
                Author = string.Join(", ", m.Array("authors").Select(a => a.Str("name")).Where(n => n is not null)),
                Summary = m.Str("summary"), Updated = m.Date("dateModified"), Downloads = m.Long("downloadCount"),
                PageUrl = m.TryGetProperty("links", out var links) ? links.Str("websiteUrl") : null,
                Note = m.TryGetProperty("allowModDistribution", out var allow) && allow.ValueKind == JsonValueKind.False ? "The author allows downloads only on CurseForge's site." : null,
            }).ToList(), null);
    }

    public async Task<ModSourceResult<IReadOnlyList<ModListingFile>>> GetFilesAsync(ModListing listing, CancellationToken cancellationToken = default)
    {
        var (json, error) = await ModSourceHttp.GetJsonAsync(http, $"{ApiBase}/v1/mods/{Uri.EscapeDataString(listing.Id)}/files?pageSize=15", Name, Key, cancellationToken);
        if (json is null) return ModSourceResult<IReadOnlyList<ModListingFile>>.Fail(error!);
        using (json)
            return new(json.RootElement.Array("data").Select(f => new ModListingFile
            {
                Id = (f.Long("id") ?? 0).ToString(), Name = f.Str("fileName") ?? f.Str("displayName") ?? string.Empty, Version = f.Str("displayName"),
                SizeBytes = f.Long("fileLength"), Updated = f.Date("fileDate"), DownloadUrl = f.Str("downloadUrl"),
                Note = f.Str("downloadUrl") is null ? "The author allows downloads only on CurseForge's site." : null,
            }).ToList(), null);
    }

    public async Task<ModSourceResult<Uri>> ResolveDownloadAsync(ModListing listing, ModListingFile file, CancellationToken cancellationToken = default)
    {
        if (Uri.TryCreate(file.DownloadUrl, UriKind.Absolute, out var direct)) return new(direct, null);
        var (json, error) = await ModSourceHttp.GetJsonAsync(http, $"{ApiBase}/v1/mods/{Uri.EscapeDataString(listing.Id)}/files/{Uri.EscapeDataString(file.Id)}/download-url", Name, Key, cancellationToken);
        if (json is null) return ModSourceResult<Uri>.Fail(error == ModSourceHttp.Describe(HttpStatusCode.Forbidden, Name) ? "The author allows downloads only on CurseForge's site. Use Open Page." : error!);
        using (json)
            return Uri.TryCreate(json.RootElement.Str("data"), UriKind.Absolute, out var uri) ? new(uri, null) : ModSourceResult<Uri>.Fail("The author allows downloads only on CurseForge's site. Use Open Page.");
    }
}

// GitHub releases of repositories the owner adds ("owner/repo"); their ZIP assets are the files.
public sealed class GitHubReleasesModSource(HttpClient http, Func<IReadOnlyList<string>> repositories) : IModSource
{
    private static readonly Regex RepoPattern = new(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9._-]{1,100}$");
    public string Id => "github";
    public string Name => "GitHub releases";
    public bool IsAllowedDownloadHost(Uri uri) => ModSourceHttp.HostIn(uri, "github.com", "githubusercontent.com");

    // "owner/repo" from that text or a github.com address; null when it is neither.
    public static string? NormalizeRepository(string? text)
    {
        var value = (text ?? string.Empty).Trim().TrimEnd('/');
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return null;
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            if (parts.Length < 2) return null;
            value = parts[0] + "/" + parts[1];
        }
        if (value.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) value = value[..^4];
        return RepoPattern.IsMatch(value) ? value : null;
    }

    private static void Accept(HttpRequestMessage request) => request.Headers.Accept.ParseAdd("application/vnd.github+json");

    public async Task<ModSourceResult<IReadOnlyList<ModListing>>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var repos = repositories();
        if (repos.Count == 0) return ModSourceResult<IReadOnlyList<ModListing>>.Fail("Add a repository you trust first (owner/repo).");
        var list = new List<ModListing>();
        foreach (var repo in repos.Take(20))
        {
            var (json, error) = await ModSourceHttp.GetJsonAsync(http, $"https://api.github.com/repos/{repo}", Name, Accept, cancellationToken);
            if (json is null) { list.Add(new ModListing { Source = Id, Id = repo, Name = repo, Note = error }); continue; }
            using (json)
            {
                var r = json.RootElement;
                if (!ModSourceHttp.Matches(query, repo, r.Str("description"))) continue;
                list.Add(new ModListing
                {
                    Source = Id, Id = repo, Name = r.Str("name") ?? repo, Author = r.TryGetProperty("owner", out var owner) ? owner.Str("login") : null,
                    Summary = r.Str("description"), Updated = r.Date("pushed_at"), Downloads = null, PageUrl = r.Str("html_url"),
                });
            }
        }
        return new(list, null);
    }

    public async Task<ModSourceResult<IReadOnlyList<ModListingFile>>> GetFilesAsync(ModListing listing, CancellationToken cancellationToken = default)
    {
        var (json, error) = await ModSourceHttp.GetJsonAsync(http, $"https://api.github.com/repos/{listing.Id}/releases?per_page=10", Name, Accept, cancellationToken);
        if (json is null) return ModSourceResult<IReadOnlyList<ModListingFile>>.Fail(error!);
        using (json)
        {
            var files = new List<ModListingFile>();
            foreach (var release in json.RootElement.EnumerateArray().Where(r => !r.Bool("draft")))
                foreach (var asset in release.Array("assets").Where(a => (a.Str("name") ?? string.Empty).EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
                    files.Add(new ModListingFile
                    {
                        Id = (asset.Long("id") ?? 0).ToString(), Name = asset.Str("name") ?? string.Empty, Version = release.Str("tag_name"),
                        SizeBytes = asset.Long("size"), Updated = asset.Date("updated_at"), DownloadUrl = asset.Str("browser_download_url"),
                        Note = release.Bool("prerelease") ? "A pre-release." : null,
                    });
            return new(files, files.Count == 0 ? "The repository's releases have no ZIP files." : null);
        }
    }

    public Task<ModSourceResult<Uri>> ResolveDownloadAsync(ModListing listing, ModListingFile file, CancellationToken cancellationToken = default) =>
        Task.FromResult(Uri.TryCreate(file.DownloadUrl, UriKind.Absolute, out var uri) ? new ModSourceResult<Uri>(uri, null) : ModSourceResult<Uri>.Fail("GitHub gave no download link."));
}

// Nexus Mods through the existing client and key: its three lists searched here, or one MOD by id or page address.
// A free account gets files only through the site's Mod Manager Download button (the nxm:// link).
public sealed class NexusModSource(INexusModsClient nexus, Func<string> key) : IModSource
{
    public string Id => "nexus";
    public string Name => "Nexus Mods";
    public bool IsAllowedDownloadHost(Uri uri) => NexusModsLinks.IsAllowedDownloadHost(uri);

    private static ModListing ToListing(NexusModDto m) => new()
    {
        Source = "nexus", Id = m.ModId.ToString(), Name = m.DisplayName, Author = m.Author, Summary = m.Summary, Version = m.Version,
        Updated = m.UpdatedTimestamp > 0 ? DateTimeOffset.FromUnixTimeSeconds(m.UpdatedTimestamp) : null, Downloads = null, PageUrl = NexusModsLinks.ModPageUrl(m.ModId),
    };

    public async Task<ModSourceResult<IReadOnlyList<ModListing>>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var apiKey = key();
        if (string.IsNullOrWhiteSpace(apiKey)) return ModSourceResult<IReadOnlyList<ModListing>>.Fail("Connect your Nexus Mods key in the Nexus Mods card below first.");
        if (NexusModsLinks.TryParseModReference(query, out var modId))
        {
            var one = await nexus.GetModAsync(apiKey, modId, cancellationToken);
            return one.Ok && one.Value is not null ? new([ToListing(one.Value)], null) : ModSourceResult<IReadOnlyList<ModListing>>.Fail(one.Error ?? "Nexus found no such MOD.");
        }
        var all = new Dictionary<int, NexusModDto>();
        foreach (var kind in new[] { "trending", "latest_updated", "latest_added" })
        {
            var list = await nexus.GetListAsync(apiKey, kind, cancellationToken);
            if (!list.Ok) return ModSourceResult<IReadOnlyList<ModListing>>.Fail(list.Error!);
            foreach (var m in list.Value ?? []) all.TryAdd(m.ModId, m);
        }
        return new(all.Values.Where(m => ModSourceHttp.Matches(query, m.Name, m.Author, m.Summary)).Select(ToListing).ToList(), null);
    }

    public async Task<ModSourceResult<IReadOnlyList<ModListingFile>>> GetFilesAsync(ModListing listing, CancellationToken cancellationToken = default)
    {
        var files = await nexus.GetFilesAsync(key(), int.Parse(listing.Id), cancellationToken);
        if (!files.Ok) return ModSourceResult<IReadOnlyList<ModListingFile>>.Fail(files.Error!);
        return new((files.Value ?? []).Where(f => !string.Equals(f.CategoryName, "ARCHIVED", StringComparison.OrdinalIgnoreCase)).Select(f => new ModListingFile
        {
            Id = f.FileId.ToString(), Name = f.FileName ?? f.DisplayName, Version = f.Version, SizeBytes = f.SizeKb * 1024,
            Note = f.IsPrimary ? "The main file." : null,
        }).ToList(), null);
    }

    public async Task<ModSourceResult<Uri>> ResolveDownloadAsync(ModListing listing, ModListingFile file, CancellationToken cancellationToken = default)
    {
        var link = await nexus.GetDownloadLinkAsync(key(), int.Parse(listing.Id), int.Parse(file.Id), null, null, cancellationToken);
        return link.Ok && link.Value is not null ? new(link.Value, null)
            : ModSourceResult<Uri>.Fail("Nexus gives free accounts files only through Mod Manager Download. Use Open Page, then Mod Manager Download on the file; with MystTiq set to take those links, it arrives here.");
    }
}
