namespace PalworldManager.Models;
public sealed class WorldValidationReport
{
    public List<WorldIssue> Errors { get; set; } = [];
    public List<WorldIssue> Warnings { get; set; } = [];
    public bool IsValid => Errors.Count == 0;
}
public sealed class WorldRoundTripExpectation
{
    public int PlayerCount { get; set; }
    public int GuildCount { get; set; }
    public int BaseCount { get; set; }
    public Dictionary<string,string> ExpectedPlayerMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
