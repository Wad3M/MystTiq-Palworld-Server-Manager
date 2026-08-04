namespace PalworldManager.Models;
public enum WorldRepairKind { RebindPlayer, AddPlayerToGuild, RemovePlayerFromGuild, TransferGuildLeadership, AssignBaseToGuild, CreateRecoveryGuild }
public sealed class WorldRepairOperation
{
    public Guid OperationId { get; set; } = Guid.NewGuid();
    public WorldRepairKind Kind { get; set; }
    public string EntityId { get; set; } = "";
    public string SourceValue { get; set; } = "";
    public string DestinationValue { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Confirmed { get; set; }
}
public sealed class WorldRepairPlan
{
    public List<WorldRepairOperation> Operations { get; set; } = [];
    public List<WorldIssue> RemainingIssues { get; set; } = [];
    public bool Ready => Operations.All(x => x.Confirmed) && RemainingIssues.All(x => !x.BlocksActivation);
}
