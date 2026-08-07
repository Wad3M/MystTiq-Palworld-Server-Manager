using PalworldManager.Models;

namespace PalworldManager;

public partial class MainWindow
{
    private PlayerHealthReport? currentPlayerHealthReport;

    private IReadOnlyList<PlayerRow> CurrentPlayerRows() => PlayersGrid.Items.Cast<object>().OfType<PlayerRow>().ToList();

    private void RefreshPlayerToolkit(PlayerRow player)
    {
        currentPlayerHealthReport = playerHealth.Analyze(player, CurrentPlayerRows());
        PlayerToolkitHealthText.Text = $"{currentPlayerHealthReport.Score}% — {currentPlayerHealthReport.OverallStatus}";
        PlayerToolkitSummaryText.Text = string.Join("\n", currentPlayerHealthReport.Checks.Select(c =>
            $"{(c.Status == "Healthy" ? "✓" : "⚠")} {c.Component}: {c.Status} ({c.Confidence})"));
        PlayerHealthChecksGrid.ItemsSource = currentPlayerHealthReport.Checks;
        PlayerRepairPreviewText.Text = currentPlayerHealthReport.RepairRecommendations.Count == 0
            ? "No repair recommendations."
            : string.Join("\n", currentPlayerHealthReport.RepairRecommendations.Select(r => "• " + r));
    }

    private void AnalyzePlayerHealth_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetAdministrationPlayer(out var player, out _)) return;
        RefreshPlayerToolkit(player);
        RecordAudit("Information", "Players", "Player health analyzed", player.Name + " • " + currentPlayerHealthReport?.Score + "%", 4);
    }

    private void ComparePlayers_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetAdministrationPlayer(out var source, out _)) return;
        var candidates = CurrentPlayerRows().Where(p => PlayerKey(p) != PlayerKey(source)).OrderBy(p => p.Name).ToList();
        if (candidates.Count == 0)
        {
            AppDialog.Show("No second known player is available for comparison.", "Player Comparison", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var combo = new ComboBox { ItemsSource = candidates, DisplayMemberPath = "Name", SelectedIndex = 0, Margin = new Thickness(0, 8, 0, 12) };
        var okay = new Button { Content = "COMPARE", IsDefault = true, MinWidth = 110 };
        var cancel = new Button { Content = "CANCEL", IsCancel = true, MinWidth = 110, Margin = new Thickness(0,0,8,0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel); buttons.Children.Add(okay);
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = "Compare " + source.Name + " with:", Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.Bold });
        panel.Children.Add(combo); panel.Children.Add(buttons);
        var chooser = new Window { Title = "MystTiq — Player Comparison", Owner = this, Width = 460, Height = 190, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = new SolidColorBrush(Color.FromRgb(11,17,24)), Content = panel };
        okay.Click += (_, _) => chooser.DialogResult = true;
        if (chooser.ShowDialog() != true || combo.SelectedItem is not PlayerRow destination) return;

        var rows = playerHealth.Compare(source, destination);
        var grid = new DataGrid { IsReadOnly = true, AutoGenerateColumns = true, ItemsSource = rows, Margin = new Thickness(12) };
        var result = new Window { Title = $"MystTiq — {source.Name} vs {destination.Name}", Owner = this, Width = 760, Height = 520, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(11,17,24)), Content = grid };
        result.ShowDialog();
        RecordAudit("Information", "Players", "Players compared", source.Name + " vs " + destination.Name, 4);
    }

    private void ExportPlayerReport_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetAdministrationPlayer(out var player, out var key)) return;
        currentPlayerHealthReport = playerHealth.Analyze(player, CurrentPlayerRows());
        var timeline = PlayerTimelineList.Items.Cast<object>().Select(x => x?.ToString() ?? "").Where(x => x.Length > 0).ToList();
        var folder = Path.Combine(settings.LogsRoot, "PlayerReports");
        var path = playerHealth.ExportHtml(player, currentPlayerHealthReport, playerAdministration.GetSummary(key), timeline, folder);
        RecordAudit("Success", "Players", "Player report exported", path, 4);
        AppDialog.Show("Player report created:\n\n" + path, "Export Player Report", MessageBoxButton.OK, MessageBoxImage.Information);
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    private void PreviewPlayerRepair_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetAdministrationPlayer(out var player, out _)) return;
        currentPlayerHealthReport = playerHealth.Analyze(player, CurrentPlayerRows());
        RefreshPlayerToolkit(player);
        var message = currentPlayerHealthReport.RepairRecommendations.Count == 0
            ? "No repairs are currently recommended."
            : "Recommended repairs:\n\n" + string.Join("\n", currentPlayerHealthReport.RepairRecommendations.Select(r => "• " + r)) +
              "\n\nThis release is preview-only. No save files will be modified.";
        AppDialog.Show(message, "Repair Preview", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
