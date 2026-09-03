using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessPalworldAdminService
{
    private readonly IServerPathProfile paths;
    private readonly HeadlessActivityLogService activity;

    public HeadlessPalworldAdminService(IServerPathProfile paths, HeadlessActivityLogService activity)
    {
        this.paths = paths;
        this.activity = activity;
    }

    public async Task<HeadlessPlayerAdminResult> ExecuteAsync(string action, string playerId, string? message, string? item, CancellationToken token)
    {
        action = (action ?? string.Empty).Trim().ToLowerInvariant();
        playerId = (playerId ?? string.Empty).Trim();
        message = (message ?? string.Empty).Trim();
        item = (item ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(playerId))
            return Fail(action, "A player UserID/SteamID is required.");

        if (action is "whisper" or "promote" or "give-item")
        {
            var reason = action switch
            {
                "whisper" => "Vanilla Palworld REST does not expose a private per-player whisper endpoint. Install/detect a supported administration extension before MystTiq can safely enable this action.",
                "promote" => "Vanilla Palworld does not provide a remote per-player promotion endpoint. In-game admin authentication or a detected permissions mod is required.",
                _ => "Vanilla Palworld does not provide a remote give-item endpoint. MystTiq will not invent a mod-specific command without detecting and validating that provider."
            };
            activity.Record("Warning", "Players", $"Player {action} unavailable", $"{playerId}: {reason}");
            return new HeadlessPlayerAdminResult(false, false, action, playerId, reason);
        }

        if (action is not ("kick" or "ban"))
            return Fail(action, $"Unsupported player administration action '{action}'.");

        var settingsPath = Path.Combine(paths.ConfigRoot, "PalWorldSettings.ini");
        if (!File.Exists(settingsPath))
            return Fail(action, $"PalWorldSettings.ini was not found at {settingsPath}.");

        var text = await File.ReadAllTextAsync(settingsPath, token);
        if (ReadBooleanOption(text, "RESTAPIEnabled") is not true)
            return Fail(action, "Palworld REST API is disabled in PalWorldSettings.ini.");

        var port = ReadIntegerOption(text, "RESTAPIPort") ?? 8212;
        var password = ReadQuotedOption(text, "AdminPassword");
        if (string.IsNullOrWhiteSpace(password))
            return Fail(action, "Palworld AdminPassword is empty; authenticated player administration is unavailable.");

        using var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}/v1/api/"),
            Timeout = TimeSpan.FromSeconds(15),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"admin:{password}")));

        var payload = JsonSerializer.SerializeToUtf8Bytes(new { userid = playerId, message = string.IsNullOrWhiteSpace(message) ? (action == "kick" ? "Removed by administrator." : "Banned by administrator.") : message });
        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, action) { Content = content, Version = HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionExact };
        request.Headers.ConnectionClose = true;
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, token);
        var body = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
        {
            var detail = $"Palworld REST /{action} returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {body}".Trim();
            activity.Record("Error", "Players", $"Player {action} failed", $"{playerId}: {detail}");
            return new HeadlessPlayerAdminResult(false, true, action, playerId, detail);
        }

        var success = $"Player {action} request accepted for {playerId}.";
        activity.Record("Success", "Players", $"Player {action}", success);
        return new HeadlessPlayerAdminResult(true, true, action, playerId, success);
    }

    private HeadlessPlayerAdminResult Fail(string action, string detail)
    {
        activity.Record("Warning", "Players", $"Player {action} blocked", detail);
        return new HeadlessPlayerAdminResult(false, false, action, string.Empty, detail);
    }

    private static bool? ReadBooleanOption(string text, string name)
    {
        var value = ReadOption(text, name);
        return bool.TryParse(value, out var parsed) ? parsed : null;
    }
    private static int? ReadIntegerOption(string text, string name)
    {
        var value = ReadOption(text, name);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }
    private static string ReadQuotedOption(string text, string name) => ReadOption(text, name)?.Trim('"') ?? string.Empty;
    private static string? ReadOption(string text, string name)
    {
        var pattern = "(?:^|,)" + Regex.Escape(name) + "=(\"[^\"]*\"|[^,\r\n)]*)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}

public sealed record HeadlessPlayerAdminResult(bool Success, bool Supported, string Action, string PlayerId, string Message);
