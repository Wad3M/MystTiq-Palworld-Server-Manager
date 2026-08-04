using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class Plm1SaveDecoder
{
    private readonly IPalworldSaveCodec codec;
    public Plm1SaveDecoder(IPalworldSaveCodec codec) => this.codec = codec;

    public PalworldSaveHeader Inspect(string savePath)
    {
        if (!File.Exists(savePath)) throw new FileNotFoundException("Palworld save was not found.", savePath);
        using var stream = File.OpenRead(savePath);
        var bytes = new byte[Math.Min(16, (int)stream.Length)];
        _ = stream.Read(bytes, 0, bytes.Length);
        var text = ToDisplayText(bytes);
        var plm1Offset = IndexOf(bytes, "PlM1"u8);
        var gvasOffset = IndexOf(bytes, "GVAS"u8);
        var kind = plm1Offset >= 0
            ? PalworldSaveContainerKind.PlM1
            : gvasOffset >= 0
                ? PalworldSaveContainerKind.Gvas
                : FirstNonWhitespace(bytes) == (byte)'{'
                    ? PalworldSaveContainerKind.Json
                    : PalworldSaveContainerKind.Unknown;
        var header = new PalworldSaveHeader
        {
            Path = savePath,
            Length = stream.Length,
            MagicHex = Convert.ToHexString(bytes),
            MagicText = text,
            Kind = kind,
            AppearsCompressed = kind == PalworldSaveContainerKind.PlM1
        };
        if (kind == PalworldSaveContainerKind.PlM1 && plm1Offset > 0)
            header.Warnings.Add($"PlM1 container signature detected at header offset {plm1Offset}; the leading bytes contain Palworld size metadata.");
        else if (kind == PalworldSaveContainerKind.Gvas && gvasOffset > 0)
            header.Warnings.Add($"GVAS container signature detected at header offset {gvasOffset}.");
        else if (header.Kind == PalworldSaveContainerKind.Unknown)
            header.Warnings.Add("The save container is readable but its signature is not yet classified by the built-in header reader.");
        if (header.Length < 32) header.Warnings.Add("Save is unexpectedly small and may be truncated.");
        return header;
    }

    public async Task<RealSaveDecodeResult> DecodeAsync(string savePath, string outputDirectory, CancellationToken cancellationToken)
    {
        var header = Inspect(savePath);
        if (!codec.IsAvailable()) throw new InvalidOperationException("Palworld save tooling is not configured. Set PalworldSaveToolsPath or install the bundled converter.");
        var result = await codec.DecodeAsync(savePath, outputDirectory, cancellationToken);
        result.CodecVersion = DetectCodecVersion(result.Diagnostics);
        if (!result.Success) throw new InvalidDataException("The save converter did not produce valid JSON.");
        using (JsonDocument.Parse(await File.ReadAllTextAsync(result.JsonPath, cancellationToken))) { }
        return new RealSaveDecodeResult { Header = header, Codec = result };
    }

    private static byte FirstNonWhitespace(byte[] bytes) => bytes.FirstOrDefault(b => !char.IsWhiteSpace((char)b));

    private static int IndexOf(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty || source.Length < value.Length) return -1;
        for (var i = 0; i <= source.Length - value.Length; i++)
            if (source.Slice(i, value.Length).SequenceEqual(value)) return i;
        return -1;
    }

    private static string ToDisplayText(IEnumerable<byte> bytes) =>
        new(bytes.Select(b => b is >= 32 and <= 126 ? (char)b : '·').ToArray());
    private static string DetectCodecVersion(IEnumerable<string> diagnostics)
    {
        var text = string.Join(" ", diagnostics);
        var match = Regex.Match(text, @"(?i)(?:version|v)\s*(\d+\.\d+(?:\.\d+)*)");
        return match.Success ? match.Groups[1].Value : "Unknown";
    }
}
