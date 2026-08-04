using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class RepairPreviewService
{
    public RepairPreview Build(WorldSnapshot snapshot, IEnumerable<PlayerMappingRecord> mappings, WorldRepairPlan plan)
    {
        var preview = new RepairPreview();
        foreach (var mapping in mappings.Where(x => x.Confirmed && !string.IsNullOrWhiteSpace(x.DestinationPlayerGuid) && !x.SourcePlayerGuid.Equals(x.DestinationPlayerGuid, StringComparison.OrdinalIgnoreCase)))
            preview.Items.Add(new RepairPreviewItem { Category="Player", EntityId=mapping.SourcePlayerGuid, Field="Player GUID", Before=mapping.SourcePlayerGuid, After=mapping.DestinationPlayerGuid, Reason=mapping.Explanation, IsDestructive=true });
        foreach (var operation in plan.Operations)
            preview.Items.Add(new RepairPreviewItem { Category=operation.Kind.ToString(), EntityId=operation.EntityId, Field=FieldFor(operation.Kind), Before=operation.SourceValue, After=operation.DestinationValue, Reason=operation.Description, IsDestructive=operation.Kind is WorldRepairKind.RebindPlayer or WorldRepairKind.RemovePlayerFromGuild or WorldRepairKind.TransferGuildLeadership or WorldRepairKind.AssignBaseToGuild });
        preview.BlockingIssues.AddRange(plan.RemainingIssues.Where(x => x.BlocksActivation));
        return preview;
    }
    private static string FieldFor(WorldRepairKind kind) => kind switch
    {
        WorldRepairKind.RebindPlayer => "Player GUID",
        WorldRepairKind.AddPlayerToGuild or WorldRepairKind.RemovePlayerFromGuild => "Guild membership",
        WorldRepairKind.TransferGuildLeadership => "Guild leader",
        WorldRepairKind.AssignBaseToGuild => "Base guild",
        WorldRepairKind.CreateRecoveryGuild => "Recovery guild",
        _ => "Relationship"
    };
}
