using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Explicit composition root for MystTiq's core server/MOD service graph.
/// Construction policy lives here instead of inside MainWindow. This is deliberately
/// simple explicit composition (not a service locator) so dependencies remain visible.
/// </summary>
public sealed class ApplicationServiceComposition
{
    public required Ue4ssRuntimeResolver Ue4ssRuntimeResolver { get; init; }
    public required RuntimeStateService RuntimeState { get; init; }
    public required ServerService Server { get; init; }
    public required BackupService Backups { get; init; }
    public required ConfigService Config { get; init; }
    public required ModService Mods { get; init; }
    public required ModHealthEvaluationService ModHealthEvaluation { get; init; }
    public required ModVerificationService ModVerification { get; init; }
    public required ModDashboardStateService ModDashboardState { get; init; }
    public required ModPlatformHealthService ModPlatformHealth { get; init; }
    public required ModRepairRecommendationEngine ModRepairRecommendations { get; init; }
    public required ModLifecycleCoordinator ModLifecycle { get; init; }
    public required ModVerificationReportExportService ModVerificationReportExporter { get; init; }
    public required ModCompatibilityService ModCompatibility { get; init; }
    public required EnvironmentService Environment { get; init; }
    public required ModInventorySnapshotService ModInventory { get; init; }
    public required ModCoordinator ModCoordinator { get; init; }
    public required InstallerService Installer { get; init; }
    public required CrashAnalyzerService CrashAnalyzer { get; init; }
    public required ServerDoctorService ServerDoctor { get; init; }
    public required WorldTelemetryService WorldTelemetry { get; init; }

    public static ApplicationServiceComposition Create(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var ue4ssRuntimeResolver = new Ue4ssRuntimeResolver(settings);
        var runtimeState = new RuntimeStateService();
        var server = new ServerService(settings);
        var backups = new BackupService(settings);
        var config = new ConfigService(settings);
        var modHealthEvaluation = new ModHealthEvaluationService();
        var modPlatformHealth = new ModPlatformHealthService();
        var modRepairRecommendations = new ModRepairRecommendationEngine();
        var modVerificationReportExporter = new ModVerificationReportExportService();
        var mods = new ModService(settings, ue4ssRuntimeResolver, runtimeState);
        var modVerification = new ModVerificationService(settings, runtimeState, server, modHealthEvaluation);
        var modDashboardState = new ModDashboardStateService(modHealthEvaluation);
        var modLifecycle = new ModLifecycleCoordinator(mods, modRepairRecommendations);
        var modCompatibility = new ModCompatibilityService(settings);
        var environment = new EnvironmentService(settings);
        var modInventory = new ModInventorySnapshotService(mods, environment);
        var modCoordinator = new ModCoordinator(
            modInventory,
            modVerification,
            modCompatibility,
            modRepairRecommendations,
            modVerificationReportExporter,
            server);

        return new ApplicationServiceComposition
        {
            Ue4ssRuntimeResolver = ue4ssRuntimeResolver,
            RuntimeState = runtimeState,
            Server = server,
            Backups = backups,
            Config = config,
            Mods = mods,
            ModHealthEvaluation = modHealthEvaluation,
            ModVerification = modVerification,
            ModDashboardState = modDashboardState,
            ModPlatformHealth = modPlatformHealth,
            ModRepairRecommendations = modRepairRecommendations,
            ModLifecycle = modLifecycle,
            ModVerificationReportExporter = modVerificationReportExporter,
            ModCompatibility = modCompatibility,
            Environment = environment,
            ModInventory = modInventory,
            ModCoordinator = modCoordinator,
            Installer = new InstallerService(settings),
            CrashAnalyzer = new CrashAnalyzerService(settings),
            ServerDoctor = new ServerDoctorService(settings),
            WorldTelemetry = new WorldTelemetryService()
        };
    }
}
