namespace PalworldManager.Models;

public sealed class BaseManagerRow
{
    public string BaseId { get; set; } = "";
    public string Name { get; set; } = "Base";
    public string InternalName { get; set; } = "";
    public string GuildId { get; set; } = "";
    public string GuildName { get; set; } = "Unassigned";
    public string PalboxId { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public string SourcePath { get; set; } = "";
    public string Health { get; set; } = "Unresolved";
    public string Location => $"{X:0.##}, {Y:0.##}, {Z:0.##}";
    public string PalboxDisplay => string.IsNullOrWhiteSpace(PalboxId) ? "Not identified" : PalboxId;
    public string OwnershipDisplay => string.IsNullOrWhiteSpace(GuildId) ? "No guild" : GuildName;
}

public sealed class BaseManagerSummary
{
    public string WorldPath { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public bool CodecAvailable { get; set; }
    public List<BaseManagerRow> Bases { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public int HealthyCount => Bases.Count(x => x.Health.Equals("Healthy", StringComparison.OrdinalIgnoreCase));
    public int OrphanedCount => Bases.Count(x => x.Health.Equals("Orphaned", StringComparison.OrdinalIgnoreCase));
    public int PalboxCount => Bases.Count(x => !string.IsNullOrWhiteSpace(x.PalboxId));
    public int GuildCount => Bases.Where(x => !string.IsNullOrWhiteSpace(x.GuildId)).Select(x => x.GuildId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
}
