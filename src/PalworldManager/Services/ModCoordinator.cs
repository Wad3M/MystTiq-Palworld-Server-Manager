using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Application-facing orchestration boundary for MOD workflows.
/// MainWindow consumes this facade instead of coordinating inventory, runtime
/// verification, compatibility, repair recommendations, and exports itself.
/// </summary>
public sealed class ModCoordinator
{
    private readonly ModInventorySnapshotService inventory;
    private readonly ModVerificationService verification;
    private readonly ModCompatibilityService compatibility;
    private readonly ModRepairRecommendationEngine recommendations;
    private readonly ModVerificationReportExportService exporter;
    private readonly ServerService server;

    public ModCoordinator(
        ModInventorySnapshotService inventory,
        ModVerificationService verification,
        ModCompatibilityService compatibility,
        ModRepairRecommendationEngine recommendations,
        ModVerificationReportExportService exporter,
        ServerService server)
    {
        this.inventory = inventory;
        this.verification = verification;
        this.compatibility = compatibility;
        this.recommendations = recommendations;
        this.exporter = exporter;
        this.server = server;
    }

    public ModInventorySnapshot RefreshInventory(string trigger = "Scan Library", bool force = true) =>
        inventory.Current(trigger, force);

    public VerificationResult VerifyOne(ModRow mod) =>
        verification.VerifyAll([mod], server.IsRunning()).Single();

    public ModVerificationWorkflowResult VerifyAll(string trigger = "Verify & Scan All MODs", bool force = true)
    {
        var snapshot = inventory.Current(trigger, force);
        var verified = verification.VerifyAll(snapshot.Mods, server.IsRunning());
        var staticCompatibility = compatibility.Scan(snapshot.Mods);
        var advice = recommendations.Build(snapshot.Mods, verified);
        return new ModVerificationWorkflowResult(snapshot, verified, staticCompatibility, advice);
    }

    public ModCompatibilityWorkflowResult ScanCompatibility(string trigger = "Scan MOD compatibility", bool force = true)
    {
        var snapshot = inventory.Current(trigger, force);
        return new ModCompatibilityWorkflowResult(snapshot, compatibility.Scan(snapshot.Mods));
    }

    public ModVerificationExportWorkflowResult ExportVerification(string trigger = "Export MOD verification report")
    {
        var workflow = VerifyAll(trigger, force: true);
        var exported = exporter.Export(workflow.Verification, workflow.Recommendations);
        return new ModVerificationExportWorkflowResult(workflow, exported);
    }

    public void InvalidateInventory() => inventory.Invalidate();
}
