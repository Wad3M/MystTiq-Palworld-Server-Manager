using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using PalworldManager.Models;
using PalworldManager.Services.Infrastructure;

namespace PalworldManager.Services;

/// <summary>
/// Checks the public MystTiq GitHub release catalog.
/// Uses GitHub's latest published full-release endpoint: drafts and prereleases
/// are not treated as the normal public update channel.
/// </summary>
public sealed class MystTiqReleaseService
{
    public const string RepositoryOwner = "Wad3M";
    public const string RepositoryName = "MystTiq-Palworld-Server-Manager";
    public const string ReleasesPage =
        "https://github.com/Wad3M/MystTiq-Palworld-Server-Manager/releases";
    public const string LatestReleaseApi =
        "https://api.github.com/repos/Wad3M/MystTiq-Palworld-Server-Manager/releases/latest";

    private static readonly Regex NumericVersionPattern =
        new(@"(?<!\d)(?<v>\d+\.\d+\.\d+(?:\.\d+)?)(?!\d)", RegexOptions.Compiled);

    public async Task<MystTiqReleaseCheckResult> CheckLatestAsync(
        string? installedVersion = null,
        CancellationToken cancellationToken = default)
    {
        installedVersion ??= ApplicationVersion.Version;

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(ApplicationVersion.UserAgent);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");

        using var response = await client.GetAsync(LatestReleaseApi, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = json.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagProperty)
            ? tagProperty.GetString() ?? string.Empty
            : string.Empty;
        var releaseUrl = root.TryGetProperty("html_url", out var urlProperty)
            ? urlProperty.GetString() ?? ReleasesPage
            : ReleasesPage;

        DateTimeOffset? publishedAt = null;
        if (root.TryGetProperty("published_at", out var publishedProperty) &&
            publishedProperty.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(publishedProperty.GetString(), out var parsedPublished))
        {
            publishedAt = parsedPublished;
        }

        var installed = ParseVersion(installedVersion)
            ?? throw new InvalidOperationException(
                $"Installed MystTiq version '{installedVersion}' could not be parsed.");
        var latest = ParseVersion(tag)
            ?? throw new InvalidOperationException(
                $"GitHub release tag '{tag}' does not contain a supported numeric version.");

        var comparison = installed.CompareTo(latest) switch
        {
            < 0 => MystTiqReleaseComparison.UpdateAvailable,
            0 => MystTiqReleaseComparison.UpToDate,
            > 0 => MystTiqReleaseComparison.NewerThanPublicRelease
        };

        var detail = comparison switch
        {
            MystTiqReleaseComparison.UpdateAvailable =>
                $"A newer public MystTiq release ({FormatVersion(latest)}) is available.",
            MystTiqReleaseComparison.UpToDate =>
                "This MystTiq build matches the latest public GitHub release.",
            MystTiqReleaseComparison.NewerThanPublicRelease =>
                "This MystTiq build is newer than the latest public GitHub release.",
            _ => "MystTiq release comparison is unavailable."
        };

        return new MystTiqReleaseCheckResult(
            FormatVersion(installed),
            FormatVersion(latest),
            tag,
            releaseUrl,
            publishedAt,
            comparison,
            detail);
    }

    public static Version? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = NumericVersionPattern.Match(value);
        if (!match.Success)
            return null;

        return Version.TryParse(match.Groups["v"].Value, out var version)
            ? version
            : null;
    }

    private static string FormatVersion(Version version)
    {
        var components = version.Build >= 0
            ? version.Revision >= 0
                ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
                : $"{version.Major}.{version.Minor}.{version.Build}"
            : $"{version.Major}.{version.Minor}";
        return components;
    }
}
