using MystTiq.Core.Models;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.6.2.0: scoped to one server profile -- editing server root/steamcmd/backup/runtime paths and
// launch arguments touches only this profile's entry inside HeadlessConfiguration.Servers. Api/
// Lifecycle stay fleet-wide (one process, one port, one auth store) and are still editable from
// any profile's instance since they're shared top-level config, matching pre-v0.6.2.0 behavior for
// the sole "default" profile in a non-fleet deployment.
public sealed class HeadlessConfigurationApiService
{
    private readonly string configurationPath;
    private readonly HeadlessConfiguration runningConfiguration;
    private readonly ServerProfileId profileId;
    private readonly HeadlessConfigurationService configurationService;
    private readonly SemaphoreSlim configurationGate = new(1, 1);

    public HeadlessConfigurationApiService(
        string configurationPath,
        HeadlessConfiguration runningConfiguration,
        ServerProfileId profileId,
        HeadlessConfigurationService? configurationService = null)
    {
        this.configurationPath = configurationPath;
        this.runningConfiguration = runningConfiguration;
        this.profileId = profileId;
        this.configurationService = configurationService ?? new HeadlessConfigurationService();
    }

    public HeadlessEditableConfiguration GetEditable()
    {
        var current = configurationService.LoadOrDefault(configurationPath);
        var server = current.FindServer(profileId.Value)
            ?? throw new KeyNotFoundException($"Server profile '{profileId}' no longer exists in configuration.");

        return HeadlessEditableConfiguration.From(
            current,
            server,
            configurationPath,
            restartRequired: !Equals(current, runningConfiguration),
            validationErrors: configurationService.Validate(current).Errors);
    }

    public async Task<HeadlessConfigurationSaveResult> SaveAsync(
        HeadlessConfigurationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await configurationGate.WaitAsync(0, cancellationToken))
            return HeadlessConfigurationSaveResult.Failure("A configuration write is already in progress.");

        try
        {
            var current = configurationService.LoadOrDefault(configurationPath);
            var existing = current.FindServer(profileId.Value);
            if (existing is null)
                return HeadlessConfigurationSaveResult.Failure($"Server profile '{profileId}' no longer exists in configuration.");

            var updatedServer = existing with
            {
                ServerRoot = request.Server.ServerRoot.Trim(),
                SteamCmdPath = request.Server.SteamCmdPath.Trim(),
                BackupRoot = request.Server.BackupRoot.Trim(),
                RuntimeRoot = request.Server.RuntimeRoot.Trim(),
                LaunchArguments = request.Server.LaunchArguments
                    .Where(argument => !string.IsNullOrWhiteSpace(argument))
                    .Select(argument => argument.Trim())
                    .ToArray()
            };

            var updated = current with
            {
                Api = current.Api with
                {
                    Enabled = request.Api.Enabled,
                    BindAddress = request.Api.BindAddress.Trim(),
                    Port = request.Api.Port
                },
                Lifecycle = request.Lifecycle,
                Servers = current.Servers
                    .Select(s => s.Id.Equals(profileId.Value, StringComparison.OrdinalIgnoreCase) ? updatedServer : s)
                    .ToArray()
            };

            var validation = configurationService.Validate(updated);
            if (!validation.Valid)
                return HeadlessConfigurationSaveResult.Invalid(validation.Errors);

            var rollbackPath = configurationService.SaveValidated(
                updated,
                configurationPath,
                createRollbackCopy: true);

            return new HeadlessConfigurationSaveResult(
                true,
                true,
                rollbackPath,
                [],
                "Configuration saved. Restart the MystTiq system service to apply the new configuration.");
        }
        catch (Exception ex)
        {
            return HeadlessConfigurationSaveResult.Failure(ex.Message);
        }
        finally
        {
            configurationGate.Release();
        }
    }
}

// Fleet-level: lists/adds/removes server profiles. Adding or removing a profile always requires a
// process restart to take effect (same "restart required" convention as every other config edit --
// this pass deliberately does not attempt to hot-construct/tear-down a ServerProfileHost at
// runtime, which would need its own careful lifecycle story).
public sealed class HeadlessFleetConfigurationService
{
    private readonly string configurationPath;
    private readonly HeadlessConfigurationService configurationService;
    private readonly SemaphoreSlim gate = new(1, 1);

    public HeadlessFleetConfigurationService(string configurationPath, HeadlessConfigurationService? configurationService = null)
    {
        this.configurationPath = configurationPath;
        this.configurationService = configurationService ?? new HeadlessConfigurationService();
    }

    public async Task<HeadlessConfigurationSaveResult> AddServerAsync(HeadlessAddServerProfileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await gate.WaitAsync(0, cancellationToken))
            return HeadlessConfigurationSaveResult.Failure("A configuration write is already in progress.");

        try
        {
            var current = configurationService.LoadOrDefault(configurationPath);
            var id = request.Id.Trim();
            if (current.FindServer(id) is not null)
                return HeadlessConfigurationSaveResult.Failure($"Server profile '{id}' already exists.");

            var profile = new HeadlessServerProfileConfiguration(
                id,
                request.Name.Trim(),
                request.ServerRoot.Trim(),
                request.SteamCmdPath.Trim(),
                request.BackupRoot.Trim(),
                request.RuntimeRoot.Trim(),
                request.LaunchArguments.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToArray(),
                request.Runtime);

            var updated = current with { Servers = current.Servers.Append(profile).ToArray() };
            var validation = configurationService.Validate(updated);
            if (!validation.Valid) return HeadlessConfigurationSaveResult.Invalid(validation.Errors);

            var rollbackPath = configurationService.SaveValidated(updated, configurationPath, createRollbackCopy: true);
            return new HeadlessConfigurationSaveResult(true, true, rollbackPath, [], $"Server profile '{id}' added. Restart the MystTiq system service to apply it.");
        }
        catch (Exception ex) { return HeadlessConfigurationSaveResult.Failure(ex.Message); }
        finally { gate.Release(); }
    }

    public async Task<HeadlessConfigurationSaveResult> RemoveServerAsync(string id, CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken))
            return HeadlessConfigurationSaveResult.Failure("A configuration write is already in progress.");

        try
        {
            var current = configurationService.LoadOrDefault(configurationPath);
            if (id.Equals(HeadlessConfiguration.DefaultServerProfileId, StringComparison.OrdinalIgnoreCase))
                return HeadlessConfigurationSaveResult.Failure("The default server profile cannot be removed.");
            if (current.FindServer(id) is null)
                return HeadlessConfigurationSaveResult.Failure($"Server profile '{id}' does not exist.");

            var updated = current with
            {
                Servers = current.Servers.Where(s => !s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).ToArray()
            };
            var validation = configurationService.Validate(updated);
            if (!validation.Valid) return HeadlessConfigurationSaveResult.Invalid(validation.Errors);

            var rollbackPath = configurationService.SaveValidated(updated, configurationPath, createRollbackCopy: true);
            return new HeadlessConfigurationSaveResult(true, true, rollbackPath, [], $"Server profile '{id}' removed. Restart the MystTiq system service to apply it.");
        }
        catch (Exception ex) { return HeadlessConfigurationSaveResult.Failure(ex.Message); }
        finally { gate.Release(); }
    }
}

public sealed record HeadlessAddServerProfileRequest(
    string Id,
    string Name,
    string ServerRoot,
    string SteamCmdPath,
    string BackupRoot,
    string RuntimeRoot,
    IReadOnlyList<string> LaunchArguments,
    ServerRuntimeKind Runtime);

public sealed record HeadlessApiEditableConfiguration(
    bool Enabled,
    string BindAddress,
    int Port);

public sealed record HeadlessServerEditableConfiguration(
    string ServerRoot,
    string SteamCmdPath,
    string BackupRoot,
    string RuntimeRoot,
    IReadOnlyList<string> LaunchArguments);

public sealed record HeadlessConfigurationUpdateRequest(
    HeadlessApiEditableConfiguration Api,
    HeadlessLifecycleConfiguration Lifecycle,
    HeadlessServerEditableConfiguration Server);

public sealed record HeadlessEditableConfiguration(
    int SchemaVersion,
    string ConfigurationPath,
    HeadlessApiEditableConfiguration Api,
    HeadlessLifecycleConfiguration Lifecycle,
    HeadlessServerEditableConfiguration Server,
    bool AuthenticationEnabled,
    bool TlsEnabled,
    bool RestartRequired,
    IReadOnlyList<string> ValidationErrors)
{
    public static HeadlessEditableConfiguration From(
        HeadlessConfiguration configuration,
        HeadlessServerProfileConfiguration server,
        string configurationPath,
        bool restartRequired,
        IReadOnlyList<string> validationErrors) =>
        new(
            configuration.SchemaVersion,
            configurationPath,
            new HeadlessApiEditableConfiguration(
                configuration.Api.Enabled,
                configuration.Api.BindAddress,
                configuration.Api.Port),
            configuration.Lifecycle,
            new HeadlessServerEditableConfiguration(
                server.ServerRoot,
                server.SteamCmdPath,
                server.BackupRoot,
                server.RuntimeRoot,
                server.LaunchArguments),
            configuration.Api.Authentication.Enabled,
            configuration.Api.Tls.Enabled,
            restartRequired,
            validationErrors);
}

public sealed record HeadlessConfigurationSaveResult(
    bool Success,
    bool RestartRequired,
    string? RollbackPath,
    IReadOnlyList<string> ValidationErrors,
    string Message)
{
    public static HeadlessConfigurationSaveResult Invalid(IReadOnlyList<string> errors) =>
        new(false, false, null, errors, "Configuration validation failed.");

    public static HeadlessConfigurationSaveResult Failure(string message) =>
        new(false, false, null, [], message);
}
