using MystTiq.Core.Models;
using MystTiq.Core.Operations;
using MystTiq.Core.Providers;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.6.2.0: bundles one server profile's full service graph -- exactly the set of services
// LocalManagementApiHost.Create constructed once per process before this milestone, now
// constructed once per configured HeadlessServerProfileConfiguration entry. Every service here is
// unmodified from its pre-fleet form; isolation comes entirely from each profile getting its own
// IServerPathProfile (so ManagerRuntimeRoot/etc. never collide) and its own IServerLifecycleService
// (so PalServer.exe process supervision never crosses profiles). Fleet-level singletons
// (OperationCoordinator, HeadlessRbacService, HeadlessAuthAbuseGuardService,
// IManagementServiceStatusProvider/ILinuxServiceManager for the mysttiq-server process itself) are
// NOT part of this bundle -- they're constructed once in LocalManagementApiHost.Create and shared
// across every ServerProfileHost.
public sealed class ServerProfileHost : IAsyncDisposable
{
    public required ServerProfileId Id { get; init; }
    public required HeadlessServerProfileConfiguration ServerProfile { get; init; }
    public required IServerPathProfile Paths { get; init; }
    public required IServerLifecycleService Lifecycle { get; init; }
    public required HeadlessMonitoringService Monitoring { get; init; }
    public required HeadlessActivityLogService Activity { get; init; }
    public required HeadlessNotificationRoutingService NotificationRouting { get; init; }
    public required HeadlessNotificationService Notifications { get; init; }
    public required HeadlessCrashAndSaveToolsService CrashAndSaveTools { get; init; }
    public required HeadlessPalworldAdminService PlayerAdmin { get; init; }
    public required PlayerModerationCoordinator PlayerModeration { get; init; }
    public required HeadlessPlayerMetadataService PlayerMetadata { get; init; }
    public required HeadlessPlayerRegistryService PlayerRegistry { get; init; }
    public required HeadlessBackupService Backups { get; init; }
    public required HeadlessConfigurationApiService ConfigurationApi { get; init; }
    public required HeadlessDoctorService Doctor { get; init; }
    public required HeadlessServerDistributionService ServerDistribution { get; init; }
    public required HeadlessConsoleCaptureProxyService ConsoleCaptureProxy { get; init; }
    public required HeadlessWorldExplorerService WorldExplorer { get; init; }
    public required HeadlessWorldTransactionService WorldTransactions { get; init; }
    public required HeadlessPlayerGuildExplorerService PlayerGuildExplorer { get; init; }
    public required HeadlessSaveCodecService SaveCodec { get; init; }
    public required HeadlessGuildOwnershipService GuildOwnership { get; init; }
    public required HeadlessBaseOwnershipService BaseOwnership { get; init; }
    public required HeadlessCharacterMigrationService CharacterMigration { get; init; }
    public required HeadlessDiagnosticsService Diagnostics { get; init; }
    public required HeadlessConsoleLogWriter ConsoleLog { get; init; }
    public required HeadlessModManagementService ModManagement { get; init; }
    public required NetworkDiagnosticsService NetworkDiagnostics { get; init; }
    public required PalworldSettingsConfigurationService PalworldConfiguration { get; init; }
    public required PalworldRconService Rcon { get; init; }
    public required HeadlessEnvironmentChecklistService EnvironmentChecklist { get; init; }
    public required HeadlessHistoricalMetricsService HistoricalMetrics { get; init; }
    public required HeadlessAlertCenterService AlertCenter { get; init; }
    public required HeadlessAutomationService Automation { get; init; }
    public required HeadlessWorldCloneService WorldClone { get; init; }
    public required WanReachabilityService WanReachability { get; init; }
    public required HeadlessFleetCrashRecoveryService CrashRecovery { get; init; }
    // v0.8.2.0: exposed so service-run's own supervisor (which owns this profile's crash recovery in service mode)
    // sends the same alerts and keeps the same persisted recovery state.
    public required CrashAlertObserver CrashAlerts { get; init; }
    public required SupervisorRecoveryStateStore RecoveryState { get; init; }
    public required HeadlessPalEditService PalEdit { get; init; }
    public required HeadlessPlayerDeletionService PlayerDeletion { get; init; }
    public required HeadlessPlayerCopyService PlayerCopy { get; init; }
    public required HeadlessDiscordBotService DiscordBot { get; init; }
    public required HeadlessAntiCheatService AntiCheat { get; init; }
    public required HeadlessWhitelistService Whitelist { get; init; }
    public required HeadlessKitService Kits { get; init; }
    public required HeadlessGameIdCatalogService GameIds { get; init; }
    public required HeadlessTeleportService Teleport { get; init; }
    public required HeadlessTemporaryBanService TemporaryBans { get; init; }
    public required HeadlessComponentUpdateService ComponentUpdates { get; init; }
    public required HeadlessModSafeStartService ModSafeStart { get; init; }
    // v0.8.17.0: process priority and eco mode for this server, and the machine's own readings (one monitor for the fleet).
    public required HeadlessResourcePolicyService ResourcePolicy { get; init; }
    public required HeadlessHostMonitor HostMonitor { get; init; }
    // v0.8.20.0: the machine's history (one instance for the fleet).
    public required HeadlessHostHistoryService HostHistory { get; init; }
    // v0.8.18.0: bandwidth (the server's Engine.ini network limits).
    public required HeadlessNetworkPolicyService NetworkPolicy { get; init; }

    public async ValueTask DisposeAsync()
    {
        await Automation.DisposeAsync();
        await CrashRecovery.DisposeAsync();
        await DiscordBot.DisposeAsync();
    }
}
