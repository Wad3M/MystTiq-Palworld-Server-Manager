using PalworldManager.Models;
using PalworldManager.Services;

namespace PalworldManager;

public partial class MainWindow
{
    private readonly ObservableCollection<DiagnosticResultRow> diagnosticsRows = [];
    private DiagnosticsSnapshot? lastDiagnosticsSnapshot;
    private CancellationTokenSource? diagnosticsCts;

    private void InitializeDiagnosticsCenter()
    {
        DiagnosticsGrid.ItemsSource = diagnosticsRows;
        RefreshDiagnosticsSummary(null);
    }

    private async void RunFullDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        if (diagnosticsCts is not null) return;
        diagnosticsCts = new CancellationTokenSource();
        DiagnosticsRunButton.IsEnabled = false;
        DiagnosticsExportButton.IsEnabled = false;
        DiagnosticsSupportButton.IsEnabled = false;
        DiagnosticsProgressBar.IsIndeterminate = true;
        DiagnosticsStatusText.Text = "Starting full diagnostics...";
        diagnosticsRows.Clear();
        try
        {
            var progress = new Progress<string>(message => DiagnosticsStatusText.Text = message);
            var service = new DiagnosticsService(settings);
            lastDiagnosticsSnapshot = await service.RunAllAsync(progress, diagnosticsCts.Token);
            foreach (var row in lastDiagnosticsSnapshot.Results) diagnosticsRows.Add(row);
            RefreshDiagnosticsSummary(lastDiagnosticsSnapshot);
            DiagnosticsExportButton.IsEnabled = true;
            DiagnosticsSupportButton.IsEnabled = true;
            Log($"[DIAGNOSTICS] Completed with score {lastDiagnosticsSnapshot.Score}%: {lastDiagnosticsSnapshot.Passed} passed, {lastDiagnosticsSnapshot.Warnings} warnings, {lastDiagnosticsSnapshot.Failed} failed.");
        }
        catch (OperationCanceledException)
        {
            DiagnosticsStatusText.Text = "Diagnostics were cancelled.";
        }
        catch (Exception ex)
        {
            DiagnosticsStatusText.Text = $"Diagnostics failed: {ex.Message}";
            Log($"[DIAGNOSTICS] Failed: {ex.Message}");
            AppDialog.Show(ex.Message, "Diagnostics Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            DiagnosticsProgressBar.IsIndeterminate = false;
            DiagnosticsRunButton.IsEnabled = true;
            diagnosticsCts.Dispose();
            diagnosticsCts = null;
        }
    }

    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        if (lastDiagnosticsSnapshot is null) return;
        try
        {
            var exported = new DiagnosticsService(settings).Export(lastDiagnosticsSnapshot);
            DiagnosticsStatusText.Text = $"Exported diagnostics to {exported.TextPath}";
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{exported.TextPath}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "Export Diagnostics Failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void CreateSupportPackage_Click(object sender, RoutedEventArgs e)
    {
        if (lastDiagnosticsSnapshot is null) return;
        try
        {
            var path = new DiagnosticsService(settings).CreateSupportPackage(lastDiagnosticsSnapshot);
            DiagnosticsStatusText.Text = $"Support package created: {path}";
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "Support Package Failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void CopyDiagnosticsSummary_Click(object sender, RoutedEventArgs e)
    {
        if (lastDiagnosticsSnapshot is null) return;
        Clipboard.SetText(DiagnosticsService.BuildTextReport(lastDiagnosticsSnapshot));
        DiagnosticsStatusText.Text = "Diagnostics summary copied to the clipboard.";
    }

    private void OpenDiagnosticsCenterFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = ApplicationPathService.Current.DiagnosticsRoot;
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private void RefreshDiagnosticsSummary(DiagnosticsSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            DiagnosticsScoreText.Text = "—";
            DiagnosticsHealthText.Text = "Not Run";
            DiagnosticsPassedText.Text = "0";
            DiagnosticsWarningText.Text = "0";
            DiagnosticsFailedText.Text = "0";
            DiagnosticsLastRunText.Text = "Run full diagnostics to inspect the application, workspace, server, world, backups, MODs, transactions, and notifications.";
            return;
        }
        DiagnosticsScoreText.Text = $"{snapshot.Score}%";
        DiagnosticsHealthText.Text = snapshot.OverallStatus;
        DiagnosticsPassedText.Text = snapshot.Passed.ToString(CultureInfo.InvariantCulture);
        DiagnosticsWarningText.Text = snapshot.Warnings.ToString(CultureInfo.InvariantCulture);
        DiagnosticsFailedText.Text = snapshot.Failed.ToString(CultureInfo.InvariantCulture);
        DiagnosticsLastRunText.Text = $"Last run {snapshot.CompletedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}. Support packages redact protected credentials and include only recent, size-limited logs.";
    }
}
