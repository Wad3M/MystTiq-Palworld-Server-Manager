using System.Text;

namespace PalworldManager.Services;

public sealed record PalworldSaveContainerInfo(
    string Kind,
    int Offset,
    string HeaderHex,
    string HeaderText)
{
    public bool IsPlm => string.Equals(Kind, "PlM", StringComparison.Ordinal);
    public bool IsPlz => string.Equals(Kind, "PlZ", StringComparison.Ordinal);
    public string DisplaySignature => Offset >= 0 ? $"{Kind} (offset {Offset})" : "Unknown";
}

public static class PalworldSaveContainerDetector
{
    private static readonly byte[] Plm = "PlM"u8.ToArray();
    private static readonly byte[] Plz = "PlZ"u8.ToArray();

    public static PalworldSaveContainerInfo Inspect(string path, int probeLength = 64)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A save path is required.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("Palworld save was not found.", path);

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        var length = (int)Math.Min(Math.Max(probeLength, 16), stream.Length);
        var buffer = new byte[length];
        var count = stream.Read(buffer, 0, buffer.Length);
        if (count != buffer.Length) Array.Resize(ref buffer, count);

        var plmOffset = IndexOf(buffer, Plm);
        var plzOffset = IndexOf(buffer, Plz);
        var offset = SelectFirst(plmOffset, plzOffset);
        var kind = offset == plmOffset && plmOffset >= 0
            ? "PlM"
            : offset == plzOffset && plzOffset >= 0
                ? "PlZ"
                : "Unknown";

        var displayBytes = buffer.Take(Math.Min(buffer.Length, 16)).ToArray();
        return new PalworldSaveContainerInfo(
            kind,
            offset,
            Convert.ToHexString(displayBytes),
            ToDisplayText(displayBytes));
    }

    private static int SelectFirst(int left, int right)
    {
        if (left < 0) return right;
        if (right < 0) return left;
        return Math.Min(left, right);
    }

    private static int IndexOf(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty || source.Length < value.Length) return -1;
        for (var index = 0; index <= source.Length - value.Length; index++)
            if (source.Slice(index, value.Length).SequenceEqual(value)) return index;
        return -1;
    }

    private static string ToDisplayText(IEnumerable<byte> bytes) =>
        new(bytes.Select(value => value is >= 32 and <= 126 ? (char)value : '·').ToArray());
}
