using PalworldManager.Models;
using PalworldManager.Services.Infrastructure;

namespace PalworldManager.Services;

public sealed class ModVerificationReportExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ModVerificationExportResult Export(
        IEnumerable<VerificationResult> verification,
        IEnumerable<ModRepairRecommendation> recommendations)
    {
        var verified = verification.ToList();
        var advice = recommendations.ToList();
        var exportedAt = DateTime.Now;
        var root = Path.Combine(ApplicationPathService.Current.ActivityRoot, "mod-verification-reports");
        Directory.CreateDirectory(root);
        var stem = $"MystTiq_MOD_Verification_{exportedAt:yyyyMMdd_HHmmss}";
        var textPath = Path.Combine(root, stem + ".txt");
        var jsonPath = Path.Combine(root, stem + ".json");

        var payload = new
        {
            Product = ApplicationVersion.ProductName,
            Version = ApplicationVersion.DisplayVersion,
            ExportedAt = exportedAt,
            Mods = verified,
            Recommendations = advice
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, JsonOptions));

        var lines = new List<string>
        {
            $"{ApplicationVersion.ProductName} {ApplicationVersion.DisplayVersion}",
            "MOD Verification Report",
            $"Exported: {exportedAt:yyyy-MM-dd HH:mm:ss}",
            $"MODs: {verified.Count}",
            ""
        };
        foreach (var item in verified.OrderBy(x => x.Name))
        {
            lines.Add($"[{ModHealthEvaluationService.ToDisplayText(item.HealthStatus)}] {item.Name} ({item.Package})");
            lines.Add($"  Type: {item.Type} | Files: {item.FilesStatus} | Runtime: {item.RuntimeStatus} | Score: {item.HealthScore}");
            lines.Add($"  Errors: {item.ErrorSummary}");
            lines.Add($"  Evidence: {item.Details}");
            foreach (var recommendation in advice.Where(x => x.Package.Equals(item.Package, StringComparison.OrdinalIgnoreCase)))
                lines.Add($"  Recommendation [{recommendation.Severity}]: {recommendation.Action} — {recommendation.Reason}");
            lines.Add("");
        }
        File.WriteAllLines(textPath, lines);
        return new ModVerificationExportResult(textPath, jsonPath, verified.Count, exportedAt);
    }
}
