namespace PalworldManager.Models;

public sealed record ModVerificationWorkflowResult(
    ModInventorySnapshot Inventory,
    IReadOnlyList<VerificationResult> Verification,
    ModCompatibilitySummary Compatibility,
    IReadOnlyList<ModRepairRecommendation> Recommendations);

public sealed record ModCompatibilityWorkflowResult(
    ModInventorySnapshot Inventory,
    ModCompatibilitySummary Compatibility);

public sealed record ModVerificationExportWorkflowResult(
    ModVerificationWorkflowResult Workflow,
    ModVerificationExportResult Export);

public sealed record ModDashboardSummarySnapshot(
    int Installed,
    int Healthy,
    int RuntimeUnverified,
    int Attention,
    int Failed,
    int Disabled,
    int Unknown,
    int Updates,
    int Conflicts,
    int MissingDependencies);
