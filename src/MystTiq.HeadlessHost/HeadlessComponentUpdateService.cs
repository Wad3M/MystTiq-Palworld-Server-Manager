// MystTiq v1.0.0.3: file reviewed for this release (2026-10-05).
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.7.45.0: Update Center Overhaul. Replaces the existence-only checks
// HeadlessServerDistributionService/HeadlessEnvironmentChecklistService already did for these same
// components with real installed/latest VERSION tracking, matching the v0.2.16.4 reference's
// full component-by-component table. Grounded against that codebase's own real capabilities before
// writing any check: some components genuinely have no reliable unattended "latest version" source
// (Python, VC++ Runtime, MSVC Build Tools, the PlM/Oodle tooling this project vendors under its own
// "palworld-plm-tools" folder name with no single canonical upstream) -- those are reported as
// "Unavailable" with an honest reason, not a fabricated number, matching this project's established
// disclosed-gap convention (see HeadlessEnvironmentChecklistService's "BACKEND REQUIRED" rows).
public sealed class HeadlessComponentUpdateService
{
    private const string PalworldDedicatedServerAppId = "2394010";
    private const string MystTiqRepo = "Wad3M/MystTiq-Palworld-Server-Manager";
    private const string Ue4ssRepo = "Okaetsu/RE-UE4SS";
    private const string PalDefenderRepo = "Ultimeit/PalDefender";

    private static readonly HttpClient Http = BuildHttpClient();
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(8);

    private readonly IServerPathProfile paths;
    private readonly HeadlessModManagementService modManagement;

    public HeadlessComponentUpdateService(IServerPathProfile paths, HeadlessModManagementService modManagement)
    {
        this.paths = paths;
        this.modManagement = modManagement;
    }

    public async Task<ComponentVersionSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var components = new List<ComponentVersionInfo>
        {
            await CheckMystTiqAsync(now, cancellationToken),
            CheckSteamCmd(now),
            await CheckPalworldServerAsync(now, cancellationToken),
            await CheckUe4ssAsync(now, cancellationToken),
            await CheckPalDefenderAsync(now, cancellationToken),
            await CheckPythonAsync(now, cancellationToken),
            await CheckPipAsync(now, cancellationToken),
            await CheckSaveToolsAsync(now, cancellationToken),
            CheckPlmOodle(now),
            await CheckDotNetAsync(now, cancellationToken),
            CheckVcRuntime(now),
            CheckCppBuildTools(now),
        };
        return new ComponentVersionSnapshot(components, now);
    }

    // ------------------------------------------------------------------
    // Core Server
    // ------------------------------------------------------------------

    private async Task<ComponentVersionInfo> CheckMystTiqAsync(DateTimeOffset now, CancellationToken ct)
    {
        var installed = typeof(HeadlessComponentUpdateService).Assembly.GetName().Version?.ToString(4) ?? "unknown";
        // v0.9.10.0 (external review): /releases/latest skips prereleases, and every MystTiq release so far is published as
        // one, so a newer version was never seen. The release list is read instead, and ReleaseChannel picks the newest one
        // this installation should consider.
        var releases = await TryGetJsonAsync<List<GitHubReleaseDto>>($"https://api.github.com/repos/{MystTiqRepo}/releases?per_page=30", ct);
        if (releases is null)
            return Unavailable("Core Server", "MystTiq Server Manager", installed, $"GitHub: {MystTiqRepo}", now,
                "Could not reach the GitHub releases API to check for a newer version.");
        var candidates = releases.Select(r => new ReleaseCandidate(r.TagName ?? string.Empty, r.Prerelease, r.Draft, r.HtmlUrl)).ToArray();
        if (ReleaseChannel.PickNewest(candidates, installed) is not { } release)
            return Unavailable("Core Server", "MystTiq Server Manager", installed, $"GitHub: {MystTiqRepo}", now,
                $"No published release was found ({ReleaseChannel.Describe(installed)}).");

        var tag = release.Tag;
        var latest = tag.TrimStart('v', 'V');
        return Compare("Core Server", "MystTiq Server Manager", installed, latest, $"GitHub: {MystTiqRepo}", now,
            $"Latest published release: {tag} ({ReleaseChannel.Describe(installed)})." + (release.Url is { Length: > 0 } url ? $" {url}" : string.Empty),
            updateIsInformationalOnly: true);
    }

    private ComponentVersionInfo CheckSteamCmd(DateTimeOffset now)
    {
        var exists = File.Exists(paths.SteamCmdExecutable);
        return new ComponentVersionInfo(
            "Core Server", "SteamCMD",
            exists ? "Present" : "Not detected",
            "N/A",
            exists ? "SelfUpdating" : "NotInstalled",
            "Valve (self-updating)",
            now,
            "SteamCMD has no version number of its own -- it self-updates on every launch, so there is nothing to compare against a \"latest\" release.");
    }

    private async Task<ComponentVersionInfo> CheckPalworldServerAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (!File.Exists(paths.ServerExecutable))
            return NotInstalled("Core Server", "Palworld Dedicated Server", $"Steam App ID {PalworldDedicatedServerAppId}", now,
                "Server executable was not found at the configured install path.");

        var manifestPath = Path.Combine(paths.ServerRoot, "steamapps", $"appmanifest_{PalworldDedicatedServerAppId}.acf");
        var buildId = TryReadAcfBuildId(manifestPath);
        if (buildId is null)
            return Unavailable("Core Server", "Palworld Dedicated Server", "Installed (build id unknown)", $"Steam App ID {PalworldDedicatedServerAppId}", now,
                $"Server is installed, but its SteamCMD manifest ({manifestPath}) was not found or could not be read to determine the installed build id.");

        // v0.9.5.0: the latest build comes from SteamCMD's app info for the public branch. The Steam Web API's
        // ISteamApps/UpToDateCheck, used before, compares against a developer-set minimum version (1000 for this app),
        // not the build, so it answered "up to date" for build 25080279 while Steam's public build was 25247047 and a
        // v1.0.5 game could not join the v1.0.4 server.
        var (publicBuild, error) = await GetPublicBuildAsync(ct);
        if (publicBuild is null)
            return Unavailable("Core Server", "Palworld Dedicated Server", buildId, "SteamCMD app info (public branch)", now,
                $"Could not read Steam's current public build for app {PalworldDedicatedServerAppId}: {error}");

        var published = publicBuild.Updated is { } updated ? $" (published {updated.ToLocalTime():yyyy-MM-dd})" : string.Empty;
        if (!long.TryParse(buildId, out var installedNumber) || !long.TryParse(publicBuild.BuildId, out var publicNumber))
            return Unavailable("Core Server", "Palworld Dedicated Server", buildId, "SteamCMD app info (public branch)", now,
                $"Steam's public build is {publicBuild.BuildId}{published}; the installed build id could not be compared with it.");
        if (installedNumber < publicNumber)
            return new ComponentVersionInfo("Core Server", "Palworld Dedicated Server", buildId, publicBuild.BuildId, "UpdateAvailable", "SteamCMD app info (public branch)", now,
                $"Steam's public build is {publicBuild.BuildId}{published}; this server has {buildId}. Players on the current game cannot join until it is updated: stop the server and use Update. After a game update, check that PalDefender and UE4SS support it.");
        return new ComponentVersionInfo("Core Server", "Palworld Dedicated Server", buildId, publicBuild.BuildId, "UpToDate", "SteamCMD app info (public branch)", now,
            installedNumber == publicNumber
                ? $"The installed build is Steam's current public build{published}."
                : $"The installed build is newer than Steam's public build {publicBuild.BuildId}{published} (a beta branch?).");
    }

    public sealed record SteamPublicBuild(string BuildId, DateTimeOffset? Updated);

    private static readonly SemaphoreSlim PublicBuildGate = new(1, 1);
    private static readonly Dictionary<string, (DateTimeOffset At, SteamPublicBuild? Build, string Error)> PublicBuildCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan PublicBuildCacheAge = TimeSpan.FromMinutes(15);

    // SteamCMD takes 10-30 s to log in and fetch app info, so one answer serves every server using the same SteamCMD
    // for 15 minutes.
    private async Task<(SteamPublicBuild? Build, string Error)> GetPublicBuildAsync(CancellationToken ct)
    {
        var steamCmd = paths.SteamCmdExecutable;
        if (!File.Exists(steamCmd)) return (null, $"SteamCMD was not found at {steamCmd}.");
        await PublicBuildGate.WaitAsync(ct);
        try
        {
            lock (PublicBuildCache)
                if (PublicBuildCache.TryGetValue(steamCmd, out var cached) && DateTimeOffset.UtcNow - cached.At < PublicBuildCacheAge)
                    return (cached.Build, cached.Error);
            var (exitCode, output, _) = await RunProcessAsync(steamCmd, $"+login anonymous +app_info_update 1 +app_info_print {PalworldDedicatedServerAppId} +quit", ct, TimeSpan.FromSeconds(120));
            var build = ParsePublicBuild(output);
            var error = build is not null ? string.Empty
                : exitCode == -1 && output.Length == 0 ? "SteamCMD did not answer within two minutes."
                : $"SteamCMD's app info had no public branch build (exit code {exitCode}).";
            lock (PublicBuildCache) PublicBuildCache[steamCmd] = (DateTimeOffset.UtcNow, build, error);
            return (build, error);
        }
        finally { PublicBuildGate.Release(); }
    }

    private static int publicBuildRefreshing;

    // v0.9.5.0: for the Doctor, which must not wait on SteamCMD: the last known public build (null until one is known),
    // and a refresh started in the background when it is missing or older than 15 minutes.
    public SteamPublicBuild? PeekPublicBuild()
    {
        var steamCmd = paths.SteamCmdExecutable;
        (DateTimeOffset At, SteamPublicBuild? Build, string Error) cached;
        bool known;
        lock (PublicBuildCache) known = PublicBuildCache.TryGetValue(steamCmd, out cached);
        if ((!known || DateTimeOffset.UtcNow - cached.At >= PublicBuildCacheAge) && File.Exists(steamCmd) &&
            Interlocked.CompareExchange(ref publicBuildRefreshing, 1, 0) == 0)
        {
            _ = Task.Run(async () =>
            {
                try { await GetPublicBuildAsync(CancellationToken.None); }
                finally { Interlocked.Exchange(ref publicBuildRefreshing, 0); }
            });
        }
        return known ? cached.Build : null;
    }

    public string? InstalledServerBuild() =>
        TryReadAcfBuildId(Path.Combine(paths.ServerRoot, "steamapps", $"appmanifest_{PalworldDedicatedServerAppId}.acf"));

    // The public branch's build in SteamCMD's `app_info_print` output:
    //   "branches" { "public" { "buildid" "25247047" "timeupdated" "1789441330" } ... }
    public static SteamPublicBuild? ParsePublicBuild(string appInfo)
    {
        var branches = appInfo.IndexOf("\"branches\"", StringComparison.Ordinal);
        if (branches < 0) return null;
        var match = Regex.Match(appInfo[branches..], "\"public\"\\s*\\{\\s*\"buildid\"\\s*\"(\\d+)\"(?:\\s*\"timeupdated\"\\s*\"(\\d+)\")?");
        if (!match.Success) return null;
        DateTimeOffset? updated = long.TryParse(match.Groups[2].Value, out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;
        return new SteamPublicBuild(match.Groups[1].Value, updated);
    }
    // v0.9.5.0: PalDefender delivers give-item, kits and teleports, and hooks the game; after the game update to v1.0.5
    // the installed v1.8.3 took the server down about 40 s after each player joined (live, 2026-09-28), while v1.9.2 was
    // already out. The installed version is the DLL's product version; the latest is its newest GitHub release.
    private async Task<ComponentVersionInfo> CheckPalDefenderAsync(DateTimeOffset now, CancellationToken ct)
    {
        const string component = "PalDefender";
        var dll = Path.Combine(paths.RuntimeBinaryRoot, "PalDefender.dll");
        if (!File.Exists(dll))
            return NotInstalled("Core Server", component, $"GitHub: {PalDefenderRepo}", now,
                "PalDefender is not installed; Give Item, kits and teleports need it (Windows servers only).");
        var installed = ExtractVersion(FileVersionInfo.GetVersionInfo(dll).ProductVersion) ?? "unknown";
        var warning = PalDefenderGameWarning(paths.RuntimeBinaryRoot);
        var note = warning is null ? string.Empty
            : $" PalDefender's own log says it is not updated for this game version (\"{warning}\"); players may be disconnected or the server may stop when they join until it is updated.";
        var release = await TryGetJsonAsync<GitHubReleaseDto>($"https://api.github.com/repos/{PalDefenderRepo}/releases/latest", ct);
        if (release?.TagName is not { Length: > 0 } tag)
            return Unavailable("Core Server", component, installed, $"GitHub: {PalDefenderRepo}", now,
                "Could not reach the GitHub releases API to check for a newer PalDefender." + note);
        var result = Compare("Core Server", component, installed, tag.TrimStart('v', 'V'), $"GitHub: {PalDefenderRepo}", now,
            $"Latest release: {tag}" + (release.PublishedAt is { } at ? $" ({at.ToLocalTime():yyyy-MM-dd})." : ".") +
            " Replace PalDefender.dll and d3d9.dll in the server's Win64 folder with the server stopped." + note);
        return warning is not null && result.Status == "UpToDate" ? result with { Status = "UpdateAvailable" } : result;
    }

    // The warning PalDefender logs at start-up when the game has changed under it, from its newest session log.
    public static string? PalDefenderGameWarning(string runtimeBinaryRoot)
    {
        try
        {
            var logs = new DirectoryInfo(Path.Combine(runtimeBinaryRoot, "PalDefender", "Logs"));
            if (!logs.Exists) return null;
            var newest = logs.GetFiles("*.log").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
            if (newest is null) return null;
            using var stream = new FileStream(newest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n').Select(l => l.Trim())
                .FirstOrDefault(l => l.Contains("hasn't yet updated to the latest version", StringComparison.OrdinalIgnoreCase))
                is { } line ? Regex.Replace(line, @"^\[[^\]]*\]\[[^\]]*\]\s*", string.Empty) : null;
        }
        catch { return null; }
    }

    // v0.9.9.0: the last UE4SS result, for the Alert Center. It is refreshed in the background at most every six hours (and
    // whenever the Update Center checks), so an alert evaluation never waits on GitHub. Null until the first check finishes.
    private static readonly TimeSpan Ue4ssRefresh = TimeSpan.FromHours(6);
    private ComponentVersionInfo? ue4ssCached;
    private DateTimeOffset ue4ssCheckedAt = DateTimeOffset.MinValue;
    private int ue4ssRefreshing;

    public ComponentVersionInfo? PeekUe4ssStatus()
    {
        if (DateTimeOffset.UtcNow - ue4ssCheckedAt > Ue4ssRefresh && Interlocked.CompareExchange(ref ue4ssRefreshing, 1, 0) == 0)
        {
            _ = Task.Run(async () =>
            {
                try { await CheckUe4ssAsync(DateTimeOffset.UtcNow, CancellationToken.None); }
                catch { ue4ssCheckedAt = DateTimeOffset.UtcNow; }
                finally { Interlocked.Exchange(ref ue4ssRefreshing, 0); }
            });
        }
        return ue4ssCached;
    }

    private async Task<ComponentVersionInfo> CheckUe4ssAsync(DateTimeOffset now, CancellationToken ct)
    {
        var result = await CheckUe4ssCoreAsync(now, ct);
        ue4ssCached = result;
        ue4ssCheckedAt = DateTimeOffset.UtcNow;
        return result;
    }

    private async Task<ComponentVersionInfo> CheckUe4ssCoreAsync(DateTimeOffset now, CancellationToken ct)
    {
        var inventory = await modManagement.GetInventoryAsync(ct);
        var installed = inventory.Ue4ss.InstalledVersion;
        if (!inventory.Ue4ss.HasUe4ssRoot || string.Equals(installed, "Not detected", StringComparison.OrdinalIgnoreCase))
            return NotInstalled("Core Server", "UE4SS Runtime", $"GitHub: {Ue4ssRepo}", now,
                "UE4SS runtime was not detected under this server's binaries.");

        // v0.7.47.0 bug fix: this used to hardcode /releases/tags/experimental-palworld, the fork's
        // original rolling tag. The fork has since switched release strategy to discrete, newly-
        // tagged releases per commit ("Future releases will be a new release instead of updating
        // the old one," confirmed via the fork's own release notes) -- a newer release (e.g.
        // "2281fa31") now exists that the old hardcoded tag lookup could never see, silently
        // reporting stale data. Fixed to fetch the full release list and pick whichever one was
        // actually published most recently, matching HeadlessUe4ssReleaseCatalogService's own
        // ordering (the same fix, applied here too rather than just in the newer feature).
        var releases = await TryGetJsonAsync<List<GitHubReleaseDto>>($"https://api.github.com/repos/{Ue4ssRepo}/releases", ct);
        var release = releases?.Where(r => r.TagName is { Length: > 0 }).OrderByDescending(r => r.PublishedAt).FirstOrDefault();
        if (release is null)
            return Unavailable("Core Server", "UE4SS Runtime", installed, $"GitHub: {Ue4ssRepo}", now,
                "Could not reach the GitHub releases API to check the latest Palworld-fork release.");

        var publishedAt = release.PublishedAt?.ToString("yyyy-MM-dd") ?? "unknown date";
        var latestDisplay = $"{release.TagName} (published {publishedAt})";

        // v0.7.60.0 bug fix: this always reported "CheckManually" before, regardless of what was
        // actually installed, because `installed` was a generic, uncomparable placeholder string --
        // the real reason this app could tell a user "up to date" without that being true. Now that
        // ApplyUe4ssInstallAsync records exactly which release it applied, an exact tag match is a
        // genuine, verifiable "up to date" -- not a guess. Falls back to the same honest
        // "CheckManually" this always did whenever no recorded tag exists (installs made before this
        // feature existed, or files copied in manually, bypassing MystTiq's own install flow
        // entirely -- exactly how this project's own real install was done until this version).
        var installedTag = modManagement.TryGetInstalledUe4ssReleaseTag();
        if (installedTag is { Length: > 0 })
        {
            var upToDate = string.Equals(installedTag, release.TagName, StringComparison.OrdinalIgnoreCase);
            return new ComponentVersionInfo("Core Server", "UE4SS Runtime", installed, latestDisplay,
                upToDate ? "UpToDate" : "UpdateAvailable", $"GitHub: {Ue4ssRepo}", now,
                upToDate
                    ? $"Installed release ({installedTag}) exactly matches the latest published release."
                    : $"Installed release ({installedTag}) does not match the latest published release ({release.TagName})." +
                      (release.HtmlUrl is { Length: > 0 } releaseUrl ? $" {releaseUrl}" : string.Empty));
        }

        // v0.9.9.0: without a recorded tag, the installed UE4SS.dll is compared by content with the one inside each of the
        // newest releases' downloads (hashed once per asset and cached). An identical file names the release exactly; file
        // dates cannot (they are when the files were extracted here, not when UE4SS was built).
        var matched = await MatchInstalledUe4ssAsync(releases!.Where(r => r.TagName is { Length: > 0 }).OrderByDescending(r => r.PublishedAt).Take(3).ToArray(), ct);
        if (matched is not null)
        {
            var isLatest = string.Equals(matched.Tag, release.TagName, StringComparison.OrdinalIgnoreCase);
            return new ComponentVersionInfo("Core Server", "UE4SS Runtime", $"{matched.Tag} ({matched.Asset})", latestDisplay,
                isLatest ? "UpToDate" : "UpdateAvailable", $"GitHub: {Ue4ssRepo}", now,
                isLatest
                    ? $"The installed UE4SS.dll is identical to the one in the latest release ({matched.Tag}, {matched.Asset})."
                    : $"The installed UE4SS.dll is identical to the one in release {matched.Tag} ({matched.Asset}); the latest release is {release.TagName}." +
                      (release.HtmlUrl is { Length: > 0 } latestUrl ? $" {latestUrl}" : string.Empty));
        }

        // This fork's tags are commit-hash-style identifiers, not incrementing version numbers, so
        // without a recorded install tag or an identical file there is no reliable way to compare
        // "installed" vs "latest" -- this is reported for the user to judge, not auto-compared.
        return new ComponentVersionInfo("Core Server", "UE4SS Runtime", installed,
            latestDisplay, "CheckManually", $"GitHub: {Ue4ssRepo}", now,
            "No record of which release was installed through MystTiq exists for this install, and its UE4SS.dll is not identical to the one in any of the three newest releases (an older release, a custom build, or the downloads could not be read) -- installed vs. latest cannot be safely auto-compared. Compare dates/notes manually, or reinstall through MystTiq's own UE4SS install flow to enable automatic comparison going forward." +
            (release.HtmlUrl is { Length: > 0 } url ? $" {url}" : string.Empty));
    }

    private sealed record Ue4ssMatch(string Tag, string Asset);

    // The active UE4SS.dll (the modern ue4ss folder first, then the legacy layout beside the game binary).
    private string? InstalledUe4ssDll() => new[] { Path.Combine(paths.RuntimeBinaryRoot, "ue4ss", "UE4SS.dll"), Path.Combine(paths.RuntimeBinaryRoot, "UE4SS.dll") }.FirstOrDefault(File.Exists);

    private async Task<Ue4ssMatch?> MatchInstalledUe4ssAsync(IReadOnlyList<GitHubReleaseDto> releases, CancellationToken ct)
    {
        try
        {
            if (InstalledUe4ssDll() is not { } dll) return null;
            string installedHash;
            await using (var stream = File.OpenRead(dll)) installedHash = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream, ct));

            var cachePath = Path.Combine(paths.ManagerRuntimeRoot, "ue4ss-release-hashes.json");
            Dictionary<string, string[]> cache;
            try { cache = File.Exists(cachePath) ? JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(cachePath)) ?? [] : []; }
            catch { cache = []; }

            var known = new List<(string Tag, string Asset, IReadOnlyList<string> Hashes)>();
            var changed = false;
            foreach (var release in releases)
            {
                foreach (var asset in release.Assets ?? [])
                {
                    if (asset.Name is not { Length: > 0 } name || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || asset.DownloadUrl is not { Length: > 0 } url || asset.Size > Ue4ssReleaseFiles.MaximumDownloadBytes) continue;
                    var key = $"{asset.Id}:{asset.UpdatedAt:O}";
                    if (!cache.TryGetValue(key, out var hashes))
                    {
                        using var response = await Downloads.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                        if (!response.IsSuccessStatusCode) continue;
                        await using var body = await response.Content.ReadAsStreamAsync(ct);
                        using var buffer = new MemoryStream();
                        await body.CopyToAsync(buffer, ct);
                        buffer.Position = 0;
                        hashes = [.. Ue4ssReleaseFiles.DllHashes(buffer)];
                        cache[key] = hashes;
                        changed = true;
                    }

                    known.Add((release.TagName!, name, hashes));
                }
            }

            if (changed)
            {
                Directory.CreateDirectory(paths.ManagerRuntimeRoot);
                File.WriteAllText(cachePath, JsonSerializer.Serialize(cache));
            }

            return Ue4ssReleaseFiles.Match(installedHash, known) is { } hit ? new Ue4ssMatch(hit.Tag, hit.Asset) : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
    }

    // A release download is a few megabytes; the ordinary client's 10 s would cut a slow connection off.
    private static readonly HttpClient Downloads = BuildDownloadClient();
    private static HttpClient BuildDownloadClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MystTiq-Palworld-Server-Manager");
        return client;
    }

    // ------------------------------------------------------------------
    // Save & Runtime Dependencies
    // ------------------------------------------------------------------

    private async Task<ComponentVersionInfo> CheckPythonAsync(DateTimeOffset now, CancellationToken ct)
    {
        var python = FindOnPath(OperatingSystem.IsWindows() ? ["python.exe", "py.exe"] : ["python3", "python"]);
        if (python is null)
            return NotInstalled("Save & Runtime Dependencies", "Python Runtime", "python.org", now,
                "Python was not found on PATH.");

        var (_, stdout, stderr) = await RunProcessAsync(python, "--version", ct);
        var version = ExtractVersion(string.IsNullOrWhiteSpace(stdout) ? stderr : stdout);
        return new ComponentVersionInfo("Save & Runtime Dependencies", "Python Runtime",
            version ?? "Detected (version unavailable)", "Unavailable", "Unavailable", "python.org", now,
            "Python.org does not publish a simple, reliable unattended feed for \"latest version\" -- this row shows what's installed only.");
    }

    private async Task<ComponentVersionInfo> CheckPipAsync(DateTimeOffset now, CancellationToken ct)
    {
        var python = FindOnPath(OperatingSystem.IsWindows() ? ["python.exe", "py.exe"] : ["python3", "python"]);
        if (python is null)
            return NotInstalled("Save & Runtime Dependencies", "pip", "PyPI: pip", now,
                "pip requires a Python installation, which was not found on PATH.");

        var (_, stdout, _) = await RunProcessAsync(python, "-m pip --version", ct);
        var installed = ExtractVersion(stdout);
        if (installed is null)
            return NotInstalled("Save & Runtime Dependencies", "pip", "PyPI: pip", now,
                "Python was found, but \"python -m pip --version\" did not return a recognizable version.");

        var latest = await TryGetJsonAsync<PyPiPackageDto>("https://pypi.org/pypi/pip/json", ct);
        if (latest?.Info?.Version is not { Length: > 0 } latestVersion)
            return Unavailable("Save & Runtime Dependencies", "pip", installed, "PyPI: pip", now,
                "Could not reach PyPI to check the latest pip release.");

        return Compare("Save & Runtime Dependencies", "pip", installed, latestVersion, "PyPI: pip", now,
            $"PyPI's current pip release is {latestVersion}.");
    }

    // v0.7.82.0: the Update Center's first real, actionable in-place update -- direct request ("the
    // update center where it says update available should allow us to click on it to update"). pip
    // is the one component here genuinely upgradable with a single, safe, well-known command;
    // reuses the exact same python resolution CheckPipAsync above already does, so this can only
    // ever target the same installation the status check reported on.
    public async Task<ComponentUpdateResult> UpdatePipAsync(CancellationToken cancellationToken)
    {
        var python = FindOnPath(OperatingSystem.IsWindows() ? ["python.exe", "py.exe"] : ["python3", "python"]);
        if (python is null)
            return new ComponentUpdateResult(false, "pip requires a Python installation, which was not found on PATH.");

        var (exitCode, stdout, stderr) = await RunProcessAsync(python, "-m pip install --upgrade pip", cancellationToken, TimeSpan.FromSeconds(60));
        if (exitCode != 0)
            return new ComponentUpdateResult(false, $"pip upgrade failed (exit code {exitCode}). {stderr.Trim()}");

        return new ComponentUpdateResult(true, stdout.Trim().Length > 0 ? stdout.Trim() : "pip upgraded successfully.");
    }

    private async Task<ComponentVersionInfo> CheckSaveToolsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var scriptPath = FindFirstExisting(
            Path.Combine(paths.ServerRoot, "Tools", "palworld-save-tools", "convert.py"),
            Path.Combine(paths.ServerRoot, "Tools", "PalworldSaveTools", "convert.py"));
        if (scriptPath is null)
            return NotInstalled("Save & Runtime Dependencies", "Palworld Save Tools", "PyPI: palworld-save-tools", now,
                "Required to decode Level.sav for Players, Guilds, Bases, and Inspector data.");

        var python = FindOnPath(OperatingSystem.IsWindows() ? ["python.exe", "py.exe"] : ["python3", "python"]);
        string? installed = null;
        if (python is not null)
        {
            var (_, stdout, _) = await RunProcessAsync(python, "-m pip show palworld-save-tools", ct);
            var match = Regex.Match(stdout ?? string.Empty, @"(?im)^Version:\s*(\S+)");
            if (match.Success) installed = match.Groups[1].Value;
        }
        installed ??= "Installed (version metadata unavailable)";

        var latest = await TryGetJsonAsync<PyPiPackageDto>("https://pypi.org/pypi/palworld-save-tools/json", ct);
        if (latest?.Info?.Version is not { Length: > 0 } latestVersion)
            return Unavailable("Save & Runtime Dependencies", "Palworld Save Tools", installed, "PyPI: palworld-save-tools", now,
                "Could not reach PyPI to check the latest palworld-save-tools release.");

        if (installed.StartsWith("Installed ("))
            return new ComponentVersionInfo("Save & Runtime Dependencies", "Palworld Save Tools", installed, latestVersion, "Unknown",
                "PyPI: palworld-save-tools", now,
                "Installed copy has no pip package metadata (likely a vendored/manual install), so its version could not be read to compare.");

        return Compare("Save & Runtime Dependencies", "Palworld Save Tools", installed, latestVersion, "PyPI: palworld-save-tools", now,
            $"PyPI's current palworld-save-tools release is {latestVersion}.");
    }

    private ComponentVersionInfo CheckPlmOodle(DateTimeOffset now)
    {
        var plm = FindFirstExisting(
            Path.Combine(paths.ServerRoot, "Tools", "palworld-plm-tools", "convert.py"),
            Path.Combine(paths.ServerRoot, "Tools", "palworld-plm-tools", ".myst-install.json"));
        // No single canonical upstream project exists for this component -- confirmed by research
        // before writing this check. This project's own Environment Checklist already discloses the
        // same real gap ("no safe current management API") rather than fabricating one.
        return new ComponentVersionInfo("Save & Runtime Dependencies", "PlM/Oodle Decoder",
            plm is not null ? "Detected (version unavailable)" : "Not detected", "Unavailable",
            plm is not null ? "Unknown" : "NotInstalled", "No canonical upstream source", now,
            "Required for newer PlM Level.sav containers. No single canonical upstream project exists for this component, so there is no reliable version or update source to check against.");
    }

    private async Task<ComponentVersionInfo> CheckDotNetAsync(DateTimeOffset now, CancellationToken ct)
    {
        var (_, stdout, _) = await RunProcessAsync("dotnet", "--list-runtimes", ct);
        var installed = Regex.Matches(stdout ?? string.Empty, @"Microsoft\.NETCore\.App\s+(\S+)")
            .Select(m => m.Groups[1].Value)
            .Select(v => Version.TryParse(v, out var parsed) ? parsed : null)
            .Where(v => v is not null)
            .Max();
        if (installed is null)
            return NotInstalled("Save & Runtime Dependencies", ".NET Runtime", "dotnet/core releases-index.json", now,
                "\"dotnet --list-runtimes\" did not return a Microsoft.NETCore.App runtime.");

        var index = await TryGetJsonAsync<DotNetReleasesIndexDto>("https://raw.githubusercontent.com/dotnet/core/main/release-notes/releases-index.json", ct);
        var latestChannel = index?.ReleasesIndex?
            .Where(r => Version.TryParse(r.LatestRuntime, out _))
            .Select(r => Version.Parse(r.LatestRuntime!))
            .Max();
        if (latestChannel is null)
            return Unavailable("Save & Runtime Dependencies", ".NET Runtime", installed.ToString(), "dotnet/core releases-index.json", now,
                "Could not reach the .NET release-notes feed to check the latest runtime.");

        return Compare("Save & Runtime Dependencies", ".NET Runtime", installed.ToString(), latestChannel.ToString(), "dotnet/core releases-index.json", now,
            $"Latest supported .NET runtime channel is {latestChannel}.");
    }

    private ComponentVersionInfo CheckVcRuntime(DateTimeOffset now)
    {
        if (!OperatingSystem.IsWindows())
            return new ComponentVersionInfo("Save & Runtime Dependencies", "Visual C++ Runtime", "N/A (Linux)", "N/A", "NotApplicable", "N/A", now,
                "Not applicable on Linux.");

        var version = ReadVcRuntimeRegistryVersion();
        return new ComponentVersionInfo("Save & Runtime Dependencies", "Visual C++ Runtime",
            version ?? "Not detected", "Unavailable", version is not null ? "Unknown" : "NotInstalled",
            "Windows Registry (no reliable \"latest\" feed)", now,
            "Microsoft does not publish a simple, reliable unattended feed for the latest VC++ Redistributable version number -- this row shows what's installed only.");
    }

    private static string? ReadVcRuntimeRegistryVersion()
    {
        if (!OperatingSystem.IsWindows()) return null;
        foreach (var keyPath in new[]
        {
            @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\X64",
            @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64",
            @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\X86",
        })
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                if (key?.GetValue("Version") is string value && !string.IsNullOrWhiteSpace(value))
                    return value.TrimStart('v', 'V');
            }
            catch { }
        }
        return null;
    }

    private ComponentVersionInfo CheckCppBuildTools(DateTimeOffset now)
    {
        var cl = DetectCppToolchainExecutable();
        if (cl is null)
            return NotInstalled("Save & Runtime Dependencies", "Microsoft C++ Build Tools",
                "Windows Registry (no reliable \"latest\" feed)", now,
                "Required for native Python dependencies such as pyooz.");

        string? version = null;
        try { version = FileVersionInfo.GetVersionInfo(cl).FileVersion; } catch { }
        return new ComponentVersionInfo("Save & Runtime Dependencies", "Microsoft C++ Build Tools",
            version ?? "Detected (version unavailable)", "Unavailable", "Unknown",
            "Windows Registry (no reliable \"latest\" feed)", now,
            "Microsoft does not publish a simple, reliable unattended feed for the latest Build Tools version -- this row shows what's installed only.");
    }

    private static string? DetectCppToolchainExecutable()
    {
        if (OperatingSystem.IsWindows())
        {
            var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) };
            foreach (var root in roots.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var vs = Path.Combine(root, "Microsoft Visual Studio", "2022");
                if (!Directory.Exists(vs)) continue;
                try
                {
                    var cl = Directory.EnumerateFiles(vs, "cl.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (cl is not null) return cl;
                }
                catch { }
            }
            return FindOnPath(new[] { "cl.exe" });
        }
        return FindOnPath(new[] { "g++", "gcc", "clang++" });
    }

    // ------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------

    // Public so the logic harness can test the wording.
    public static ComponentVersionInfo Compare(string group, string component, string installed, string latest, string source, DateTimeOffset now, string detail, bool updateIsInformationalOnly = false)
    {
        var status = "Unknown";
        if (Version.TryParse(NormalizeForVersionParse(installed), out var installedVersion) &&
            Version.TryParse(NormalizeForVersionParse(latest), out var latestVersion))
        {
            status = installedVersion >= latestVersion ? "UpToDate" : "UpdateAvailable";
            // v0.7.102.0: "Up to date" alone hid that this install is AHEAD of the latest published release
            // (0.7.x running while the newest public release is 0.2.16.4), which reads as a mistake.
            if (installedVersion > latestVersion)
                detail += $" This install ({installed}) is newer than the latest published release ({latest}), so it is a local or unreleased build; there is nothing newer to update to.";
        }
        else if (string.Equals(installed, latest, StringComparison.OrdinalIgnoreCase))
        {
            status = "UpToDate";
        }
        else
        {
            status = "UpdateAvailable";
            detail += " (version strings could not be numerically compared; ordering inferred from inequality.)";
        }
        if (status == "UpdateAvailable" && updateIsInformationalOnly)
            detail += " No automatic self-update is performed -- this is informational only.";
        return new ComponentVersionInfo(group, component, installed, latest, status, source, now, detail);
    }

    private static string NormalizeForVersionParse(string value)
    {
        var match = Regex.Match(value, @"\d+(\.\d+){1,3}");
        return match.Success ? match.Value : value;
    }

    private static ComponentVersionInfo NotInstalled(string group, string component, string source, DateTimeOffset now, string detail) =>
        new(group, component, "Not detected", "Unavailable", "NotInstalled", source, now, detail);

    private static ComponentVersionInfo Unavailable(string group, string component, string installed, string source, DateTimeOffset now, string detail) =>
        new(group, component, installed, "Unavailable", "Unknown", source, now, detail);

    private static string? ExtractVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = Regex.Match(text, @"\d+\.\d+(\.\d+)?");
        return match.Success ? match.Value : null;
    }

    private static string? TryReadAcfBuildId(string manifestPath)
    {
        try
        {
            if (!File.Exists(manifestPath)) return null;
            var text = File.ReadAllText(manifestPath);
            var match = Regex.Match(text, "\"buildid\"\\s*\"(?<v>\\d+)\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["v"].Value : null;
        }
        catch { return null; }
    }

    private static string? FindOnPath(IEnumerable<string> names)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            foreach (var name in names)
            {
                try { var full = Path.Combine(dir, name); if (File.Exists(full)) return full; } catch { }
            }
        return null;
    }

    private static string? FindFirstExisting(params string[] candidates) => candidates.FirstOrDefault(File.Exists);

    // v0.7.82.0: gained an optional timeout override for UpdatePipAsync below -- the fixed 8-second
    // ProcessTimeout every version check uses is right for a "--version" query, but a real "pip
    // install --upgrade pip" downloads a package and needs real room to finish. Every existing
    // caller keeps passing no timeout, so behavior is unchanged for them.
    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            if (!process.Start()) return (-1, string.Empty, string.Empty);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout ?? ProcessTimeout);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            return (process.ExitCode, stdout, stderr);
        }
        catch { return (-1, string.Empty, string.Empty); }
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

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static HttpClient BuildHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MystTiq-Palworld-Server-Manager");
        return client;
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; init; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; init; }
        [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; init; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; init; }
        [JsonPropertyName("draft")] public bool Draft { get; init; }
        [JsonPropertyName("assets")] public List<GitHubAssetDto>? Assets { get; init; }
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("id")] public long Id { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("size")] public long Size { get; init; }
        [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
        [JsonPropertyName("browser_download_url")] public string? DownloadUrl { get; init; }
    }

    private sealed class PyPiPackageDto
    {
        [JsonPropertyName("info")] public PyPiInfoDto? Info { get; init; }
    }

    private sealed class PyPiInfoDto
    {
        [JsonPropertyName("version")] public string? Version { get; init; }
    }

    private sealed class DotNetReleasesIndexDto
    {
        [JsonPropertyName("releases-index")] public List<DotNetReleaseChannelDto>? ReleasesIndex { get; init; }
    }

    private sealed class DotNetReleaseChannelDto
    {
        [JsonPropertyName("latest-runtime")] public string? LatestRuntime { get; init; }
        [JsonPropertyName("support-phase")] public string? SupportPhase { get; init; }
    }
}

// v0.9.9.0: pure, so the logic harness covers it: the SHA-256 of every UE4SS.dll inside a release download, and which
// release (newest first) holds a file identical to the installed one.
public static class Ue4ssReleaseFiles
{
    public const long MaximumDownloadBytes = 200L * 1024 * 1024;

    public static IReadOnlyList<string> DllHashes(Stream zip)
    {
        using var archive = new System.IO.Compression.ZipArchive(zip, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true);
        var hashes = new List<string>();
        foreach (var entry in archive.Entries.Where(e => e.Name.Equals("UE4SS.dll", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = entry.Open();
            hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)));
        }
        return hashes;
    }

    public static (string Tag, string Asset)? Match(string installedHash, IEnumerable<(string Tag, string Asset, IReadOnlyList<string> Hashes)> releases)
    {
        foreach (var (tag, asset, hashes) in releases)
            if (hashes.Any(h => h.Equals(installedHash, StringComparison.OrdinalIgnoreCase))) return (tag, asset);
        return null;
    }
}

public sealed record ComponentVersionInfo(
    string Group,
    string Component,
    string InstalledVersion,
    string LatestVersion,
    string Status,
    string Source,
    DateTimeOffset LastChecked,
    string Detail);

public sealed record ComponentVersionSnapshot(IReadOnlyList<ComponentVersionInfo> Components, DateTimeOffset ObservedAt);

// v0.7.82.0: result of an actual in-place component update (currently only pip) -- distinct from
// ComponentVersionInfo, which is read-only comparison data.
public sealed record ComponentUpdateResult(bool Success, string Message);

public sealed record ReleaseCandidate(string Tag, bool Prerelease, bool Draft, string? Url);

// v0.9.10.0: which published MystTiq release counts as "latest". A development version (0.x) considers prereleases too,
// since that is how 0.x releases are published; from 1.0 on only stable releases count. Drafts never do. The newest is
// chosen by version number, not by publish date or list order.
public static class ReleaseChannel
{
    public static bool IncludesPrereleases(string installed) =>
        !Version.TryParse(installed, out var v) || v.Major < 1;

    public static string Describe(string installed) =>
        IncludesPrereleases(installed) ? "development versions count prereleases" : "stable releases only";

    public static ReleaseCandidate? PickNewest(IEnumerable<ReleaseCandidate> releases, string installed)
    {
        var prereleases = IncludesPrereleases(installed);
        return releases
            .Where(r => !r.Draft && (prereleases || !r.Prerelease))
            .Select(r => (Release: r, Version: Version.TryParse(r.Tag.TrimStart('v', 'V'), out var v) ? v : null))
            .Where(x => x.Version is not null)
            .OrderByDescending(x => x.Version)
            .Select(x => x.Release)
            .FirstOrDefault();
    }
}
