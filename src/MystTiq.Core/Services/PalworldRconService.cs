using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace MystTiq.Core.Services;

public sealed record PalworldRconStatus(
    bool Enabled,
    int Port,
    bool PasswordConfigured,
    string Endpoint,
    string Detail);

public sealed record PalworldRconDoctorResult(
    bool Success,
    PalworldRconStatus Status,
    bool TcpReachable,
    bool Authenticated,
    string Detail,
    IReadOnlyList<string> Checks);

public sealed record PalworldRconCommandResult(
    bool Success,
    string Command,
    string Response,
    string Message,
    DateTimeOffset ExecutedAt);

/// <summary>
/// Server-side Source RCON client for the PalServer instance managed by this headless host.
/// The GUI never receives the AdminPassword. Each request authenticates locally from the
/// active PalWorldSettings.ini, keeping RCON credentials inside the server boundary.
/// </summary>
public sealed class PalworldRconService
{
    private const int DefaultPort = 25575;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private readonly PalworldSettingsConfigurationService configuration;

    public PalworldRconService(PalworldSettingsConfigurationService configuration) =>
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public PalworldRconStatus GetStatus()
    {
        var snapshot = configuration.Load();
        if (!snapshot.Exists)
            return new(false, DefaultPort, false, $"127.0.0.1:{DefaultPort}", snapshot.Detail);

        var enabled = ParseBool(Get(snapshot, "RCONEnabled"));
        var port = ParsePort(Get(snapshot, "RCONPort"), DefaultPort);
        var password = Unquote(Get(snapshot, "AdminPassword"));
        var detail = enabled
            ? password.Length > 0 ? "RCON is enabled and an AdminPassword is configured." : "RCON is enabled but AdminPassword is empty."
            : "RCON is disabled in PalWorldSettings.ini.";
        return new(enabled, port, password.Length > 0, $"127.0.0.1:{port}", detail);
    }

    public async Task<PalworldRconDoctorResult> DoctorAsync(CancellationToken cancellationToken = default)
    {
        var status = GetStatus();
        var checks = new List<string>
        {
            status.Enabled ? "PASS: RCONEnabled=True." : "FAIL: RCONEnabled is not True.",
            status.PasswordConfigured ? "PASS: AdminPassword is configured." : "FAIL: AdminPassword is empty.",
            $"INFO: Server-side RCON endpoint is {status.Endpoint}."
        };
        if (!status.Enabled || !status.PasswordConfigured)
            return new(false, status, false, false, "RCON prerequisites are not satisfied.", checks);

        try
        {
            var response = await ExecuteInternalAsync("Info", cancellationToken);
            checks.Add("PASS: TCP connection and Source RCON authentication succeeded.");
            checks.Add(string.IsNullOrWhiteSpace(response) ? "WARN: Info returned an empty response." : "PASS: Info returned a server response.");
            return new(true, status, true, true, "RCON doctor completed successfully.", checks);
        }
        catch (SocketException ex)
        {
            checks.Add($"FAIL: TCP connection failed: {ex.Message}");
            return new(false, status, false, false, "RCON TCP endpoint is not reachable.", checks);
        }
        catch (UnauthorizedAccessException ex)
        {
            checks.Add($"FAIL: Authentication failed: {ex.Message}");
            return new(false, status, true, false, "RCON authentication failed.", checks);
        }
        catch (Exception ex)
        {
            checks.Add($"FAIL: {ex.Message}");
            return new(false, status, true, false, "RCON doctor failed.", checks);
        }
    }

    public async Task<PalworldRconCommandResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        var normalized = (command ?? string.Empty).Trim();
        if (normalized.StartsWith('/')) normalized = normalized[1..];
        if (normalized.Length == 0)
            return new(false, string.Empty, string.Empty, "RCON command cannot be empty.", DateTimeOffset.UtcNow);
        if (normalized.Length > 2048 || normalized.Contains('\r') || normalized.Contains('\n'))
            return new(false, normalized, string.Empty, "RCON command is too long or contains a line break.", DateTimeOffset.UtcNow);

        var status = GetStatus();
        if (!status.Enabled)
            return new(false, normalized, string.Empty, "RCON is disabled in PalWorldSettings.ini.", DateTimeOffset.UtcNow);
        if (!status.PasswordConfigured)
            return new(false, normalized, string.Empty, "AdminPassword is not configured; RCON authentication cannot proceed.", DateTimeOffset.UtcNow);

        try
        {
            var response = await ExecuteInternalAsync(normalized, cancellationToken);
            return new(true, normalized, response, string.IsNullOrWhiteSpace(response) ? "Command sent; the server returned no text." : "Command completed.", DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            return new(false, normalized, string.Empty, ex.Message, DateTimeOffset.UtcNow);
        }
    }

    private async Task<string> ExecuteInternalAsync(string command, CancellationToken cancellationToken)
    {
        var snapshot = configuration.Load();
        var port = ParsePort(Get(snapshot, "RCONPort"), DefaultPort);
        var password = Unquote(Get(snapshot, "AdminPassword"));
        if (password.Length == 0) throw new UnauthorizedAccessException("AdminPassword is empty.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DefaultTimeout);
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync("127.0.0.1", port, timeout.Token);
        await using var stream = client.GetStream();

        const int authId = 5101;
        await WritePacketAsync(stream, authId, 3, password, timeout.Token);
        var authenticated = false;
        for (var i = 0; i < 4; i++)
        {
            var packet = await ReadPacketAsync(stream, timeout.Token);
            if (packet.Id == -1) throw new UnauthorizedAccessException("PalServer rejected the RCON AdminPassword.");
            if (packet.Id == authId && packet.Type == 2) { authenticated = true; break; }
        }
        if (!authenticated) throw new UnauthorizedAccessException("PalServer did not return an RCON authentication response.");

        const int commandId = 5102;
        await WritePacketAsync(stream, commandId, 2, command, timeout.Token);
        var first = await ReadPacketAsync(stream, timeout.Token);
        if (first.Id == -1) throw new UnauthorizedAccessException("RCON session was rejected.");
        var response = new StringBuilder(first.Body);
        // Source RCON can fragment larger responses (for example ShowPlayers). Give the socket
        // a brief opportunity to deliver queued fragments, but keep the overall request bounded
        // by DefaultTimeout so a legacy RCON endpoint can never stall the management API.
        for (var i = 0; i < 31; i++)
        {
            await Task.Delay(20, timeout.Token);
            if (!stream.DataAvailable) break;
            var packet = await ReadPacketAsync(stream, timeout.Token);
            if (packet.Id == -1) throw new UnauthorizedAccessException("RCON session was rejected.");
            if (packet.Id == commandId && packet.Body.Length > 0)
            {
                if (response.Length > 0) response.AppendLine();
                response.Append(packet.Body);
            }
        }
        return response.ToString();
    }

    private static async Task WritePacketAsync(NetworkStream stream, int id, int type, string body, CancellationToken token)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
        var payloadSize = bodyBytes.Length + 10;
        var buffer = new byte[payloadSize + 4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), payloadSize);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4, 4), id);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8, 4), type);
        bodyBytes.CopyTo(buffer.AsSpan(12));
        buffer[^2] = 0;
        buffer[^1] = 0;
        await stream.WriteAsync(buffer, token);
        await stream.FlushAsync(token);
    }

    private static async Task<RconPacket> ReadPacketAsync(NetworkStream stream, CancellationToken token)
    {
        var lengthBytes = new byte[4];
        await ReadExactlyAsync(stream, lengthBytes, token);
        var size = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        if (size is < 10 or > 1_048_576)
            throw new InvalidDataException($"Invalid RCON packet size: {size}.");
        var payload = new byte[size];
        await ReadExactlyAsync(stream, payload, token);
        var id = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4));
        var type = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4, 4));
        var bodyLength = Math.Max(0, size - 10);
        var body = Encoding.UTF8.GetString(payload, 8, bodyLength).TrimEnd('\0');
        return new(id, type, body);
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken token)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], token);
            if (read == 0) throw new EndOfStreamException("RCON connection closed unexpectedly.");
            offset += read;
        }
    }

    private static string? Get(PalworldConfigurationSnapshot snapshot, string name) =>
        snapshot.Settings.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static bool ParseBool(string? value) => bool.TryParse(Unquote(value), out var parsed) && parsed;
    private static int ParsePort(string? value, int fallback) => int.TryParse(Unquote(value), out var parsed) && parsed is > 0 and <= 65535 ? parsed : fallback;
    private static string Unquote(string? value) => (value ?? string.Empty).Trim().Trim('"');
    private sealed record RconPacket(int Id, int Type, string Body);
}
