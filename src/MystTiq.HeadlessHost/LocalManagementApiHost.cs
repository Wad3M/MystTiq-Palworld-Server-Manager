using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using MystTiq.Core.Models;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class LocalManagementApiHost : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);

    private LocalManagementApiHost(WebApplication app) => this.app = app;

    public static LocalManagementApiHost Create(
        HeadlessConfiguration configuration,
        IServerLifecycleService lifecycle,
        IManagementServiceStatusProvider serviceStatusProvider,
        string? configurationPath = null,
        ILinuxServiceManager? linuxServiceManager = null,
        HeadlessSecretFileService? secretFiles = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(serviceStatusProvider);
        secretFiles ??= new HeadlessSecretFileService();

        if (!IPAddress.TryParse(configuration.Api.BindAddress, out var bindAddress))
            throw new InvalidOperationException("Management API bind address must be a literal IP address.");

        var loopback = IPAddress.IsLoopback(bindAddress);
        if (!loopback && (!configuration.Api.Authentication.Enabled || !configuration.Api.Tls.Enabled))
            throw new InvalidOperationException("Non-loopback management API requires both authentication and TLS.");

        string? bearerToken = null;
        if (configuration.Api.Authentication.Enabled)
            bearerToken = secretFiles.ReadRequiredSecret(configuration.Api.Authentication.TokenFile);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(bindAddress, configuration.Api.Port, listen =>
            {
                if (configuration.Api.Tls.Enabled)
                {
                    var password = secretFiles.ReadRequiredSecret(configuration.Api.Tls.CertificatePasswordFile);
                    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                        configuration.Api.Tls.CertificatePath,
                        password);
                    listen.UseHttps(certificate);
                }
            });
        });

        var app = builder.Build();
        var host = new LocalManagementApiHost(app);
        var paths = ServerPathProfile.ForCurrentPlatform(
            HeadlessConfigurationService.ToRuntimeConfiguration(configuration));
        var monitoring = new HeadlessMonitoringService(paths, lifecycle);
        var activity = new HeadlessActivityLogService(paths);
        var notifications = new HeadlessNotificationService(paths, activity);
        var crashAndSaveTools = new HeadlessCrashAndSaveToolsService(paths, activity);
        var playerAdmin = new HeadlessPalworldAdminService(paths, activity);
        var playerMetadata = new HeadlessPlayerMetadataService(paths, activity);
        var backups = new HeadlessBackupService(paths, lifecycle, activity);
        var effectiveConfigurationPath = configurationPath ?? HeadlessConfigurationService.DefaultPath;
        var configurationApi = new HeadlessConfigurationApiService(effectiveConfigurationPath, configuration);
        var doctor = new HeadlessDoctorService(configuration, effectiveConfigurationPath, paths, lifecycle, linuxServiceManager);
        var distribution = ServerDistributionPlatformService.ForCurrentPlatform();
        var serverDistribution = new HeadlessServerDistributionService(paths, lifecycle, distribution);
        var worldExplorer = new HeadlessWorldExplorerService(paths);
        var operations = new OperationCoordinator(paths);
        var worldTransactions = new HeadlessWorldTransactionService(paths, lifecycle, backups, activity, worldExplorer, operations);
        var playerGuildExplorer = new HeadlessPlayerGuildExplorerService(paths, monitoring);
        var saveCodec = new HeadlessSaveCodecService(crashAndSaveTools);
        var guildOwnership = new HeadlessGuildOwnershipService(paths, lifecycle, backups, activity, playerGuildExplorer, saveCodec, operations);
        var baseOwnership = new HeadlessBaseOwnershipService(paths, lifecycle, backups, activity, playerGuildExplorer, saveCodec, operations);
        var consoleLog = new HeadlessConsoleLogWriter(paths);
        var modManagement = new HeadlessModManagementService(paths, lifecycle, activity, consoleLog);
        var networkDiagnostics = new NetworkDiagnosticsService(NetworkDiagnosticsPlatformService.ForCurrentPlatform(), paths.ConfigRoot);
        var palworldConfiguration = new PalworldSettingsConfigurationService(paths);
        var rcon = new PalworldRconService(palworldConfiguration);
        var environmentChecklist = new HeadlessEnvironmentChecklistService(paths);
        var historicalMetrics = new HeadlessHistoricalMetricsService(paths);

        app.Use(async (context, next) =>
        {
            var started = DateTimeOffset.UtcNow;
            await next();
            var path = context.Request.Path.Value ?? string.Empty;
            if (!path.Equals("/healthz", StringComparison.OrdinalIgnoreCase) &&
                !path.Equals("/api/v1/status/poll", StringComparison.OrdinalIgnoreCase) &&
                !path.Equals("/api/v1/metrics", StringComparison.OrdinalIgnoreCase) &&
                !path.Equals("/api/v1/players", StringComparison.OrdinalIgnoreCase) &&
                !path.Equals("/api/v1/logs/tail", StringComparison.OrdinalIgnoreCase) &&
                !path.Equals("/api/v1/activity/tail", StringComparison.OrdinalIgnoreCase) &&
                !path.Equals("/api/v1/history", StringComparison.OrdinalIgnoreCase))
            {
                activity.Record(context.Response.StatusCode >= 400 ? "Warning" : "Information", "API",
                    $"{context.Request.Method} {path}",
                    $"HTTP {context.Response.StatusCode} in {(DateTimeOffset.UtcNow - started).TotalMilliseconds:F0} ms");
            }
        });

        app.Use(async (context, next) =>
        {
            if (!configuration.Api.Authentication.Enabled || string.Equals(context.Request.Path.Value, "/healthz", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            var header = context.Request.Headers["Authorization"].ToString();
            const string prefix = "Bearer ";
            if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "missing-bearer-token" });
                return;
            }

            var supplied = header[prefix.Length..].Trim();
            if (bearerToken is null || !HeadlessSecretFileService.FixedTimeEquals(bearerToken, supplied))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "invalid-bearer-token" });
                return;
            }

            await next();
        });

        app.MapGet("/healthz", () => Results.Ok(new
        {
            status = "ok",
            component = "mysttiq-headless",
            api = loopback ? "local" : "remote-secured",
            apiVersion = 1,
            version = typeof(LocalManagementApiHost).Assembly.GetName().Version?.ToString() ?? "unknown",
            platform = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsLinux() ? "linux" : "unknown",
            authentication = configuration.Api.Authentication.Enabled,
            tls = configuration.Api.Tls.Enabled
        }));

        app.MapGet("/api/v1/status", async (CancellationToken token) => Results.Ok(await lifecycle.GetStatusAsync(token)));
        // One coherent polling endpoint for desktop/console consumers.  Keeping the periodic
        // sampling server-side prevents each GUI refresh from fanning out into multiple requests.
        app.MapGet("/api/v1/status/poll", async (int? lines, CancellationToken token) =>
        {
            var count = Math.Clamp(lines ?? 120, 10, 500);
            var statusTask = lifecycle.GetStatusAsync(token);
            var serviceTask = serviceStatusProvider.GetStatusAsync(token);
            var playersTask = monitoring.GetPlayersAsync(token);
            var metricsTask = monitoring.GetMetricsAsync(token);
            await Task.WhenAll(statusTask, serviceTask, playersTask, metricsTask);

            // Dashboard support data is sampled inside this same aggregate request so the desktop
            // never creates competing periodic polling loops.
            var world = worldExplorer.ExploreDashboard();
            var backupInventory = backups.GetInventory();
            var palworldSettings = palworldConfiguration.Load();
            var status = await statusTask;
            var players = await playersTask;
            var metrics = await metricsTask;
            historicalMetrics.Record(metrics, players, backupInventory, world, status);

            return Results.Ok(new
            {
                observedAt = DateTimeOffset.UtcNow,
                status,
                service = await serviceTask,
                players,
                metrics,
                logTail = monitoring.GetLogTail(count),
                world,
                backupInventory,
                palworldSettings
            });
        });
        app.MapGet("/api/v1/service", async (CancellationToken token) => Results.Ok(await serviceStatusProvider.GetStatusAsync(token)));
        app.MapGet("/api/v1/players", async (CancellationToken token) =>
            Results.Ok(await monitoring.GetPlayersAsync(token)));
        app.MapGet("/api/v1/logs/tail", (int? lines) =>
            Results.Ok(monitoring.GetLogTail(lines ?? 120)));
        app.MapGet("/api/v1/activity/tail", (int? lines) =>
            Results.Ok(activity.GetTail(lines ?? 200)));
        app.MapGet("/api/v1/notifications", () => Results.Ok(notifications.GetSnapshot()));
        app.MapPost("/api/v1/notifications/self-test", () => Results.Ok(notifications.CreateSelfTest()));
        app.MapPost("/api/v1/notifications/mark-all-read", () => Results.Ok(notifications.MarkAllRead()));
        app.MapPost("/api/v1/notifications/{id}/read", (string id, HeadlessNotificationFlagRequest request) =>
        { try { return Results.Ok(notifications.SetRead(id, request.Value)); } catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); } });
        app.MapPost("/api/v1/notifications/{id}/pin", (string id, HeadlessNotificationFlagRequest request) =>
        { try { return Results.Ok(notifications.SetPinned(id, request.Value)); } catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); } });
        app.MapDelete("/api/v1/notifications/{id}", (string id) =>
        { try { return Results.Ok(notifications.Dismiss(id)); } catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); } });
        app.MapGet("/api/v1/rcon/status", () => Results.Ok(rcon.GetStatus()));
        app.MapGet("/api/v1/rcon/doctor", async (CancellationToken token) =>
        {
            var result = await rcon.DoctorAsync(token);
            activity.Record(result.Success ? "Information" : "Warning", "RCON", "RCON doctor", result.Detail);
            return result.Success ? Results.Ok(result) : Results.Json(result, statusCode: StatusCodes.Status424FailedDependency);
        });
        app.MapPost("/api/v1/rcon/command", async (RconCommandRequest request, CancellationToken token) =>
        {
            var result = await rcon.ExecuteAsync(request.Command, token);
            var commandVerb = result.Command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "(empty)";
            activity.Record(result.Success ? "Information" : "Warning", "RCON", $"RCON command: {commandVerb}", result.Message);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });
        app.MapPost("/api/v1/players/{playerId}/action", async (string playerId, PlayerAdminRequest request, CancellationToken token) =>
        {
            var result = await playerAdmin.ExecuteAsync(request.Action, playerId, request.Message, request.Item, token);
            return result.Success ? Results.Ok(result) : result.Supported ? Results.Conflict(result) : Results.Json(result, statusCode: StatusCodes.Status422UnprocessableEntity);
        });
        app.MapGet("/api/v1/players/{playerId}/metadata", (string playerId) => Results.Ok(playerMetadata.Get(playerId)));
        app.MapPut("/api/v1/players/{playerId}/metadata", (string playerId, PlayerNotesRequest request) => Results.Ok(playerMetadata.SaveNotes(playerId, request.Notes)));
        app.MapPost("/api/v1/players/{playerId}/warnings", (string playerId, PlayerWarningRequest request) => Results.Ok(playerMetadata.AddWarning(playerId, request.Message)));
        app.MapGet("/api/v1/metrics", async (CancellationToken token) =>
            Results.Ok(await monitoring.GetMetricsAsync(token)));
        app.MapGet("/api/v1/history", (double? hours, int? maxSamples) =>
        {
            var requestedHours = Math.Clamp(hours ?? 1d, 1d, 24d * 30d);
            return Results.Ok(historicalMetrics.Snapshot(TimeSpan.FromHours(requestedHours), maxSamples ?? 600));
        });
        app.MapGet("/api/v1/doctor", async (CancellationToken token) =>
            Results.Ok(await doctor.RunAsync(token)));

        app.MapGet("/api/v1/diagnostics/network", async (CancellationToken token) =>
        {
            Console.WriteLine($"[diagnostics.network] start {DateTimeOffset.UtcNow:O}");
            var runtime = await lifecycle.GetStatusAsync(token);
            var report = await networkDiagnostics.RunAsync(
                runtime,
                configuration.Server.LaunchArguments,
                NetworkDiagnosticsService.DefaultStartupGrace,
                token);
            Console.WriteLine($"[diagnostics.network] end health={report.NetworkHealth} port={report.EffectiveGamePort} pid={report.PalServerProcessId?.ToString() ?? "none"}");
            return Results.Ok(report);
        });

        app.MapPost("/api/v1/diagnostics/network/firewall/repair", async (CancellationToken token) =>
        {
            var resolved = NetworkDiagnosticsService.ResolveGamePort(configuration.Server.LaunchArguments);
            Console.WriteLine($"[diagnostics.network.firewall] repair requested port={resolved.Port}");
            var result = await networkDiagnostics.RepairFirewallAsync(resolved.Port, token);
            Console.WriteLine($"[diagnostics.network.firewall] success={result.Success} changed={result.Changed}");
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        app.MapPost("/api/v1/diagnostics/network/restart", async (CancellationToken token) =>
        {
            var before = await lifecycle.GetStatusAsync(token);
            var report = await networkDiagnostics.RunAsync(before, configuration.Server.LaunchArguments, TimeSpan.Zero, token);
            if (!string.Equals(report.RecommendedAction, "Restart Palworld Server", StringComparison.Ordinal))
                return Results.Conflict(new { success = false, message = "Network diagnostics do not currently recommend a restart.", report });

            Console.WriteLine($"[diagnostics.network.recovery] controlled restart requested port={report.EffectiveGamePort}");
            var restart = await lifecycle.RestartAsync(
                configuration.Server.LaunchArguments,
                TimeSpan.FromSeconds(configuration.Lifecycle.StartupTimeoutSeconds),
                TimeSpan.FromSeconds(configuration.Lifecycle.StopTimeoutSeconds),
                token);
            var after = await lifecycle.GetStatusAsync(token);
            var verified = await networkDiagnostics.RunAsync(after, configuration.Server.LaunchArguments, NetworkDiagnosticsService.DefaultStartupGrace, token);
            return Results.Ok(new { success = restart.Success, restart, diagnostics = verified });
        });

        app.MapGet("/api/v1/world/explorer", () =>
            Results.Ok(worldExplorer.Explore()));

        app.MapPost("/api/v1/crash-analyzer/analyze", () =>
            Results.Ok(crashAndSaveTools.Analyze()));

        app.MapGet("/api/v1/crash-analyzer/history", (int? maximum) =>
            Results.Ok(crashAndSaveTools.History(maximum ?? 50)));

        app.MapGet("/api/v1/save-tools/diagnostics", async (CancellationToken token) =>
            Results.Ok(await crashAndSaveTools.DiagnoseSaveToolsAsync(false, token)));

        app.MapPost("/api/v1/save-tools/self-test", async (CancellationToken token) =>
            Results.Ok(await crashAndSaveTools.DiagnoseSaveToolsAsync(true, token)));

        app.MapGet("/api/v1/save-tools/files", () =>
            Results.Ok(crashAndSaveTools.BrowseSaves()));

        app.MapGet("/api/v1/world/validate", () =>
            Results.Ok(worldTransactions.ValidateActiveWorld()));

        app.MapGet("/api/v1/world/transactions", (int? maximum) =>
            Results.Ok(worldTransactions.GetHistory(maximum ?? 100)));

        app.MapPost("/api/v1/world/import/analyze", async (string? mode, HttpRequest request, CancellationToken token) =>
        {
            try { return Results.Ok(await worldTransactions.AnalyzeArchiveAsync(request.Body, mode ?? "world-import", token)); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidDataException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        app.MapPost("/api/v1/world/import/apply", async (HeadlessWorldTransactionApplyRequest request, HttpRequest httpRequest, CancellationToken token) =>
        {
            // Failure injection is an explicit test-only switch. Production hosts ignore the header.
            var failureStage = Environment.GetEnvironmentVariable("MYSTTIQ_ENABLE_FAILURE_INJECTION") == "1"
                ? httpRequest.Headers["X-MystTiq-Test-Failure-Stage"].ToString()
                : null;
            var result = string.IsNullOrWhiteSpace(failureStage)
                ? await worldTransactions.ApplyAsync(request, token)
                : await worldTransactions.ApplyCoreAsync(request, failureStage, token);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapGet("/api/v1/world/players-guilds", async (CancellationToken token) =>
            Results.Ok(await playerGuildExplorer.ExploreAsync(token)));

        app.MapPost("/api/v1/guilds/ownership/preview", async (HeadlessGuildOwnershipPreviewRequest request, CancellationToken token) =>
        {
            try { return Results.Ok(await guildOwnership.PreviewAsync(request.OperationType, request.GuildId, request.PlayerId, token)); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        app.MapPost("/api/v1/guilds/ownership/apply", async (HeadlessGuildOwnershipApplyRequest request, CancellationToken token) =>
        {
            var result = await guildOwnership.ApplyAsync(request, token);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapPost("/api/v1/bases/ownership/preview", async (HeadlessBaseOwnershipTransferRequest request, CancellationToken token) =>
            Results.Ok(await baseOwnership.PreviewTransferAsync(request.BaseId, request.TargetGuildId, token)));

        app.MapPost("/api/v1/bases/ownership/apply", async (HeadlessBaseOwnershipApplyRequest request, CancellationToken token) =>
        {
            var result = await baseOwnership.ApplyTransferAsync(request, token);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapPost("/api/v1/bases/recovery/preview", async (HeadlessBaseRecoveryRequest request, CancellationToken token) =>
            Results.Ok(await baseOwnership.PreviewRecoveryAsync(request.BaseId, token)));

        app.MapPost("/api/v1/bases/recovery/apply", async (HeadlessBaseRecoveryApplyRequest request, CancellationToken token) =>
        {
            var result = await baseOwnership.ApplyRecoveryAsync(request, token);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapGet("/api/v1/operations", (int? max) =>
            Results.Ok(operations.ListRecent(max ?? 50)));

        app.MapGet("/api/v1/operations/{id}", (string id) =>
        {
            var record = operations.Find(new OperationId(id));
            return record is null ? Results.NotFound() : Results.Ok(record);
        });

        app.MapGet("/api/v1/mods", async (CancellationToken token) =>
            Results.Ok(await modManagement.GetInventoryAsync(token)));

        app.MapGet("/api/v1/mods/verify", async (CancellationToken token) =>
            Results.Ok(await modManagement.VerifyAsync(token)));

        app.MapGet("/api/v1/ue4ss", async (CancellationToken token) =>
        {
            var inventory = await modManagement.GetInventoryAsync(token);
            return Results.Ok(inventory.Ue4ss);
        });

        app.MapPost("/api/v1/mods/{type}/{package}/enabled", async (
            string type, string package, bool enabled, CancellationToken token) =>
        {
            var result = await modManagement.SetEnabledAsync(type, package, enabled, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        app.MapPost("/api/v1/mods/{type}/{package}/install-zip", async (string type, string package, HttpRequest request, CancellationToken token) =>
        {
            var result = await modManagement.InstallZipAsync(type, package, request.Body, request.ContentLength, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        }).DisableAntiforgery();

        app.MapDelete("/api/v1/mods/{type}/{package}", async (string type, string package, CancellationToken token) =>
        {
            var result = await modManagement.DeleteAsync(type, package, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        app.MapPost("/api/v1/mods/all/enabled", async (bool enabled, CancellationToken token) =>
        {
            var result = await modManagement.SetAllEnabledAsync(enabled, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        app.MapPost("/api/v1/mods/repair", async (CancellationToken token) =>
        {
            var result = await modManagement.RepairAsync(token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        app.MapGet("/api/v1/mods/workshop", async (CancellationToken token) =>
            Results.Ok(await modManagement.ScanWorkshopAsync(token)));

        app.MapPost("/api/v1/mods/workshop/{workshopId}/import", async (string workshopId, CancellationToken token) =>
        {
            var result = await modManagement.ImportWorkshopItemAsync(workshopId, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        app.MapGet("/api/v1/server/environment", () =>
            Results.Ok(environmentChecklist.GetSnapshot()));

        app.MapGet("/api/v1/server/distribution", () =>
            Results.Ok(serverDistribution.GetStatus()));

        app.MapGet("/api/v1/server/distribution/plan", (bool? validate) =>
            Results.Ok(serverDistribution.GetPlan(validate ?? true)));

        app.MapPost("/api/v1/server/distribution/update", async (
            bool? validate,
            CancellationToken token) =>
        {
            var result = await serverDistribution.UpdateAsync(validate ?? true, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        app.MapGet("/api/v1/backups", () =>
            Results.Ok(backups.GetInventory()));

        app.MapPost("/api/v1/backups/create", async (CancellationToken token) =>
        {
            var result = await backups.CreateAsync(token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        app.MapDelete("/api/v1/backups/{fileName}", async (string fileName, CancellationToken token) =>
        {
            var result = await backups.DeleteAsync(fileName, token);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapPost("/api/v1/backups/{fileName}/restore", async (string fileName, HeadlessBackupRestoreRequest request, CancellationToken token) =>
        {
            var result = await backups.RestoreAsync(fileName, request.Confirmed, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        app.MapPost("/api/v1/backups/{fileName}/verify", async (string fileName, CancellationToken token) =>
            Results.Ok(await backups.VerifyAsync(fileName, token)));

        app.MapPost("/api/v1/backups/verify-all", async (CancellationToken token) =>
            Results.Ok(await backups.VerifyAllAsync(token)));

        app.MapPost("/api/v1/backups/retention/preview", (HeadlessBackupRetentionRequest request) =>
            Results.Ok(backups.PreviewRetention(request)));

        app.MapPost("/api/v1/backups/retention/apply", async (HeadlessBackupRetentionApplyRequest request, CancellationToken token) =>
        {
            var result = await backups.ApplyRetentionAsync(request.Token, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        app.MapGet("/api/v1/palworld/config", () => Results.Ok(palworldConfiguration.Load()));

        app.MapPost("/api/v1/palworld/config/defaults", (PalworldDefaultConfigurationRequest request) =>
        {
            var result = palworldConfiguration.CreateDefault(request);
            if (result.Success)
                activity.Record("Information", "Palworld Configuration", "Created default PalWorldSettings.ini", "First-run settings created through the authenticated management API; sensitive values omitted.");
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapPut("/api/v1/palworld/config", (PalworldConfigurationUpdateRequest request) =>
        {
            var rows = request.Settings.Select(x => new PalworldSettingEntry(x.Name, x.DisplayName, x.Category, x.Value, x.DefaultValue, x.IsModified));
            var result = palworldConfiguration.Save(rows);
            if (result.Success) activity.Record("Information", "Palworld Configuration", "Saved PalWorldSettings.ini", result.BackupPath ?? string.Empty);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapGet("/api/v1/config/editable", () =>
            Results.Ok(configurationApi.GetEditable()));

        app.MapPut("/api/v1/config/editable", async (
            HeadlessConfigurationUpdateRequest request,
            CancellationToken token) =>
        {
            var result = await configurationApi.SaveAsync(request, token);
            return result.Success
                ? Results.Ok(result)
                : result.ValidationErrors.Count > 0
                    ? Results.BadRequest(result)
                    : Results.Conflict(result);
        });

        app.MapGet("/api/v1/config", () => Results.Ok(new
        {
            configuration.SchemaVersion,
            Api = new
            {
                configuration.Api.Enabled,
                configuration.Api.BindAddress,
                configuration.Api.Port,
                Scope = loopback ? "loopback" : "remote-secured",
                AuthenticationEnabled = configuration.Api.Authentication.Enabled,
                TlsEnabled = configuration.Api.Tls.Enabled
            },
            configuration.Lifecycle,
            configuration.Server
        }));

        app.MapPost("/api/v1/server/start", async (CancellationToken token) => await host.RunLifecycleActionAsync(async () =>
        {
            await modManagement.LogPreStartDiagnosticsAsync(token);
            return await lifecycle.StartAsync(configuration.Server.LaunchArguments, TimeSpan.FromSeconds(configuration.Lifecycle.StartupTimeoutSeconds), token);
        }));
        app.MapPost("/api/v1/server/stop", async (CancellationToken token) => await host.RunLifecycleActionAsync(() =>
            lifecycle.StopAsync(TimeSpan.FromSeconds(configuration.Lifecycle.StopTimeoutSeconds), token)));
        app.MapPost("/api/v1/server/force-stop", async (CancellationToken token) => await host.RunLifecycleActionAsync(() =>
            lifecycle.StopAsync(TimeSpan.Zero, token)));
        app.MapPost("/api/v1/server/restart", async (CancellationToken token) => await host.RunLifecycleActionAsync(async () =>
        {
            await modManagement.LogPreStartDiagnosticsAsync(token);
            return await lifecycle.RestartAsync(configuration.Server.LaunchArguments, TimeSpan.FromSeconds(configuration.Lifecycle.StartupTimeoutSeconds), TimeSpan.FromSeconds(configuration.Lifecycle.StopTimeoutSeconds), token);
        }));

        return host;
    }

    public Task StartAsync(CancellationToken cancellationToken) => app.StartAsync(cancellationToken);
    public Task WaitForShutdownAsync(CancellationToken cancellationToken) => ((IHost)app).WaitForShutdownAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => app.StopAsync(cancellationToken);
    public string[] Addresses => app.Urls.ToArray();

    public async ValueTask DisposeAsync()
    {
        lifecycleGate.Dispose();
        await app.DisposeAsync();
    }

    private async Task<IResult> RunLifecycleActionAsync(Func<Task<ServerLifecycleOperationResult>> operation)
    {
        if (!await lifecycleGate.WaitAsync(0)) return Results.Conflict(new { error = "lifecycle-operation-in-progress" });
        try
        {
            var result = await operation();
            return result.Success ? Results.Ok(result) : Results.Json(result, statusCode: MapStatusCode(result.ExitCode));
        }
        finally { lifecycleGate.Release(); }
    }

    private static int MapStatusCode(HeadlessExitCode exitCode) => exitCode switch
    {
        HeadlessExitCode.AlreadyRunning => StatusCodes.Status409Conflict,
        HeadlessExitCode.NotRunning => StatusCodes.Status409Conflict,
        HeadlessExitCode.ServerExecutableMissing => StatusCodes.Status424FailedDependency,
        HeadlessExitCode.StartupTimeout => StatusCodes.Status504GatewayTimeout,
        HeadlessExitCode.StopTimeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status500InternalServerError
    };
}

public sealed record PlayerAdminRequest(string Action, string? Message, string? Item);
public sealed record PalworldConfigurationUpdateRequest(IReadOnlyList<PalworldConfigurationSettingRequest> Settings);
public sealed record PalworldConfigurationSettingRequest(string Name, string DisplayName, string Category, string Value, string DefaultValue, bool IsModified);

public sealed record RconCommandRequest(string Command);
