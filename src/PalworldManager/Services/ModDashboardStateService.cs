using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Pure application service for building and merging MOD Dashboard rows.
/// Contains no WPF controls and can be regression-tested independently.
/// </summary>
public sealed class ModDashboardStateService(ModHealthEvaluationService healthEvaluator)
{
    public IReadOnlyList<ModDashboardRow> RefreshFromInventory(
        IEnumerable<ModRow> mods,
        IEnumerable<ModDashboardRow> previousRows,
        bool serverRunning)
    {
        var previous = previousRows
            .GroupBy(row => row.Package, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var updated = new List<ModDashboardRow>();

        foreach (var mod in mods)
        {
            if (previous.TryGetValue(mod.Package, out var existing))
            {
                var filesChanged = !string.Equals(existing.FilesStatus, mod.Deployed ? "Present" : "Missing", StringComparison.OrdinalIgnoreCase);
                var enabledChanged = !string.Equals(existing.EnabledStatus, mod.Enabled ? "Enabled" : "Disabled", StringComparison.OrdinalIgnoreCase);
                existing.Name = mod.Name;
                existing.Type = mod.Type;
                existing.FilesStatus = mod.Deployed ? "Present" : "Missing";
                existing.EnabledStatus = mod.Enabled ? "Enabled" : "Disabled";

                if (filesChanged || enabledChanged)
                {
                    existing.RuntimeStatus = "Not checked";
                    existing.ErrorStatus = "Not checked";
                    existing.LastVerified = "Never";
                    ApplyHealth(existing, healthEvaluator.Evaluate(mod, serverRunning, runtimeChecked: false));
                }

                if (serverRunning && mod.LoadedByUe4ss && IsUe4ss(mod))
                {
                    existing.RuntimeStatus = "Loaded";
                    existing.ErrorStatus = existing.ErrorStatus == "Not checked" ? "None" : existing.ErrorStatus;
                    existing.LastVerified = DateTime.Now.ToString("g");
                    ApplyHealth(existing, healthEvaluator.Evaluate(mod, serverRunning: true, runtimeChecked: true));
                }
                updated.Add(existing);
                continue;
            }

            var evaluation = healthEvaluator.Evaluate(mod, serverRunning, runtimeChecked: false);
            updated.Add(new ModDashboardRow
            {
                Package = mod.Package,
                Name = mod.Name,
                Type = mod.Type,
                FilesStatus = mod.Deployed ? "Present" : "Missing",
                EnabledStatus = mod.Enabled ? "Enabled" : "Disabled",
                RuntimeStatus = "Not checked",
                ErrorStatus = "Not checked",
                DependencyStatus = "Not scanned",
                ConflictStatus = "Not scanned",
                VersionStatus = "Not checked",
                Compatibility = "Not scanned",
                Health = evaluation.DisplayStatus,
                HealthScore = evaluation.Score,
                Details = evaluation.Detail
            });
        }
        return updated;
    }

    public IReadOnlyList<ModDashboardRow> FromVerification(
        IEnumerable<VerificationResult> verification,
        ModCompatibilitySummary compatibility)
    {
        var staticResults = compatibility.Results.ToDictionary(x => x.Package, StringComparer.OrdinalIgnoreCase);
        return verification.Select(result =>
        {
            staticResults.TryGetValue(result.Package, out var staticResult);
            return new ModDashboardRow
            {
                Package = result.Package,
                Name = result.Name,
                Type = result.Type,
                FilesStatus = result.FilesStatus,
                EnabledStatus = result.Enabled ? "Enabled" : "Disabled",
                RuntimeStatus = result.RuntimeStatus,
                ErrorStatus = result.ErrorSummary,
                DependencyStatus = staticResult?.DependencyStatus ?? "Not scanned",
                ConflictStatus = staticResult?.ConflictStatus ?? "Not scanned",
                VersionStatus = staticResult?.VersionStatus ?? "Not checked",
                Compatibility = staticResult?.OverallStatus ?? "Not scanned",
                Health = ModHealthEvaluationService.ToDisplayText(result.HealthStatus),
                HealthScore = result.HealthScore,
                Details = result.Details,
                LastVerified = result.VerifiedAt.ToString("g")
            };
        }).ToList();
    }

    public IReadOnlyList<ModDashboardRow> ApplyCompatibility(
        IEnumerable<ModRow> mods,
        IEnumerable<ModDashboardRow> existingRows,
        ModCompatibilitySummary compatibility,
        IEnumerable<LocalModRow> localRows,
        bool serverRunning)
    {
        var existing = existingRows.GroupBy(row => row.Package, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var staticResults = compatibility.Results.ToDictionary(result => result.Package, StringComparer.OrdinalIgnoreCase);
        var local = localRows.ToList();
        var updated = new List<ModDashboardRow>();

        foreach (var mod in mods)
        {
            existing.TryGetValue(mod.Package, out var row);
            row ??= CreateInitialRow(mod, serverRunning);
            if (!staticResults.TryGetValue(mod.Package, out var result))
            {
                updated.Add(row);
                continue;
            }

            row.DependencyStatus = result.DependencyStatus;
            row.ConflictStatus = result.ConflictStatus;
            row.VersionStatus = result.VersionStatus;
            row.Compatibility = result.OverallStatus;

            const string workshopPrefix = "Steam Workshop ";
            if (mod.Source.StartsWith(workshopPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var workshopId = mod.Source[workshopPrefix.Length..].Trim();
                var localRow = local.FirstOrDefault(item => item.WorkshopId.Equals(workshopId, StringComparison.OrdinalIgnoreCase));
                if (localRow?.UpdateStatus == "UPDATE AVAILABLE")
                {
                    row.VersionStatus = "Update available";
                    if (row.Compatibility == "Compatible") row.Compatibility = "Attention";
                }
                else if (localRow?.UpdateStatus == "CURRENT") row.VersionStatus = "Current";
            }

            row.Details = string.IsNullOrWhiteSpace(row.Details) || row.Details.StartsWith("Run Verify", StringComparison.OrdinalIgnoreCase)
                ? result.Details
                : row.Details + " Compatibility: " + result.Details;
            updated.Add(row);
        }
        return updated;
    }

    public ModDashboardSummarySnapshot Summarize(IEnumerable<ModDashboardRow> rows)
    {
        var list = rows.ToList();
        return new ModDashboardSummarySnapshot(
            list.Count,
            list.Count(x => x.Health == "Healthy"),
            list.Count(x => x.Health is "Runtime Unverified" or "Active / Unverified"),
            list.Count(x => x.Health is "Misconfigured" or "Attention"),
            list.Count(x => x.Health is "Failed" or "Missing"),
            list.Count(x => x.Health == "Disabled"),
            list.Count(x => x.Health == "Unknown"),
            list.Count(x => x.VersionStatus.StartsWith("Update", StringComparison.OrdinalIgnoreCase)),
            list.Count(x => x.Compatibility == "Conflict"),
            list.Count(x => x.DependencyStatus.StartsWith("Missing ", StringComparison.OrdinalIgnoreCase)));
    }

    private ModDashboardRow CreateInitialRow(ModRow mod, bool serverRunning)
    {
        var evaluation = healthEvaluator.Evaluate(mod, serverRunning, runtimeChecked: false);
        return new ModDashboardRow
        {
            Package = mod.Package, Name = mod.Name, Type = mod.Type,
            FilesStatus = mod.Deployed ? "Present" : "Missing",
            EnabledStatus = mod.Enabled ? "Enabled" : "Disabled",
            RuntimeStatus = "Not checked", ErrorStatus = "Not checked",
            Health = evaluation.DisplayStatus, HealthScore = evaluation.Score, Details = evaluation.Detail
        };
    }

    private static void ApplyHealth(ModDashboardRow row, ModHealthEvaluation evaluation)
    {
        row.Health = evaluation.DisplayStatus;
        row.HealthScore = evaluation.Score;
        row.Details = evaluation.Detail;
    }

    private static bool IsUe4ss(ModRow mod) =>
        mod.Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) ||
        mod.Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase);
}
