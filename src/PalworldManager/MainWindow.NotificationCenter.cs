using Microsoft.Win32;
using PalworldManager.Models;
using PalworldManager.Services;
using PalworldManager.Services.Infrastructure;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Data;

namespace PalworldManager;

public partial class MainWindow
{
    private readonly NotificationCenterService notificationService = new();
    private readonly ObservableCollection<NotificationEntry> notifications = [];
    private ICollectionView? notificationView;
    private bool notificationFlyoutWasOpenOnBellMouseDown;

    private void InitializeNotificationCenter()
    {
        foreach (var item in notificationService.Load()
                     .OrderByDescending(x => x.IsPinned)
                     .ThenByDescending(x => x.TimestampUtc))
        {
            notifications.Add(item);
        }

        notificationView = CollectionViewSource.GetDefaultView(notifications);
        notificationView.Filter = NotificationFilter;
        NotificationGrid.ItemsSource = notificationView;
        NotificationFlyoutList.ItemsSource = notifications;
        RefreshNotificationCenterSummary();
        UpdateNotificationBellVisual();
    }

    private void AddNotification(string severity, string category, string title, string message, int? pageIndex = null)
    {
        // Navigation noise and routine background polling should remain in Activity & Audit,
        // not flood the operator-facing Notification Center.
        if (category.Equals("Navigation", StringComparison.OrdinalIgnoreCase)) return;

        var entry = new NotificationEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Severity = NormalizeNotificationSeverity(severity),
            Category = category,
            Title = title,
            Message = message,
            PageIndex = pageIndex,
            IsRead = false
        };

        notifications.Insert(0, entry);
        while (notifications.Count > 100)
        {
            var removable = notifications.LastOrDefault(x => !x.IsPinned) ?? notifications[^1];
            notifications.Remove(removable);
        }

        PersistNotifications();
        notificationView?.Refresh();
        RefreshNotificationCenterSummary();
    }

    private static string NormalizeNotificationSeverity(string severity) => severity switch
    {
        "Critical" => "Critical",
        "Warning" => "Warning",
        "Success" => "Success",
        _ => "Information"
    };

    private void PersistNotifications()
    {
        try { notificationService.Save(notifications); }
        catch { }
    }

    private bool NotificationFilter(object item)
    {
        if (item is not NotificationEntry entry) return false;
        var query = NotificationSearchBox?.Text?.Trim() ?? "";
        var severity = (NotificationSeverityFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All severities";
        var state = (NotificationStateFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All notifications";

        if (!severity.StartsWith("All", StringComparison.OrdinalIgnoreCase) &&
            !entry.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase)) return false;
        if (state == "Unread only" && entry.IsRead) return false;
        if (state == "Pinned only" && !entry.IsPinned) return false;

        return string.IsNullOrWhiteSpace(query) ||
               $"{entry.Severity} {entry.Category} {entry.Title} {entry.Message}"
                   .Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void NotificationFilter_Changed(object sender, EventArgs e)
    {
        if (notificationView is null || NotificationStatusText is null) return;
        notificationView.Refresh();
        RefreshNotificationCenterSummary();
    }

    private void NotificationSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (NotificationGrid.SelectedItem is not NotificationEntry entry)
        {
            NotificationDetailText.Text = "Select a notification to review details.";
            return;
        }

        NotificationDetailText.Text =
            $"{entry.TimestampDisplay}\n" +
            $"{entry.Severity} • {entry.Category}\n\n" +
            $"{entry.Title}\n\n" +
            entry.Message;
    }

    private void DashboardOpenNotifications_Click(object sender, RoutedEventArgs e) => OpenNotificationFlyout();

    private void NotificationBell_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // A Popup with StaysOpen=False closes before the placement-target Click event fires.
        // Capture the pre-click state so clicking an open bell closes it instead of reopening it.
        notificationFlyoutWasOpenOnBellMouseDown = NotificationFlyout?.IsOpen == true;
    }

    private void NotificationBell_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (notifications.Count == 0)
        {
            CloseNotificationFlyout();
            return;
        }

        if (notificationFlyoutWasOpenOnBellMouseDown)
        {
            CloseNotificationFlyout();
        }
        else
        {
            OpenNotificationFlyout();
        }

        notificationFlyoutWasOpenOnBellMouseDown = false;
    }

    private void NotificationFlyout_Closed(object? sender, EventArgs e) => UpdateNotificationBellVisual();

    private void OpenNotificationFlyout()
    {
        if (notifications.Count == 0)
        {
            CloseNotificationFlyout();
            return;
        }

        NotificationFlyout.IsOpen = true;
        NotificationFlyoutList.Items.Refresh();
        RefreshNotificationCenterSummary();
        UpdateNotificationBellVisual();
    }

    private void CloseNotificationFlyout()
    {
        if (NotificationFlyout is not null)
            NotificationFlyout.IsOpen = false;

        notificationFlyoutWasOpenOnBellMouseDown = false;
        UpdateNotificationBellVisual();
    }

    private void UpdateNotificationBellVisual()
    {
        if (NotificationBellButton is null) return;

        NotificationBellButton.Visibility = notifications.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        var isOpen = NotificationFlyout?.IsOpen == true;
        NotificationBellButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
            isOpen ? "#245B83" : "#132334"));
        NotificationBellButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
            isOpen ? "#79C8FF" : "#31506C"));
        NotificationBellButton.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
            isOpen ? "#FFFFFF" : "#DCE8F5"));
        NotificationBellButton.ToolTip = isOpen ? "Close notifications" : "Show recent notifications";
    }

    private void NotificationFlyoutSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (NotificationFlyoutList.SelectedItem is not NotificationEntry entry) return;
        entry.IsRead = true;
        PersistNotifications();
        notificationView?.Refresh();
        RefreshNotificationCenterSummary();
        NotificationFlyoutList.SelectedItem = null;
    }

    private void NotificationFlyoutMarkAllRead_Click(object sender, RoutedEventArgs e)
    {
        foreach (var entry in notifications) entry.IsRead = true;
        PersistNotifications();
        notificationView?.Refresh();
        RefreshNotificationCenterSummary();
    }

    private void NotificationFlyoutClear_Click(object sender, RoutedEventArgs e)
    {
        var removable = notifications.Where(x => !x.IsPinned).ToList();
        foreach (var entry in removable) notifications.Remove(entry);
        PersistNotifications();
        notificationView?.Refresh();

        if (notifications.Count == 0)
            CloseNotificationFlyout();

        RefreshNotificationCenterSummary();
    }

    private void NotificationFlyoutActivity_Click(object sender, RoutedEventArgs e)
    {
        CloseNotificationFlyout();
        NavigateToPage(MainPageIndex.ActivityAudit);
    }

    private void NotificationRefresh_Click(object sender, RoutedEventArgs e)
    {
        notificationView?.Refresh();
        RefreshNotificationCenterSummary();
        NotificationStatusText.Text = $"Refreshed {DateTime.Now:T}.";
    }

    private void NotificationMarkRead_Click(object sender, RoutedEventArgs e)
    {
        if (NotificationGrid.SelectedItem is not NotificationEntry entry) return;
        entry.IsRead = true;
        PersistNotifications();
        notificationView?.Refresh();
        RefreshNotificationCenterSummary();
    }

    private void NotificationMarkAllRead_Click(object sender, RoutedEventArgs e)
    {
        foreach (var entry in notifications) entry.IsRead = true;
        PersistNotifications();
        notificationView?.Refresh();
        RefreshNotificationCenterSummary();
        NotificationStatusText.Text = "All notifications marked as read.";
    }

    private void NotificationPin_Click(object sender, RoutedEventArgs e)
    {
        if (NotificationGrid.SelectedItem is not NotificationEntry entry) return;
        entry.IsPinned = !entry.IsPinned;
        ReorderNotifications();
        PersistNotifications();
        notificationView?.Refresh();
        RefreshNotificationCenterSummary();
    }

    private void NotificationDismiss_Click(object sender, RoutedEventArgs e)
    {
        if (NotificationGrid.SelectedItem is not NotificationEntry entry) return;
        notifications.Remove(entry);
        PersistNotifications();
        notificationView?.Refresh();
        RefreshNotificationCenterSummary();
        NotificationDetailText.Text = "Notification dismissed.";
    }

    private void NotificationDismissRead_Click(object sender, RoutedEventArgs e)
    {
        var removable = notifications.Where(x => x.IsRead && !x.IsPinned).ToList();
        foreach (var entry in removable) notifications.Remove(entry);
        PersistNotifications();
        notificationView?.Refresh();
        RefreshNotificationCenterSummary();
        NotificationStatusText.Text = $"Dismissed {removable.Count} read notification(s). Pinned items were preserved.";
    }

    private void NotificationOpenRelated_Click(object sender, RoutedEventArgs e)
    {
        if (NotificationGrid.SelectedItem is not NotificationEntry entry || entry.PageIndex is null)
        {
            NotificationStatusText.Text = "The selected notification has no linked page.";
            return;
        }

        entry.IsRead = true;
        PersistNotifications();
        NavigateToPage(entry.PageIndex.Value);
    }

    private void NotificationExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON file|*.json",
            FileName = $"MystTiqNotifications_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };
        if (dialog.ShowDialog() != true) return;
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(notifications.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        NotificationStatusText.Text = $"Exported to {dialog.FileName}";
    }

    private void ReorderNotifications()
    {
        var ordered = notifications.OrderByDescending(x => x.IsPinned).ThenByDescending(x => x.TimestampUtc).ToList();
        notifications.Clear();
        foreach (var entry in ordered) notifications.Add(entry);
    }

    private void RefreshNotificationCenterSummary()
    {
        var unread = notifications.Count(x => !x.IsRead);
        var warnings = notifications.Count(x => !x.IsRead && x.Severity == "Warning");
        var critical = notifications.Count(x => !x.IsRead && x.Severity == "Critical");
        var pinned = notifications.Count(x => x.IsPinned);

        if (NotificationSummaryText is not null)
            NotificationSummaryText.Text = $"{unread} unread • {critical} critical • {warnings} warning • {pinned} pinned";
        if (NotificationStatusText is not null && notificationView is not null)
            NotificationStatusText.Text = $"Showing {notificationView.Cast<object>().Count()} of {notifications.Count} notification(s).";

        if (DashboardNotificationSummaryText is not null)
            DashboardNotificationSummaryText.Text = unread == 0 ? "No unread notifications" : $"{unread} unread notification(s)";
        if (DashboardNotificationDetailText is not null)
        {
            var important = notifications.FirstOrDefault(x => !x.IsRead && x.Severity is "Critical" or "Warning")
                            ?? notifications.FirstOrDefault(x => !x.IsRead)
                            ?? notifications.FirstOrDefault();
            DashboardNotificationDetailText.Text = important is null
                ? "Important manager events will appear here."
                : $"{important.TimestampDisplay} • {important.Category}: {important.Title}";
        }

        if (NotificationUnreadBadge is not null)
        {
            NotificationUnreadBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
            NotificationUnreadBadgeText.Text = unread > 99 ? "99+" : unread.ToString();
            NotificationUnreadBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                critical > 0 ? "#B53B3B" : warnings > 0 ? "#B37A18" : "#2E76B5"));
        }

        if (NotificationFlyoutSummaryText is not null)
            NotificationFlyoutSummaryText.Text = unread == 0
                ? $"{notifications.Count} recent notification(s) • all read"
                : $"{unread} unread • {critical} critical • {warnings} warning";

        if (notifications.Count == 0 && NotificationFlyout?.IsOpen == true)
            NotificationFlyout.IsOpen = false;

        UpdateNotificationBellVisual();
        if (NotificationBellButton is not null && NotificationFlyout?.IsOpen != true)
        {
            NotificationBellButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                critical > 0 ? "#D95A5A" : warnings > 0 ? "#D69A38" : unread > 0 ? "#4B91C8" : "#31506C"));
        }
    }
    private void HandleInfrastructureNotification(InfrastructureNotification notification)
    {
        void Apply() => AddNotification(
            notification.Level switch
            {
                NotificationLevel.Success => "Success",
                NotificationLevel.Warning => "Warning",
                NotificationLevel.Error => "Critical",
                _ => "Information"
            },
            notification.Category,
            notification.Title,
            notification.Message,
            notification.PageIndex);

        if (Dispatcher.CheckAccess()) Apply();
        else Dispatcher.BeginInvoke(new Action(Apply));
    }

    private void HandlePageOperationProgress(OperationProgress progress)
    {
        void Apply()
        {
            var percent = progress.Percent is null ? string.Empty : $" ({progress.Percent}%)";
            var display = progress.State switch
            {
                OperationState.Completed => $"{progress.OperationName} complete",
                OperationState.Cancelled => $"{progress.OperationName} cancelled",
                OperationState.Failed => $"{progress.OperationName} failed",
                _ => $"{progress.OperationName}: {progress.Step}{percent}"
            };

            if (CurrentOperationText is not null)
                CurrentOperationText.Text = display;
            if (UpdateStatusText is not null && progress.State != OperationState.Running)
                UpdateStatusText.Text = display;
        }

        if (Dispatcher.CheckAccess()) Apply();
        else Dispatcher.BeginInvoke(new Action(Apply));
    }

}

