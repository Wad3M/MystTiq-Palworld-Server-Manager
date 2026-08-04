using System.Buffers.Binary;
using System.Net.Sockets;

namespace PalworldManager.Services;

public sealed class RconClient : IAsyncDisposable
{
    private TcpClient? client;
    private NetworkStream? stream;
    private int requestId;

    public bool IsConnected => client?.Connected == true && stream is not null;

    public async Task ConnectAsync(string host, int port, string password, CancellationToken ct = default)
    {
        await DisconnectAsync();
        client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        stream = client.GetStream();
        var id = Interlocked.Increment(ref requestId);
        await SendPacketAsync(id, 3, password, ct);
        var response = await ReadPacketAsync(ct);
        if (response.Id == -1)
        {
            await DisconnectAsync();
            throw new UnauthorizedAccessException("RCON authentication failed. Verify AdminPassword and RCONPort.");
        }
    }

    public async Task<string> ExecuteAsync(string command, CancellationToken ct = default)
    {
        if (stream is null || client?.Connected != true)
            throw new InvalidOperationException("RCON is not connected.");

        var id = Interlocked.Increment(ref requestId);
        await SendPacketAsync(id, 2, command, ct);
        var response = await ReadPacketAsync(ct);
        if (response.Id != id && response.Id != 0)
            throw new IOException("RCON returned an unexpected response identifier.");
        return response.Body;
    }

    private async Task SendPacketAsync(int id, int type, string body, CancellationToken ct)
    {
        if (stream is null) throw new InvalidOperationException("RCON is not connected.");
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var length = 4 + 4 + bodyBytes.Length + 2;
        var packet = new byte[length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(0, 4), length);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(4, 4), id);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(8, 4), type);
        bodyBytes.CopyTo(packet, 12);
        await stream.WriteAsync(packet, ct);
        await stream.FlushAsync(ct);
    }

    private async Task<(int Id, int Type, string Body)> ReadPacketAsync(CancellationToken ct)
    {
        if (stream is null) throw new InvalidOperationException("RCON is not connected.");
        var lengthBuffer = new byte[4];
        await ReadExactAsync(stream, lengthBuffer, ct);
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
        if (length < 10 || length > 1024 * 1024)
            throw new IOException($"Invalid RCON packet length: {length}.");
        var payload = new byte[length];
        await ReadExactAsync(stream, payload, ct);
        var id = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4));
        var type = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4, 4));
        var bodyLength = Math.Max(0, length - 10);
        var body = Encoding.UTF8.GetString(payload, 8, bodyLength).TrimEnd('\0');
        return (id, type, body);
    }

    private static async Task ReadExactAsync(Stream source, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await source.ReadAsync(buffer.AsMemory(offset), ct);
            if (read == 0) throw new EndOfStreamException("RCON connection closed by the server.");
            offset += read;
        }
    }

    public async Task DisconnectAsync()
    {
        if (stream is not null)
            await stream.DisposeAsync();
        client?.Dispose();
        stream = null;
        client = null;
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
