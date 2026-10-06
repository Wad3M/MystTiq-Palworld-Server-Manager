// MystTiq v1.0.5.0: file reviewed for this release (2026-10-06).
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.1.0 (asked 2026-10-05: "the update page should have a button to update every option"): PalDefender's Update. Its
/// GitHub release ships PalDefender.dll and its loader d3d9.dll as separate downloads; both are replaced in the server's
/// Win64 folder and nothing else is touched (d3d9_config.json and the PalDefender folder with its settings and logs stay).
/// Before replacing, the downloads must be Windows DLLs and PalDefender.dll's own version must be the release's; the files
/// they replace are kept in a backup folder. A PalDefender that is switched off (its files renamed, see NativeModCatalog)
/// stays off: the new files take the renamed names. The caller makes sure PalServer is stopped.
/// </summary>
public sealed class PalDefenderUpdater
{
    public const string Repo = "Ultimeit/PalDefender";
    public const string MainDll = "PalDefender.dll";
    public const string Loader = "d3d9.dll";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly HttpClient http;
    private readonly string apiBase;

    public PalDefenderUpdater(HttpClient http, string apiBase = "https://api.github.com")
    {
        this.http = http;
        this.apiBase = apiBase.TrimEnd('/');
    }

    /// <summary>Which files in Win64 the update writes: PalDefender.dll (or its switched-off name) and its d3d9.dll loader (or its
    /// switched-off name). Null Main: PalDefender is not installed, so there is nothing to update.</summary>
    public sealed record FilePlan(string? Main, string? Loader, string? Note);

    public static FilePlan Plan(IReadOnlyList<NativeFile> win64)
    {
        bool Has(string name) => win64.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var main = Has(MainDll) ? MainDll : NativeModCatalog.NewestDisabled(win64, MainDll)?.Name;
        if (main is null) return new FilePlan(null, null, "PalDefender is not installed on this server, so there is nothing to update.");
        if (Has(Loader)) return new FilePlan(main, Loader, null);
        if (Has("version.dll"))
            return new FilePlan(main, null, "PalDefender is loaded through version.dll here; that loader was left as it is.");
        var disabledLoader = NativeModCatalog.NewestDisabled(win64, Loader)?.Name;
        return disabledLoader is not null
            ? new FilePlan(main, disabledLoader, null)
            : new FilePlan(main, null, "No d3d9.dll loader was found next to PalDefender.dll; only PalDefender.dll was replaced.");
    }

    public async Task<ComponentUpdateResult> UpdateAsync(string win64, string backupRoot, CancellationToken cancellationToken)
    {
        if (!await Gate.WaitAsync(0, cancellationToken))
            return new ComponentUpdateResult(false, "A PalDefender update is already running.");
        var staging = Path.Combine(backupRoot, "staging-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (!Directory.Exists(win64))
                return new ComponentUpdateResult(false, $"The server's Win64 folder was not found: {win64}");
            var files = new DirectoryInfo(win64).GetFiles().Select(f => new NativeFile(f.Name, f.LastWriteTimeUtc)).ToList();
            var plan = Plan(files);
            if (plan.Main is null) return new ComponentUpdateResult(false, plan.Note!);

            var release = await GetJsonAsync<Release>($"{apiBase}/repos/{Repo}/releases/latest", cancellationToken);
            if (release?.TagName is not { Length: > 0 } tag)
                return new ComponentUpdateResult(false, "Could not read PalDefender's latest release from GitHub.");
            var wanted = plan.Loader is not null ? new[] { MainDll, Loader } : new[] { MainDll };
            Directory.CreateDirectory(staging);
            foreach (var name in wanted)
            {
                var asset = release.Assets?.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
                if (asset?.DownloadUrl is not { Length: > 0 } url)
                    return new ComponentUpdateResult(false, $"PalDefender {tag} has no {name} download; nothing was changed.");
                var bytes = await http.GetByteArrayAsync(url, cancellationToken);
                if (bytes.Length < 2 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z')
                    return new ComponentUpdateResult(false, $"The downloaded {name} is not a Windows DLL; nothing was changed.");
                await File.WriteAllBytesAsync(Path.Combine(staging, name), bytes, cancellationToken);
            }

            var downloadedVersion = VersionOf(Path.Combine(staging, MainDll));
            if (!SameVersion(downloadedVersion, tag))
                return new ComponentUpdateResult(false, $"The downloaded PalDefender.dll says it is version {downloadedVersion ?? "unknown"}, not {tag}; nothing was changed.");

            var installedVersion = VersionOf(Path.Combine(win64, plan.Main)) ?? "unknown";
            var backup = Path.Combine(backupRoot, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}-v{installedVersion}");
            Directory.CreateDirectory(backup);
            var targets = new List<(string Source, string Target)> { (MainDll, plan.Main) };
            if (plan.Loader is not null) targets.Add((Loader, plan.Loader));
            foreach (var (_, target) in targets)
            {
                var existing = Path.Combine(win64, target);
                if (File.Exists(existing)) File.Copy(existing, Path.Combine(backup, target), overwrite: true);
            }
            foreach (var (source, target) in targets)
                File.Copy(Path.Combine(staging, source), Path.Combine(win64, target), overwrite: true);

            var off = !plan.Main.Equals(MainDll, StringComparison.OrdinalIgnoreCase) ? " It stays switched off, as it was." : string.Empty;
            return new ComponentUpdateResult(true,
                $"PalDefender updated from {installedVersion} to {tag.TrimStart('v', 'V')}: {string.Join(" and ", targets.Select(t => t.Target))} replaced, your settings kept.{off} The previous files are in {backup}." +
                (plan.Note is not null ? " " + plan.Note : string.Empty));
        }
        catch (IOException ex)
        {
            return new ComponentUpdateResult(false, $"PalDefender could not be replaced (is PalServer still running?): {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ComponentUpdateResult(false, $"PalDefender could not be replaced: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return new ComponentUpdateResult(false, $"PalDefender could not be downloaded: {ex.Message}");
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
            Gate.Release();
        }
    }

    public static string? VersionOf(string dll)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(dll);
            var match = Regex.Match(info.ProductVersion ?? info.FileVersion ?? string.Empty, @"\d+\.\d+(\.\d+)?");
            return match.Success ? match.Value : null;
        }
        catch { return null; }
    }

    // "1.9.3" and "v1.9.3" are the same; "1.9.3" and "1.9.3.0" too.
    public static bool SameVersion(string? version, string tag)
    {
        static Version? Parse(string? text)
        {
            var match = Regex.Match(text ?? string.Empty, @"\d+(\.\d+){1,3}");
            if (!match.Success || !Version.TryParse(match.Value, out var v)) return null;
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
        var a = Parse(version);
        return a is not null && a == Parse(tag);
    }

    private async Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken) where T : class
    {
        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException) { return null; }
    }

    private sealed class Release
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; init; }
        [JsonPropertyName("assets")] public List<Asset>? Assets { get; init; }
    }

    private sealed class Asset
    {
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("browser_download_url")] public string? DownloadUrl { get; init; }
    }
}
