// MystTiq v1.1.5.0: file reviewed for this release (2026-10-25).
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v1.0.1.0 (asked 2026-10-05: an Update button for every Update Center row): MystTiq's own Update. A running program
/// cannot replace its own files, and the upgrade notes already say to extract each version into a new folder, so this does
/// exactly that: it downloads the release's ZIP for this system, checks it against the release's SHA256SUMS.txt (refusing
/// any mismatch), and unpacks it beside the current folder (or under %LOCALAPPDATA%\MystTiq\versions when that folder may
/// not be written). Since v1.1.5.0 a copy installed by the setup is updated by the next setup (.msi), and a portable copy
/// gets the portable ZIP. Settings, servers and the service's data live outside the application folder, so the new version picks
/// them up when it is started. Nothing running is stopped or replaced.
/// </summary>
public sealed class MystTiqSelfUpdate
{
    public const string Repo = "Wad3M/MystTiq-Palworld-Server-Manager";
    private readonly HttpClient http;
    private readonly string apiBase;

    // v1.1.5.0 (roadmap W-1): opens a downloaded setup; tests pass their own so nothing is started.
    private readonly Func<string, bool> startSetup;
    private readonly string setupFolder;

    public MystTiqSelfUpdate(HttpClient http, string apiBase = "https://api.github.com", Func<string, bool>? startSetup = null, string? setupFolder = null)
    {
        this.http = http;
        this.apiBase = apiBase.TrimEnd('/');
        this.startSetup = startSetup ?? StartWindowsInstaller;
        this.setupFolder = setupFolder ?? Path.Combine(MystTiq.Core.Services.PortableMode.LocalAppData, "MystTiq", "updates");
    }

    /// <summary>v1.1.5.0 (roadmap W-1, W-2): how this copy was installed, which decides what an update downloads.</summary>
    public enum CopyKind { Zip, Portable, Setup }

    /// <summary>The setup leaves installed.txt in the program folder; portable.txt makes a portable copy.</summary>
    public static CopyKind KindOf(string appFolder) =>
        File.Exists(Path.Combine(appFolder, "installed.txt")) ? CopyKind.Setup
        : MystTiq.Core.Services.PortableMode.Detect(appFolder) is not null ? CopyKind.Portable
        : CopyKind.Zip;

    public static string AssetName(string version, bool windows, CopyKind kind) => kind switch
    {
        CopyKind.Setup when windows => $"MystTiqPalworldServer_v{version}_Windows-x64.msi",
        CopyKind.Portable when windows => $"MystTiqPalworldServer_v{version}_Windows-x64-Portable.zip",
        _ => AssetName(version, windows),
    };

    private static bool StartWindowsInstaller(string msiPath)
    {
        // msiexec asks for administrator rights itself (UAC); the setup closes MystTiq when it replaces the files.
        var info = new System.Diagnostics.ProcessStartInfo("msiexec.exe") { UseShellExecute = true };
        info.ArgumentList.Add("/i");
        info.ArgumentList.Add(msiPath);
        return System.Diagnostics.Process.Start(info) is not null;
    }

    public sealed record Result(bool Success, string Message, string? Folder);

    public static string AssetName(string version, bool windows) =>
        $"MystTiqPalworldServer_v{version}_{(windows ? "Windows" : "Linux")}-x64.zip";

    /// <summary>The SHA-256 the checksum list gives the file ("hash  name" per line, as `sha256sum` writes it).</summary>
    public static string? ExpectedHash(string sums, string fileName)
    {
        foreach (var line in sums.Split('\n'))
        {
            var parts = line.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1].TrimStart('*').Equals(fileName, StringComparison.OrdinalIgnoreCase) && parts[0].Length == 64)
                return parts[0].ToLowerInvariant();
        }
        return null;
    }

    /// <summary>A folder that does not exist yet: the name itself, then "-2", "-3" ...</summary>
    public static string FreeFolder(string parent, string name, Func<string, bool> exists)
    {
        var candidate = Path.Combine(parent, name);
        for (var i = 2; exists(candidate); i++) candidate = Path.Combine(parent, $"{name}-{i}");
        return candidate;
    }

    public async Task<Result> DownloadAsync(string version, string appFolder, string fallbackParent, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var kind = KindOf(appFolder);
        var asset = AssetName(version, windows, kind);
        var release = await GetJsonAsync<Release>($"{apiBase}/repos/{Repo}/releases/tags/v{version}", cancellationToken);
        if (release is null)
            return new Result(false, $"MystTiq v{version} was not found on GitHub.", null);
        var zipUrl = release.Assets?.FirstOrDefault(a => string.Equals(a.Name, asset, StringComparison.OrdinalIgnoreCase))?.DownloadUrl;
        var sumsUrl = release.Assets?.FirstOrDefault(a => string.Equals(a.Name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))?.DownloadUrl;
        if (zipUrl is null) return new Result(false, $"MystTiq v{version} has no {asset} download.", null);
        if (sumsUrl is null) return new Result(false, $"MystTiq v{version} has no SHA256SUMS.txt, so its download cannot be checked; nothing was downloaded.", null);

        string? expected;
        try { expected = ExpectedHash(await http.GetStringAsync(sumsUrl, cancellationToken), asset); }
        catch (HttpRequestException ex) { return new Result(false, $"The checksum list could not be downloaded: {ex.Message}", null); }
        if (expected is null) return new Result(false, $"SHA256SUMS.txt does not list {asset}; nothing was downloaded.", null);

        var temp = Path.Combine(Path.GetTempPath(), "mysttiq-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var zipPath = Path.Combine(temp, asset);
            progress?.Report($"Downloading {asset}…");
            string actual;
            try
            {
                using var response = await http.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using (var file = File.Create(zipPath)) await body.CopyToAsync(file, cancellationToken);
                await using var check = File.OpenRead(zipPath);
                actual = Convert.ToHexString(await SHA256.HashDataAsync(check, cancellationToken)).ToLowerInvariant();
            }
            catch (HttpRequestException ex) { return new Result(false, $"{asset} could not be downloaded: {ex.Message}", null); }
            if (actual != expected)
                return new Result(false, $"{asset} does not match its checksum (expected {expected[..12]}…, got {actual[..12]}…); it was deleted and nothing was unpacked.", null);

            // v1.1.5.0 (roadmap W-1): an installed copy is updated by the next setup, kept where it stays after this returns.
            if (kind == CopyKind.Setup && windows)
            {
                Directory.CreateDirectory(setupFolder);
                var kept = Path.Combine(setupFolder, asset);
                File.Copy(zipPath, kept, overwrite: true);
                return startSetup(kept)
                    ? new Result(true, $"The MystTiq v{version} setup is open. Follow it; when it asks, let it close MystTiq. Your settings and servers carry over.", null)
                    : new Result(false, $"The MystTiq v{version} setup was downloaded to {kept} but could not be opened; open it yourself.", null);
            }

            progress?.Report($"Unpacking {asset}…");
            var unpacked = Path.Combine(temp, "unpacked");
            // v1.0.7.4 (roadmap Q-15): even a checksum-verified download is checked for size, entries and free space first.
            if (MystTiq.Core.Services.ArchiveLimits.CheckFile(zipPath, temp) is { } tooBig)
                return new Result(false, $"{asset} was not unpacked: {tooBig}", null);
            ZipFile.ExtractToDirectory(zipPath, unpacked);
            // The ZIP holds one folder (MystTiqPalworldServer_vX_Windows-x64); a flat ZIP is taken as it is.
            var entries = Directory.GetFileSystemEntries(unpacked);
            var top = entries.Length == 1 && Directory.Exists(entries[0]) ? entries[0] : unpacked;
            var name = top == unpacked ? Path.GetFileNameWithoutExtension(asset) : Path.GetFileName(top);
            var desktop = windows ? "MystTiq.Desktop.exe" : "MystTiq.Desktop";
            if (!File.Exists(Path.Combine(top, desktop)))
                return new Result(false, $"{asset} does not contain {desktop}; nothing was unpacked.", null);

            var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(appFolder)) ?? fallbackParent;
            string target;
            try
            {
                target = FreeFolder(parent, name, Path.Exists);
                MoveFolder(top, target);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                Directory.CreateDirectory(fallbackParent);
                target = FreeFolder(fallbackParent, name, Path.Exists);
                MoveFolder(top, target);
            }
            // v1.1.5.0 (roadmap W-2): a portable copy keeps its settings in its own data folder, which has to come along.
            if (kind == CopyKind.Portable)
                return new Result(true, $"MystTiq v{version} (portable) is ready in {target}. Exit MystTiq from the tray, move the data folder from {appFolder} into {target}, then start {desktop} there.", target);
            return new Result(true, $"MystTiq v{version} is ready in {target}. Exit MystTiq from the tray, then start {desktop} there; your settings and servers carry over.", target);
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    // Directory.Move cannot cross drives (temp on C:, MystTiq on E:); copy then.
    private static void MoveFolder(string source, string target)
    {
        try { Directory.Move(source, target); return; }
        catch (IOException) when (!Directory.Exists(target)) { }
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
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
        catch (Exception ex) when (ex is HttpRequestException or JsonException) { return null; }
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
