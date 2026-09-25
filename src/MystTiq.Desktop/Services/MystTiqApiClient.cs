using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

public sealed class MystTiqApiClient : IMystTiqApiClient
{
    public async Task<ApiHealthDto> GetHealthAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var health = await client.GetFromJsonAsync<ApiHealthDto>("/healthz", cancellationToken);
        return health ?? throw new InvalidOperationException("MystTiq returned an empty health response.");
    }

    public async Task<StatusPollingDto> GetStatusPollingAsync(
        ConnectionProfile profile,
        int logLines = 120,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var count = Math.Clamp(logLines, 10, 500);
        var snapshot = await client.GetFromJsonAsync<StatusPollingDto>($"/api/v1/status/poll?lines={count}", cancellationToken);
        return snapshot ?? throw new InvalidOperationException("MystTiq returned an empty status-polling snapshot.");
    }

    public async Task<ServerStatusDto> GetStatusAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var status = await client.GetFromJsonAsync<ServerStatusDto>("/api/v1/status", cancellationToken);
        return status ?? throw new InvalidOperationException("MystTiq returned an empty status response.");
    }

    public async Task<ServiceStatusDto> GetServiceStatusAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var status = await client.GetFromJsonAsync<ServiceStatusDto>("/api/v1/service", cancellationToken);
        return status ?? throw new InvalidOperationException("MystTiq returned an empty service-status response.");
    }

    public Task<LifecycleOperationResultDto> StartServerAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default) =>
        RunLifecycleAsync(profile, bearerToken, "/api/v1/server/start", cancellationToken);

    public Task<LifecycleOperationResultDto> StopServerAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default) =>
        RunLifecycleAsync(profile, bearerToken, "/api/v1/server/stop", cancellationToken);

    public Task<LifecycleOperationResultDto> ForceStopServerAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default) =>
        RunLifecycleAsync(profile, bearerToken, "/api/v1/server/force-stop", cancellationToken);

    public Task<LifecycleOperationResultDto> RestartServerAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default) =>
        RunLifecycleAsync(profile, bearerToken, "/api/v1/server/restart", cancellationToken);

    public async Task<IReadOnlyList<ServerInstanceDto>> GetAllInstancesAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<List<ServerInstanceDto>>("/api/v1/server/instances", cancellationToken) ?? [];
    }

    public async Task<InstanceTerminationResultDto> TerminateInstanceAsync(
        ConnectionProfile profile,
        int processId,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync($"/api/v1/server/instances/{processId}/terminate", content: null, cancellationToken);
        return await ReadOperationAsync<InstanceTerminationResultDto>(response, cancellationToken);
    }

    public async Task<PlayersSnapshotDto> GetPlayersAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var snapshot = await client.GetFromJsonAsync<PlayersSnapshotDto>("/api/v1/players", cancellationToken);
        return snapshot ?? throw new InvalidOperationException("MystTiq returned an empty player snapshot.");
    }

    public async Task<LogTailSnapshotDto> GetLogTailAsync(
        ConnectionProfile profile,
        int lines = 120,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var count = Math.Clamp(lines, 10, 500);
        var snapshot = await client.GetFromJsonAsync<LogTailSnapshotDto>($"/api/v1/logs/tail?lines={count}", cancellationToken);
        return snapshot ?? throw new InvalidOperationException("MystTiq returned an empty log-tail snapshot.");
    }

    public async Task<ActivityLogSnapshotDto> GetActivityLogTailAsync(
        ConnectionProfile profile,
        int lines = 200,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<ActivityLogSnapshotDto>($"/api/v1/activity/tail?lines={Math.Clamp(lines, 10, 500)}", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty activity log snapshot.");
    }

    public async Task<NotificationSnapshotDto> GetNotificationsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<NotificationSnapshotDto>("/api/v1/notifications", cancellationToken) ?? throw new InvalidOperationException("MystTiq returned empty notification state."); }
    public async Task<NotificationSnapshotDto> RunNotificationSelfTestAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync("/api/v1/notifications/self-test", null, cancellationToken); return await ReadOperationAsync<NotificationSnapshotDto>(response, cancellationToken); }
    public async Task<NotificationSnapshotDto> MarkAllNotificationsReadAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync("/api/v1/notifications/mark-all-read", null, cancellationToken); return await ReadOperationAsync<NotificationSnapshotDto>(response, cancellationToken); }
    public async Task<NotificationSnapshotDto> SetNotificationReadAsync(ConnectionProfile profile, string id, bool value, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync($"/api/v1/notifications/{Uri.EscapeDataString(id)}/read", new NotificationFlagRequestDto(value), cancellationToken); return await ReadOperationAsync<NotificationSnapshotDto>(response, cancellationToken); }
    public async Task<NotificationSnapshotDto> SetNotificationPinnedAsync(ConnectionProfile profile, string id, bool value, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync($"/api/v1/notifications/{Uri.EscapeDataString(id)}/pin", new NotificationFlagRequestDto(value), cancellationToken); return await ReadOperationAsync<NotificationSnapshotDto>(response, cancellationToken); }
    public async Task<NotificationSnapshotDto> DismissNotificationAsync(ConnectionProfile profile, string id, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.DeleteAsync($"/api/v1/notifications/{Uri.EscapeDataString(id)}", cancellationToken); return await ReadOperationAsync<NotificationSnapshotDto>(response, cancellationToken); }

    public async Task<NotificationChannelConfigurationDto> GetNotificationChannelsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<NotificationChannelConfigurationDto>("/api/v1/notifications/channels", cancellationToken) ?? new(); }
    public async Task<NotificationChannelConfigurationDto> SaveNotificationChannelsAsync(ConnectionProfile profile, NotificationChannelConfigurationDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PutAsJsonAsync("/api/v1/notifications/channels", request, cancellationToken); return await ReadOperationAsync<NotificationChannelConfigurationDto>(response, cancellationToken); }
    public async Task<IReadOnlyList<NotificationTemplateDto>> GetNotificationTemplatesAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<List<NotificationTemplateDto>>("/api/v1/notifications/templates", cancellationToken) ?? []; }
    public async Task<IReadOnlyList<NotificationTemplateDto>> SaveNotificationTemplatesAsync(ConnectionProfile profile, List<NotificationTemplateDto> request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PutAsJsonAsync("/api/v1/notifications/templates", request, cancellationToken); return await ReadOperationAsync<List<NotificationTemplateDto>>(response, cancellationToken); }
    public async Task<DiscordBotConfigurationViewDto> GetDiscordBotConfigAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<DiscordBotConfigurationViewDto>("/api/v1/notifications/discord-bot", cancellationToken) ?? new(); }
    public async Task<DiscordBotConfigurationViewDto> SaveDiscordBotConfigAsync(ConnectionProfile profile, DiscordBotConfigurationDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PutAsJsonAsync("/api/v1/notifications/discord-bot", request, cancellationToken); return await ReadOperationAsync<DiscordBotConfigurationViewDto>(response, cancellationToken); }
    public async Task<AntiCheatRuleSetDto> GetAntiCheatRulesAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<AntiCheatRuleSetDto>("/api/v1/anticheat/rules", cancellationToken) ?? new(); }
    public async Task<AntiCheatRuleSetDto> SaveAntiCheatRulesAsync(ConnectionProfile profile, AntiCheatRuleSetDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PutAsJsonAsync("/api/v1/anticheat/rules", request, cancellationToken); return await ReadOperationAsync<AntiCheatRuleSetDto>(response, cancellationToken); }
    public async Task<IReadOnlyList<AntiCheatFindingDto>> GetAntiCheatFindingsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<List<AntiCheatFindingDto>>("/api/v1/anticheat/findings", cancellationToken) ?? []; }

    public async Task<IReadOnlyList<AutomationRuleDto>> GetAutomationRulesAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<List<AutomationRuleDto>>("/api/v1/automation/rules", cancellationToken) ?? []; }
    public async Task<AutomationRuleDto> CreateAutomationRuleAsync(ConnectionProfile profile, AutomationRuleRequestDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync("/api/v1/automation/rules", request, cancellationToken); return await ReadOperationAsync<AutomationRuleDto>(response, cancellationToken); }
    public async Task<AutomationRuleDto> UpdateAutomationRuleAsync(ConnectionProfile profile, string id, AutomationRuleRequestDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PutAsJsonAsync($"/api/v1/automation/rules/{Uri.EscapeDataString(id)}", request, cancellationToken); return await ReadOperationAsync<AutomationRuleDto>(response, cancellationToken); }
    public async Task DeleteAutomationRuleAsync(ConnectionProfile profile, string id, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.DeleteAsync($"/api/v1/automation/rules/{Uri.EscapeDataString(id)}", cancellationToken); response.EnsureSuccessStatusCode(); }
    public async Task<AutomationRuleDto> SetAutomationRuleEnabledAsync(ConnectionProfile profile, string id, bool enabled, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync($"/api/v1/automation/rules/{Uri.EscapeDataString(id)}/enabled", new AutomationEnabledRequestDto(enabled), cancellationToken); return await ReadOperationAsync<AutomationRuleDto>(response, cancellationToken); }
    public async Task RunAutomationRuleNowAsync(ConnectionProfile profile, string id, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync($"/api/v1/automation/rules/{Uri.EscapeDataString(id)}/run-now", null, cancellationToken); response.EnsureSuccessStatusCode(); }
    public async Task<IReadOnlyList<AutomationRunRecordDto>> GetAutomationRunsAsync(ConnectionProfile profile, int max = 100, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<List<AutomationRunRecordDto>>($"/api/v1/automation/runs?max={max}", cancellationToken) ?? []; }
    public async Task CancelAutomationRunAsync(ConnectionProfile profile, string id, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync($"/api/v1/automation/runs/{Uri.EscapeDataString(id)}/cancel", null, cancellationToken); response.EnsureSuccessStatusCode(); }

    public async Task<IReadOnlyList<MystTiqPrincipalDto>> GetPrincipalsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<List<MystTiqPrincipalDto>>("/api/v1/security/principals", cancellationToken) ?? []; }
    public async Task<HeadlessCreatePrincipalResultDto> CreatePrincipalAsync(ConnectionProfile profile, HeadlessCreatePrincipalRequestDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync("/api/v1/security/principals", request, cancellationToken); return await ReadOperationAsync<HeadlessCreatePrincipalResultDto>(response, cancellationToken); }
    public async Task RevokePrincipalAsync(ConnectionProfile profile, string id, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.DeleteAsync($"/api/v1/security/principals/{Uri.EscapeDataString(id)}", cancellationToken); response.EnsureSuccessStatusCode(); }
    // v0.7.114.0: named user accounts. Sign-in carries no token; a failed sign-in is a 401 with a readable body.
    public async Task<UserLoginResultDto> LoginAsync(ConnectionProfile profile, string username, string password, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, null); using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password }, cancellationToken); return await ReadOperationAsync<UserLoginResultDto>(response, cancellationToken); }
    public async Task LogoutAsync(ConnectionProfile profile, string? bearerToken, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync("/api/v1/auth/logout", null, cancellationToken); }
    public async Task<UserAccountResultDto> ChangeOwnPasswordAsync(ConnectionProfile profile, string currentPassword, string newPassword, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword, newPassword }, cancellationToken); return await ReadOperationAsync<UserAccountResultDto>(response, cancellationToken); }
    public async Task<IReadOnlyList<UserAccountDto>> GetUsersAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<List<UserAccountDto>>("/api/v1/security/users", cancellationToken) ?? []; }
    public async Task<UserAccountResultDto> CreateUserAsync(ConnectionProfile profile, string username, string displayName, string role, string password, string? scopedServerProfileId, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync("/api/v1/security/users", new { username, displayName, role, password, scopedServerProfileId }, cancellationToken); return await ReadOperationAsync<UserAccountResultDto>(response, cancellationToken); }
    public async Task<UserAccountResultDto> UpdateUserAsync(ConnectionProfile profile, string id, string displayName, string role, string? scopedServerProfileId, bool enabled, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PutAsJsonAsync($"/api/v1/security/users/{Uri.EscapeDataString(id)}", new { displayName, role, scopedServerProfileId, enabled }, cancellationToken); return await ReadOperationAsync<UserAccountResultDto>(response, cancellationToken); }
    public async Task<UserAccountResultDto> SetUserPasswordAsync(ConnectionProfile profile, string id, string password, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync($"/api/v1/security/users/{Uri.EscapeDataString(id)}/password", new { password }, cancellationToken); return await ReadOperationAsync<UserAccountResultDto>(response, cancellationToken); }
    public async Task<UserAccountResultDto> DeleteUserAsync(ConnectionProfile profile, string id, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.DeleteAsync($"/api/v1/security/users/{Uri.EscapeDataString(id)}", cancellationToken); return await ReadOperationAsync<UserAccountResultDto>(response, cancellationToken); }
    public async Task<MystTiqPrincipalDto> WhoAmIAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<MystTiqPrincipalDto>("/api/v1/security/whoami", cancellationToken) ?? throw new InvalidOperationException("MystTiq returned an empty principal."); }

    public async Task<CharacterMigrationPreviewDto> PreviewCharacterMigrationAsync(ConnectionProfile profile, CharacterMigrationPreviewRequestDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync("/api/v1/players/migration/preview", request, cancellationToken); return await ReadOperationAsync<CharacterMigrationPreviewDto>(response, cancellationToken); }
    public async Task<CharacterMigrationResultDto> ApplyCharacterMigrationAsync(ConnectionProfile profile, CharacterMigrationApplyRequestDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync("/api/v1/players/migration/apply", request, cancellationToken); return await ReadOperationAsync<CharacterMigrationResultDto>(response, cancellationToken); }
    public async Task<CharacterDispositionResultDto> DisposeSourceCharacterAsync(ConnectionProfile profile, string sourcePlayerId, CharacterDispositionRequestDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync($"/api/v1/players/migration/{Uri.EscapeDataString(sourcePlayerId)}/disposition", request, cancellationToken); return await ReadOperationAsync<CharacterDispositionResultDto>(response, cancellationToken); }

    public async Task<IReadOnlyList<ServerProfileSummaryDto>> GetServerProfilesAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<List<ServerProfileSummaryDto>>("/api/v1/servers", cancellationToken) ?? []; }
    public async Task<IReadOnlyList<FleetActionResultDto>> BackupAllAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync("/api/v1/fleet/backup-all", null, cancellationToken); return await ReadOperationAsync<List<FleetActionResultDto>>(response, cancellationToken); }
    public async Task<IReadOnlyList<FleetActionResultDto>> DoctorAllAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync("/api/v1/fleet/doctor-all", null, cancellationToken); return await ReadOperationAsync<List<FleetActionResultDto>>(response, cancellationToken); }
    public async Task<IReadOnlyList<FleetActionResultDto>> UpdateAllAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync("/api/v1/fleet/update-all", null, cancellationToken); return await ReadOperationAsync<List<FleetActionResultDto>>(response, cancellationToken); }

    public async Task<AlertRuleSetDto> GetAlertRulesAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<AlertRuleSetDto>("/api/v1/alerts/rules", cancellationToken) ?? new(); }
    public async Task<AlertRuleSetDto> SaveAlertRulesAsync(ConnectionProfile profile, AlertRuleSetDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PutAsJsonAsync("/api/v1/alerts/rules", request, cancellationToken); return await ReadOperationAsync<AlertRuleSetDto>(response, cancellationToken); }
    public async Task<AlertRuleSetDto> MuteAlertsAsync(ConnectionProfile profile, int minutes, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync("/api/v1/alerts/mute", new AlertMuteRequestDto { Minutes = minutes }, cancellationToken); return await ReadOperationAsync<AlertRuleSetDto>(response, cancellationToken); }
    // v0.8.4.0: outside delivery pause and a test notification through the normal path.
    public async Task<NotificationDeliveryStateDto> GetNotificationDeliveryAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<NotificationDeliveryStateDto>("/api/v1/notifications/delivery", cancellationToken) ?? new(); }
    public async Task<NotificationDeliveryStateDto> PauseNotificationDeliveryAsync(ConnectionProfile profile, int minutes, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync("/api/v1/notifications/delivery/pause", new NotificationDeliveryPauseRequestDto { Minutes = minutes }, cancellationToken); return await ReadOperationAsync<NotificationDeliveryStateDto>(response, cancellationToken); }
    public async Task<NotificationTestResultDto> SendTestNotificationAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync("/api/v1/notifications/test", null, cancellationToken); return await ReadOperationAsync<NotificationTestResultDto>(response, cancellationToken); }
    public async Task<DiskSpacePredictionDto> GetDiskSpacePredictionAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<DiskSpacePredictionDto>("/api/v1/alerts/predictions", cancellationToken) ?? throw new InvalidOperationException("MystTiq returned an empty disk-space prediction."); }

    public async Task<BackupClassificationEntryDto> SetBackupClassAsync(ConnectionProfile profile, string fileName, BackupSetClassRequestDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PutAsJsonAsync($"/api/v1/backups/{Uri.EscapeDataString(fileName)}/class", request, cancellationToken); return await ReadOperationAsync<BackupClassificationEntryDto>(response, cancellationToken); }

    public async Task<CrashAnalysisSnapshotDto> AnalyzeCrashesAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync("/api/v1/crash-analyzer/analyze", null, cancellationToken); return await ReadOperationAsync<CrashAnalysisSnapshotDto>(response, cancellationToken); }
    public async Task<IReadOnlyList<CrashAnalysisSnapshotDto>> GetCrashHistoryAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<List<CrashAnalysisSnapshotDto>>("/api/v1/crash-analyzer/history?maximum=50", cancellationToken) ?? []; }
    public async Task<SaveToolsDiagnosticsDto> GetSaveToolsDiagnosticsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<SaveToolsDiagnosticsDto>("/api/v1/save-tools/diagnostics", cancellationToken) ?? throw new InvalidOperationException("MystTiq returned empty Save Tools diagnostics."); }
    public async Task<SaveToolsDiagnosticsDto> RunSaveToolsSelfTestAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync("/api/v1/save-tools/self-test", null, cancellationToken); return await ReadOperationAsync<SaveToolsDiagnosticsDto>(response, cancellationToken); }
    public async Task<SaveFileInventoryDto> GetSaveFilesAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<SaveFileInventoryDto>("/api/v1/save-tools/files", cancellationToken) ?? throw new InvalidOperationException("MystTiq returned empty save inventory."); }

    public async Task<RconStatusDto> GetRconStatusAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<RconStatusDto>("/api/v1/rcon/status", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty RCON status response.");
    }

    public async Task<RconDoctorResultDto> RunRconDoctorAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.GetAsync("/api/v1/rcon/doctor", cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<RconDoctorResultDto>(cancellationToken);
        if (result is not null) return result;
        throw new HttpRequestException($"RCON doctor failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    public async Task<RconCommandResultDto> SendRconCommandAsync(ConnectionProfile profile, string command, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/rcon/command", new RconCommandRequestDto(command), cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<RconCommandResultDto>(cancellationToken);
        if (result is not null) return result;
        throw new HttpRequestException($"RCON command failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    public async Task<RconCommandResultDto> GetBanListAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.GetAsync("/api/v1/players/ban-list", cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<RconCommandResultDto>(cancellationToken);
        if (result is not null) return result;
        throw new HttpRequestException($"Ban list request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    public async Task<RconCommandResultDto> TeleportToMeAsync(ConnectionProfile profile, string playerId, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync($"/api/v1/players/{Uri.EscapeDataString(playerId)}/teleport-to-me", null, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<RconCommandResultDto>(cancellationToken);
        if (result is not null) return result;
        throw new HttpRequestException($"Teleport request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    public async Task<RconCommandResultDto> TeleportToPlayerAsync(ConnectionProfile profile, string playerId, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync($"/api/v1/players/{Uri.EscapeDataString(playerId)}/teleport-to-player", null, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<RconCommandResultDto>(cancellationToken);
        if (result is not null) return result;
        throw new HttpRequestException($"Teleport request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    public async Task<RconCommandResultDto> SaveWorldNowAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync("/api/v1/world/save-now", null, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<RconCommandResultDto>(cancellationToken);
        if (result is not null) return result;
        throw new HttpRequestException($"Save World Now failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    public async Task<WhitelistConfigDto> GetWhitelistAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<WhitelistConfigDto>("/api/v1/players/whitelist", cancellationToken) ?? new(); }
    public async Task<WhitelistConfigDto> SaveWhitelistAsync(ConnectionProfile profile, WhitelistConfigDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PutAsJsonAsync("/api/v1/players/whitelist", request, cancellationToken); return await ReadOperationAsync<WhitelistConfigDto>(response, cancellationToken); }

    // v0.8.17.0: the HOST tab. A refused or invalid policy comes back as a result with its message (400), or as a refusal.
    public async Task<HostPageSnapshotDto> GetHostAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<HostPageSnapshotDto>("/api/v1/host", cancellationToken) ?? new(); }
    public async Task<ResourcePolicySaveResultDto> SaveResourcePolicyAsync(ConnectionProfile profile, ResourcePolicyDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PutAsJsonAsync("/api/v1/resources/policy", request, cancellationToken); return await ReadOperationAsync<ResourcePolicySaveResultDto>(response, cancellationToken); }
    // v0.8.20.0: the machine's history.
    public async Task<HostHistoryDto> GetHostHistoryAsync(ConnectionProfile profile, double hours, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<HostHistoryDto>($"/api/v1/host/history?hours={hours.ToString(System.Globalization.CultureInfo.InvariantCulture)}", cancellationToken) ?? new(); }
    // v0.8.18.0: bandwidth. An invalid policy comes back as a result with its message (400).
    public async Task<NetworkPolicySaveResultDto> SaveNetworkPolicyAsync(ConnectionProfile profile, NetworkPolicyDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PutAsJsonAsync("/api/v1/network/policy", request, cancellationToken); return await ReadOperationAsync<NetworkPolicySaveResultDto>(response, cancellationToken); }

    // v0.7.94.0: starter kits. ReadOperationAsync returns the body even for 400/409, which is what the
    // caller needs: validation errors and delivery failures both come back as a normal result object.
    public async Task<KitSnapshotDto> GetKitsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<KitSnapshotDto>("/api/v1/players/kits", cancellationToken) ?? new(); }
    public async Task<KitSaveResultDto> SaveKitsAsync(ConnectionProfile profile, KitConfigDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PutAsJsonAsync("/api/v1/players/kits", request, cancellationToken); return await ReadOperationAsync<KitSaveResultDto>(response, cancellationToken); }
    public async Task<KitCommandResultDto> TestKitProviderAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync("/api/v1/players/kits/test-provider", null, cancellationToken); return await ReadOperationAsync<KitCommandResultDto>(response, cancellationToken); }
    // v0.8.3.0: the Give Item picker's item/Pal ids.
    public async Task<GameIdCatalogDto> GetGameIdCatalogAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<GameIdCatalogDto>("/api/v1/players/give/catalog", cancellationToken) ?? new(); }
    // v0.7.113.0: teleport points.
    public async Task<TeleportSnapshotDto> GetTeleportAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<TeleportSnapshotDto>("/api/v1/teleport", cancellationToken) ?? new(); }
    public async Task<TeleportSaveResultDto> SaveTeleportAsync(ConnectionProfile profile, TeleportConfigDto config, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PutAsJsonAsync("/api/v1/teleport", config, cancellationToken); return await ReadOperationAsync<TeleportSaveResultDto>(response, cancellationToken); }
    public async Task<TeleportActionResultDto> SendToTeleportPointAsync(ConnectionProfile profile, string pointName, string playerId, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync($"/api/v1/teleport/points/{Uri.EscapeDataString(pointName)}/send", new { playerId }, cancellationToken); return await ReadOperationAsync<TeleportActionResultDto>(response, cancellationToken); }
    public async Task<TeleportCaptureResultDto> CaptureTeleportPositionAsync(ConnectionProfile profile, string playerId, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync("/api/v1/teleport/capture", new { playerId }, cancellationToken); return await ReadOperationAsync<TeleportCaptureResultDto>(response, cancellationToken); }
    public async Task<KitGiveResultDto> GiveItemsAsync(ConnectionProfile profile, string playerId, IReadOnlyList<KitEntryDto> entries, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync($"/api/v1/players/{Uri.EscapeDataString(playerId)}/give", new { entries }, cancellationToken); return await ReadOperationAsync<KitGiveResultDto>(response, cancellationToken); }
    public async Task<KitGiveResultDto> GiveKitAsync(ConnectionProfile profile, string kitId, string playerId, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsJsonAsync($"/api/v1/players/kits/{Uri.EscapeDataString(kitId)}/give", new { playerId }, cancellationToken); return await ReadOperationAsync<KitGiveResultDto>(response, cancellationToken); }
    public async Task ForgetKitClaimsAsync(ConnectionProfile profile, string playerId, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); using var response = await client.DeleteAsync($"/api/v1/players/kits/claims/{Uri.EscapeDataString(playerId)}", cancellationToken); response.EnsureSuccessStatusCode(); }

    public async Task<TemporaryBanConfigDto> GetTemporaryBansAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    { using var client = BuildClient(profile, bearerToken); return await client.GetFromJsonAsync<TemporaryBanConfigDto>("/api/v1/players/temp-bans", cancellationToken) ?? new(); }

    public async Task<PlayerAdminActionResultDto> CreateTemporaryBanAsync(ConnectionProfile profile, string playerId, string playerName, string reason, double durationHours, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var payload = new { PlayerName = playerName, Reason = reason, DurationHours = durationHours };
        using var response = await client.PostAsJsonAsync($"/api/v1/players/{Uri.EscapeDataString(playerId)}/temp-ban", payload, cancellationToken);
        return await ReadOperationAsync<PlayerAdminActionResultDto>(response, cancellationToken);
    }

    public async Task<PlayerAdminActionResultDto> RunPlayerAdminActionAsync(
        ConnectionProfile profile,
        string playerId,
        string action,
        string? message = null,
        string? item = null,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var payload = new PlayerAdminActionRequestDto { Action = action, Message = message, Item = item };
        using var response = await client.PostAsJsonAsync($"/api/v1/players/{Uri.EscapeDataString(playerId)}/action", payload, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<PlayerAdminActionResultDto>(cancellationToken);
        if (result is not null) return result;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"Player administration request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {body}".Trim());
    }

    public async Task<IReadOnlyList<ProviderDescriptorDto>> GetPlayerModerationProvidersAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var providers = await client.GetFromJsonAsync<List<ProviderDescriptorDto>>("/api/v1/players/moderation/providers", cancellationToken);
        return providers ?? [];
    }

    public async Task<IReadOnlyList<PlayerRegistryRecordDto>> GetPlayerRegistryAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var records = await client.GetFromJsonAsync<List<PlayerRegistryRecordDto>>("/api/v1/players/registry", cancellationToken);
        return records ?? [];
    }

    public async Task<RuntimeMetricsSnapshotDto> GetMetricsAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var snapshot = await client.GetFromJsonAsync<RuntimeMetricsSnapshotDto>("/api/v1/metrics", cancellationToken);
        return snapshot ?? throw new InvalidOperationException("MystTiq returned an empty runtime-metrics snapshot.");
    }

    public async Task<HistoricalMetricsSnapshotDto> GetHistoricalMetricsAsync(
        ConnectionProfile profile,
        double hours = 1,
        int maxSamples = 600,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var safeHours = Math.Clamp(hours, 1d, 24d * 30d);
        var safeMax = Math.Clamp(maxSamples, 30, 1200);
        var snapshot = await client.GetFromJsonAsync<HistoricalMetricsSnapshotDto>(
            $"/api/v1/history?hours={safeHours.ToString(System.Globalization.CultureInfo.InvariantCulture)}&maxSamples={safeMax}",
            cancellationToken);
        return snapshot ?? throw new InvalidOperationException("MystTiq returned an empty historical-metrics snapshot.");
    }

    public async Task<DoctorReportDto> RunDoctorAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<DoctorReportDto>("/api/v1/doctor", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty Doctor report.");
    }

    public async Task<DiagnosticsReportDto> GetDiagnosticsReportAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<DiagnosticsReportDto>("/api/v1/diagnostics/report", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty diagnostics report.");
    }

    public async Task<DiagnosticFindingDto?> RecheckDiagnosticFindingAsync(ConnectionProfile profile, string id, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync($"/api/v1/diagnostics/{Uri.EscapeDataString(id)}/recheck", null, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await ReadOperationAsync<DiagnosticFindingDto>(response, cancellationToken);
    }

    public async Task<HeadlessDiagnosticFixResultDto> FixDiagnosticFindingAsync(ConnectionProfile profile, string id, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync($"/api/v1/diagnostics/{Uri.EscapeDataString(id)}/fix", null, cancellationToken);
        return await ReadOperationAsync<HeadlessDiagnosticFixResultDto>(response, cancellationToken);
    }

    public async Task<BackupInventoryDto> GetBackupsAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<BackupInventoryDto>("/api/v1/backups", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty backup inventory.");
    }

    public async Task<BackupOperationResultDto> CreateBackupAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync("/api/v1/backups/create", null, cancellationToken);
        return await ReadOperationAsync<BackupOperationResultDto>(response, cancellationToken);
    }

    public async Task<BackupOperationResultDto> DeleteBackupAsync(
        ConnectionProfile profile,
        string fileName,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.DeleteAsync(
            $"/api/v1/backups/{Uri.EscapeDataString(fileName)}",
            cancellationToken);
        return await ReadOperationAsync<BackupOperationResultDto>(response, cancellationToken);
    }

    public async Task<BackupOperationResultDto> RestoreBackupAsync(
        ConnectionProfile profile,
        string fileName,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync(
            $"/api/v1/backups/{Uri.EscapeDataString(fileName)}/restore",
            JsonContent.Create(new { confirmed = true }),
            cancellationToken);
        return await ReadOperationAsync<BackupOperationResultDto>(response, cancellationToken);
    }

    public async Task<BackupVerificationResultDto> VerifyBackupAsync(ConnectionProfile profile, string fileName, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync($"/api/v1/backups/{Uri.EscapeDataString(fileName)}/verify", null, cancellationToken);
        return await ReadOperationAsync<BackupVerificationResultDto>(response, cancellationToken);
    }

    public async Task<BackupVerificationBatchDto> VerifyAllBackupsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync("/api/v1/backups/verify-all", null, cancellationToken);
        return await ReadOperationAsync<BackupVerificationBatchDto>(response, cancellationToken);
    }

    public async Task<BackupRetentionPreviewDto> PreviewBackupRetentionAsync(ConnectionProfile profile, int keepLatest, int maxAgeDays, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/backups/retention/preview", new { keepLatest, maxAgeDays }, cancellationToken);
        return await ReadOperationAsync<BackupRetentionPreviewDto>(response, cancellationToken);
    }

    public async Task<BackupRetentionApplyResultDto> ApplyBackupRetentionAsync(ConnectionProfile profile, string token, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/backups/retention/apply", new { token }, cancellationToken);
        return await ReadOperationAsync<BackupRetentionApplyResultDto>(response, cancellationToken);
    }

    public async Task<EditableConfigurationDto> GetEditableConfigurationAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<EditableConfigurationDto>("/api/v1/config/editable", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty editable configuration.");
    }

    public async Task<PalworldConfigurationSnapshotDto> GetPalworldConfigurationAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<PalworldConfigurationSnapshotDto>("/api/v1/palworld/config", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty Palworld configuration snapshot.");
    }

    public async Task<PalworldConfigurationSaveResultDto> CreateDefaultPalworldConfigurationAsync(ConnectionProfile profile, PalworldDefaultConfigurationRequestDto request, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/palworld/config/defaults", request, cancellationToken);
        return await ReadOperationAsync<PalworldConfigurationSaveResultDto>(response, cancellationToken);
    }

    public async Task<PalworldConfigurationSaveResultDto> SavePalworldConfigurationAsync(ConnectionProfile profile, IReadOnlyList<PalworldSettingDto> settings, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PutAsJsonAsync("/api/v1/palworld/config", new { settings }, cancellationToken);
        return await ReadOperationAsync<PalworldConfigurationSaveResultDto>(response, cancellationToken);
    }

    public async Task<ConfigurationSaveResultDto> SaveEditableConfigurationAsync(
        ConnectionProfile profile,
        EditableConfigurationDto configuration,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);

        var payload = new
        {
            api = configuration.Api,
            lifecycle = configuration.Lifecycle,
            server = configuration.Server
        };

        using var response = await client.PutAsJsonAsync(
            "/api/v1/config/editable",
            payload,
            cancellationToken);

        return await ReadOperationAsync<ConfigurationSaveResultDto>(response, cancellationToken);
    }

    private static async Task<T> ReadOperationAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            var refusedBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (AccessRefusal.Describe(response.StatusCode, refusedBody) is { } refusal)
                throw new HttpRequestException(refusal, null, response.StatusCode);
            return System.Text.Json.JsonSerializer.Deserialize<T>(refusedBody, System.Text.Json.JsonSerializerOptions.Web)
                ?? throw new HttpRequestException($"MystTiq request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.", null, response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        if (result is not null)
            return result;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"MystTiq request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {body}".Trim());
    }

    public async Task<EnvironmentChecklistSnapshotDto> GetEnvironmentChecklistAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<EnvironmentChecklistSnapshotDto>(
            "/api/v1/server/environment", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty server environment checklist.");
    }

    public async Task<WorldCloneResultDto> CloneWorldAsync(
        ConnectionProfile profile,
        WorldCloneRequestDto request,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/server/clone", request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<WorldCloneResultDto>(cancellationToken);
        if (result is not null) return result;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"World clone request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {body}".Trim());
    }

    public async Task<AddFleetProfileResultDto> AddFleetProfileAsync(
        ConnectionProfile profile,
        AddFleetProfileRequestDto request,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/servers", request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<AddFleetProfileResultDto>(cancellationToken);
        if (result is not null) return result;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"Add server profile request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {body}".Trim());
    }

    public async Task<ServerDistributionStatusDto> GetServerDistributionStatusAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<ServerDistributionStatusDto>(
            "/api/v1/server/distribution",
            cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty server distribution status.");
    }

    public async Task<ServerDistributionPlanDto> GetServerDistributionPlanAsync(
        ConnectionProfile profile,
        bool validate = true,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<ServerDistributionPlanDto>(
            $"/api/v1/server/distribution/plan?validate={validate.ToString().ToLowerInvariant()}",
            cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty SteamCMD plan.");
    }

    public async Task<ServerDistributionOperationResultDto> UpdateServerDistributionAsync(
        ConnectionProfile profile,
        bool validate = true,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync(
            $"/api/v1/server/distribution/update?validate={validate.ToString().ToLowerInvariant()}",
            null,
            cancellationToken);

        return await ReadOperationAsync<ServerDistributionOperationResultDto>(
            response,
            cancellationToken);
    }

    public async Task<ComponentVersionSnapshotDto> GetComponentVersionsAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<ComponentVersionSnapshotDto>("/api/v1/update-center/components", cancellationToken)
            ?? new ComponentVersionSnapshotDto();
    }

    public async Task<ComponentUpdateResultDto> UpdatePipAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync("/api/v1/update-center/components/pip/update", null, cancellationToken);
        return await ReadOperationAsync<ComponentUpdateResultDto>(response, cancellationToken);
    }

    public async Task<Ue4ssReleaseCatalogDto> GetUe4ssReleaseCatalogAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<Ue4ssReleaseCatalogDto>("/api/v1/ue4ss/releases", cancellationToken)
            ?? new Ue4ssReleaseCatalogDto();
    }

    public async Task<Ue4ssInstallStatusDto> GetUe4ssInstallStatusAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<Ue4ssInstallStatusDto>("/api/v1/ue4ss/install/status", cancellationToken)
            ?? new Ue4ssInstallStatusDto();
    }

    public async Task<Ue4ssInstallPreviewDto?> PreviewUe4ssInstallAsync(
        ConnectionProfile profile,
        string source,
        string tagName,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/ue4ss/install/preview", new { source, tagName }, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await ReadOperationAsync<Ue4ssInstallPreviewDto>(response, cancellationToken);
    }

    public async Task<Ue4ssInstallResultDto> ApplyUe4ssInstallAsync(
        ConnectionProfile profile,
        string token,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/ue4ss/install/apply", new { token }, cancellationToken);
        return await ReadOperationAsync<Ue4ssInstallResultDto>(response, cancellationToken);
    }

    public async Task<Ue4ssInstallResultDto> RollbackUe4ssInstallAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync("/api/v1/ue4ss/install/rollback", null, cancellationToken);
        return await ReadOperationAsync<Ue4ssInstallResultDto>(response, cancellationToken);
    }

    public async Task<WorldExplorerSnapshotDto> GetWorldExplorerAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<WorldExplorerSnapshotDto>(
            "/api/v1/world/explorer",
            cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty World Explorer snapshot.");
    }

    public async Task<WorldValidationReportDto> ValidateActiveWorldAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<WorldValidationReportDto>("/api/v1/world/validate", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty world validation report.");
    }

    public async Task<IReadOnlyList<WorldTransactionJournalDto>> GetWorldTransactionsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<List<WorldTransactionJournalDto>>("/api/v1/world/transactions?maximum=100", cancellationToken) ?? [];
    }

    public async Task<WorldImportPreviewDto> AnalyzeWorldArchiveAsync(ConnectionProfile profile, string mode, Stream archive, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken); using var content = new StreamContent(archive);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        using var response = await client.PostAsync($"/api/v1/world/import/analyze?mode={Uri.EscapeDataString(mode)}", content, cancellationToken);
        return await ReadOperationAsync<WorldImportPreviewDto>(response, cancellationToken);
    }

    public async Task<WorldTransactionResultDto> ApplyWorldTransactionAsync(ConnectionProfile profile, string previewToken, bool confirmed, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/world/import/apply", new WorldTransactionApplyRequestDto(previewToken, confirmed), cancellationToken);
        return await ReadOperationAsync<WorldTransactionResultDto>(response, cancellationToken);
    }

    public async Task<PlayerGuildSnapshotDto> GetPlayerGuildExplorerAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<PlayerGuildSnapshotDto>(
            "/api/v1/world/players-guilds",
            cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty Player & Guild Explorer snapshot.");
    }

    public async Task<GuildOwnershipPreviewDto> PreviewGuildOwnershipAsync(ConnectionProfile profile, string operationType, string guildId, string playerId, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/guilds/ownership/preview", new { operationType, guildId, playerId }, cancellationToken);
        return await ReadOperationAsync<GuildOwnershipPreviewDto>(response, cancellationToken);
    }

    public async Task<GuildOwnershipResultDto> ApplyGuildOwnershipAsync(ConnectionProfile profile, string previewToken, bool confirmed, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/guilds/ownership/apply", new { previewToken, confirmed }, cancellationToken);
        return await ReadOperationAsync<GuildOwnershipResultDto>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<PalInstanceDto>> GetPalsAsync(ConnectionProfile profile, string? ownerPlayerId = null, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var query = string.IsNullOrWhiteSpace(ownerPlayerId) ? "" : $"?ownerPlayerId={Uri.EscapeDataString(ownerPlayerId)}";
        return await client.GetFromJsonAsync<IReadOnlyList<PalInstanceDto>>($"/api/v1/pals{query}", cancellationToken) ?? [];
    }

    public async Task<PalEditPreviewDto> PreviewPalEditAsync(ConnectionProfile profile, string instanceId, PalEditFieldChangesDto changes, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        // Explicit lowercase-first wire shape, matching the same convention every other request
        // body in this file uses (e.g. PreviewGuildOwnershipAsync's anonymous object) -- avoids
        // depending on an assumed camelCase naming policy for a PascalCase record type.
        var wireChanges = new
        {
            nickName = changes.NickName, level = changes.Level, rank = changes.Rank,
            talentHp = changes.TalentHp, talentShot = changes.TalentShot, talentDefense = changes.TalentDefense,
            gender = changes.Gender, isRarePal = changes.IsRarePal
        };
        using var response = await client.PostAsJsonAsync("/api/v1/pals/edit/preview", new { instanceId, changes = wireChanges }, cancellationToken);
        return await ReadOperationAsync<PalEditPreviewDto>(response, cancellationToken);
    }

    public async Task<PalEditResultDto> ApplyPalEditAsync(ConnectionProfile profile, string previewToken, bool confirmed, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/pals/edit/apply", new { previewToken, confirmed }, cancellationToken);
        return await ReadOperationAsync<PalEditResultDto>(response, cancellationToken);
    }

    public async Task<BaseOwnershipPreviewDto> PreviewBaseOwnershipTransferAsync(ConnectionProfile profile, string baseId, string targetGuildId, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/bases/ownership/preview", new { baseId, targetGuildId }, cancellationToken);
        return await ReadOperationAsync<BaseOwnershipPreviewDto>(response, cancellationToken);
    }

    public async Task<BaseOwnershipResultDto> ApplyBaseOwnershipTransferAsync(ConnectionProfile profile, string previewToken, bool confirmed, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/bases/ownership/apply", new { previewToken, confirmed }, cancellationToken);
        return await ReadOperationAsync<BaseOwnershipResultDto>(response, cancellationToken);
    }

    public async Task<BaseRecoveryPreviewDto> PreviewBaseRecoveryAsync(ConnectionProfile profile, string baseId, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/bases/recovery/preview", new { baseId }, cancellationToken);
        return await ReadOperationAsync<BaseRecoveryPreviewDto>(response, cancellationToken);
    }

    public async Task<BaseOwnershipResultDto> ApplyBaseRecoveryAsync(ConnectionProfile profile, string previewToken, bool confirmed, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/bases/recovery/apply", new { previewToken, confirmed }, cancellationToken);
        return await ReadOperationAsync<BaseOwnershipResultDto>(response, cancellationToken);
    }

    public async Task<PlayerDeletionPreviewDto> PreviewPlayerDeletionAsync(ConnectionProfile profile, string playerId, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync($"/api/v1/players/{Uri.EscapeDataString(playerId)}/delete/preview", null, cancellationToken);
        return await ReadOperationAsync<PlayerDeletionPreviewDto>(response, cancellationToken);
    }

    public async Task<PlayerDeletionResultDto> ApplyPlayerDeletionAsync(ConnectionProfile profile, string previewToken, bool confirmed, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/players/delete/apply", new { previewToken, confirmed }, cancellationToken);
        return await ReadOperationAsync<PlayerDeletionResultDto>(response, cancellationToken);
    }

    public async Task<PlayerCopyPreviewDto> PreviewPlayerCopyAsync(ConnectionProfile profile, string sourcePlayerId, string destinationPlayerId, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/players/copy/preview", new { sourcePlayerId, destinationPlayerId }, cancellationToken);
        return await ReadOperationAsync<PlayerCopyPreviewDto>(response, cancellationToken);
    }

    public async Task<PlayerCopyResultDto> ApplyPlayerCopyAsync(ConnectionProfile profile, string previewToken, bool confirmed, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync("/api/v1/players/copy/apply", new { previewToken, confirmed }, cancellationToken);
        return await ReadOperationAsync<PlayerCopyResultDto>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<OperationRecordDto>> GetOperationsAsync(ConnectionProfile profile, int max = 50, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<List<OperationRecordDto>>($"/api/v1/operations?max={max}", cancellationToken) ?? [];
    }

    public async Task<PlayerMetadataDto> GetPlayerMetadataAsync(
        ConnectionProfile profile, string playerId, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var id = Uri.EscapeDataString(playerId);
        return await client.GetFromJsonAsync<PlayerMetadataDto>($"/api/v1/players/{id}/metadata", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned empty player metadata.");
    }

    public async Task<PlayerMetadataDto> SavePlayerNotesAsync(
        ConnectionProfile profile, string playerId, string notes, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var id = Uri.EscapeDataString(playerId);
        using var response = await client.PutAsJsonAsync($"/api/v1/players/{id}/metadata", new PlayerNotesRequestDto(notes), cancellationToken);
        return await ReadOperationAsync<PlayerMetadataDto>(response, cancellationToken);
    }

    public async Task<PlayerMetadataDto> AddPlayerWarningAsync(
        ConnectionProfile profile, string playerId, string message, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var id = Uri.EscapeDataString(playerId);
        using var response = await client.PostAsJsonAsync($"/api/v1/players/{id}/warnings", new PlayerWarningRequestDto(message), cancellationToken);
        return await ReadOperationAsync<PlayerMetadataDto>(response, cancellationToken);
    }

    public async Task<NetworkDiagnosticReportDto> GetNetworkDiagnosticsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<NetworkDiagnosticReportDto>("/api/v1/diagnostics/network", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty network diagnostic report.");
    }

    public async Task<PortCheckResultDto> CheckPortAsync(ConnectionProfile profile, int port, string protocol, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<PortCheckResultDto>($"/api/v1/diagnostics/port-check?port={port}&protocol={Uri.EscapeDataString(protocol)}", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty port-check result.");
    }

    public async Task<NetworkRecoveryResultDto> RestartFromNetworkDiagnosticsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync("/api/v1/diagnostics/network/restart", null, cancellationToken);
        return await ReadOperationAsync<NetworkRecoveryResultDto>(response, cancellationToken);
    }

    public async Task<FirewallRepairResultDto> RepairNetworkFirewallAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync("/api/v1/diagnostics/network/firewall/repair", null, cancellationToken);
        return await ReadOperationAsync<FirewallRepairResultDto>(response, cancellationToken);
    }

    public async Task<WanReachabilityReportDto> GetWanReachabilityAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<WanReachabilityReportDto>("/api/v1/diagnostics/network/wan", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty WAN reachability report.");
    }

    public async Task<UpnpRepairResultDto> RepairUpnpMappingAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync("/api/v1/diagnostics/network/wan/upnp/repair", null, cancellationToken);
        return await ReadOperationAsync<UpnpRepairResultDto>(response, cancellationToken);
    }

    public async Task<ModInventoryDto> GetModsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<ModInventoryDto>("/api/v1/mods", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty MOD inventory.");
    }

    public async Task<ModVerificationResultDto> VerifyModsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<ModVerificationResultDto>("/api/v1/mods/verify", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty MOD verification result.");
    }

    public async Task<ModMutationResultDto> SetModEnabledAsync(ConnectionProfile profile, string type, string package, bool enabled, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var path = $"/api/v1/mods/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(package)}/enabled?enabled={enabled.ToString().ToLowerInvariant()}";
        using var response = await client.PostAsync(path, null, cancellationToken);
        return await ReadOperationAsync<ModMutationResultDto>(response, cancellationToken);
    }

    public async Task<ModMutationResultDto> InstallModZipAsync(ConnectionProfile profile, string type, string package, Stream archive, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken); using var content = new StreamContent(archive);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        using var response = await client.PostAsync($"/api/v1/mods/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(package)}/install-zip", content, cancellationToken);
        return await ReadOperationAsync<ModMutationResultDto>(response, cancellationToken);
    }

    public async Task<ModMutationResultDto> DeleteModAsync(ConnectionProfile profile, string type, string package, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken); using var response = await client.DeleteAsync($"/api/v1/mods/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(package)}", cancellationToken);
        return await ReadOperationAsync<ModMutationResultDto>(response, cancellationToken);
    }

    public async Task<ModMutationResultDto> RollbackModAsync(ConnectionProfile profile, string type, string package, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync($"/api/v1/mods/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(package)}/rollback", null, cancellationToken);
        return await ReadOperationAsync<ModMutationResultDto>(response, cancellationToken);
    }

    public async Task<ModMutationResultDto> RepairModAsync(ConnectionProfile profile, string type, string package, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync($"/api/v1/mods/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(package)}/repair", null, cancellationToken);
        return await ReadOperationAsync<ModMutationResultDto>(response, cancellationToken);
    }

    public async Task<ModMutationResultDto> SetAllModsEnabledAsync(ConnectionProfile profile, bool enabled, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync($"/api/v1/mods/all/enabled?enabled={enabled.ToString().ToLowerInvariant()}", null, cancellationToken);
        return await ReadOperationAsync<ModMutationResultDto>(response, cancellationToken);
    }

    public async Task<ModMutationResultDto> RepairModsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken); using var response = await client.PostAsync("/api/v1/mods/repair", null, cancellationToken);
        return await ReadOperationAsync<ModMutationResultDto>(response, cancellationToken);
    }

    public async Task<WorkshopScanResultDto> ScanWorkshopModsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<WorkshopScanResultDto>("/api/v1/mods/workshop", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty Workshop scan result.");
    }

    public async Task<ModMutationResultDto> ImportWorkshopModAsync(ConnectionProfile profile, string workshopId, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync($"/api/v1/mods/workshop/{Uri.EscapeDataString(workshopId)}/import", null, cancellationToken);
        return await ReadOperationAsync<ModMutationResultDto>(response, cancellationToken);
    }

    public async Task<ModUpdateCheckResultDto> CheckModUpdateAsync(ConnectionProfile profile, string type, string package, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        return await client.GetFromJsonAsync<ModUpdateCheckResultDto>($"/api/v1/mods/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(package)}/check-update", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty MOD update check result.");
    }

    public async Task<ModDescriptionResultDto> GetModDescriptionAsync(ConnectionProfile profile, string type, string package, bool refresh, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        var query = refresh ? "?refresh=true" : string.Empty;
        return await client.GetFromJsonAsync<ModDescriptionResultDto>($"/api/v1/mods/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(package)}/description{query}", cancellationToken)
            ?? throw new InvalidOperationException("MystTiq returned an empty MOD description result.");
    }

    public async Task<ModMutationResultDto> SetModDescriptionSourceAsync(ConnectionProfile profile, string type, string package, string sourceUrl, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsJsonAsync($"/api/v1/mods/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(package)}/description/source", new ModDescriptionSourceRequestDto(sourceUrl), cancellationToken);
        return await ReadOperationAsync<ModMutationResultDto>(response, cancellationToken);
    }

    public async Task<ModMutationResultDto> BeginModSafeStartAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync("/api/v1/mods/safe-start", null, cancellationToken);
        return await ReadOperationAsync<ModMutationResultDto>(response, cancellationToken);
    }

    public async Task<SafeStartStatusDto?> GetModSafeStartStatusAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.GetAsync("/api/v1/mods/safe-start/status", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SafeStartStatusDto>(cancellationToken);
    }

    public async Task<ModMutationResultDto> CancelModSafeStartAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync("/api/v1/mods/safe-start/cancel", null, cancellationToken);
        return await ReadOperationAsync<ModMutationResultDto>(response, cancellationToken);
    }

    private async Task<LifecycleOperationResultDto> RunLifecycleAsync(
        ConnectionProfile profile,
        string? bearerToken,
        string path,
        CancellationToken cancellationToken)
    {
        using var client = BuildClient(profile, bearerToken);
        using var response = await client.PostAsync(path, content: null, cancellationToken);

        LifecycleOperationResultDto? result = null;
        try
        {
            result = await response.Content.ReadFromJsonAsync<LifecycleOperationResultDto>(cancellationToken);
        }
        catch
        {
            // Preserve HTTP failure detail below when a non-JSON response is returned.
        }

        if (result is not null)
            return result;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"MystTiq lifecycle request failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {body}".Trim());
    }

    private static HttpClient BuildClient(ConnectionProfile profile, string? bearerToken)
    {
        var client = new HttpClient(BuildHandler(profile))
        {
            BaseAddress = profile.BaseAddress,
            Timeout = TimeSpan.FromSeconds(120)
        };

        if (!string.IsNullOrWhiteSpace(bearerToken))
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", bearerToken.Trim());

        return client;
    }

    private static HttpMessageHandler BuildHandler(ConnectionProfile profile)
    {
        var handler = new HttpClientHandler();
        var expectedPin = NormalizeFingerprint(profile.ServerCertificateSha256);

        if (!string.IsNullOrWhiteSpace(expectedPin))
        {
            handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
            {
                if (certificate is null)
                    return false;

                using var cert2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
                return VerifyCertificatePin(cert2, expectedPin);
            };
        }

        // Without a pin, normal OS certificate validation remains authoritative.
        return string.IsNullOrWhiteSpace(profile.ServerId)
            ? handler
            : new ServerScopedRoutingHandler(profile.ServerId) { InnerHandler = handler };
    }

    // v0.7.63.0: routes a multi-server fleet host's requests to one specific server profile.
    // A fleet host (see LocalManagementApiHost's "Multi-Server Fleet" header comment) serves every
    // per-server route twice: once unprefixed (hard-wired to its "default" profile only, for
    // backward compatibility) and once under /api/v1/servers/{profileId}/... for every profile
    // including "default". This client had never adopted the scoped form, so a non-default profile
    // (e.g. a second local server) was simply unreachable from the GUI regardless of connection/tab
    // setup. Rather than touching every individual call site above, this single handler rewrites
    // the small, closed set of per-server /api/v1/ paths to their scoped form when profile.ServerId
    // is set. The excluded prefixes below are the genuinely fleet-only routes registered directly
    // on the host app (not per-profile) -- see LocalManagementApiHost.cs's `app.Map*` calls -- which
    // have no scoped twin to rewrite to and must stay unprefixed regardless of ServerId.
    private sealed class ServerScopedRoutingHandler(string serverId) : DelegatingHandler
    {
        private static readonly string[] FleetOnlyPrefixes =
        [
            "/api/v1/diagnostics/port-check",
            "/api/v1/ue4ss/releases",
            "/api/v1/service",
            "/api/v1/security/",
            "/api/v1/auth/", // v0.7.114.0: sign-in is fleet-wide, never per server
            "/api/v1/operations",
            "/api/v1/servers",
            "/api/v1/fleet/",
        ];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri;
            if (uri is not null && uri.AbsolutePath.StartsWith("/api/v1/", StringComparison.Ordinal) &&
                !FleetOnlyPrefixes.Any(p => uri.AbsolutePath.StartsWith(p, StringComparison.Ordinal)))
            {
                var rewritten = "/api/v1/servers/" + Uri.EscapeDataString(serverId) + uri.AbsolutePath["/api/v1".Length..];
                var builder = new UriBuilder(uri) { Path = rewritten };
                request.RequestUri = builder.Uri;
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    // v0.6.4.0: extracted so LocalDiagnosticsService's TLS-stage local-PC diagnostic check can
    // report a real pin-mismatch finding using the exact same fixed-time comparison, rather than
    // duplicating it.
    public static bool VerifyCertificatePin(X509Certificate2 certificate, string expectedPinHex)
    {
        var actual = SHA256.HashData(certificate.RawData);
        var expected = Convert.FromHexString(expectedPinHex);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static string? NormalizeFingerprint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        if (normalized.Length != 64)
            throw new ArgumentException("TLS certificate SHA-256 fingerprint must contain exactly 64 hexadecimal characters.");

        _ = Convert.FromHexString(normalized);
        return normalized;
    }
}
