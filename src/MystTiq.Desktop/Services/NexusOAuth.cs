// MystTiq v1.1.6.0: file reviewed for this release (2026-10-26).
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MystTiq.Desktop.Services;

/// <summary>The tokens Nexus Mods issued to this app for the signed-in person.</summary>
public sealed record NexusTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);

/// <summary>Who is signed in, read from the verified access token.</summary>
public sealed record NexusAccount(long UserId, string Name, bool IsPremium);

/// <summary>The end of a sign-in: the account, or why it did not happen (a message for the person).</summary>
public sealed record NexusSignInResult(NexusAccount? Account, string? Error)
{
    public bool Ok => Account is not null;
}

/// <summary>
/// v1.1.6.0 (Nexus Mods support, 2026-10-26: "your mod uses Personal API keys. Which is not allowed under our upload
/// guidelines. Please update your mod to use OAUTH instead"): signing in to Nexus Mods with OAuth 2.0 and PKCE
/// (https://modding.wiki/en/api/oauth2-guide), as a public desktop app. The browser opens Nexus's own sign-in page; the
/// person approves MystTiq there; Nexus sends a one-time code back to a listener on this PC (127.0.0.1 only, the
/// registered redirect address); the code and the PKCE verifier are exchanged for tokens; the access token's RS256
/// signature is checked with Nexus's published public key. The tokens are kept like the old key was (encrypted for this
/// Windows account; in memory elsewhere), refreshed before they run out, and never sent to the MystTiq server. A personal
/// API key saved by an earlier version is deleted.
/// </summary>
public sealed class NexusOAuth
{
    public const string AuthorizeUrl = "https://users.nexusmods.com/oauth/authorize";
    public const string TokenUrl = "https://users.nexusmods.com/oauth/token";
    /// <summary>The callback port and path registered with Nexus Mods for this app.</summary>
    public const int CallbackPort = 47823;
    public const string CallbackPath = "/nexus/callback";
    public static readonly string RedirectUri = $"http://127.0.0.1:{CallbackPort}{CallbackPath}";
    public const string CredentialId = "nexusmods-oauth";
    /// <summary>Where versions before v1.1.6.0 kept a personal API key.</summary>
    public const string LegacyKeyCredentialId = "nexusmods-api-key";
    public static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Nexus Mods' public key for its access tokens (from the OAuth guide).</summary>
    public const string NexusPublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAs/57oX8HW8xC+W/etH7J
        PgoSTiGPKZa6Gq3/K/7GgrpJcZhPdr9MTGocb2uLzQBJW+u1XpSgyeKH4JCxxeHF
        3zcUtb7SUg3KnxlR5QUmOnqBvbUuL4opUpfgWUGltASduYqZBJD2WTK8Hvwh9X1v
        ACeqp1zgorZm3f0J2H15TDbzIp9ihCFuthJUFumdzvrt/WvimW2fiyqndTNQwe5h
        XM8hj8cemdWQXCd99qnj7UQkpu+yNisVMHQCsAqXITe6Ehp6IY9eCd4DJKjDvyLc
        3vbY8UL+bcVK5tYAKemZ56uw3q1YdcyqGlItyLi4j4EISdBQaCCqT7YZUhMYzhUd
        1QIDAQAB
        -----END PUBLIC KEY-----
        """;

    private readonly CredentialStore? store;
    private readonly HttpClient http;
    private readonly string publicKeyPem;
    private readonly Func<string, bool> openBrowser;
    private readonly int port;
    private readonly SemaphoreSlim gate = new(1, 1);
    private NexusTokens? tokens;

    public NexusOAuth(CredentialStore? store, string? clientId = null, HttpMessageHandler? handler = null, string? publicKeyPem = null,
        Func<string, bool>? openBrowser = null, int? port = null)
    {
        this.store = store;
        ClientId = clientId ?? BuiltInClientId;
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromSeconds(30);
        http.DefaultRequestHeaders.TryAddWithoutValidation("Application-Name", NexusModsClient.ApplicationName);
        this.publicKeyPem = publicKeyPem ?? NexusPublicKeyPem;
        this.openBrowser = openBrowser ?? OpenInBrowser;
        this.port = port ?? CallbackPort;
        // Versions before v1.1.6.0 kept a personal API key; Nexus no longer allows them, so it is removed.
        LegacyKeyRemoved = store?.TryLoad(LegacyKeyCredentialId) is { Length: > 0 };
        if (LegacyKeyRemoved) store!.Delete(LegacyKeyCredentialId);
        tokens = Deserialize(store?.TryLoad(CredentialId));
        Account = tokens is null ? null : ReadAccount(tokens.AccessToken, this.publicKeyPem, ignoreExpiry: true);
        if (Account is null) tokens = null;
    }

    /// <summary>The Client ID Nexus Mods issued for MystTiq (Directory.Build.props, NexusOAuthClientId).</summary>
    public static string BuiltInClientId =>
        typeof(NexusOAuth).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "NexusOAuthClientId")?.Value?.Trim() ?? string.Empty;

    public string ClientId { get; }
    public bool IsConfigured => ClientId.Length > 0;
    public NexusAccount? Account { get; private set; }
    public bool IsSignedIn => tokens is not null && Account is not null;
    /// <summary>True when this start found and deleted a personal API key saved by an earlier version.</summary>
    public bool LegacyKeyRemoved { get; }

    // ---- PKCE (RFC 7636) ------------------------------------------------------------------------------------------------

    public static string NewVerifier() => Base64Url(RandomNumberGenerator.GetBytes(48)); // 64 characters
    public static string ChallengeFor(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public string BuildAuthorizeUrl(string state, string challenge) =>
        AuthorizeUrl + "?" + string.Join("&", new (string Key, string Value)[]
        {
            ("client_id", ClientId), ("response_type", "code"), ("scope", string.Empty), ("redirect_uri", RedirectUriFor(port)),
            ("state", state), ("code_challenge_method", "S256"), ("code_challenge", challenge),
        }.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));

    private static string RedirectUriFor(int port) => $"http://127.0.0.1:{port}{CallbackPath}";

    // ---- Signing in ------------------------------------------------------------------------------------------------------

    /// <summary>Opens Nexus's sign-in page and waits (up to five minutes) for the person to approve MystTiq there.</summary>
    public async Task<NexusSignInResult> SignInAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return new(null, Localizer.Instance["nexus.notConfigured"]);
        if (!await gate.WaitAsync(0, cancellationToken)) return new(null, Localizer.Instance["nexus.signInRunning"]);
        TcpListener? listener = null;
        try
        {
            var verifier = NewVerifier();
            var state = Base64Url(RandomNumberGenerator.GetBytes(24));
            try
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
            }
            catch (SocketException) { return new(null, string.Format(Localizer.Instance["nexus.portBusy"], port)); }

            if (!openBrowser(BuildAuthorizeUrl(state, ChallengeFor(verifier))))
                return new(null, Localizer.Instance["nexus.browserFailed"]);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(SignInTimeout);
            string code;
            try { code = await WaitForCodeAsync(listener, state, timeout.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(null, Localizer.Instance["nexus.timedOut"]); }
            catch (NexusCallbackException ex) { return new(null, ex.Message); }
            listener.Stop();
            listener = null;

            var issued = await RequestTokensAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code", ["redirect_uri"] = RedirectUriFor(port), ["scope"] = string.Empty,
                ["client_id"] = ClientId, ["code"] = code, ["code_verifier"] = verifier,
            }, previousRefresh: null, cancellationToken);
            if (issued.Error is not null) return new(null, issued.Error);
            return new(Account, null);
        }
        finally
        {
            listener?.Stop();
            gate.Release();
        }
    }

    /// <summary>A current access token, refreshed first when it ends within a minute; null when nobody is signed in or Nexus
    /// no longer accepts the refresh (access revoked): then the person is signed out.</summary>
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var current = tokens;
        if (current is null) return null;
        if (current.ExpiresAtUtc - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(1)) return current.AccessToken;
        await gate.WaitAsync(cancellationToken);
        try
        {
            current = tokens;
            if (current is null) return null;
            if (current.ExpiresAtUtc - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(1)) return current.AccessToken;
            var refreshed = await RequestTokensAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token", ["client_id"] = ClientId, ["refresh_token"] = current.RefreshToken,
            }, current.RefreshToken, cancellationToken);
            if (refreshed.Rejected) { SignOut(); return null; }
            return refreshed.Error is null ? tokens?.AccessToken : null;
        }
        finally { gate.Release(); }
    }

    /// <summary>Forgets the tokens on this PC. (Access is fully withdrawn on Nexus Mods' site, under the account's apps.)</summary>
    public void SignOut()
    {
        tokens = null;
        Account = null;
        store?.Delete(CredentialId);
    }

    private async Task<(string? Error, bool Rejected)> RequestTokensAsync(Dictionary<string, string> form, string? previousRefresh, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try { response = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(form), cancellationToken); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return (string.Format(Localizer.Instance["nexus.unreachable"], ex.Message), false); }
        using (response)
        {
            if ((int)response.StatusCode is >= 400 and < 500) return (Localizer.Instance["nexus.refused"], true);
            if (!response.IsSuccessStatusCode) return (string.Format(Localizer.Instance["nexus.answered"], (int)response.StatusCode), false);
            var parsed = ParseTokens(await response.Content.ReadAsStringAsync(cancellationToken), previousRefresh, DateTimeOffset.UtcNow);
            var account = parsed is null ? null : ReadAccount(parsed.AccessToken, publicKeyPem);
            if (parsed is null || account is null) return (Localizer.Instance["nexus.unverified"], false);
            tokens = parsed;
            Account = account;
            if (store is not null && OperatingSystem.IsWindows()) store.Save(CredentialId, Serialize(parsed));
            return (null, false);
        }
    }

    // ---- The callback listener -------------------------------------------------------------------------------------------

    private sealed class NexusCallbackException(string message) : Exception(message);

    private static async Task<string> WaitForCodeAsync(TcpListener listener, string expectedState, CancellationToken cancellationToken)
    {
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken))) { }
            var outcome = ReadCallback(requestLine, expectedState);
            if (outcome.NotCallback)
            {
                await RespondAsync(stream, 404, "Not found.", cancellationToken);
                continue;
            }
            await RespondAsync(stream, outcome.Code is null ? 400 : 200,
                outcome.Code is null ? Localizer.Instance["nexus.page.failed"] : Localizer.Instance["nexus.page.done"], cancellationToken);
            return outcome.Code ?? throw new NexusCallbackException(outcome.Error!);
        }
    }

    /// <summary>Reads the callback's request line: the code when the state matches, or why not.</summary>
    public static (string? Code, string? Error, bool NotCallback) ReadCallback(string requestLine, string expectedState)
    {
        var parts = requestLine.Split(' ');
        if (parts.Length < 2 || parts[0] != "GET") return (null, null, true);
        var target = parts[1];
        var question = target.IndexOf('?');
        var path = question < 0 ? target : target[..question];
        if (!string.Equals(path, CallbackPath, StringComparison.Ordinal)) return (null, null, true);
        var query = (question < 0 ? string.Empty : target[(question + 1)..]).Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .GroupBy(pair => Uri.UnescapeDataString(pair[0]))
            .ToDictionary(g => g.Key, g => Uri.UnescapeDataString(g.First().Length > 1 ? g.First()[1].Replace('+', ' ') : string.Empty));
        if (!query.TryGetValue("state", out var state) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(expectedState)))
            return (null, Localizer.Instance["nexus.stateMismatch"], false);
        if (query.TryGetValue("error", out var error))
            return (null, string.Format(Localizer.Instance["nexus.denied"], query.TryGetValue("error_description", out var description) ? description : error), false);
        return query.TryGetValue("code", out var code) && code.Length > 0 ? (code, null, false) : (null, Localizer.Instance["nexus.noCode"], false);
    }

    private static async Task RespondAsync(Stream stream, int status, string message, CancellationToken cancellationToken)
    {
        var body = $"<!doctype html><html><head><meta charset=\"utf-8\"><title>MystTiq</title></head><body style=\"font-family:sans-serif;background:#10141c;color:#e8ecf4;padding:40px\"><h2>MystTiq</h2><p>{WebUtility.HtmlEncode(message)}</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(body);
        var head = $"HTTP/1.1 {status} {(status == 200 ? "OK" : status == 404 ? "Not Found" : "Bad Request")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static bool OpenInBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); return true; }
        catch { return false; }
    }

    // ---- Tokens ------------------------------------------------------------------------------------------------------------

    /// <summary>Reads Nexus's token answer; a refresh that omits the refresh token keeps the previous one.</summary>
    public static NexusTokens? ParseTokens(string json, string? previousRefresh, DateTimeOffset now)
    {
        try
        {
            var node = JsonNode.Parse(json);
            var access = node?["access_token"]?.GetValue<string>();
            var refresh = node?["refresh_token"]?.GetValue<string>() ?? previousRefresh;
            var lifetime = node?["expires_in"]?.GetValue<double>() ?? 0;
            if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh) || lifetime <= 0) return null;
            return new NexusTokens(access, refresh, now.AddSeconds(lifetime));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return null; }
    }

    /// <summary>The account in an access token whose RS256 signature checks out with the public key (and, unless asked not
    /// to, has not expired); null otherwise.</summary>
    public static NexusAccount? ReadAccount(string jwt, string publicKeyPem, bool ignoreExpiry = false)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return null;
            var header = JsonNode.Parse(Base64UrlDecode(parts[0]));
            if (header?["alg"]?.GetValue<string>() != "RS256") return null;
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            if (!rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Base64UrlDecode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return null;
            var payload = JsonNode.Parse(Base64UrlDecode(parts[1]));
            if (!ignoreExpiry && payload?["exp"]?.GetValue<long>() is { } exp && DateTimeOffset.FromUnixTimeSeconds(exp) < DateTimeOffset.UtcNow.AddMinutes(-5))
                return null;
            var user = payload?["user"];
            var name = user?["username"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name)) return null;
            var roles = user?["membership_roles"]?.AsArray().Select(r => r?.GetValue<string>() ?? string.Empty).ToArray() ?? [];
            var premium = roles.Any(r => r.Equals("premium", StringComparison.OrdinalIgnoreCase) || r.Equals("lifetimepremium", StringComparison.OrdinalIgnoreCase));
            return new NexusAccount(user?["id"]?.GetValue<long>() ?? 0, name, premium);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or CryptographicException or ArgumentException) { return null; }
    }

    private static string Serialize(NexusTokens t) => JsonSerializer.Serialize(new { access = t.AccessToken, refresh = t.RefreshToken, expires = t.ExpiresAtUtc.ToUnixTimeSeconds() });

    private static NexusTokens? Deserialize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            var node = JsonNode.Parse(text);
            var access = node?["access"]?.GetValue<string>();
            var refresh = node?["refresh"]?.GetValue<string>();
            var expires = node?["expires"]?.GetValue<long>() ?? 0;
            return string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh) ? null : new NexusTokens(access, refresh, DateTimeOffset.FromUnixTimeSeconds(expires));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return null; }
    }

    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
