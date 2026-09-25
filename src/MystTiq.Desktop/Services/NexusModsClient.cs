using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MystTiq.Core.Services;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

// v0.7.93.0: talks to the Nexus Mods public API (v1) directly from the desktop app, never through the
// MystTiq management server: the user's own API key is theirs to use from their own machine, and
// Nexus's API policy says a key must not be stored or used server-side on a user's behalf. Requests
// carry the Application-Name/Application-Version headers the policy requires.
//
// What the v1 API can and cannot do (verified against its published spec): it lists trending / latest
// added / latest updated mods (10 each) and looks up a mod by id, but has no catalog search; and a
// direct download link is only issued to Premium accounts, unless the caller supplies the one-time
// key and expiry from an nxm:// link. This client exposes exactly that and nothing more.
public interface INexusModsClient
{
    Task<NexusCallResult<NexusUserDto>> ValidateAsync(string apiKey, CancellationToken cancellationToken = default);
    Task<NexusCallResult<IReadOnlyList<NexusModDto>>> GetListAsync(string apiKey, string kind, CancellationToken cancellationToken = default);
    Task<NexusCallResult<NexusModDto>> GetModAsync(string apiKey, int modId, CancellationToken cancellationToken = default);
    Task<NexusCallResult<IReadOnlyList<NexusFileDto>>> GetFilesAsync(string apiKey, int modId, CancellationToken cancellationToken = default);
    Task<NexusCallResult<Uri>> GetDownloadLinkAsync(string apiKey, int modId, int fileId, string? key, long? expires, CancellationToken cancellationToken = default);
    Task<string?> DownloadToFileAsync(Uri uri, string destinationPath, long maxBytes, IProgress<string>? progress, CancellationToken cancellationToken = default);
}

public sealed class NexusModsClient : INexusModsClient
{
    public const string ApplicationName = "MystTiq-Palworld-Server-Manager";
    private const string ApiBase = "https://api.nexusmods.com";
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var version = typeof(NexusModsClient).Assembly.GetName().Version?.ToString(3) ?? "0";
        client.DefaultRequestHeaders.TryAddWithoutValidation("Application-Name", ApplicationName);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Application-Version", version);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ApplicationName, version));
        return client;
    }

    public Task<NexusCallResult<NexusUserDto>> ValidateAsync(string apiKey, CancellationToken cancellationToken = default) =>
        GetAsync<NexusUserDto>(apiKey, "/v1/users/validate.json", cancellationToken);

    public async Task<NexusCallResult<IReadOnlyList<NexusModDto>>> GetListAsync(string apiKey, string kind, CancellationToken cancellationToken = default)
    {
        if (kind is not ("trending" or "latest_added" or "latest_updated"))
            return new(null, "Unknown list.", null, null);
        var result = await GetAsync<List<NexusModDto>>(apiKey, $"/v1/games/{NexusModsLinks.PalworldGameDomain}/mods/{kind}.json", cancellationToken);
        return new(result.Value, result.Error, result.HourlyRemaining, result.DailyRemaining);
    }

    public Task<NexusCallResult<NexusModDto>> GetModAsync(string apiKey, int modId, CancellationToken cancellationToken = default) =>
        GetAsync<NexusModDto>(apiKey, $"/v1/games/{NexusModsLinks.PalworldGameDomain}/mods/{modId}.json", cancellationToken);

    public async Task<NexusCallResult<IReadOnlyList<NexusFileDto>>> GetFilesAsync(string apiKey, int modId, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<NexusFilesResponseDto>(apiKey, $"/v1/games/{NexusModsLinks.PalworldGameDomain}/mods/{modId}/files.json", cancellationToken);
        return new(result.Value?.Files, result.Error, result.HourlyRemaining, result.DailyRemaining);
    }

    public async Task<NexusCallResult<Uri>> GetDownloadLinkAsync(string apiKey, int modId, int fileId, string? key, long? expires, CancellationToken cancellationToken = default)
    {
        var path = $"/v1/games/{NexusModsLinks.PalworldGameDomain}/mods/{modId}/files/{fileId}/download_link.json";
        if (!string.IsNullOrWhiteSpace(key) && expires is > 0)
            path += $"?key={Uri.EscapeDataString(key)}&expires={expires}";
        var result = await GetAsync<List<NexusDownloadLinkDto>>(apiKey, path, cancellationToken);
        if (!result.Ok) return new(null, result.Error, result.HourlyRemaining, result.DailyRemaining);
        var first = result.Value?.FirstOrDefault(l => Uri.TryCreate(l.Uri, UriKind.Absolute, out var u) && NexusModsLinks.IsAllowedDownloadHost(u));
        return first is null
            ? new(null, "Nexus returned no usable download location.", result.HourlyRemaining, result.DailyRemaining)
            : new(new Uri(first.Uri!), null, result.HourlyRemaining, result.DailyRemaining);
    }

    // The CDN link is a pre-authorized URL, so no API key is sent to it. Streams to disk with a hard
    // size cap; returns an error string, or null on success.
    public async Task<string?> DownloadToFileAsync(Uri uri, string destinationPath, long maxBytes, IProgress<string>? progress, CancellationToken cancellationToken = default)
    {
        if (!NexusModsLinks.IsAllowedDownloadHost(uri))
            return "Refusing to download from a host that is not a Nexus Mods domain.";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return $"The download server answered {(int)response.StatusCode}.";
            var length = response.Content.Headers.ContentLength;
            if (length > maxBytes) return $"The file is {length / 1024 / 1024} MB, over the {maxBytes / 1024 / 1024} MB limit.";

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = File.Create(destinationPath);
            var buffer = new byte[81920];
            long total = 0;
            int read;
            var lastReport = DateTime.UtcNow;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > maxBytes) return $"The download exceeded the {maxBytes / 1024 / 1024} MB limit and was stopped.";
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                if ((DateTime.UtcNow - lastReport).TotalMilliseconds > 400)
                {
                    lastReport = DateTime.UtcNow;
                    progress?.Report(length is > 0 ? $"Downloading… {total / 1024 / 1024} of {length / 1024 / 1024} MB" : $"Downloading… {total / 1024 / 1024} MB");
                }
            }

            return null;
        }
        catch (OperationCanceledException) { return "Download cancelled."; }
        catch (Exception ex) { return $"Download failed: {ex.Message}"; }
    }

    private static async Task<NexusCallResult<T>> GetAsync<T>(string apiKey, string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return new(default, "Enter your Nexus Mods API key first.", null, null);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ApiBase + path);
            request.Headers.TryAddWithoutValidation("apikey", apiKey.Trim());
            using var response = await Http.SendAsync(request, cancellationToken);
            var hourly = ReadHeaderInt(response, "x-rl-hourly-remaining");
            var daily = ReadHeaderInt(response, "x-rl-daily-remaining");
            if (!response.IsSuccessStatusCode)
                return new(default, DescribeFailure(response.StatusCode), hourly, daily);

            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return value is null
                ? new(default, "Nexus returned an empty response.", hourly, daily)
                : new(value, null, hourly, daily);
        }
        catch (OperationCanceledException) { return new(default, "Cancelled.", null, null); }
        catch (Exception ex) { return new(default, $"Could not reach Nexus Mods: {ex.Message}", null, null); }
    }

    private static int? ReadHeaderInt(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), out var parsed) ? parsed : null;

    private static string DescribeFailure(HttpStatusCode status) => (int)status switch
    {
        401 => "Nexus rejected the API key. Check it and try again.",
        403 => "Nexus only allows direct downloads for Premium accounts. Use Open on Nexus, or paste the nxm:// link from the Mod Manager Download button.",
        404 => "Nexus could not find that (wrong id, or not a Palworld mod).",
        410 => "That nxm:// link has expired. Click Mod Manager Download on Nexus again for a fresh one.",
        429 => "Nexus rate limit reached. Wait a while before trying again.",
        _ => $"Nexus answered {(int)status}."
    };
}
