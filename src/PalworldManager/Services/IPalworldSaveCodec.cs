using PalworldManager.Models;
namespace PalworldManager.Services;

public interface IPalworldSaveCodec
{
    string Name { get; }
    bool IsAvailable();
    Task<SaveCodecResult> DecodeAsync(string savePath, string outputDirectory, CancellationToken cancellationToken);
    Task<SaveCodecResult> EncodeAsync(string jsonPath, string outputSavePath, CancellationToken cancellationToken);
}
