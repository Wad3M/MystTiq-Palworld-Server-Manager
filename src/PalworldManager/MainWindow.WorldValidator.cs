using System.Text;
using PalworldManager.Models;
using PalworldManager.Services;

namespace PalworldManager;

public partial class MainWindow
{
    private readonly WorldValidatorService worldValidatorService = new();
    private WorldValidatorReport? currentWorldValidatorReport;

    private void RefreshWorldValidator(bool forceRefresh = true)
    {
        try
        {
            WorldValidatorStatusText.Text = "Scanning the active world...";
            var snapshot = worldDiscovery.Current(forceRefresh);
            currentWorldValidatorReport = worldValidatorService.Validate(snapshot);
            WorldValidatorFindingsGrid.ItemsSource = currentWorldValidatorReport.Findings;

            WorldValidatorScoreText.Text = currentWorldValidatorReport.HealthScore + "%";
            WorldValidatorOverallText.Text = currentWorldValidatorReport.OverallStatus;
            WorldValidatorPlayersText.Text = currentWorldValidatorReport.PlayerCount.ToString();
            WorldValidatorGuildsText.Text = currentWorldValidatorReport.GuildCount.ToString();
            WorldValidatorBasesText.Text = currentWorldValidatorReport.BaseCount.ToString();
            WorldValidatorCriticalText.Text = currentWorldValidatorReport.CriticalCount.ToString();
            WorldValidatorWarningsText.Text = currentWorldValidatorReport.WarningCount.ToString();
            WorldValidatorRepairableText.Text = currentWorldValidatorReport.RepairableCount.ToString();
            WorldValidatorWorldText.Text = string.IsNullOrWhiteSpace(currentWorldValidatorReport.WorldPath)
                ? "No active world resolved"
                : currentWorldValidatorReport.WorldPath;
            WorldValidatorStatusText.Text =
                $"Validated {currentWorldValidatorReport.PlayerCount} player(s), {currentWorldValidatorReport.GuildCount} guild(s), and {currentWorldValidatorReport.BaseCount} base(s) in {currentWorldValidatorReport.Duration.TotalSeconds:0.###} sec.";

            WorldValidatorScoreText.Foreground = currentWorldValidatorReport.CriticalCount > 0
                ? new SolidColorBrush(Color.FromRgb(240, 91, 87))
                : currentWorldValidatorReport.WarningCount > 0
                    ? new SolidColorBrush(Color.FromRgb(230, 197, 107))
                    : new SolidColorBrush(Color.FromRgb(72, 201, 117));
        }
        catch (Exception ex)
        {
            WorldValidatorStatusText.Text = "World validation failed: " + ex.Message;
            WorldValidatorFindingsGrid.ItemsSource = null;
        }
    }

    private void WorldValidatorRefresh_Click(object sender, RoutedEventArgs e) => RefreshWorldValidator(forceRefresh: true);

    private void WorldValidatorExport_Click(object sender, RoutedEventArgs e)
    {
        if (currentWorldValidatorReport is null)
        {
            AppDialog.Show("Run World Validator before exporting a report.", "World Validator", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export World Validation Report",
            Filter = "Text report (*.txt)|*.txt|CSV report (*.csv)|*.csv",
            FileName = $"MystTiq_WorldValidation_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };
        if (dialog.ShowDialog(this) != true) return;

        if (Path.GetExtension(dialog.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = new StringBuilder("Category,Check,Severity,Message,EntityId,RecommendedAction,RepairAvailable\r\n");
            foreach (var row in currentWorldValidatorReport.Findings)
                csv.AppendLine(string.Join(',', Csv(row.Category), Csv(row.Check), Csv(row.Status), Csv(row.Message), Csv(row.EntityId), Csv(row.RecommendedAction), row.RepairAvailable));
            File.WriteAllText(dialog.FileName, csv.ToString());
        }
        else
        {
            File.WriteAllText(dialog.FileName, BuildWorldValidatorText(currentWorldValidatorReport));
        }

        WorldValidatorStatusText.Text = "Validation report exported to " + dialog.FileName;
    }

    private void WorldValidatorOpenRepair_Click(object sender, RoutedEventArgs e)
    {
        if (currentWorldValidatorReport is null || currentWorldValidatorReport.RepairableCount == 0)
        {
            AppDialog.Show("No repairable validation findings are currently available.", "World Validator", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        NavigateToPage(MainPageIndex.Recovery);
        RefreshPlayerRecovery();
        RefreshGuildBaseRecovery();
    }

    private void WorldValidatorSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (WorldValidatorFindingsGrid.SelectedItem is not WorldValidationFindingRow row)
        {
            WorldValidatorDetailText.Text = "Select a validation finding to review its evidence and recommended action.";
            return;
        }

        WorldValidatorDetailText.Text =
            $"{row.Status.ToUpperInvariant()} — {row.Category} / {row.Check}\n\n" +
            row.Message +
            (string.IsNullOrWhiteSpace(row.EntityId) ? "" : $"\n\nEntity ID\n{row.EntityId}") +
            $"\n\nRecommended action\n{row.RecommendedAction}" +
            $"\n\nRepair transaction available: {(row.RepairAvailable ? "Yes" : "No")}";
    }

    private static string BuildWorldValidatorText(WorldValidatorReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("=== MystTiq World Validator ===");
        builder.AppendLine($"Generated: {DateTime.Now:O}");
        builder.AppendLine($"World: {report.WorldId}");
        builder.AppendLine($"Path: {report.WorldPath}");
        builder.AppendLine($"Health: {report.HealthScore}% ({report.OverallStatus})");
        builder.AppendLine($"Players: {report.PlayerCount}  Guilds: {report.GuildCount}  Bases: {report.BaseCount}");
        builder.AppendLine($"Critical: {report.CriticalCount}  Warnings: {report.WarningCount}  Repairable: {report.RepairableCount}");
        builder.AppendLine();
        foreach (var finding in report.Findings)
        {
            builder.AppendLine($"[{finding.Status}] {finding.Category} / {finding.Check}");
            builder.AppendLine(finding.Message);
            if (!string.IsNullOrWhiteSpace(finding.EntityId)) builder.AppendLine("Entity: " + finding.EntityId);
            builder.AppendLine("Action: " + finding.RecommendedAction);
            builder.AppendLine("Repair available: " + (finding.RepairAvailable ? "Yes" : "No"));
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static string Csv(string? value) => '"' + (value ?? string.Empty).Replace("\"", "\"\"") + '"';
}
