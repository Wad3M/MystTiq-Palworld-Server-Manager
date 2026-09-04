using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using MystTiq.Core.Automation;
using MystTiq.Core.Models;
using MystTiq.Core.Operations;
using MystTiq.Core.Providers;
using MystTiq.Core.Security;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.6.2.0 "Multi-Server Fleet": this host now constructs one full service graph per configured
// server profile (ServerProfileHost), bundled in `profiles`, instead of one graph for the whole
// process. Every route body is unchanged business logic -- MapProfileRoutes registers the same
// handlers twice per profile (once at its unprefixed legacy path for the "default" profile only,
// so every pre-fleet client keeps working with zero changes, and once under
// /api/v1/servers/{profileId}/... for every profile including "default"). A small number of
// services are fleet-level singletons shared by every profile: OperationCoordinator (fleet-wide
// operation visibility), HeadlessRbacService/HeadlessAuthAbuseGuardService (one principal/auth
// surface for the whole app), and a fleet-rooted HeadlessActivityLogService used only for
// HTTP-request-level logging and auth-abuse auditing (domain events like "backup created" still
// log to each profile's own activity log).
public sealed class LocalManagementApiHost : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly IReadOnlyDictionary<ServerProfileId, ServerProfileHost> profiles;

    private LocalManagementApiHost(WebApplication app, IReadOnlyDictionary<ServerProfileId, ServerProfileHost> profiles)
    {
        this.app = app;
        this.profiles = profiles;
    }

    public static LocalManagementApiHost Create(
        HeadlessConfiguration configuration,
        Func<HeadlessServerProfileConfiguration, IServerPathProfile, IServerLifecycleService> lifecycleFactory,
        IManagementServiceStatusProvider serviceStatusProvider,
        string? configurationPath = null,
        ILinuxServiceManager? linuxServiceManager = null,
        HeadlessSecretFileService? secretFiles = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(lifecycleFactory);
        ArgumentNullException.ThrowIfNull(serviceStatusProvider);
        if (configuration.Servers is null || configuration.Servers.Count == 0)
            throw new InvalidOperationException("Configuration must include at least one server profile.");
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
        var effectiveConfigurationPath = configurationPath ?? HeadlessConfigurationService.DefaultPath;

        // Fleet-level singletons.
        var fleetPaths = ServerPathProfile.ForCurrentPlatform(
            new ServerRuntimeConfiguration(configuration.FleetRoot, configuration.FleetRoot, configuration.FleetRoot, configuration.FleetRoot));
        var fleetActivity = new HeadlessActivityLogService(fleetPaths);
        var operations = new OperationCoordinator(fleetPaths);
        var rbac = new HeadlessRbacService(fleetPaths);
        var authAbuseGuard = new HeadlessAuthAbuseGuardService(fleetActivity);
        var fleetConfigurationApi = new HeadlessFleetConfigurationService(effectiveConfigurationPath);

        // One full service graph per configured server profile.
        var profiles = new Dictionary<ServerProfileId, ServerProfileHost>();
        foreach (var serverConfig in configuration.Servers)
        {
            var profileId = new ServerProfileId(serverConfig.Id);
            var paths = ServerPathProfile.ForCurrentPlatform(HeadlessConfigurationService.ToRuntimeConfiguration(serverConfig));
            var lifecycle = lifecycleFactory(serverConfig, paths);
            var monitoring = new HeadlessMonitoringService(paths, lifecycle);
            var activity = new HeadlessActivityLogService(paths);
            var notificationRouting = new HeadlessNotificationRoutingService(paths, activity);
            var notifications = new HeadlessNotificationService(paths, activity, notificationRouting);
            var crashAndSaveTools = new HeadlessCrashAndSaveToolsService(paths, activity);
            var playerAdmin = new HeadlessPalworldAdminService(paths, activity);
            var playerMetadata = new HeadlessPlayerMetadataService(paths, activity);
            var playerRegistry = new HeadlessPlayerRegistryService(paths);
            var backups = new HeadlessBackupService(paths, lifecycle, activity);
            var configurationApi = new HeadlessConfigurationApiService(effectiveConfigurationPath, configuration, profileId);
            var doctor = new HeadlessDoctorService(configuration, effectiveConfigurationPath, paths, lifecycle, linuxServiceManager);
            var distribution = ServerDistributionPlatformService.ForCurrentPlatform();
            var serverDistribution = new HeadlessServerDistributionService(paths, lifecycle, distribution);
            var worldExplorer = new HeadlessWorldExplorerService(paths);
            var worldTransactions = new HeadlessWorldTransactionService(paths, lifecycle, backups, activity, worldExplorer, operations, profileId);
            var playerGuildExplorer = new HeadlessPlayerGuildExplorerService(paths, monitoring);
            var saveCodec = new HeadlessSaveCodecService(crashAndSaveTools);
            var guildOwnership = new HeadlessGuildOwnershipService(paths, lifecycle, backups, activity, playerGuildExplorer, saveCodec, operations, profileId);
            var baseOwnership = new HeadlessBaseOwnershipService(paths, lifecycle, backups, activity, playerGuildExplorer, saveCodec, operations, profileId);
            var characterMigration = new HeadlessCharacterMigrationService(paths, lifecycle, backups, activity, playerGuildExplorer, saveCodec, operations, profileId);
            var consoleLog = new HeadlessConsoleLogWriter(paths);
            var modManagement = new HeadlessModManagementService(paths, lifecycle, activity, consoleLog);
            var networkPlatform = NetworkDiagnosticsPlatformService.ForCurrentPlatform();
            var networkDiagnostics = new NetworkDiagnosticsService(networkPlatform, paths.ConfigRoot);
            var wanReachability = new WanReachabilityService(networkPlatform);
            var palworldConfiguration = new PalworldSettingsConfigurationService(paths);
            var rcon = new PalworldRconService(palworldConfiguration);
            var rconModeration = new RconPlayerModerationProvider(rcon, activity);
            var playerModeration = new PlayerModerationCoordinator([playerAdmin, rconModeration]);
            var environmentChecklist = new HeadlessEnvironmentChecklistService(paths);
            var historicalMetrics = new HeadlessHistoricalMetricsService(paths);
            var alertCenter = new HeadlessAlertCenterService(paths, historicalMetrics, notifications, modManagement);
            var automation = new HeadlessAutomationService(paths, configuration, serverConfig, lifecycle, backups, notifications, notificationRouting, rcon, operations, activity, alertCenter, monitoring);
            var diagnostics = new HeadlessDiagnosticsService(doctor, environmentChecklist, lifecycle, paths, serverDistribution, palworldConfiguration, playerRegistry, playerGuildExplorer);
            var worldClone = new HeadlessWorldCloneService(paths, lifecycle, serverConfig, fleetConfigurationApi, palworldConfiguration, activity);

            profiles[profileId] = new ServerProfileHost
            {
                Id = profileId,
                ServerProfile = serverConfig,
                Paths = paths,
                Lifecycle = lifecycle,
                Monitoring = monitoring,
                Activity = activity,
                NotificationRouting = notificationRouting,
                Notifications = notifications,
                CrashAndSaveTools = crashAndSaveTools,
                PlayerAdmin = playerAdmin,
                PlayerModeration = playerModeration,
                PlayerMetadata = playerMetadata,
                PlayerRegistry = playerRegistry,
                Backups = backups,
                ConfigurationApi = configurationApi,
                Doctor = doctor,
                Diagnostics = diagnostics,
                ServerDistribution = serverDistribution,
                WorldExplorer = worldExplorer,
                WorldTransactions = worldTransactions,
                PlayerGuildExplorer = playerGuildExplorer,
                SaveCodec = saveCodec,
                GuildOwnership = guildOwnership,
                BaseOwnership = baseOwnership,
                CharacterMigration = characterMigration,
                ConsoleLog = consoleLog,
                ModManagement = modManagement,
                NetworkDiagnostics = networkDiagnostics,
                PalworldConfiguration = palworldConfiguration,
                Rcon = rcon,
                EnvironmentChecklist = environmentChecklist,
                HistoricalMetrics = historicalMetrics,
                AlertCenter = alertCenter,
                Automation = automation,
                WorldClone = worldClone,
                WanReachability = wanReachability
            };
        }

        var host = new LocalManagementApiHost(app, profiles);

        app.Use(async (context, next) =>
        {
            var started = DateTimeOffset.UtcNow;
            await next();
            var path = context.Request.Path.Value ?? string.Empty;
            if (!path.Equals("/healthz", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains("/status/poll", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains("/metrics", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains("/players", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains("/logs/tail", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains("/activity/tail", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains("/history", StringComparison.OrdinalIgnoreCase))
            {
                fleetActivity.Record(context.Response.StatusCode >= 400 ? "Warning" : "Information", "API",
                    $"{context.Request.Method} {path}",
                    $"HTTP {context.Response.StatusCode} in {(DateTimeOffset.UtcNow - started).TotalMilliseconds:F0} ms");
            }
        });

        app.Use(async (context, next) =>
        {
            if (!configuration.Api.Authentication.Enabled || string.Equals(context.Request.Path.Value, "/healthz", StringComparison.OrdinalIgnoreCase))
            {
                // Auth disabled (today's default) => every request resolves to LegacyOwner, so
                // RequireRole never rejects anything -- identical to today's "auth disabled = full
                // access" behavior.
                context.Items[RbacEndpointExtensions.PrincipalItemKey] = MystTiqPrincipal.LegacyOwner;
                await next();
                return;
            }

            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (authAbuseGuard.IsLockedOut(remoteIp))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsJsonAsync(new { error = "too-many-failed-attempts" });
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
            if (bearerToken is not null && HeadlessSecretFileService.FixedTimeEquals(bearerToken, supplied))
            {
                authAbuseGuard.RecordSuccess(remoteIp);
                context.Items[RbacEndpointExtensions.PrincipalItemKey] = MystTiqPrincipal.LegacyOwner;
                await next();
                return;
            }

            var principal = rbac.Authenticate(supplied);
            if (principal is null)
            {
                authAbuseGuard.RecordFailure(remoteIp);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "invalid-bearer-token" });
                return;
            }

            authAbuseGuard.RecordSuccess(remoteIp);
            context.Items[RbacEndpointExtensions.PrincipalItemKey] = principal;
            await next();
        });

        // Compound process identity (v0.6.2.0): reports every server profile this process
        // manages so LocalManagementBootstrapper can verify identity instead of blindly trusting
        // "a compatible-looking process answered" -- directly closes the cross-talk bug found
        // during the v0.6.1.0 screenshot session (a "local" profile silently attaching to a
        // different already-running instance).
        app.MapGet("/healthz", () => Results.Ok(new
        {
            status = "ok",
            component = "mysttiq-headless",
            api = loopback ? "local" : "remote-secured",
            apiVersion = 1,
            version = typeof(LocalManagementApiHost).Assembly.GetName().Version?.ToString() ?? "unknown",
            platform = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsLinux() ? "linux" : "unknown",
            authentication = configuration.Api.Authentication.Enabled,
            tls = configuration.Api.Tls.Enabled,
            serverProfileIds = profiles.Keys.Select(id => id.Value).OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray()
        }));

        app.MapGet("/api/v1/service", async (CancellationToken token) => Results.Ok(await serviceStatusProvider.GetStatusAsync(token)));

        app.MapGet("/api/v1/security/principals", () =>
            Results.Ok(rbac.ListPrincipals().Select(p => new MystTiqPrincipalDto(p.Id, p.Name, p.Role, p.ExpiresUtc, p.IsExpired, p.ScopedServerProfileId))));
        app.MapPost("/api/v1/security/principals", (HeadlessCreatePrincipalRequest request) =>
        {
            var (principal, token) = rbac.CreatePrincipal(request.Name, request.Role, request.ExpiresUtc, request.ScopedServerProfileId);
            return Results.Ok(new HeadlessCreatePrincipalResult(new MystTiqPrincipalDto(principal.Id, principal.Name, principal.Role, principal.ExpiresUtc, principal.IsExpired, principal.ScopedServerProfileId), token));
        }).RequireRole(MystTiqRole.Owner);
        app.MapDelete("/api/v1/security/principals/{id}", (string id) =>
            rbac.RevokePrincipal(id) ? Results.Ok() : Results.NotFound()).RequireRole(MystTiqRole.Owner);
        app.MapGet("/api/v1/security/whoami", (HttpContext context) =>
        {
            var principal = context.Items[RbacEndpointExtensions.PrincipalItemKey] as MystTiqPrincipal ?? MystTiqPrincipal.LegacyOwner;
            return Results.Ok(new MystTiqPrincipalDto(principal.Id, principal.Name, principal.Role, principal.ExpiresUtc, principal.IsExpired, principal.ScopedServerProfileId));
        });

        app.MapGet("/api/v1/operations", (int? max) =>
            Results.Ok(operations.ListRecent(max ?? 50)));
        app.MapGet("/api/v1/operations/{id}", (string id) =>
        {
            var record = operations.Find(new OperationId(id));
            return record is null ? Results.NotFound() : Results.Ok(record);
        });

        // Fleet-scope routes: list/add/remove server profiles, and staggered bulk actions across
        // every configured profile. Adding/removing a profile always requires a process restart
        // (see HeadlessFleetConfigurationService) -- this pass deliberately does not hot-construct
        // or tear down a ServerProfileHost at runtime.
        app.MapGet("/api/v1/servers", async (CancellationToken token) =>
        {
            var summaries = await Task.WhenAll(profiles.Values.Select(async p => new
            {
                id = p.Id.Value,
                p.ServerProfile.Name,
                runtime = p.ServerProfile.Runtime.ToString(),
                status = await p.Lifecycle.GetStatusAsync(token)
            }));
            return Results.Ok(summaries);
        });
        app.MapPost("/api/v1/servers", async (HeadlessAddServerProfileRequest request, CancellationToken token) =>
        {
            var result = await fleetConfigurationApi.AddServerAsync(request, token);
            return result.Success ? Results.Ok(result) : result.ValidationErrors.Count > 0 ? Results.BadRequest(result) : Results.Conflict(result);
        }).RequireRole(MystTiqRole.Owner);
        app.MapDelete("/api/v1/servers/{profileId}", async (string profileId, CancellationToken token) =>
        {
            var result = await fleetConfigurationApi.RemoveServerAsync(profileId, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        }).RequireRole(MystTiqRole.Owner);

        app.MapPost("/api/v1/fleet/backup-all", async (CancellationToken token) =>
            Results.Ok(await host.RunFleetActionAsync(configuration, async p => (object)await p.Backups.CreateAsync(BackupClass.Scheduled, token))))
            .RequireRole(MystTiqRole.Operator);
        app.MapPost("/api/v1/fleet/doctor-all", async (CancellationToken token) =>
            Results.Ok(await host.RunFleetActionAsync(configuration, async p => (object)await p.Doctor.RunAsync(token))))
            .RequireRole(MystTiqRole.Operator);
        app.MapPost("/api/v1/fleet/update-all", async (bool? validate, CancellationToken token) =>
            Results.Ok(await host.RunFleetActionAsync(configuration, async p => (object)await p.ServerDistribution.UpdateAsync(validate ?? true, token))))
            .RequireRole(MystTiqRole.Operator);

        // Every profile's routes: mapped under /api/v1/servers/{profileId}/... for every profile
        // (including "default"), and additionally at the classic unprefixed paths for the
        // "default" profile only -- so a pre-fleet client (or a single-server deployment that
        // never adds a second profile) keeps working with zero changes.
        foreach (var profileHost in profiles.Values)
        {
            MapProfileRoutes(app.MapGroup($"/api/v1/servers/{profileHost.Id.Value}"), profileHost, host, operations, configuration, loopback, serviceStatusProvider);
            if (profileHost.Id.Equals(ServerProfileId.Default))
                MapProfileRoutes(app.MapGroup("/api/v1"), profileHost, host, operations, configuration, loopback, serviceStatusProvider);
        }

        return host;
    }

    // Same handler bodies as the pre-fleet single-server host -- only the receiver changed, from
    // bare local variables to `p.X` on the resolved ServerProfileHost. `routes` is either the
    // top-level `app` (only for the "default" profile's backward-compatible unprefixed mapping)
    // or a `MapGroup("/api/v1/servers/{profileId}")` group (for every profile).
    private static void MapProfileRoutes(
        IEndpointRouteBuilder routes, ServerProfileHost p, LocalManagementApiHost host,
        IOperationCoordinator operations, HeadlessConfiguration configuration, bool loopback,
        IManagementServiceStatusProvider serviceStatusProvider)
    {
        routes.MapGet("/status", async (CancellationToken token) => Results.Ok(await p.Lifecycle.GetStatusAsync(token)));
        routes.MapGet("/status/poll", async (int? lines, CancellationToken token) =>
        {
            var count = Math.Clamp(lines ?? 120, 10, 500);
            var statusTask = p.Lifecycle.GetStatusAsync(token);
            var serviceTask = serviceStatusProvider.GetStatusAsync(token);
            var playersTask = p.Monitoring.GetPlayersAsync(token);
            var metricsTask = p.Monitoring.GetMetricsAsync(token);
            await Task.WhenAll(statusTask, serviceTask, playersTask, metricsTask);

            var world = p.WorldExplorer.ExploreDashboard();
            var backupInventory = p.Backups.GetInventory();
            var palworldSettings = p.PalworldConfiguration.Load();
            var status = await statusTask;
            var players = await playersTask;
            var metrics = await metricsTask;
            p.HistoricalMetrics.Record(metrics, players, backupInventory, world, status);
            p.PlayerRegistry.Observe(players, DateTimeOffset.UtcNow);

            return Results.Ok(new
            {
                observedAt = DateTimeOffset.UtcNow,
                status,
                service = await serviceTask,
                players,
                metrics,
                logTail = p.Monitoring.GetLogTail(count),
                world,
                backupInventory,
                palworldSettings
            });
        });
        routes.MapGet("/players", async (CancellationToken token) =>
            Results.Ok(await p.Monitoring.GetPlayersAsync(token)));
        routes.MapGet("/logs/tail", (int? lines) =>
            Results.Ok(p.Monitoring.GetLogTail(lines ?? 120)));
        routes.MapGet("/activity/tail", (int? lines) =>
            Results.Ok(p.Activity.GetTail(lines ?? 200)));
        routes.MapGet("/notifications", () => Results.Ok(p.Notifications.GetSnapshot()));
        routes.MapPost("/notifications/self-test", () => Results.Ok(p.Notifications.CreateSelfTest()));
        routes.MapPost("/notifications/mark-all-read", () => Results.Ok(p.Notifications.MarkAllRead()));
        routes.MapPost("/notifications/{id}/read", (string id, HeadlessNotificationFlagRequest request) =>
        { try { return Results.Ok(p.Notifications.SetRead(id, request.Value)); } catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); } });
        routes.MapPost("/notifications/{id}/pin", (string id, HeadlessNotificationFlagRequest request) =>
        { try { return Results.Ok(p.Notifications.SetPinned(id, request.Value)); } catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); } });
        routes.MapDelete("/notifications/{id}", (string id) =>
        { try { return Results.Ok(p.Notifications.Dismiss(id)); } catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); } });

        routes.MapGet("/notifications/channels", () => Results.Ok(p.NotificationRouting.GetChannels()));
        routes.MapPut("/notifications/channels", (NotificationChannelConfiguration request) =>
            Results.Ok(p.NotificationRouting.SaveChannels(request))).RequireRole(MystTiqRole.Admin, p.Id);
        routes.MapGet("/notifications/templates", () => Results.Ok(p.NotificationRouting.GetTemplates()));
        routes.MapPut("/notifications/templates", (List<NotificationTemplate> request) =>
            Results.Ok(p.NotificationRouting.SaveTemplates(request))).RequireRole(MystTiqRole.Admin, p.Id);
        routes.MapGet("/rcon/status", () => Results.Ok(p.Rcon.GetStatus()));
        routes.MapGet("/rcon/doctor", async (CancellationToken token) =>
        {
            var result = await p.Rcon.DoctorAsync(token);
            p.Activity.Record(result.Success ? "Information" : "Warning", "RCON", "RCON doctor", result.Detail);
            return result.Success ? Results.Ok(result) : Results.Json(result, statusCode: StatusCodes.Status424FailedDependency);
        });
        routes.MapPost("/rcon/command", async (RconCommandRequest request, CancellationToken token) =>
        {
            var result = await p.Rcon.ExecuteAsync(request.Command, token);
            var commandVerb = result.Command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "(empty)";
            p.Activity.Record(result.Success ? "Information" : "Warning", "RCON", $"RCON command: {commandVerb}", result.Message);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });
        routes.MapPost("/players/{playerId}/action", async (string playerId, PlayerAdminRequest request, CancellationToken token) =>
        {
            var normalizedAction = (request.Action ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedAction is "kick" or "ban")
            {
                // v0.6.5.0: kick/ban route through the Provider Framework coordinator, which tries
                // REST first (today's pre-v0.6.5.0 behavior, unchanged) and falls back to RCON when
                // REST is disabled/misconfigured -- REST-only servers see no behavior change.
                var moderationResult = await p.PlayerModeration.ExecuteAsync(normalizedAction, playerId, request.Message, token);
                return moderationResult.Success ? Results.Ok(moderationResult)
                    : moderationResult.Supported ? Results.Conflict(moderationResult)
                    : Results.Json(moderationResult, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await p.PlayerAdmin.ExecuteAsync(normalizedAction, playerId, request.Message, request.Item, token);
            return result.Success ? Results.Ok(result) : result.Supported ? Results.Conflict(result) : Results.Json(result, statusCode: StatusCodes.Status422UnprocessableEntity);
        });
        routes.MapGet("/players/moderation/providers", async (CancellationToken token) =>
            Results.Ok(await p.PlayerModeration.GetProviderHealthAsync(token)));
        routes.MapGet("/players/registry", () => Results.Ok(p.PlayerRegistry.Snapshot()));
        routes.MapGet("/players/registry/events", (int? limit) => Results.Ok(p.PlayerRegistry.RecentEvents(limit ?? 200)));
        routes.MapGet("/players/{playerId}/metadata", (string playerId) => Results.Ok(p.PlayerMetadata.Get(playerId)));
        routes.MapPut("/players/{playerId}/metadata", (string playerId, PlayerNotesRequest request) => Results.Ok(p.PlayerMetadata.SaveNotes(playerId, request.Notes)));
        routes.MapPost("/players/{playerId}/warnings", (string playerId, PlayerWarningRequest request) => Results.Ok(p.PlayerMetadata.AddWarning(playerId, request.Message)));
        routes.MapPut("/players/{playerId}/flag", (string playerId, PlayerFlagRequest request) => Results.Ok(p.PlayerMetadata.SetFlag(playerId, request.Flag))).RequireRole(MystTiqRole.Operator, p.Id);
        routes.MapGet("/metrics", async (CancellationToken token) =>
            Results.Ok(await p.Monitoring.GetMetricsAsync(token)));
        routes.MapGet("/history", (double? hours, int? maxSamples) =>
        {
            var requestedHours = Math.Clamp(hours ?? 1d, 1d, 24d * 30d);
            return Results.Ok(p.HistoricalMetrics.Snapshot(TimeSpan.FromHours(requestedHours), maxSamples ?? 600));
        });
        routes.MapGet("/doctor", async (CancellationToken token) =>
            Results.Ok(await p.Doctor.RunAsync(token)));

        routes.MapGet("/diagnostics/report", async (CancellationToken token) =>
            Results.Ok(await p.Diagnostics.GetUnifiedReportAsync(token)));

        routes.MapPost("/diagnostics/{id}/recheck", async (string id, CancellationToken token) =>
        {
            var finding = await p.Diagnostics.RecheckAsync(id, token);
            return finding is null ? Results.NotFound() : Results.Ok(finding);
        });

        routes.MapPost("/diagnostics/{id}/fix", async (string id, CancellationToken token) =>
        {
            var result = await p.Diagnostics.FixAsync(id, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        }).RequireRole(MystTiqRole.Operator, p.Id);

        routes.MapGet("/diagnostics/network", async (CancellationToken token) =>
        {
            var runtime = await p.Lifecycle.GetStatusAsync(token);
            var report = await p.NetworkDiagnostics.RunAsync(
                runtime,
                p.ServerProfile.LaunchArguments,
                NetworkDiagnosticsService.DefaultStartupGrace,
                token);
            return Results.Ok(report);
        });

        routes.MapPost("/diagnostics/network/firewall/repair", async (CancellationToken token) =>
        {
            var resolved = NetworkDiagnosticsService.ResolveGamePort(p.ServerProfile.LaunchArguments);
            var result = await p.NetworkDiagnostics.RepairFirewallAsync(resolved.Port, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        routes.MapGet("/diagnostics/network/wan", async (CancellationToken token) =>
        {
            var resolved = NetworkDiagnosticsService.ResolveGamePort(p.ServerProfile.LaunchArguments);
            var report = await p.WanReachability.RunAsync(resolved.Port, token);
            return Results.Ok(report);
        });

        routes.MapPost("/diagnostics/network/wan/upnp/repair", async (CancellationToken token) =>
        {
            var resolved = NetworkDiagnosticsService.ResolveGamePort(p.ServerProfile.LaunchArguments);
            var result = await p.WanReachability.RepairUpnpMappingAsync(resolved.Port, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        }).RequireRole(MystTiqRole.Operator, p.Id);

        routes.MapPost("/diagnostics/network/restart", async (CancellationToken token) =>
        {
            var before = await p.Lifecycle.GetStatusAsync(token);
            var report = await p.NetworkDiagnostics.RunAsync(before, p.ServerProfile.LaunchArguments, TimeSpan.Zero, token);
            if (!string.Equals(report.RecommendedAction, "Restart Palworld Server", StringComparison.Ordinal))
                return Results.Conflict(new { success = false, message = "Network diagnostics do not currently recommend a restart.", report });

            var restart = await p.Lifecycle.RestartAsync(
                p.ServerProfile.LaunchArguments,
                TimeSpan.FromSeconds(configuration.Lifecycle.StartupTimeoutSeconds),
                TimeSpan.FromSeconds(configuration.Lifecycle.StopTimeoutSeconds),
                token);
            var after = await p.Lifecycle.GetStatusAsync(token);
            var verified = await p.NetworkDiagnostics.RunAsync(after, p.ServerProfile.LaunchArguments, NetworkDiagnosticsService.DefaultStartupGrace, token);
            return Results.Ok(new { success = restart.Success, restart, diagnostics = verified });
        });

        routes.MapGet("/world/explorer", () =>
            Results.Ok(p.WorldExplorer.Explore()));

        routes.MapPost("/crash-analyzer/analyze", () =>
            Results.Ok(p.CrashAndSaveTools.Analyze()));

        routes.MapGet("/crash-analyzer/history", (int? maximum) =>
            Results.Ok(p.CrashAndSaveTools.History(maximum ?? 50)));

        routes.MapGet("/save-tools/diagnostics", async (CancellationToken token) =>
            Results.Ok(await p.CrashAndSaveTools.DiagnoseSaveToolsAsync(false, token)));

        routes.MapPost("/save-tools/self-test", async (CancellationToken token) =>
            Results.Ok(await p.CrashAndSaveTools.DiagnoseSaveToolsAsync(true, token)));

        routes.MapGet("/save-tools/files", () =>
            Results.Ok(p.CrashAndSaveTools.BrowseSaves()));

        routes.MapGet("/world/validate", () =>
            Results.Ok(p.WorldTransactions.ValidateActiveWorld()));

        routes.MapGet("/world/transactions", (int? maximum) =>
            Results.Ok(p.WorldTransactions.GetHistory(maximum ?? 100)));

        routes.MapPost("/world/import/analyze", async (string? mode, HttpRequest request, CancellationToken token) =>
        {
            try { return Results.Ok(await p.WorldTransactions.AnalyzeArchiveAsync(request.Body, mode ?? "world-import", token)); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidDataException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        routes.MapPost("/world/import/apply", async (HeadlessWorldTransactionApplyRequest request, HttpRequest httpRequest, CancellationToken token) =>
        {
            var failureStage = Environment.GetEnvironmentVariable("MYSTTIQ_ENABLE_FAILURE_INJECTION") == "1"
                ? httpRequest.Headers["X-MystTiq-Test-Failure-Stage"].ToString()
                : null;
            var result = string.IsNullOrWhiteSpace(failureStage)
                ? await p.WorldTransactions.ApplyAsync(request, token)
                : await p.WorldTransactions.ApplyCoreAsync(request, failureStage, token);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).RequireRole(MystTiqRole.Admin, p.Id);

        routes.MapGet("/world/players-guilds", async (CancellationToken token) =>
            Results.Ok(await p.PlayerGuildExplorer.ExploreAsync(token)));

        routes.MapPost("/guilds/ownership/preview", async (HeadlessGuildOwnershipPreviewRequest request, CancellationToken token) =>
        {
            try { return Results.Ok(await p.GuildOwnership.PreviewAsync(request.OperationType, request.GuildId, request.PlayerId, token)); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        routes.MapPost("/guilds/ownership/apply", async (HeadlessGuildOwnershipApplyRequest request, CancellationToken token) =>
        {
            var result = await p.GuildOwnership.ApplyAsync(request, token);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).RequireRole(MystTiqRole.Admin, p.Id);

        routes.MapPost("/bases/ownership/preview", async (HeadlessBaseOwnershipTransferRequest request, CancellationToken token) =>
            Results.Ok(await p.BaseOwnership.PreviewTransferAsync(request.BaseId, request.TargetGuildId, token)));

        routes.MapPost("/bases/ownership/apply", async (HeadlessBaseOwnershipApplyRequest request, CancellationToken token) =>
        {
            var result = await p.BaseOwnership.ApplyTransferAsync(request, token);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).RequireRole(MystTiqRole.Admin, p.Id);

        routes.MapPost("/bases/recovery/preview", async (HeadlessBaseRecoveryRequest request, CancellationToken token) =>
            Results.Ok(await p.BaseOwnership.PreviewRecoveryAsync(request.BaseId, token)));

        routes.MapPost("/bases/recovery/apply", async (HeadlessBaseRecoveryApplyRequest request, CancellationToken token) =>
        {
            var result = await p.BaseOwnership.ApplyRecoveryAsync(request, token);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).RequireRole(MystTiqRole.Admin, p.Id);

        routes.MapPost("/players/migration/preview", async (HeadlessCharacterMigrationPreviewRequest request, CancellationToken token) =>
            Results.Ok(await p.CharacterMigration.PreviewAsync(request.SourcePlayerId, request.DestinationPlayerId, token)))
            .RequireRole(MystTiqRole.Admin, p.Id);

        routes.MapPost("/players/migration/apply", async (HeadlessCharacterMigrationApplyRequest request, CancellationToken token) =>
        {
            var result = await p.CharacterMigration.ApplyAsync(request, token);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).RequireRole(MystTiqRole.Admin, p.Id);

        routes.MapPost("/players/migration/{sourcePlayerId}/disposition", async (string sourcePlayerId, HeadlessCharacterDispositionRequest request, CancellationToken token) =>
        {
            var result = await p.CharacterMigration.DisposeSourceCharacterAsync(sourcePlayerId, request.Disposition, token);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).RequireRole(MystTiqRole.Admin, p.Id);

        routes.MapGet("/automation/rules", () => Results.Ok(p.Automation.ListRules()));
        routes.MapPost("/automation/rules", (AutomationRuleRequest request) =>
            Results.Ok(p.Automation.CreateRule(request.Name, request.Trigger, request.Condition, request.Action))).RequireRole(MystTiqRole.Admin, p.Id);
        routes.MapPut("/automation/rules/{id}", (string id, AutomationRuleRequest request) =>
        {
            try { return Results.Ok(p.Automation.UpdateRule(new AutomationRuleId(id), request.Name, request.Trigger, request.Condition, request.Action)); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        }).RequireRole(MystTiqRole.Admin, p.Id);
        routes.MapDelete("/automation/rules/{id}", (string id) =>
            p.Automation.DeleteRule(new AutomationRuleId(id)) ? Results.Ok() : Results.NotFound()).RequireRole(MystTiqRole.Admin, p.Id);
        routes.MapPost("/automation/rules/{id}/enabled", (string id, AutomationEnabledRequest request) =>
        {
            try { return Results.Ok(p.Automation.SetEnabled(new AutomationRuleId(id), request.Enabled)); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        }).RequireRole(MystTiqRole.Admin, p.Id);
        routes.MapPost("/automation/rules/{id}/run-now", (string id, CancellationToken token) =>
        {
            try { p.Automation.RunNow(new AutomationRuleId(id), token); return Results.Accepted(); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        }).RequireRole(MystTiqRole.Operator, p.Id);
        routes.MapGet("/automation/runs", (int? max) => Results.Ok(p.Automation.ListRuns(max ?? 100)));
        routes.MapPost("/automation/runs/{id}/cancel", (string id) =>
            p.Automation.CancelRun(id) ? Results.Ok() : Results.NotFound()).RequireRole(MystTiqRole.Operator, p.Id);

        routes.MapGet("/alerts/rules", () => Results.Ok(p.AlertCenter.GetRules()));
        routes.MapPut("/alerts/rules", (AlertRuleSet request) => Results.Ok(p.AlertCenter.SaveRules(request))).RequireRole(MystTiqRole.Admin, p.Id);
        routes.MapGet("/alerts/predictions", () => Results.Ok(p.AlertCenter.GetDiskSpacePrediction()));

        routes.MapGet("/mods", async (CancellationToken token) =>
            Results.Ok(await p.ModManagement.GetInventoryAsync(token)));

        routes.MapGet("/mods/verify", async (CancellationToken token) =>
            Results.Ok(await p.ModManagement.VerifyAsync(token)));

        routes.MapGet("/ue4ss", async (CancellationToken token) =>
        {
            var inventory = await p.ModManagement.GetInventoryAsync(token);
            return Results.Ok(inventory.Ue4ss);
        });

        routes.MapPost("/mods/{type}/{package}/enabled", async (
            string type, string package, bool enabled, CancellationToken token) =>
        {
            var result = await p.ModManagement.SetEnabledAsync(type, package, enabled, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        routes.MapPost("/mods/{type}/{package}/install-zip", async (string type, string package, HttpRequest request, CancellationToken token) =>
        {
            var result = await p.ModManagement.InstallZipAsync(type, package, request.Body, request.ContentLength, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        }).DisableAntiforgery();

        routes.MapDelete("/mods/{type}/{package}", async (string type, string package, CancellationToken token) =>
        {
            var result = await p.ModManagement.DeleteAsync(type, package, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        routes.MapPost("/mods/{type}/{package}/rollback", async (string type, string package, CancellationToken token) =>
        {
            var result = await p.ModManagement.RollbackAsync(type, package, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        routes.MapPost("/mods/all/enabled", async (bool enabled, CancellationToken token) =>
        {
            var result = await p.ModManagement.SetAllEnabledAsync(enabled, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        routes.MapPost("/mods/repair", async (CancellationToken token) =>
        {
            var result = await p.ModManagement.RepairAsync(token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        routes.MapGet("/mods/workshop", async (CancellationToken token) =>
            Results.Ok(await p.ModManagement.ScanWorkshopAsync(token)));

        routes.MapPost("/mods/workshop/{workshopId}/import", async (string workshopId, CancellationToken token) =>
        {
            var result = await p.ModManagement.ImportWorkshopItemAsync(workshopId, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        routes.MapPost("/server/clone", async (HeadlessWorldCloneRequest request, CancellationToken token) =>
        {
            var result = await p.WorldClone.CloneAsync(request, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        }).RequireRole(MystTiqRole.Owner, p.Id);

        routes.MapGet("/server/environment", () =>
            Results.Ok(p.EnvironmentChecklist.GetSnapshot()));

        routes.MapGet("/server/distribution", () =>
            Results.Ok(p.ServerDistribution.GetStatus()));

        routes.MapGet("/server/distribution/plan", (bool? validate) =>
            Results.Ok(p.ServerDistribution.GetPlan(validate ?? true)));

        routes.MapPost("/server/distribution/update", async (
            bool? validate,
            CancellationToken token) =>
        {
            var result = await p.ServerDistribution.UpdateAsync(validate ?? true, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        routes.MapGet("/backups", () =>
            Results.Ok(p.Backups.GetInventory()));

        routes.MapPost("/backups/create", async (CancellationToken token) =>
        {
            var result = await p.Backups.CreateAsync(BackupClass.Manual, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        }).RequireRole(MystTiqRole.Operator, p.Id);

        routes.MapDelete("/backups/{fileName}", async (string fileName, CancellationToken token) =>
        {
            var result = await p.Backups.DeleteAsync(fileName, token);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).RequireRole(MystTiqRole.Operator, p.Id);

        routes.MapPost("/backups/{fileName}/restore", async (string fileName, HeadlessBackupRestoreRequest request, CancellationToken token) =>
        {
            var result = await p.Backups.RestoreAsync(fileName, request.Confirmed, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        }).RequireRole(MystTiqRole.Operator, p.Id);

        routes.MapPost("/backups/{fileName}/verify", async (string fileName, CancellationToken token) =>
            Results.Ok(await p.Backups.VerifyAsync(fileName, token)));

        routes.MapPost("/backups/verify-all", async (CancellationToken token) =>
            Results.Ok(await p.Backups.VerifyAllAsync(token)));

        routes.MapPost("/backups/retention/preview", (HeadlessBackupRetentionRequest request) =>
            Results.Ok(p.Backups.PreviewRetention(request)));

        routes.MapPost("/backups/retention/apply", async (HeadlessBackupRetentionApplyRequest request, CancellationToken token) =>
        {
            var result = await p.Backups.ApplyRetentionAsync(request.Token, token);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        }).RequireRole(MystTiqRole.Operator, p.Id);

        routes.MapPut("/backups/{fileName}/class", (string fileName, HeadlessBackupSetClassRequest request) =>
        {
            try { return Results.Ok(p.Backups.SetClass(fileName, request.Class, request.Reason)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireRole(MystTiqRole.Admin, p.Id);

        routes.MapGet("/palworld/config", () => Results.Ok(p.PalworldConfiguration.Load()));

        routes.MapPost("/palworld/config/defaults", (PalworldDefaultConfigurationRequest request) =>
        {
            var result = p.PalworldConfiguration.CreateDefault(request);
            if (result.Success)
                p.Activity.Record("Information", "Palworld Configuration", "Created default PalWorldSettings.ini", "First-run settings created through the authenticated management API; sensitive values omitted.");
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        routes.MapPut("/palworld/config", (PalworldConfigurationUpdateRequest request) =>
        {
            var rows = request.Settings.Select(x => new PalworldSettingEntry(x.Name, x.DisplayName, x.Category, x.Value, x.DefaultValue, x.IsModified));
            var result = p.PalworldConfiguration.Save(rows);
            if (result.Success) p.Activity.Record("Information", "Palworld Configuration", "Saved PalWorldSettings.ini", result.BackupPath ?? string.Empty);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        routes.MapGet("/config/editable", () =>
            Results.Ok(p.ConfigurationApi.GetEditable()));

        routes.MapPut("/config/editable", async (
            HeadlessConfigurationUpdateRequest request,
            CancellationToken token) =>
        {
            var result = await p.ConfigurationApi.SaveAsync(request, token);
            return result.Success
                ? Results.Ok(result)
                : result.ValidationErrors.Count > 0
                    ? Results.BadRequest(result)
                    : Results.Conflict(result);
        }).RequireRole(MystTiqRole.Admin, p.Id);

        routes.MapGet("/config", () => Results.Ok(new
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
            Server = p.ServerProfile
        }));

        routes.MapPost("/server/start", async (CancellationToken token) => await host.RunLifecycleActionAsync(operations, p.Id, "server-start", ["lifecycle", "world-mutation"], token, async () =>
        {
            await p.ModManagement.LogPreStartDiagnosticsAsync(token);
            return await p.Lifecycle.StartAsync(p.ServerProfile.LaunchArguments, TimeSpan.FromSeconds(configuration.Lifecycle.StartupTimeoutSeconds), token);
        })).RequireRole(MystTiqRole.Operator, p.Id);
        routes.MapPost("/server/stop", async (CancellationToken token) => await host.RunLifecycleActionAsync(operations, p.Id, "server-stop", ["lifecycle"], token, () =>
            p.Lifecycle.StopAsync(TimeSpan.FromSeconds(configuration.Lifecycle.StopTimeoutSeconds), token))).RequireRole(MystTiqRole.Operator, p.Id);
        routes.MapPost("/server/force-stop", async (CancellationToken token) => await host.RunLifecycleActionAsync(operations, p.Id, "server-force-stop", ["lifecycle"], token, () =>
            p.Lifecycle.StopAsync(TimeSpan.Zero, token))).RequireRole(MystTiqRole.Operator, p.Id);
        routes.MapPost("/server/restart", async (CancellationToken token) => await host.RunLifecycleActionAsync(operations, p.Id, "server-restart", ["lifecycle", "world-mutation"], token, async () =>
        {
            await p.ModManagement.LogPreStartDiagnosticsAsync(token);
            return await p.Lifecycle.RestartAsync(p.ServerProfile.LaunchArguments, TimeSpan.FromSeconds(configuration.Lifecycle.StartupTimeoutSeconds), TimeSpan.FromSeconds(configuration.Lifecycle.StopTimeoutSeconds), token);
        })).RequireRole(MystTiqRole.Operator, p.Id);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await app.StartAsync(cancellationToken);
        foreach (var p in profiles.Values)
            await p.Automation.StartAsync(cancellationToken);
    }

    public Task WaitForShutdownAsync(CancellationToken cancellationToken) => ((IHost)app).WaitForShutdownAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var p in profiles.Values)
            await p.Automation.StopAsync(cancellationToken);
        await app.StopAsync(cancellationToken);
    }

    public string[] Addresses => app.Urls.ToArray();

    public async ValueTask DisposeAsync()
    {
        foreach (var p in profiles.Values)
            await p.DisposeAsync();
        await app.DisposeAsync();
    }

    // Fleet bulk action (Backup All / Doctor All / Update All): iterates every configured profile,
    // staggering start times by configuration.FleetStaggerSeconds so N servers don't all fire the
    // same disk/network-heavy action in the same instant. Each per-profile call reuses the exact
    // same method the single-server route already calls -- this is orchestration, not new
    // business logic.
    private async Task<IReadOnlyList<object>> RunFleetActionAsync(HeadlessConfiguration configuration, Func<ServerProfileHost, Task<object>> action)
    {
        var stagger = TimeSpan.FromSeconds(Math.Max(0, configuration.FleetStaggerSeconds));
        var results = new List<object>();
        var index = 0;
        foreach (var p in profiles.Values)
        {
            if (index > 0 && stagger > TimeSpan.Zero)
                await Task.Delay(stagger);
            try
            {
                var result = await action(p);
                results.Add(new { serverProfileId = p.Id.Value, success = true, result });
            }
            catch (Exception ex)
            {
                results.Add(new { serverProfileId = p.Id.Value, success = false, error = ex.Message });
            }
            index++;
        }
        return results;
    }

    // Resource-key locking via the OperationCoordinator (not a local semaphore -- removed) so a
    // manual/automation-triggered lifecycle action correctly conflicts with an in-flight
    // World Transaction/Guild/Base Apply, not just with another concurrent lifecycle action.
    // v0.6.2.0: takes the target server profile explicitly (one lifecycle route mapping per
    // profile) so locks on one server never block another (see OperationCoordinator's
    // (profile, resourceKey) composite key).
    private async Task<IResult> RunLifecycleActionAsync(
        IOperationCoordinator coordinator, ServerProfileId profile, string kind, IReadOnlyList<string> resourceKeys,
        CancellationToken cancellationToken, Func<Task<ServerLifecycleOperationResult>> operation)
    {
        OperationHandle handle;
        try
        {
            handle = await coordinator.BeginAsync(profile, kind, "LocalManagementApiHost", resourceKeys, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = "lifecycle-operation-in-progress", detail = ex.Message });
        }

        try
        {
            var result = await operation();
            if (result.Success) coordinator.Complete(handle.Id, result.Message);
            else coordinator.Fail(handle.Id, result.Message);
            return result.Success ? Results.Ok(result) : Results.Json(result, statusCode: MapStatusCode(result.ExitCode));
        }
        catch (Exception ex)
        {
            coordinator.Fail(handle.Id, ex.Message);
            throw;
        }
        finally { handle.Dispose(); }
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
