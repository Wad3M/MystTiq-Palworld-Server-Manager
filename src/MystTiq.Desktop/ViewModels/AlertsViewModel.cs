// MystTiq v1.0.8.9: file reviewed for this release (2026-10-12).
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;
using MystTiq.Core.Services;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Windows.Input;

namespace MystTiq.Desktop.ViewModels;

/// <summary>
/// v1.0.8.7 (roadmap C-7): the Notifications, Alert Center, Automation and Activity pages, outside delivery and the Discord
/// bot -- moved out of MainWindowViewModel unchanged. The window shows them as vm.Alerts.
/// </summary>
public sealed class AlertsViewModel : PageViewModel
{
    private Action<string?> Navigate { get; }

    public AlertsViewModel(SessionContext session, Action<string?> navigate) : base(session, NavigationPage.AlertCenter)
    {
        Navigate = navigate;
        RefreshActivityCommand = new AsyncCommand(RefreshActivityAsync, () => !Session.IsBusy);
        RefreshNotificationsCommand = new AsyncCommand(RefreshNotificationsAsync, () => !Session.IsBusy);
        NotificationSelfTestCommand = new AsyncCommand(RunNotificationSelfTestAsync, () => !Session.IsBusy && Session.IsConnected);
        MarkAllNotificationsReadCommand = new AsyncCommand(MarkAllNotificationsReadAsync, () => !Session.IsBusy && Session.IsConnected);
        ToggleNotificationReadCommand = new AsyncCommand(ToggleNotificationReadAsync, () => !Session.IsBusy && SelectedNotification is not null);
        ToggleNotificationPinCommand = new AsyncCommand(ToggleNotificationPinAsync, () => !Session.IsBusy && SelectedNotification is not null);
        DismissNotificationCommand = new AsyncCommand(DismissNotificationAsync, () => !Session.IsBusy && SelectedNotification is not null);
        RefreshAutomationCommand = new AsyncCommand(RefreshAutomationAsync, () => !Session.IsBusy);
        CreateAutomationRuleCommand = new AsyncCommand(CreateAutomationRuleAsync, () => !Session.IsBusy && !string.IsNullOrWhiteSpace(NewAutomationRuleName));
        DeleteAutomationRuleCommand = new AsyncCommand(DeleteAutomationRuleAsync, () => !Session.IsBusy && SelectedAutomationRule is not null);
        ToggleAutomationRuleEnabledCommand = new AsyncCommand(ToggleAutomationRuleEnabledAsync, () => !Session.IsBusy && SelectedAutomationRule is not null);
        RunAutomationRuleNowCommand = new AsyncCommand(RunAutomationRuleNowAsync, () => !Session.IsBusy && SelectedAutomationRule is not null);
        RefreshAlertCenterCommand = new AsyncCommand(RefreshAlertCenterAsync, () => !Session.IsBusy);
        SaveAlertRulesCommand = new AsyncCommand(SaveAlertRulesAsync, () => !Session.IsBusy);
        MuteAlertsCommand = new RelayCommand<string>(m => { if (!Session.IsBusy && int.TryParse(m, out var minutes)) Session.RunUi(() => MuteAlertsAsync(minutes)); });
        PauseDeliveryCommand = new RelayCommand<string>(m => { if (!Session.IsBusy && int.TryParse(m, out var minutes)) Session.RunUi(() => PauseDeliveryAsync(minutes)); });
        SendTestNotificationCommand = new RelayCommand(() => { if (!Session.IsBusy) Session.RunUi(() => SendTestNotificationAsync()); });
        RefreshDiscordBotConfigCommand = new AsyncCommand(RefreshDiscordBotConfigAsync, () => !Session.IsBusy);
        SaveDiscordBotConfigCommand = new AsyncCommand(SaveDiscordBotConfigAsync, () => !Session.IsBusy);
        AddDiscordRoleMappingCommand = new RelayCommand(AddDiscordRoleMapping);
        RemoveDiscordRoleMappingCommand = new RelayCommand(RemoveDiscordRoleMapping);
        ToggleDiscordBotCommand = new RelayCommand(() => IsDiscordBotExpanded = !IsDiscordBotExpanded);
    }

    private string _activityState = "Not loaded";

    private string _activityFileText = "No activity log selected";

    private string _activityDetail = "Open Activity & Audit to load persistent manager events.";

    private readonly List<string> _allActivityLines = [];

    private string _activitySearchText = string.Empty;

    private string _activitySeverityFilter = "All";

    private string _activityCategoryFilter = "All";

    private string _notificationState = "Open Notifications to load persistent messages.";

    private string _notificationSearchText = string.Empty;

    private string _notificationSeverityFilter = "All";

    private NotificationItemDto? _selectedNotification;

    private string _automationState = "Open Automation to load scheduled rules.";

    private AutomationRuleDto? _selectedAutomationRule;

    private string _newAutomationRuleName = string.Empty;

    private string _newAutomationTriggerKind = "DailyTime";

    private string _newAutomationTimeOfDayUtc = "03:00";

    private int _newAutomationIntervalMinutes = 60;

    private int _newAutomationIdleThresholdMinutes = 30;

    private string _newAutomationActionKind = "CreateBackup";

    private string _newAutomationNotificationTitle = string.Empty;

    private string _newAutomationNotificationMessage = string.Empty;

    private string _newAutomationRconCommand = string.Empty;

    private string _alertCenterState = "Open Alert Center to load threshold rules.";

    private AlertRuleSetDto _alertRules = new();

    private string _discordBotState = "Open Alert Center to load Discord bot configuration.";

    private DiscordBotConfigurationDto _discordBotConfig = new();

    private bool _discordBotTokenConfigured;

    private string _discordBotConnectionState = "NotConfigured";

    private string _newRoleMappingDiscordRoleId = string.Empty;

    private string _newRoleMappingRole = "Viewer";

    private DiscordRoleMappingDto? _selectedRoleMapping;

    private bool _isDiscordBotExpanded;

    public ObservableCollection<string> ActivityLines { get; } = [];

    public ObservableCollection<NotificationItemDto> Notifications { get; } = [];

    public ObservableCollection<NotificationItemDto> FilteredNotifications { get; } = [];

    public ObservableCollection<AutomationRuleDto> AutomationRules { get; } = [];

    // v0.7.102.0: with no rules at all nothing is scheduled, including backups. The page says so and points at
    // the New Rule form below, whose defaults are already a nightly 03:00 backup.
    public bool HasNoAutomationRules => AutomationRules.Count == 0;

    public ObservableCollection<AutomationRunRecordDto> AutomationRuns { get; } = [];

    public IReadOnlyList<string> AutomationTriggerKinds { get; } = ["DailyTime", "Interval", "IdleEmpty"];

    public IReadOnlyList<string> AutomationActionKinds { get; } = ["CreateBackup", "StartServer", "StopServer", "RestartServer", "SendNotification", "SendRconCommand"];

    public string ActivityState { get => _activityState; private set => SetField(ref _activityState, value); }

    public IReadOnlyList<string> ActivitySeverityOptions { get; } = ["All", "Information", "Warning", "Error"];

    public IReadOnlyList<string> ActivityCategoryOptions { get; } = ["All", "API", "Backups", "Players", "MODs", "World Transaction", "Notifications"];

    public string ActivitySearchText { get => _activitySearchText; set { if (SetField(ref _activitySearchText, value ?? string.Empty)) ApplyActivityFilters(); } }

    public string ActivitySeverityFilter { get => _activitySeverityFilter; set { if (SetField(ref _activitySeverityFilter, value ?? "All")) ApplyActivityFilters(); } }

    public string ActivityCategoryFilter { get => _activityCategoryFilter; set { if (SetField(ref _activityCategoryFilter, value ?? "All")) ApplyActivityFilters(); } }

    public string NotificationState { get => _notificationState; private set => SetField(ref _notificationState, value); }

    public IReadOnlyList<string> NotificationSeverityOptions { get; } = ["All", "Information", "Success", "Warning", "Critical"];

    public string NotificationSearchText { get => _notificationSearchText; set { if (SetField(ref _notificationSearchText, value ?? string.Empty)) ApplyNotificationFilters(); } }

    public string NotificationSeverityFilter { get => _notificationSeverityFilter; set { if (SetField(ref _notificationSeverityFilter, value ?? "All")) ApplyNotificationFilters(); } }

    public NotificationItemDto? SelectedNotification { get => _selectedNotification; set { if (SetField(ref _selectedNotification, value)) { (ToggleNotificationReadCommand as AsyncCommand)?.RaiseCanExecuteChanged(); (ToggleNotificationPinCommand as AsyncCommand)?.RaiseCanExecuteChanged(); (DismissNotificationCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } } }

    public string NotificationBadgeText => Notifications.Count(x => !x.Read).ToString();

    public string AutomationState { get => _automationState; private set => SetField(ref _automationState, value); }

    public AutomationRuleDto? SelectedAutomationRule
    {
        get => _selectedAutomationRule;
        set { if (SetField(ref _selectedAutomationRule, value)) { (DeleteAutomationRuleCommand as AsyncCommand)?.RaiseCanExecuteChanged(); (ToggleAutomationRuleEnabledCommand as AsyncCommand)?.RaiseCanExecuteChanged(); (RunAutomationRuleNowCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    }

    public string NewAutomationRuleName { get => _newAutomationRuleName; set { if (SetField(ref _newAutomationRuleName, value ?? string.Empty)) (CreateAutomationRuleCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }

    public string NewAutomationTriggerKind { get => _newAutomationTriggerKind; set => SetField(ref _newAutomationTriggerKind, value ?? "DailyTime"); }

    public string NewAutomationTimeOfDayUtc { get => _newAutomationTimeOfDayUtc; set => SetField(ref _newAutomationTimeOfDayUtc, value ?? "03:00"); }

    public int NewAutomationIntervalMinutes { get => _newAutomationIntervalMinutes; set => SetField(ref _newAutomationIntervalMinutes, value); }

    public int NewAutomationIdleThresholdMinutes { get => _newAutomationIdleThresholdMinutes; set => SetField(ref _newAutomationIdleThresholdMinutes, value); }

    public string NewAutomationActionKind { get => _newAutomationActionKind; set => SetField(ref _newAutomationActionKind, value ?? "CreateBackup"); }

    public string NewAutomationNotificationTitle { get => _newAutomationNotificationTitle; set => SetField(ref _newAutomationNotificationTitle, value ?? string.Empty); }

    public string NewAutomationNotificationMessage { get => _newAutomationNotificationMessage; set => SetField(ref _newAutomationNotificationMessage, value ?? string.Empty); }

    public string NewAutomationRconCommand { get => _newAutomationRconCommand; set => SetField(ref _newAutomationRconCommand, value ?? string.Empty); }

    public string AlertCenterState { get => _alertCenterState; private set => SetField(ref _alertCenterState, value); }

    public AlertRuleSetDto AlertRules { get => _alertRules; set => SetField(ref _alertRules, value ?? new()); }

    public string DiscordBotState { get => _discordBotState; private set => SetField(ref _discordBotState, value); }

    public DiscordBotConfigurationDto DiscordBotConfig { get => _discordBotConfig; set => SetField(ref _discordBotConfig, value ?? new()); }

    public bool DiscordBotTokenConfigured { get => _discordBotTokenConfigured; private set { if (SetField(ref _discordBotTokenConfigured, value)) RaisePropertyChanged(nameof(DiscordBotTokenWatermark)); } }

    public string DiscordBotTokenWatermark => DiscordBotTokenConfigured ? "Token configured -- leave blank to keep it" : "Paste bot token from the Discord Developer Portal";

    public string DiscordBotConnectionState { get => _discordBotConnectionState; private set => SetField(ref _discordBotConnectionState, value); }

    public ObservableCollection<DiscordRoleMappingDto> DiscordRoleMappings { get; } = [];

    public string NewRoleMappingDiscordRoleId { get => _newRoleMappingDiscordRoleId; set => SetField(ref _newRoleMappingDiscordRoleId, value ?? string.Empty); }

    public string NewRoleMappingRole { get => _newRoleMappingRole; set => SetField(ref _newRoleMappingRole, value ?? "Viewer"); }

    public DiscordRoleMappingDto? SelectedRoleMapping { get => _selectedRoleMapping; set => SetField(ref _selectedRoleMapping, value); }

    public string ActivityFileText { get => _activityFileText; private set => SetField(ref _activityFileText, value); }

    public string ActivityDetail { get => _activityDetail; private set => SetField(ref _activityDetail, value); }

    public ICommand RefreshActivityCommand { get; }

    public ICommand RefreshNotificationsCommand { get; }

    public ICommand NotificationSelfTestCommand { get; }

    public ICommand MarkAllNotificationsReadCommand { get; }

    public ICommand ToggleNotificationReadCommand { get; }

    public ICommand ToggleNotificationPinCommand { get; }

    public ICommand DismissNotificationCommand { get; }

    public ICommand RefreshAutomationCommand { get; }

    public ICommand CreateAutomationRuleCommand { get; }

    public ICommand DeleteAutomationRuleCommand { get; }

    public ICommand ToggleAutomationRuleEnabledCommand { get; }

    public ICommand RunAutomationRuleNowCommand { get; }

    public ICommand RefreshAlertCenterCommand { get; }

    public ICommand SaveAlertRulesCommand { get; }

    public ICommand MuteAlertsCommand { get; }

    public ICommand PauseDeliveryCommand { get; }

    public ICommand SendTestNotificationCommand { get; }

    private NotificationDeliveryStateDto _notificationDelivery = new();

    public NotificationDeliveryStateDto NotificationDelivery { get => _notificationDelivery; private set => SetField(ref _notificationDelivery, value); }

    public ICommand RefreshDiscordBotConfigCommand { get; }

    public ICommand SaveDiscordBotConfigCommand { get; }

    public ICommand AddDiscordRoleMappingCommand { get; }

    public ICommand RemoveDiscordRoleMappingCommand { get; }

    public ICommand ToggleDiscordBotCommand { get; }

    public bool IsDiscordBotExpanded { get => _isDiscordBotExpanded; private set => SetField(ref _isDiscordBotExpanded, value); }

    internal async Task RefreshActivityAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { ActivityDetail = ex.Message; return; }
        Session.IsBusy = true;
        ActivityState = "Loading…";
        try
        {
            var snapshot = await Session.Api.GetActivityLogTailAsync(profile, 250, Session.BearerToken);
            _allActivityLines.Clear();
            _allActivityLines.AddRange(snapshot.Lines);
            ApplyActivityFilters();
            ActivityFileText = snapshot.FileName;
            if (!snapshot.Available) ActivityState = "Unavailable";
            ActivityDetail = snapshot.Detail;
        }
        catch (Exception ex)
        {
            ActivityState = "Unavailable";
            ActivityDetail = ex.Message;
        }
        finally { Session.IsBusy = false; }
    }

    private void ApplyActivityFilters()
    {
        var query = ActivitySearchText.Trim();
        ActivityLines.Clear();
        foreach (var line in _allActivityLines.Where(line =>
                     (string.IsNullOrWhiteSpace(query) || line.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
                     (ActivitySeverityFilter == "All" || line.Contains($"[{ActivitySeverityFilter}]", StringComparison.OrdinalIgnoreCase) ||
                      (ActivitySeverityFilter == "Error" && line.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase))) &&
                     (ActivityCategoryFilter == "All" || line.Contains($"[{ActivityCategoryFilter}]", StringComparison.OrdinalIgnoreCase))))
            ActivityLines.Add(line);
        ActivityState = $"{ActivityLines.Count} visible / {_allActivityLines.Count} event(s)";
    }

    internal async Task RefreshNotificationsAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try { ApplyNotificationSnapshot(await Session.Api.GetNotificationsAsync(Session.SelectedProfile, Session.BearerToken)); }
        catch (Exception ex) { NotificationState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task RunNotificationSelfTestAsync() => await MutateNotificationsAsync(() => Session.Api.RunNotificationSelfTestAsync(Session.SelectedProfile!, Session.BearerToken));

    private async Task MarkAllNotificationsReadAsync() => await MutateNotificationsAsync(() => Session.Api.MarkAllNotificationsReadAsync(Session.SelectedProfile!, Session.BearerToken));

    private async Task ToggleNotificationReadAsync() { if (SelectedNotification is { } item) await MutateNotificationsAsync(() => Session.Api.SetNotificationReadAsync(Session.SelectedProfile!, item.Id, !item.Read, Session.BearerToken)); }

    private async Task ToggleNotificationPinAsync() { if (SelectedNotification is { } item) await MutateNotificationsAsync(() => Session.Api.SetNotificationPinnedAsync(Session.SelectedProfile!, item.Id, !item.Pinned, Session.BearerToken)); }

    private async Task DismissNotificationAsync() { if (SelectedNotification is { } item) await MutateNotificationsAsync(() => Session.Api.DismissNotificationAsync(Session.SelectedProfile!, item.Id, Session.BearerToken)); }

    private async Task MutateNotificationsAsync(Func<Task<NotificationSnapshotDto>> operation)
    {
        if (Session.SelectedProfile is null) return; Session.IsBusy = true;
        try { ApplyNotificationSnapshot(await operation()); }
        catch (Exception ex) { NotificationState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private void ApplyNotificationSnapshot(NotificationSnapshotDto snapshot)
    {
        var selectedId = SelectedNotification?.Id;
        Notifications.Clear(); foreach (var item in snapshot.Items) Notifications.Add(item);
        ApplyNotificationFilters();
        SelectedNotification = FilteredNotifications.FirstOrDefault(x => x.Id == selectedId) ?? FilteredNotifications.FirstOrDefault();
        NotificationState = snapshot.Detail;
        RaisePropertyChanged(nameof(NotificationBadgeText));
    }

    private void ApplyNotificationFilters()
    {
        var query = NotificationSearchText.Trim(); var selectedId = SelectedNotification?.Id;
        FilteredNotifications.Clear();
        foreach (var item in Notifications.Where(x =>
                     (NotificationSeverityFilter == "All" || x.Severity.Equals(NotificationSeverityFilter, StringComparison.OrdinalIgnoreCase)) &&
                     (string.IsNullOrWhiteSpace(query) || x.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Message.Contains(query, StringComparison.OrdinalIgnoreCase))))
            FilteredNotifications.Add(item);
        SelectedNotification = FilteredNotifications.FirstOrDefault(x => x.Id == selectedId) ?? FilteredNotifications.FirstOrDefault();
    }

    public string ExportVisibleActivity() => string.Join(Environment.NewLine, ActivityLines);

    public string ExportVisibleNotifications() => string.Join(Environment.NewLine, FilteredNotifications.Select(x => $"[{x.CreatedUtc:O}] [{x.Severity}] [{(x.Read ? "READ" : "UNREAD")}] [{(x.Pinned ? "PINNED" : "UNPINNED")}] {x.Title} — {x.Message}"));

    internal async Task RefreshAutomationAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var rules = await Session.Api.GetAutomationRulesAsync(Session.SelectedProfile, Session.BearerToken);
            var selectedId = SelectedAutomationRule?.Id;
            AutomationRules.Clear(); foreach (var rule in rules) AutomationRules.Add(rule);
            SelectedAutomationRule = AutomationRules.FirstOrDefault(x => x.Id == selectedId) ?? AutomationRules.FirstOrDefault();

            var runs = await Session.Api.GetAutomationRunsAsync(Session.SelectedProfile, 100, Session.BearerToken);
            AutomationRuns.Clear(); foreach (var run in runs) AutomationRuns.Add(run);
            AutomationState = $"{AutomationRules.Count} rule(s), {AutomationRuns.Count} recent run(s).";
        }
        catch (Exception ex) { AutomationState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task CreateAutomationRuleAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var request = BuildAutomationRuleRequest();
            var rule = await Session.Api.CreateAutomationRuleAsync(Session.SelectedProfile, request, Session.BearerToken);
            AutomationRules.Add(rule);
            SelectedAutomationRule = rule;
            NewAutomationRuleName = string.Empty;
            AutomationState = $"Created rule '{rule.Name}'.";
        }
        catch (Exception ex) { AutomationState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private AutomationRuleRequestDto BuildAutomationRuleRequest()
    {
        var trigger = new AutomationTriggerDto { Kind = NewAutomationTriggerKind };
        if (NewAutomationTriggerKind == "Interval")
            trigger.Interval = TimeSpan.FromMinutes(Math.Max(1, NewAutomationIntervalMinutes));
        else if (NewAutomationTriggerKind == "IdleEmpty")
            trigger.IdleThresholdMinutes = Math.Max(1, NewAutomationIdleThresholdMinutes);
        else
            trigger.TimeOfDayUtc = TimeOnly.TryParse(NewAutomationTimeOfDayUtc, out var time) ? time : new TimeOnly(3, 0);

        var action = new AutomationActionDto { Kind = NewAutomationActionKind };
        if (NewAutomationActionKind == "SendNotification")
        {
            action.NotificationTitle = string.IsNullOrWhiteSpace(NewAutomationNotificationTitle) ? NewAutomationRuleName : NewAutomationNotificationTitle;
            action.NotificationMessage = NewAutomationNotificationMessage;
        }
        else if (NewAutomationActionKind == "SendRconCommand")
        {
            action.RconCommand = NewAutomationRconCommand;
        }

        return new AutomationRuleRequestDto(NewAutomationRuleName, trigger, new AutomationConditionDto(), action);
    }

    private async Task DeleteAutomationRuleAsync()
    {
        if (Session.SelectedProfile is null || SelectedAutomationRule is not { } rule) return;
        Session.IsBusy = true;
        try
        {
            await Session.Api.DeleteAutomationRuleAsync(Session.SelectedProfile, rule.Id, Session.BearerToken);
            AutomationRules.Remove(rule);
            SelectedAutomationRule = AutomationRules.FirstOrDefault();
            AutomationState = $"Deleted rule '{rule.Name}'.";
        }
        catch (Exception ex) { AutomationState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task ToggleAutomationRuleEnabledAsync()
    {
        if (Session.SelectedProfile is null || SelectedAutomationRule is not { } rule) return;
        Session.IsBusy = true;
        try
        {
            var updated = await Session.Api.SetAutomationRuleEnabledAsync(Session.SelectedProfile, rule.Id, !rule.Enabled, Session.BearerToken);
            var index = AutomationRules.IndexOf(rule);
            if (index >= 0) AutomationRules[index] = updated;
            SelectedAutomationRule = updated;
            AutomationState = $"Rule '{updated.Name}' is now {(updated.Enabled ? "enabled" : "disabled")}.";
        }
        catch (Exception ex) { AutomationState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task RunAutomationRuleNowAsync()
    {
        if (Session.SelectedProfile is null || SelectedAutomationRule is not { } rule) return;
        Session.IsBusy = true;
        try
        {
            await Session.Api.RunAutomationRuleNowAsync(Session.SelectedProfile, rule.Id, Session.BearerToken);
            AutomationState = $"Rule '{rule.Name}' dispatched. Refresh runs shortly to see the result.";
        }
        catch (Exception ex) { AutomationState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    internal async Task RefreshAlertCenterAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            AlertRules = await Session.Api.GetAlertRulesAsync(Session.SelectedProfile, Session.BearerToken);
            DiskSpacePrediction = await Session.Api.GetDiskSpacePredictionAsync(Session.SelectedProfile, Session.BearerToken);
            NotificationDelivery = await Session.Api.GetNotificationDeliveryAsync(Session.SelectedProfile, Session.BearerToken);
            await RefreshDeliveryHealthAsync(Session.SelectedProfile, null, force: true);
            AlertCenterState = "Alert rules and disk-space prediction loaded.";
        }
        catch (Exception ex) { AlertCenterState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task SaveAlertRulesAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            AlertRules = await Session.Api.SaveAlertRulesAsync(Session.SelectedProfile, AlertRules, Session.BearerToken);
            AlertCenterState = "Alert rules saved.";
        }
        catch (Exception ex) { AlertCenterState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task MuteAlertsAsync(int minutes)
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            AlertRules = await Session.Api.MuteAlertsAsync(Session.SelectedProfile, minutes, Session.BearerToken);
            AlertCenterState = minutes <= 0 ? "Alerts unmuted." : AlertRules.MuteStatusText;
        }
        catch (Exception ex) { AlertCenterState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task PauseDeliveryAsync(int minutes)
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            NotificationDelivery = await Session.Api.PauseNotificationDeliveryAsync(Session.SelectedProfile, minutes, Session.BearerToken);
            AlertCenterState = minutes <= 0 ? "Outside delivery resumed." : "Outside delivery paused: " + NotificationDelivery.StatusText;
        }
        catch (Exception ex) { AlertCenterState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task SendTestNotificationAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.SendTestNotificationAsync(Session.SelectedProfile, Session.BearerToken);
            NotificationDelivery = result.Delivery;
            AlertCenterState = result.Message;
        }
        catch (Exception ex) { AlertCenterState = ex.Message; }
        finally { Session.IsBusy = false; }
        // v1.0.2.0 (roadmap R-2): the sends run in the background (each tries twice, 10 s apiece); show their results.
        if (Session.SelectedProfile is { } profile)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            await RefreshDeliveryHealthAsync(profile, null, force: true);
        }
    }

    internal async Task RefreshDiscordBotConfigAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var view = await Session.Api.GetDiscordBotConfigAsync(Session.SelectedProfile, Session.BearerToken);
            DiscordBotConfig = new DiscordBotConfigurationDto
            {
                Enabled = view.Enabled,
                GuildId = view.GuildId,
                OwnerDiscordUserId = view.OwnerDiscordUserId,
                RoleMappings = view.RoleMappings,
                StatusChannelId = view.StatusChannelId,
                EventsChannelId = view.EventsChannelId,
                ShowPresence = view.ShowPresence
            };
            DiscordBotTokenConfigured = view.TokenConfigured;
            DiscordBotConnectionState = view.ConnectionState;
            DiscordRoleMappings.Clear();
            foreach (var mapping in view.RoleMappings) DiscordRoleMappings.Add(mapping);
            DiscordBotState = $"Discord bot configuration loaded. Connection: {view.ConnectionState}.";
        }
        catch (Exception ex) { DiscordBotState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task SaveDiscordBotConfigAsync()
    {
        if (Session.SelectedProfile is null) return;
        // v0.7.95.0: catch a mistyped channel id here with a plain reason instead of sending it.
        foreach (var (label, value) in new[] { ("Status channel", DiscordBotConfig.StatusChannelId), ("Events channel", DiscordBotConfig.EventsChannelId) })
        {
            if (!string.IsNullOrWhiteSpace(value) && !MystTiq.Core.Models.DiscordSnowflake.TryParse(value, out _))
            {
                DiscordBotState = $"{label} ID must be the 17-20 digit number Discord shows (enable Developer Mode, right-click the channel, Copy Channel ID), or leave it blank to turn that feature off.";
                return;
            }
        }

        Session.IsBusy = true;
        try
        {
            DiscordBotConfig.RoleMappings = [.. DiscordRoleMappings];
            var view = await Session.Api.SaveDiscordBotConfigAsync(Session.SelectedProfile, DiscordBotConfig, Session.BearerToken);
            DiscordBotTokenConfigured = view.TokenConfigured;
            DiscordBotConnectionState = view.ConnectionState;
            DiscordBotConfig.BotToken = null;
            DiscordBotState = "Discord bot configuration saved.";
        }
        catch (Exception ex) { DiscordBotState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private void AddDiscordRoleMapping()
    {
        if (string.IsNullOrWhiteSpace(NewRoleMappingDiscordRoleId)) return;
        DiscordRoleMappings.Add(new DiscordRoleMappingDto { DiscordRoleId = NewRoleMappingDiscordRoleId.Trim(), Role = NewRoleMappingRole });
        NewRoleMappingDiscordRoleId = string.Empty;
    }

    private void RemoveDiscordRoleMapping()
    {
        if (SelectedRoleMapping is null) return;
        DiscordRoleMappings.Remove(SelectedRoleMapping);
        SelectedRoleMapping = null;
    }

    public DiskSpacePredictionDto? DiskSpacePrediction { get => _diskSpacePrediction; private set => SetField(ref _diskSpacePrediction, value); }

    private DiskSpacePredictionDto? _diskSpacePrediction;

    // v1.0.2.0 (roadmap R-2, alert delivery proof): which switched-on outside channel (Discord, email, webhook) has no
    // proven delivery, on the Dashboard (a warning) and in Alert Center (every channel and the latest sends).
    private NotificationDeliveryHealthDto _deliveryHealth = new();
    public NotificationDeliveryHealthDto DeliveryHealth { get => _deliveryHealth; private set => SetField(ref _deliveryHealth, value); }
    private DateTimeOffset _deliveryHealthFetchedAt = DateTimeOffset.MinValue;
    private string? _deliveryHealthProfileId;

    // At most once a minute from the status poll; at once from Alert Center and after a test send.
    internal async Task RefreshDeliveryHealthAsync(ConnectionProfile profile, object? requestTab, bool force)
    {
        if (!force && _deliveryHealthProfileId == profile.Id && DateTimeOffset.UtcNow - _deliveryHealthFetchedAt < TimeSpan.FromMinutes(1)) return;
        _deliveryHealthFetchedAt = DateTimeOffset.UtcNow;
        _deliveryHealthProfileId = profile.Id;
        try
        {
            var health = await Session.Api.GetNotificationDeliveryHealthAsync(profile, Session.BearerToken);
            if (requestTab is not null && !ReferenceEquals(requestTab, Session.ActiveTab)) return;
            DeliveryHealth = health;
        }
        catch { /* an older service has no delivery record: nothing is shown */ }
    }
}
