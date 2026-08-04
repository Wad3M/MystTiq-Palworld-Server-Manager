namespace PalworldManager.Models;

public sealed class WorldDiscoverySnapshot
{
    public ActiveWorldContext Context { get; init; } = new("", "", "", DateTime.MinValue, 0, "Unresolved", 0);
    public string DecodedJsonPath { get; init; } = "";
    public string SourceHash { get; init; } = "";
    public DateTime ScannedUtc { get; init; } = DateTime.UtcNow;
    public TimeSpan Duration { get; init; }
    public WorldSnapshot World { get; init; } = new();
    public GuildWorldSnapshot Guilds { get; init; } = new();
    public IReadOnlyList<BaseDiscoveryRecord> BaseRecords { get; init; } = [];
    public IReadOnlyList<string> PlayerSavePaths { get; init; } = [];
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public bool IsResolved => !string.IsNullOrWhiteSpace(Context.WorldPath) && File.Exists(Context.LevelSavePath);
}
