using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

public interface IMystTiqApiClient
{
    Task<ApiHealthDto> GetHealthAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<StatusPollingDto> GetStatusPollingAsync(
        ConnectionProfile profile,
        int logLines = 120,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<ServerStatusDto> GetStatusAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<ServiceStatusDto> GetServiceStatusAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<LifecycleOperationResultDto> StartServerAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<LifecycleOperationResultDto> StopServerAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<LifecycleOperationResultDto> ForceStopServerAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<LifecycleOperationResultDto> RestartServerAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<PlayersSnapshotDto> GetPlayersAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<LogTailSnapshotDto> GetLogTailAsync(
        ConnectionProfile profile,
        int lines = 120,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<RuntimeMetricsSnapshotDto> GetMetricsAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<HistoricalMetricsSnapshotDto> GetHistoricalMetricsAsync(
        ConnectionProfile profile,
        double hours = 1,
        int maxSamples = 600,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<ActivityLogSnapshotDto> GetActivityLogTailAsync(
        ConnectionProfile profile,
        int lines = 200,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<NotificationSnapshotDto> GetNotificationsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<NotificationSnapshotDto> RunNotificationSelfTestAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<NotificationSnapshotDto> MarkAllNotificationsReadAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<NotificationSnapshotDto> SetNotificationReadAsync(ConnectionProfile profile, string id, bool value, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<NotificationSnapshotDto> SetNotificationPinnedAsync(ConnectionProfile profile, string id, bool value, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<NotificationSnapshotDto> DismissNotificationAsync(ConnectionProfile profile, string id, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<CrashAnalysisSnapshotDto> AnalyzeCrashesAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrashAnalysisSnapshotDto>> GetCrashHistoryAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<SaveToolsDiagnosticsDto> GetSaveToolsDiagnosticsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<SaveToolsDiagnosticsDto> RunSaveToolsSelfTestAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<SaveFileInventoryDto> GetSaveFilesAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<RconStatusDto> GetRconStatusAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<RconDoctorResultDto> RunRconDoctorAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<RconCommandResultDto> SendRconCommandAsync(ConnectionProfile profile, string command, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<PlayerAdminActionResultDto> RunPlayerAdminActionAsync(
        ConnectionProfile profile,
        string playerId,
        string action,
        string? message = null,
        string? item = null,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<DoctorReportDto> RunDoctorAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<BackupInventoryDto> GetBackupsAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<BackupOperationResultDto> CreateBackupAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<BackupOperationResultDto> DeleteBackupAsync(
        ConnectionProfile profile,
        string fileName,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<BackupOperationResultDto> RestoreBackupAsync(
        ConnectionProfile profile,
        string fileName,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<BackupVerificationResultDto> VerifyBackupAsync(ConnectionProfile profile, string fileName, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<BackupVerificationBatchDto> VerifyAllBackupsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<BackupRetentionPreviewDto> PreviewBackupRetentionAsync(ConnectionProfile profile, int keepLatest, int maxAgeDays, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<BackupRetentionApplyResultDto> ApplyBackupRetentionAsync(ConnectionProfile profile, string token, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<EditableConfigurationDto> GetEditableConfigurationAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<PalworldConfigurationSnapshotDto> GetPalworldConfigurationAsync(
        ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<PalworldConfigurationSaveResultDto> CreateDefaultPalworldConfigurationAsync(
        ConnectionProfile profile, PalworldDefaultConfigurationRequestDto request, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<PalworldConfigurationSaveResultDto> SavePalworldConfigurationAsync(
        ConnectionProfile profile, IReadOnlyList<PalworldSettingDto> settings, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<ConfigurationSaveResultDto> SaveEditableConfigurationAsync(
        ConnectionProfile profile,
        EditableConfigurationDto configuration,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<EnvironmentChecklistSnapshotDto> GetEnvironmentChecklistAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<ServerDistributionStatusDto> GetServerDistributionStatusAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<ServerDistributionPlanDto> GetServerDistributionPlanAsync(
        ConnectionProfile profile,
        bool validate = true,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<ServerDistributionOperationResultDto> UpdateServerDistributionAsync(
        ConnectionProfile profile,
        bool validate = true,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<WorldExplorerSnapshotDto> GetWorldExplorerAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<WorldValidationReportDto> ValidateActiveWorldAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorldTransactionJournalDto>> GetWorldTransactionsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<WorldImportPreviewDto> AnalyzeWorldArchiveAsync(ConnectionProfile profile, string mode, Stream archive, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<WorldTransactionResultDto> ApplyWorldTransactionAsync(ConnectionProfile profile, string previewToken, bool confirmed, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<PlayerGuildSnapshotDto> GetPlayerGuildExplorerAsync(
        ConnectionProfile profile,
        string? bearerToken = null,
        CancellationToken cancellationToken = default);

    Task<GuildOwnershipPreviewDto> PreviewGuildOwnershipAsync(ConnectionProfile profile, string operationType, string guildId, string playerId, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<GuildOwnershipResultDto> ApplyGuildOwnershipAsync(ConnectionProfile profile, string previewToken, bool confirmed, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<BaseOwnershipPreviewDto> PreviewBaseOwnershipTransferAsync(ConnectionProfile profile, string baseId, string targetGuildId, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<BaseOwnershipResultDto> ApplyBaseOwnershipTransferAsync(ConnectionProfile profile, string previewToken, bool confirmed, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<BaseRecoveryPreviewDto> PreviewBaseRecoveryAsync(ConnectionProfile profile, string baseId, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<BaseOwnershipResultDto> ApplyBaseRecoveryAsync(ConnectionProfile profile, string previewToken, bool confirmed, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OperationRecordDto>> GetOperationsAsync(ConnectionProfile profile, int max = 50, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<PlayerMetadataDto> GetPlayerMetadataAsync(
        ConnectionProfile profile, string playerId, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<PlayerMetadataDto> SavePlayerNotesAsync(
        ConnectionProfile profile, string playerId, string notes, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<PlayerMetadataDto> AddPlayerWarningAsync(
        ConnectionProfile profile, string playerId, string message, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<NetworkDiagnosticReportDto> GetNetworkDiagnosticsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<NetworkRecoveryResultDto> RestartFromNetworkDiagnosticsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<FirewallRepairResultDto> RepairNetworkFirewallAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);

    Task<ModInventoryDto> GetModsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<ModVerificationResultDto> VerifyModsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<ModMutationResultDto> SetModEnabledAsync(ConnectionProfile profile, string type, string package, bool enabled, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<ModMutationResultDto> InstallModZipAsync(ConnectionProfile profile, string type, string package, Stream archive, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<ModMutationResultDto> DeleteModAsync(ConnectionProfile profile, string type, string package, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<ModMutationResultDto> SetAllModsEnabledAsync(ConnectionProfile profile, bool enabled, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<ModMutationResultDto> RepairModsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<WorkshopScanResultDto> ScanWorkshopModsAsync(ConnectionProfile profile, string? bearerToken = null, CancellationToken cancellationToken = default);
    Task<ModMutationResultDto> ImportWorkshopModAsync(ConnectionProfile profile, string workshopId, string? bearerToken = null, CancellationToken cancellationToken = default);
}
