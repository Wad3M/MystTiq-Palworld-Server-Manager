// MystTiq v1.0.0.6: file reviewed for this release (2026-10-05).
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace MystTiq.Core.Services;

/// <summary>
/// v1.0.0.1: the player ID a Palworld dedicated server gives a Steam player. It is derived from the Steam ID: CityHash64
/// of the decimal SteamID64 in UTF-16LE, folded to 32 bits (low + high × 23) and written as the first 8 hex digits of the
/// 32-digit ID. Checked against this project's own world on 2026-10-04: Wade (steam_76561197962020201) is 67D8D355 and
/// Melly (steam_76561198653223616) is A3835C7B, both of which own characters. On some days the server gave the same
/// accounts other IDs (E290DA9A, 014308E2), so they started over with a new character; the identity guard uses this to
/// notice that at once.
/// </summary>
public static class SteamPlayerUid
{
    /// <summary>The 32-digit player ID for a REST/PalDefender user id such as "steam_76561197962020201", or null when it is not a Steam id.</summary>
    public static string? FromUserId(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        var text = userId.Trim();
        if (text.StartsWith("steam_", StringComparison.OrdinalIgnoreCase)) text = text[6..];
        if (text.Length is < 15 or > 20 || !ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var steamId)) return null;
        return FromSteamId(steamId);
    }

    public static string FromSteamId(ulong steamId)
    {
        var hash = CityHash64(Encoding.Unicode.GetBytes(steamId.ToString(CultureInfo.InvariantCulture)));
        var folded = unchecked((uint)((hash & 0xFFFFFFFF) + (hash >> 32) * 23));
        return folded.ToString("X8", CultureInfo.InvariantCulture) + new string('0', 24);
    }

    // Google CityHash v1.1, CityHash64 for inputs up to 64 bytes (MIT licence, Copyright (c) 2011 Google, Inc.), ported to C#.
    private const ulong K0 = 0xc3a5c85c97cb3127UL;
    private const ulong K1 = 0xb492b66fbe98f273UL;
    private const ulong K2 = 0x9ae16a3b2f90404fUL;

    public static ulong CityHash64(ReadOnlySpan<byte> s)
    {
        unchecked
        {
            var len = (ulong)s.Length;
            if (len <= 32) return len <= 16 ? HashLen0To16(s) : HashLen17To32(s);
            if (len <= 64) return HashLen33To64(s);
            // A SteamID64 is 15 to 20 digits, 30 to 40 bytes in UTF-16. Longer input is not needed, and the long-input path
            // could not be checked against the reference values, so it is not offered.
            throw new ArgumentException("SteamPlayerUid hashes at most 64 bytes.", nameof(s));
        }
    }
    private static ulong Fetch64(ReadOnlySpan<byte> s, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(s[offset..]);
    private static uint Fetch32(ReadOnlySpan<byte> s, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(s[offset..]);
    private static ulong Rotate(ulong value, int shift) => shift == 0 ? value : (value >> shift) | (value << (64 - shift));
    private static ulong ShiftMix(ulong value) => value ^ (value >> 47);

    private static ulong HashLen16(ulong u, ulong v)
    {
        unchecked
        {
            const ulong kMul = 0x9ddfea08eb382d69UL;
            var a = (u ^ v) * kMul;
            a ^= a >> 47;
            var b = (v ^ a) * kMul;
            b ^= b >> 47;
            return b * kMul;
        }
    }

    private static ulong HashLen16(ulong u, ulong v, ulong mul)
    {
        unchecked
        {
            var a = (u ^ v) * mul;
            a ^= a >> 47;
            var b = (v ^ a) * mul;
            b ^= b >> 47;
            return b * mul;
        }
    }

    private static ulong HashLen0To16(ReadOnlySpan<byte> s)
    {
        unchecked
        {
            var len = (ulong)s.Length;
            if (len >= 8)
            {
                var mul = K2 + len * 2;
                var a = Fetch64(s, 0) + K2;
                var b = Fetch64(s, s.Length - 8);
                var c = Rotate(b, 37) * mul + a;
                var d = (Rotate(a, 25) + b) * mul;
                return HashLen16(c, d, mul);
            }
            if (len >= 4)
            {
                var mul = K2 + len * 2;
                ulong a = Fetch32(s, 0);
                return HashLen16(len + (a << 3), Fetch32(s, s.Length - 4), mul);
            }
            if (len > 0)
            {
                uint a = s[0], b = s[s.Length >> 1], c = s[s.Length - 1];
                var y = a + (b << 8);
                var z = (uint)len + (c << 2);
                return ShiftMix(y * K2 ^ z * K0) * K2;
            }
            return K2;
        }
    }

    private static ulong HashLen17To32(ReadOnlySpan<byte> s)
    {
        unchecked
        {
            var len = (ulong)s.Length;
            var mul = K2 + len * 2;
            var a = Fetch64(s, 0) * K1;
            var b = Fetch64(s, 8);
            var c = Fetch64(s, s.Length - 8) * mul;
            var d = Fetch64(s, s.Length - 16) * K2;
            return HashLen16(Rotate(a + b, 43) + Rotate(c, 30) + d, a + Rotate(b + K2, 18) + c, mul);
        }
    }

    private static ulong HashLen33To64(ReadOnlySpan<byte> s)
    {
        unchecked
        {
            var len = (ulong)s.Length;
            var mul = K2 + len * 2;
            var a = Fetch64(s, 0) * K2;
            var b = Fetch64(s, 8);
            var c = Fetch64(s, s.Length - 24);
            var d = Fetch64(s, s.Length - 32);
            var e = Fetch64(s, 16) * K2;
            var f = Fetch64(s, 24) * 9;
            var g = Fetch64(s, s.Length - 8);
            var h = Fetch64(s, s.Length - 16) * mul;
            var u = Rotate(a + g, 43) + (Rotate(b, 30) + c) * 9;
            var v = ((a + g) ^ d) + f + 1;
            var w = BinaryPrimitives.ReverseEndianness((u + v) * mul) + h;
            var x = Rotate(e + f, 42) + c;
            var y = (BinaryPrimitives.ReverseEndianness((v + w) * mul) + g) * mul;
            var z = e + f + c;
            a = BinaryPrimitives.ReverseEndianness((x + z) * mul + y) + b;
            b = ShiftMix((z + a) * mul + d + h) * mul;
            return b + x;
        }
    }
}
