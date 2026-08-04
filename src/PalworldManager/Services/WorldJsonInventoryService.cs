using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class WorldJsonInventoryService
{
    public WorldSnapshot Build(string jsonPath, IEnumerable<string> playerSavePaths)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var snapshot = new WorldSnapshot { WorldName = Path.GetFileName(Path.GetDirectoryName(jsonPath)) ?? "Imported World" };
        foreach (var save in playerSavePaths.Where(p => p.EndsWith(".sav", StringComparison.OrdinalIgnoreCase) && !p.EndsWith("_dps.sav", StringComparison.OrdinalIgnoreCase)))
        {
            var id = Path.GetFileNameWithoutExtension(save);
            snapshot.Players.Add(new WorldPlayerRecord { PlayerGuid = id, SaveFilePath = save, IsHostCandidate = id.Equals("00000000000000000000000000000001", StringComparison.OrdinalIgnoreCase), Health = EntityHealth.Unresolved });
        }
        // Version-specific guild/base extractors enrich this snapshot. Keeping traversal isolated here prevents UI coupling to raw JSON.
        return snapshot;
    }
}
