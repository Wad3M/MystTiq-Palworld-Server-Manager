namespace PalworldManager.Models;

public enum EntityHealth { Healthy, Warning, Broken, Orphaned, Unresolved }
public sealed class WorldSnapshot
{
    public string WorldName { get; set; } = "";
    public List<WorldPlayerRecord> Players { get; set; } = [];
    public List<WorldGuildRecord> Guilds { get; set; } = [];
    public List<WorldBaseRecord> Bases { get; set; } = [];
    public List<WorldIssue> Issues { get; set; } = [];
}
public sealed class WorldPlayerRecord
{
    public string PlayerGuid { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string PlatformId { get; set; } = "";
    public string SaveFilePath { get; set; } = "";
    public string GuildId { get; set; } = "";
    public bool IsHostCandidate { get; set; }
    public EntityHealth Health { get; set; }
}
public sealed class WorldGuildRecord
{
    public string GuildId { get; set; } = "";
    public string GuildName { get; set; } = "";
    public string LeaderPlayerGuid { get; set; } = "";
    public List<string> MemberPlayerGuids { get; set; } = [];
    public List<string> BaseIds { get; set; } = [];
    public EntityHealth Health { get; set; }
}
public sealed class WorldBaseRecord
{
    public string BaseId { get; set; } = "";
    public string GuildId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public EntityHealth Health { get; set; }
}
public sealed class WorldIssue
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public string EntityId { get; set; } = "";
    public bool BlocksActivation { get; set; }
}
