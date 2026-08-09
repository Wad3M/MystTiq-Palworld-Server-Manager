using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Converts MOD inventory/verification evidence into deterministic operator actions.
/// Recommendations are advisory; startup blocking remains the responsibility of
/// ModLifecycleCoordinator.
/// </summary>
public sealed class ModRepairRecommendationEngine
{
    public IReadOnlyList<ModRepairRecommendation> Build(
        IEnumerable<ModRow> mods,
        IEnumerable<VerificationResult>? verification = null)
    {
        var results = verification?.ToDictionary(x => x.Package, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, VerificationResult>(StringComparer.OrdinalIgnoreCase);
        var recommendations = new List<ModRepairRecommendation>();

        foreach (var mod in mods)
        {
            results.TryGetValue(mod.Package, out var verified);
            var name = string.IsNullOrWhiteSpace(mod.Name) ? mod.Package : mod.Name;

            if (!mod.Deployed)
                recommendations.Add(new(mod.Package, name, "Blocking", "Repair or reinstall MOD files",
                    "The managed MOD is not deployed to its required runtime location."));

            if (mod.EnableReason.Contains("STATE MISMATCH", StringComparison.OrdinalIgnoreCase))
                recommendations.Add(new(mod.Package, name, "Blocking", "Run Repair States",
                    "UE4SS effective activation does not match MystTiq's canonical mods.txt state."));

            if ((mod.Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) ||
                 mod.Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase)) &&
                mod.Enabled && !mod.PresentInActiveRuntime)
                recommendations.Add(new(mod.Package, name, "Blocking", "Migrate or reinstall into the Active UE4SS Mods Root",
                    "The MOD is enabled but is not present beneath the resolver-selected runtime root."));

            if (verified?.DuplicateDetected == true)
                recommendations.Add(new(mod.Package, name, "Blocking", "Remove the duplicate logical installation",
                    "More than one installed entry resolves to the same logical MOD identity."));

            if (verified?.RuntimeErrorFound == true)
                recommendations.Add(new(mod.Package, name, "High", "Review runtime error evidence and repair/update the MOD",
                    verified.ErrorSummary));
            else if (verified?.HealthStatus == ModHealthStatus.RuntimeUnverified && mod.Enabled)
                recommendations.Add(new(mod.Package, name, "Advisory", "Start the server, then run Verify & Scan All MODs",
                    "Configuration is valid, but runtime load evidence has not been established."));
        }

        return recommendations
            .GroupBy(x => $"{x.Package}|{x.Action}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }
}
