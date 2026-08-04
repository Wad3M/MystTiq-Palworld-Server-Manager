using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class ApiClient : IDisposable
{
    private readonly HttpClient client;

    public ApiClient(AppSettings settings)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };

        client = new HttpClient(handler)
        {
            BaseAddress = new Uri(settings.ApiBaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(20),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        var raw = $"{settings.ApiUser}:{settings.GetPassword()}";
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
    }

    public async Task<JsonDocument> GetAsync(
        string endpoint,
        CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        request.Headers.ConnectionClose = true;

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            token);

        var text = await response.Content.ReadAsStringAsync(token);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"{(int)response.StatusCode} {response.ReasonPhrase}: {text}");
        }

        return JsonDocument.Parse(
            string.IsNullOrWhiteSpace(text) ? "{}" : text);
    }

    public Task SaveAsync(CancellationToken token = default) =>
        PostAsync("save", null, token);

    public Task AnnounceAsync(
        string message,
        CancellationToken token = default) =>
        PostAsync("announce", new { message }, token);

    public async Task ShutdownAsync(
        int seconds,
        string message,
        CancellationToken token = default)
    {
        try
        {
            await PostAsync(
                "shutdown",
                new
                {
                    waittime = seconds,
                    message
                },
                token);
        }
        catch (HttpRequestException exception)
            when (IsConnectionClosedDuringShutdown(exception))
        {
            // Some Palworld builds close the REST connection immediately after
            // accepting shutdown, before .NET can parse a complete HTTP response.
            // The caller verifies process exit, so this is safe to treat as accepted.
        }
        catch (IOException exception)
            when (IsConnectionClosedDuringShutdown(exception))
        {
            // Same behavior as above, surfaced through an IOException.
        }
    }

    public Task KickAsync(
        string id,
        string message,
        CancellationToken token = default) =>
        PostAsync("kick", new { userid = id, message }, token);

    public Task BanAsync(
        string id,
        string message,
        CancellationToken token = default) =>
        PostAsync("ban", new { userid = id, message }, token);

    private async Task PostAsync(
        string endpoint,
        object? body,
        CancellationToken token)
    {
        byte[] payload = body is null
            ? Array.Empty<byte>()
            : JsonSerializer.SerializeToUtf8Bytes(body);

        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        // ByteArrayContent always supplies a concrete Content-Length, avoiding
        // chunked request framing that Palworld's REST server may reject with 411.
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = content
        };

        request.Headers.ConnectionClose = true;

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            token);

        var text = await response.Content.ReadAsStringAsync(token);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"{(int)response.StatusCode} {response.ReasonPhrase}: {text}");
        }
    }

    private static bool IsConnectionClosedDuringShutdown(Exception exception)
    {
        var message = exception.ToString();

        return message.Contains("invalid status line", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("response ended prematurely", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("connection reset", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("unexpected end", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => client.Dispose();
}
