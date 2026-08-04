namespace PalworldManager.Models;

public sealed class GuildBaseRecoveryFinding
{
    public bool IsSelected { get; set; }
    public string Severity { get; set; } = "Information";
    public string Category { get; set; } = "Guild";
    public string Action { get; set; } = "Review";
    public string TargetName { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string CurrentValue { get; set; } = "";
    public string ProposedValue { get; set; } = "";
    public string Risk { get; set; } = "Low";
    public string State { get; set; } = "Pending";
    public string Description { get; set; } = "";
}

public sealed class GuildBaseRecoverySummary
{
    public string WorldPath { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public bool CodecAvailable { get; set; }
    public List<GuildRow> Guilds { get; set; } = [];
    public List<GuildWorldPlayerRow> Players { get; set; } = [];
    public List<GuildBaseRecoveryFinding> Findings { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public int OrphanedGuildCount => Guilds.Count(g => g.IsOrphaned);
    public int BaseCount => Guilds.Sum(g => g.BaseCount);
    public int MissingPlayerSaveCount => Guilds.SelectMany(g => g.Members).Count(m => !m.PlayerSaveExists);
}

public sealed class GuildBaseRecoveryPlan
{
    public string PlanId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string WorldPath { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public bool CodecAvailable { get; set; }
    public List<GuildBaseRecoveryFinding> Operations { get; set; } = [];
    public List<string> ValidationMessages { get; set; } = [];
    public bool IsReadyForTransactionalWrite => CodecAvailable && Operations.Count > 0 && ValidationMessages.All(m => !m.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase));
}
