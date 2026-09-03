using MystTiq.Core.Models;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessConfigurationApiService
{
    private readonly string configurationPath;
    private readonly HeadlessConfigurationService configurationService;
    private readonly HeadlessConfiguration runningConfiguration;
    private readonly SemaphoreSlim configurationGate = new(1, 1);

    public HeadlessConfigurationApiService(
        string configurationPath,
        HeadlessConfiguration runningConfiguration,
        HeadlessConfigurationService? configurationService = null)
    {
        this.configurationPath = configurationPath;
        this.runningConfiguration = runningConfiguration;
        this.configurationService = configurationService ?? new HeadlessConfigurationService();
    }

    public HeadlessEditableConfiguration GetEditable()
    {
        var current = configurationService.LoadOrDefault(configurationPath);

        return HeadlessEditableConfiguration.From(
            current,
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

            var updated = current with
            {
                Api = current.Api with
                {
                    Enabled = request.Api.Enabled,
                    BindAddress = request.Api.BindAddress.Trim(),
                    Port = request.Api.Port
                },
                Lifecycle = request.Lifecycle,
                Server = new HeadlessServerConfiguration(
                    request.Server.ServerRoot.Trim(),
                    request.Server.SteamCmdPath.Trim(),
                    request.Server.BackupRoot.Trim(),
                    request.Server.RuntimeRoot.Trim(),
                    request.Server.LaunchArguments
                        .Where(argument => !string.IsNullOrWhiteSpace(argument))
                        .Select(argument => argument.Trim())
                        .ToArray())
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
                configuration.Server.ServerRoot,
                configuration.Server.SteamCmdPath,
                configuration.Server.BackupRoot,
                configuration.Server.RuntimeRoot,
                configuration.Server.LaunchArguments),
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
