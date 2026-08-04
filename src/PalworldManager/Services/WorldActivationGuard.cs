using PalworldManager.Models;
namespace PalworldManager.Services;
public sealed class WorldActivationGuard
{
    public void EnsureReady(WorldValidationReport report, IEnumerable<PlayerMappingRecord> mappings, WorldRepairPlan plan)
    {
        if (!report.IsValid) throw new InvalidOperationException("World validation failed.");
        if (mappings.Any(x=>!x.Confirmed || string.IsNullOrWhiteSpace(x.DestinationPlayerGuid))) throw new InvalidOperationException("All player mappings must be confirmed.");
        if (!plan.Ready) throw new InvalidOperationException("The repair plan is incomplete.");
    }
}
