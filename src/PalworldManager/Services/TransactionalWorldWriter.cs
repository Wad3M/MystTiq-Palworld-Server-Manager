using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class TransactionalWorldWriter
{
    private readonly IPalworldSaveCodec codec;
    private readonly WorldRelationshipValidator validator = new();
    public TransactionalWorldWriter(IPalworldSaveCodec codec) => this.codec = codec;

    public async Task<string> WriteAndVerifyAsync(string workingJson, string outputSav, WorldSnapshot expected, Func<string,WorldSnapshot> decodeSnapshot, CancellationToken ct)
    {
        var pre = validator.Validate(expected);
        if (pre.Any(x => x.BlocksActivation)) throw new InvalidOperationException("The world still contains blocking relationship errors.");
        var temp = outputSav + ".myst.tmp";
        try
        {
            var encoded = await codec.EncodeAsync(workingJson, temp, ct);
            if (!encoded.Success) throw new InvalidDataException("Save encoding failed.");
            var verifyDir = Path.Combine(Path.GetDirectoryName(outputSav)!, "RoundTrip");
            var decoded = await codec.DecodeAsync(temp, verifyDir, ct);
            if (!decoded.Success) throw new InvalidDataException("The temporary save could not be decoded after writing.");
            var actual = decodeSnapshot(decoded.JsonPath);
            VerifyCounts(expected, actual);
            if (validator.Validate(actual).Any(x => x.BlocksActivation)) throw new InvalidDataException("Round-trip validation found broken relationships.");
            File.Move(temp, outputSav, true);
            return outputSav;
        }
        catch { try { if (File.Exists(temp)) File.Delete(temp); } catch { } throw; }
    }

    private static void VerifyCounts(WorldSnapshot expected, WorldSnapshot actual)
    {
        if(expected.Players.Count!=actual.Players.Count) throw new InvalidDataException("Player count changed during round trip.");
        if(expected.Guilds.Count!=actual.Guilds.Count) throw new InvalidDataException("Guild count changed during round trip.");
        if(expected.Bases.Count!=actual.Bases.Count) throw new InvalidDataException("Base count changed during round trip.");
    }
}
