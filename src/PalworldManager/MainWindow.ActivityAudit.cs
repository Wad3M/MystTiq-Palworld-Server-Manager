using PalworldManager.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Data;

namespace PalworldManager;

public partial class MainWindow
{
    private readonly ObservableCollection<AuditEntry> auditEntries = new();
    private ICollectionView? auditView;
    private static readonly string AuditFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        BrandingMigrationService.ProductFolder, "activity-audit.json");

    private void InitializeActivityAudit()
    {
        try
        {
            if (File.Exists(AuditFilePath))
            {
                var saved = JsonSerializer.Deserialize<List<AuditEntry>>(File.ReadAllText(AuditFilePath)) ?? new();
                foreach (var entry in saved.OrderByDescending(x => x.TimestampUtc).Take(2000)) auditEntries.Add(entry);
            }
        }
        catch { }
        auditView = CollectionViewSource.GetDefaultView(auditEntries);
        auditView.Filter = AuditFilter;
        AuditGrid.ItemsSource = auditView;
        var version = typeof(MainWindow).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion
            ?? typeof(MainWindow).Assembly.GetName().Version?.ToString()
            ?? "Unknown";
        RecordAudit("Information", "System", "Manager launched", $"MystTiq Palworld Server v{version} started.", 0);
        RefreshNotificationSummary();
    }

    private void RecordAudit(string severity, string category, string action, string details, int? pageIndex = null)
    {
        var entry = new AuditEntry { TimestampUtc = DateTime.UtcNow, Severity = severity, Category = category, Action = action, Details = details, PageIndex = pageIndex };
        auditEntries.Insert(0, entry);
        while (auditEntries.Count > 2000) auditEntries.RemoveAt(auditEntries.Count - 1);
        PersistAudit();
        auditView?.Refresh();

        // Activity remains the complete event record. Notification Center receives
        // operator-facing events without replacing or weakening audit history.
        if (severity is "Success" or "Warning" or "Critical")
            AddNotification(severity, category, action, details, pageIndex);

        RefreshNotificationSummary();
        RefreshDashboardActivityTicker();
    }

    private void PersistAudit()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AuditFilePath)!);
            File.WriteAllText(AuditFilePath, JsonSerializer.Serialize(auditEntries.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private bool AuditFilter(object item)
    {
        if (item is not AuditEntry e) return false;
        var q = AuditSearchBox?.Text?.Trim() ?? "";
        var sev = (AuditSeverityFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All severities";
        var cat = (AuditCategoryFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All categories";
        if (!sev.StartsWith("All") && !e.Severity.Equals(sev, StringComparison.OrdinalIgnoreCase)) return false;
        if (!cat.StartsWith("All") && !e.Category.Equals(cat, StringComparison.OrdinalIgnoreCase)) return false;
        return string.IsNullOrWhiteSpace(q) || $"{e.Severity} {e.Category} {e.Action} {e.Details}".Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void AuditFilter_Changed(object sender, EventArgs e)
    {
        // ComboBox/TextBox change events can fire while MainWindow.xaml is still being
        // constructed. At that point the view and status control may not exist yet.
        if (auditView is null || AuditStatusText is null) return;

        auditView.Refresh();
        AuditStatusText.Text = $"Showing {auditView.Cast<object>().Count()} of {auditEntries.Count} event(s).";
    }

    private void AuditRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (auditView is null || AuditStatusText is null) return;
        auditView.Refresh();
        AuditStatusText.Text = $"Refreshed {DateTime.Now:T}.";
    }
    private void AuditClear_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Clear the complete local activity and audit history?", "Clear audit history", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        auditEntries.Clear(); PersistAudit(); RefreshNotificationSummary(); if (AuditStatusText is not null) AuditStatusText.Text = "Audit history cleared.";
    }
    private void AuditExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "JSON file|*.json", FileName = $"MystAudit_{DateTime.Now:yyyyMMdd_HHmmss}.json" };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(auditEntries.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        if (AuditStatusText is not null) AuditStatusText.Text = $"Exported to {dlg.FileName}";
    }
    private void DashboardOpenActivity_Click(object sender, RoutedEventArgs e) => NavigateToPage(17);
    private void RefreshNotificationSummary()
    {
        RefreshNotificationCenterSummary();
    }


    public sealed class AuditEntry
    {
        public DateTime TimestampUtc { get; set; }
        public string Severity { get; set; } = "Information";
        public string Category { get; set; } = "System";
        public string Action { get; set; } = "Activity";
        public string Details { get; set; } = "";
        public int? PageIndex { get; set; }
        public string TimestampDisplay => TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }
}
