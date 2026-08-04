namespace PalworldManager.Models;

public sealed record ActiveWorldContext(
    string WorldPath,
    string WorldId,
    string LevelSavePath,
    DateTime LevelLastWriteUtc,
    long LevelLength,
    string ResolutionSource,
    long Generation)
{
    public bool IsResolved => !string.IsNullOrWhiteSpace(WorldPath) && File.Exists(LevelSavePath);
    public string Status => IsResolved ? "ACTIVE" : "NOT FOUND";
}
