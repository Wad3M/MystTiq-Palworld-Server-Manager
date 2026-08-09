using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Single source of truth for how MOD state contributes to server-level health.
/// Informational states (Disabled, Active / Unverified, Active, Unknown) never
/// reduce Overall Health. Only concrete problems on enabled MODs contribute.
/// </summary>
public sealed class ModPlatformHealthService
{
    public ModPlatformHealthSnapshot Evaluate(IEnumerable<ModDashboardRow> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
        {
            return new ModPlatformHealthSnapshot
            {
                Installed = 0,
                Severity = DashboardHealthSeverity.Healthy,
                Summary = "Vanilla server profile.",
                HealthLine = "Mods: None"
            };
        }

        var healthy = list.Count(IsHealthy);
        var disabled = list.Count(IsDisabled);
        var unverified = list.Count(IsUnverified);
        var active = list.Count(row => row.Health.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
                                       row.Health.Equals("Installed", StringComparison.OrdinalIgnoreCase));
        var unknown = list.Count(row => row.Health.Equals("Unknown", StringComparison.OrdinalIgnoreCase));

        // A disabled MOD cannot degrade the running server because it is intentionally
        // outside the active runtime. We may still show its local condition in the MOD
        // Dashboard, but that condition is neutral to Overall Health.
        var enabledRows = list.Where(row => !IsDisabled(row)).ToList();
        var issueRows = enabledRows.Where(HasConfirmedProblem).ToList();
        var failed = issueRows.Count(row => row.Health.Equals("Failed", StringComparison.OrdinalIgnoreCase));
        var missing = issueRows.Count(row => row.Health.Equals("Missing", StringComparison.OrdinalIgnoreCase));
        var misconfigured = issueRows.Count(row => row.Health.Equals("Misconfigured", StringComparison.OrdinalIgnoreCase));
        var attention = issueRows.Count(row => row.Health.Equals("Attention", StringComparison.OrdinalIgnoreCase));
        var runtimeErrors = issueRows.Count(HasRuntimeError);
        var conflicts = issueRows.Count(HasConfirmedConflict);
        var missingDependencies = issueRows.Count(HasMissingDependency);
        var confirmedIssues = issueRows.Count;

        var severity = confirmedIssues > 0
            ? DashboardHealthSeverity.Error
            : disabled > 0 || unverified > 0 || active > 0 || unknown > 0
                ? DashboardHealthSeverity.Informational
                : DashboardHealthSeverity.Healthy;

        string summary;
        string healthLine;

        if (confirmedIssues > 0)
        {
            summary = $"{confirmedIssues} enabled MOD{(confirmedIssues == 1 ? " has" : "s have")} confirmed issue{(confirmedIssues == 1 ? "" : "s")}.";
            healthLine = $"Mods: {confirmedIssues} confirmed issue{(confirmedIssues == 1 ? "" : "s")}";
        }
        else if (disabled == list.Count)
        {
            summary = $"All {disabled} installed MOD{(disabled == 1 ? " is" : "s are")} intentionally disabled; MOD health is neutral.";
            healthLine = disabled == 1 ? "Mods: Disabled" : $"Mods: {disabled} disabled";
        }
        else if (unverified > 0)
        {
            summary = $"{healthy} healthy • {unverified} awaiting runtime confirmation" +
                      (disabled > 0 ? $" • {disabled} disabled" : string.Empty) + ".";
            healthLine = $"Mods: {healthy} confirmed • {unverified} awaiting";
        }
        else if (disabled > 0)
        {
            summary = $"{healthy} healthy • {disabled} disabled.";
            healthLine = $"Mods: {healthy} working • {disabled} disabled";
        }
        else if (active > 0 || unknown > 0)
        {
            summary = $"{healthy} healthy • {active + unknown} informational/unverified.";
            healthLine = $"Mods: {healthy} working • {active + unknown} informational";
        }
        else
        {
            summary = $"All {healthy} installed MOD{(healthy == 1 ? " is" : "s are")} healthy.";
            healthLine = $"Mods: {healthy} working / {list.Count} installed";
        }

        return new ModPlatformHealthSnapshot
        {
            Installed = list.Count,
            Healthy = healthy,
            Disabled = disabled,
            RuntimeUnverified = unverified,
            ActiveOrUnknown = active + unknown,
            ConfirmedIssueCount = confirmedIssues,
            Failed = failed,
            Missing = missing,
            Misconfigured = misconfigured,
            Attention = attention,
            RuntimeErrors = runtimeErrors,
            Conflicts = conflicts,
            MissingDependencies = missingDependencies,
            Severity = severity,
            Summary = summary,
            HealthLine = healthLine
        };
    }

    private static bool IsHealthy(ModDashboardRow row) =>
        row.Health.Equals("Healthy", StringComparison.OrdinalIgnoreCase);

    private static bool IsDisabled(ModDashboardRow row) =>
        row.EnabledStatus.Equals("Disabled", StringComparison.OrdinalIgnoreCase) ||
        row.Health.Equals("Disabled", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnverified(ModDashboardRow row) =>
        row.Health.Equals("Runtime Unverified", StringComparison.OrdinalIgnoreCase) ||
        row.Health.Equals("Active / Unverified", StringComparison.OrdinalIgnoreCase);

    private static bool HasConfirmedProblem(ModDashboardRow row) =>
        row.Health is "Failed" or "Missing" or "Misconfigured" or "Attention" ||
        HasRuntimeError(row) ||
        HasConfirmedConflict(row) ||
        HasMissingDependency(row);

    private static bool HasRuntimeError(ModDashboardRow row) =>
        !string.IsNullOrWhiteSpace(row.ErrorStatus) &&
        !row.ErrorStatus.Equals("None", StringComparison.OrdinalIgnoreCase) &&
        !row.ErrorStatus.Equals("Not checked", StringComparison.OrdinalIgnoreCase);

    private static bool HasConfirmedConflict(ModDashboardRow row) =>
        row.Compatibility.Equals("Conflict", StringComparison.OrdinalIgnoreCase) ||
        row.ConflictStatus.Equals("Confirmed conflict", StringComparison.OrdinalIgnoreCase);

    private static bool HasMissingDependency(ModDashboardRow row) =>
        row.DependencyStatus.StartsWith("Missing ", StringComparison.OrdinalIgnoreCase);
}
