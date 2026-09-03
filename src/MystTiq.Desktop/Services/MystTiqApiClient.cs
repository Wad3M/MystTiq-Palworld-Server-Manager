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

    private static HttpClientHandler BuildHandler(ConnectionProfile profile)
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
                var actual = SHA256.HashData(cert2.RawData);
                var expected = Convert.FromHexString(expectedPin);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            };
        }

        // Without a pin, normal OS certificate validation remains authoritative.
        return handler;
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
