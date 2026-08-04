namespace PalworldManager.Models;

public sealed class GuildMemberRow
{
    public string PlayerUid { get; set; } = "";
    public string PlayerName { get; set; } = "Unknown Player";
    public bool IsLeader { get; set; }
    public bool PlayerSaveExists { get; set; }
    public string CharacterInstanceId { get; set; } = "";
    public string Role => IsLeader ? "Leader" : "Member";
    public string Presence => PlayerSaveExists ? "Save found" : "Missing save";
}
public sealed class GuildWorldPlayerRow
{
    public string PlayerUid { get; set; } = "";
    public string PlayerName { get; set; } = "Unknown Player";
    public string GuildName { get; set; } = "Unassigned";
    public string Role { get; set; } = "Unassigned";
    public string Source { get; set; } = "Guild data";
    public string SavePath { get; set; } = "";
    public bool PlayerSaveExists { get; set; }
}
public sealed class GuildBaseRow
{
    public string BaseId { get; set; } = "";
    public string Name { get; set; } = "Base";
    public string InternalName { get; set; } = "";
    public string Location { get; set; } = "Unknown";
    public string OwnerGuildId { get; set; } = "";
    public string GuildName { get; set; } = "Unassigned";
    public string PalboxDisplay { get; set; } = "Not identified";
    public string Health { get; set; } = "Unresolved";
}
public sealed class GuildRow
{
    public string GuildId { get; set; } = "";
    public string Name { get; set; } = "Unnamed Guild";
    public string LeaderUid { get; set; } = "";
    public string LeaderName { get; set; } = "Unknown";
    public string GroupType { get; set; } = "Guild";
    public int MemberCount => Members.Count;
    public int BaseCount => Bases.Count;
    public bool LeaderSaveExists => Members.Any(m => m.IsLeader && m.PlayerSaveExists);
    public bool IsOrphaned => string.IsNullOrWhiteSpace(LeaderUid) || Members.All(m => !m.PlayerUid.Equals(LeaderUid, StringComparison.OrdinalIgnoreCase)) || !LeaderSaveExists;
    public string Status => IsOrphaned ? "Orphaned" : "Healthy";
    public List<GuildMemberRow> Members { get; set; } = [];
    public List<GuildBaseRow> Bases { get; set; } = [];
}
public enum GuildSnapshotMode { None, JsonExport, DirectLevelSave }
public sealed class GuildWorldSnapshot
{
    public string SourcePath { get; set; } = "";
    public string WorldPath { get; set; } = "";
    public string LevelSavePath { get; set; } = "";
    public string DecodedJsonPath { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public GuildSnapshotMode Mode { get; set; }
    public bool IsReadOnly { get; set; } = true;
    public DateTime LoadedUtc { get; set; } = DateTime.UtcNow;
    public List<GuildRow> Guilds { get; set; } = [];
    public List<GuildWorldPlayerRow> Players { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}
public enum GuildRepairOperationType { AddPlayerToGuild, TransferLeadership, ClaimOrphanedGuild, RepairOwnershipMappings, RemovePlayerFromGuild, TransferBase, MergeGuilds }
public sealed class GuildRepairOperation
{
    public GuildRepairOperationType Type { get; set; }
    public string GuildId { get; set; } = "";
    public string TargetGuildId { get; set; } = "";
    public string PlayerUid { get; set; } = "";
    public string BaseId { get; set; } = "";
    public string Description { get; set; } = "";
}
public sealed class GuildRepairPlan
{
    public string WorldPath { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<GuildRepairOperation> Operations { get; set; } = [];
}
public sealed class GuildRepairResult
{
    public bool Success { get; set; }
    public string BackupPath { get; set; } = "";
    public string ReportPath { get; set; } = "";
    public string Message { get; set; } = "";
}
