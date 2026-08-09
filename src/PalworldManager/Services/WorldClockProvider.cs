using System.Text;
using System.Text.RegularExpressions;
using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Reads Palworld's authoritative saved world clock from decoded Level.sav JSON.
/// Palworld stores FPalGameTimeSaveData.GameDateTimeTicks in 100-nanosecond ticks.
/// One in-game day is therefore 864,000,000,000 ticks.
/// This provider never estimates the clock from process uptime.
/// </summary>
public sealed class WorldClockProvider
{
    private const long TicksPerDay = 864_000_000_000L;
    private static readonly byte[] PropertyNeedle = Encoding.UTF8.GetBytes("\"GameDateTimeTicks\"");
    private static readonly Regex WrappedValuePattern =
        new("\"value\"\\s*:\\s*\\\"?(?<ticks>-?\\d+)\\\"?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DirectValuePattern =
        new("^\\s*:\\s*\\\"?(?<ticks>-?\\d+)\\\"?", RegexOptions.Compiled);

    private readonly object gate = new();
    private string cachedPath = "";
    private DateTime cachedWriteUtc;
    private WorldClockSnapshot cached =
        new(false, 0, 0, TimeSpan.Zero, DateTime.MinValue, "Decoded Level.sav", "World clock has not been read yet.");

    public WorldClockSnapshot Read(string decodedJsonPath, DateTime sourceWriteUtc)
    {
        if (string.IsNullOrWhiteSpace(decodedJsonPath) || !File.Exists(decodedJsonPath))
            return new(false, 0, 0, TimeSpan.Zero, sourceWriteUtc, "Decoded Level.sav",
                "Decoded Level.sav JSON is unavailable, so MystTiq will not guess the in-game clock.");

        lock (gate)
        {
            if (cachedPath.Equals(decodedJsonPath, StringComparison.OrdinalIgnoreCase) &&
                cachedWriteUtc == sourceWriteUtc)
                return cached;

            cachedPath = decodedJsonPath;
            cachedWriteUtc = sourceWriteUtc;

            var ticks = TryReadGameDateTimeTicks(decodedJsonPath);
            if (ticks is null || ticks < 0)
            {
                cached = new(false, 0, 0, TimeSpan.Zero, sourceWriteUtc, "GameTimeSaveData.GameDateTimeTicks",
                    "GameDateTimeTicks was not found in the decoded world save.");
                return cached;
            }

            var day = ticks.Value / TicksPerDay;
            var remainder = ticks.Value % TicksPerDay;
            var time = TimeSpan.FromTicks(remainder);

            cached = new(
                true,
                ticks.Value,
                day,
                time,
                sourceWriteUtc,
                "GameTimeSaveData.GameDateTimeTicks",
                "Exact saved Palworld world clock; freshness follows the latest Level.sav write.");
            return cached;
        }
    }

    private static long? TryReadGameDateTimeTicks(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[64 * 1024];
        var matched = 0;

        while (true)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0) return null;

            for (var i = 0; i < read; i++)
            {
                var value = buffer[i];
                if (value == PropertyNeedle[matched])
                {
                    matched++;
                    if (matched == PropertyNeedle.Length)
                    {
                        var tail = new byte[4096];
                        var copied = Math.Min(tail.Length, read - (i + 1));
                        if (copied > 0)
                            Buffer.BlockCopy(buffer, i + 1, tail, 0, copied);
                        if (copied < tail.Length)
                            copied += stream.Read(tail, copied, tail.Length - copied);
                        var text = Encoding.UTF8.GetString(tail, 0, copied);

                        var wrapped = WrappedValuePattern.Match(text);
                        if (wrapped.Success && long.TryParse(wrapped.Groups["ticks"].Value, out var wrappedTicks))
                            return wrappedTicks;

                        var direct = DirectValuePattern.Match(text);
                        if (direct.Success && long.TryParse(direct.Groups["ticks"].Value, out var directTicks))
                            return directTicks;

                        return null;
                    }
                }
                else
                {
                    matched = value == PropertyNeedle[0] ? 1 : 0;
                }
            }
        }
    }
}
