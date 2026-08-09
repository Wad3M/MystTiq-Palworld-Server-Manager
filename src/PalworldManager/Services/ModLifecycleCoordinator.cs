using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Owns the MOD lifecycle boundary immediately before a normal PalServer start.
/// It repairs known UE4SS state drift, rescans authoritative state, and blocks a
/// modded startup when the resulting configuration is unsafe or indeterminate.
/// </summary>
public sealed class ModLifecycleCoordinator
{
    private readonly ModService mods;
    private readonly ModRepairRecommendationEngine recommendations;

    public ModLifecycleCoordinator(ModService mods, ModRepairRecommendationEngine recommendations)
    {
        this.mods = mods;
        this.recommendations = recommendations;
    }

    public ModLifecycleReport ReconcileBeforeStart()
    {
        var repair = mods.RepairUe4ssStates();
        var inventory = mods.Scan();
        var blocking = new List<string>();
        var warnings = new List<string>(repair.Warnings);

        var duplicatePackages = inventory
            .GroupBy(x => ModVerificationService.Normalize(x.Package), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
            .Select(group => group.First().Package)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in inventory.Where(x => x.Enabled))
        {
            var display = string.IsNullOrWhiteSpace(mod.Name) ? mod.Package : mod.Name;
            if (!mod.Deployed)
                blocking.Add($"{display}: required files are missing or not deployed.");

            var isUe4ss = mod.Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) ||
                          mod.Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase);
            if (isUe4ss && !mod.PresentInActiveRuntime)
                blocking.Add($"{display}: enabled UE4SS MOD is outside the Active Mods Root.");

            if (mod.EnableReason.Contains("STATE MISMATCH", StringComparison.OrdinalIgnoreCase))
                blocking.Add($"{display}: enabled-state mismatch remains after reconciliation.");

            if (duplicatePackages.Contains(mod.Package))
                blocking.Add($"{display}: duplicate logical installation detected.");
        }

        if (repair.Warnings.Count > 0)
            blocking.Add("Pre-start reconciliation reported filesystem/state warnings; MystTiq cannot guarantee the effective UE4SS activation state.");

        var advice = recommendations.Build(inventory);
        var status = blocking.Count > 0
            ? ModStartupGateStatus.Blocked
            : warnings.Count > 0
                ? ModStartupGateStatus.ReadyWithWarnings
                : ModStartupGateStatus.Ready;

        return new ModLifecycleReport(
            DateTime.Now,
            status,
            repair,
            inventory,
            advice,
            blocking.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }
}
