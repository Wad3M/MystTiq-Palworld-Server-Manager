using System.Net;
using System.Text.Json;
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

public sealed class HeadlessConfigurationService
{
    public const string LinuxDefaultPath = "/etc/mysttiq/mysttiq.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public HeadlessConfiguration LoadOrDefault(string? path = null)
    {
        path ??= LinuxDefaultPath;
        if (!File.Exists(path))
            return HeadlessConfiguration.CreateLinuxDefault();

        var json = File.ReadAllText(path);
        var schemaVersion = ReadSchemaVersion(json);
        if (schemaVersion == 1)
            return MigrateV1(json);

        var configuration = JsonSerializer.Deserialize<HeadlessConfiguration>(json, JsonOptions);
        return configuration ?? throw new InvalidDataException($"MystTiq configuration is empty or invalid JSON: {path}");
    }

    public ConfigurationValidationResult Validate(HeadlessConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var errors = new List<string>();

        if (configuration.SchemaVersion != HeadlessConfiguration.CurrentSchemaVersion)
            errors.Add($"Unsupported schemaVersion {configuration.SchemaVersion}; expected {HeadlessConfiguration.CurrentSchemaVersion}.");

        if (configuration.Api.Port is < 1024 or > 65535)
            errors.Add("api.port must be between 1024 and 65535.");

        IPAddress? bindAddress = null;
        if (!IPAddress.TryParse(configuration.Api.BindAddress, out bindAddress))
            errors.Add("api.bindAddress must be a literal IP address.");

        var loopback = bindAddress is not null && IPAddress.IsLoopback(bindAddress);
        if (!loopback)
        {
            if (!configuration.Api.Authentication.Enabled)
                errors.Add("Non-loopback API binding requires api.authentication.enabled=true.");
            if (!configuration.Api.Tls.Enabled)
                errors.Add("Non-loopback API binding requires api.tls.enabled=true.");
        }

        if (configuration.Api.Authentication.Enabled)
            ValidateAbsoluteLinuxPath(configuration.Api.Authentication.TokenFile, "api.authentication.tokenFile", errors);

        if (configuration.Api.Tls.Enabled)
        {
            ValidateAbsoluteLinuxPath(configuration.Api.Tls.CertificatePath, "api.tls.certificatePath", errors);
            ValidateAbsoluteLinuxPath(configuration.Api.Tls.CertificatePasswordFile, "api.tls.certificatePasswordFile", errors);
        }

        ValidatePositive(configuration.Lifecycle.StartupTimeoutSeconds, "lifecycle.startupTimeoutSeconds", errors);
        ValidatePositive(configuration.Lifecycle.StopTimeoutSeconds, "lifecycle.stopTimeoutSeconds", errors);
        ValidatePositive(configuration.Lifecycle.ServicePollSeconds, "lifecycle.servicePollSeconds", errors);
        ValidatePositive(configuration.Lifecycle.RecoveryBackoffSeconds, "lifecycle.recoveryBackoffSeconds", errors);
        ValidatePositive(configuration.Lifecycle.MaximumRecoveryAttempts, "lifecycle.maximumRecoveryAttempts", errors);
        ValidatePositive(configuration.Lifecycle.RecoveryWindowSeconds, "lifecycle.recoveryWindowSeconds", errors);

        ValidateAbsoluteLinuxPath(configuration.Server.ServerRoot, "server.serverRoot", errors);
        ValidateAbsoluteLinuxPath(configuration.Server.SteamCmdPath, "server.steamCmdPath", errors);
        ValidateAbsoluteLinuxPath(configuration.Server.BackupRoot, "server.backupRoot", errors);
        ValidateAbsoluteLinuxPath(configuration.Server.RuntimeRoot, "server.runtimeRoot", errors);

        if (configuration.Server.LaunchArguments is null || configuration.Server.LaunchArguments.Count == 0)
            errors.Add("server.launchArguments must contain at least one argument.");

        return new ConfigurationValidationResult(errors.Count == 0, errors);
    }

    public void WriteDefault(string? path = null, bool overwrite = false)
    {
        path ??= LinuxDefaultPath;
        if (File.Exists(path) && !overwrite)
            throw new IOException($"Configuration already exists: {path}");

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Configuration path has no parent directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(HeadlessConfiguration.CreateLinuxDefault(), JsonOptions));
    }


    public bool NeedsMigration(string? path = null)
    {
        path ??= LinuxDefaultPath;
        if (!File.Exists(path)) return false;
        return ReadSchemaVersion(File.ReadAllText(path)) < HeadlessConfiguration.CurrentSchemaVersion;
    }

    public HeadlessConfiguration MigrateFile(string? path = null)
    {
        path ??= LinuxDefaultPath;
        if (!File.Exists(path)) throw new FileNotFoundException("MystTiq configuration was not found.", path);
        var migrated = LoadOrDefault(path);
        var validation = Validate(migrated);
        if (!validation.Valid)
            throw new InvalidDataException("Migrated configuration did not validate: " + string.Join("; ", validation.Errors));
        File.WriteAllText(path, JsonSerializer.Serialize(migrated, JsonOptions));
        return migrated;
    }

    private static int ReadSchemaVersion(string json)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Name.Equals("schemaVersion", StringComparison.OrdinalIgnoreCase) && property.Value.TryGetInt32(out var version))
                return version;
        }
        return 1;
    }

    private static HeadlessConfiguration MigrateV1(string json)
    {
        var legacy = JsonSerializer.Deserialize<LegacyHeadlessConfigurationV1>(json, JsonOptions)
            ?? throw new InvalidDataException("Unable to deserialize MystTiq schema v1 configuration.");

        return new HeadlessConfiguration(
            HeadlessConfiguration.CurrentSchemaVersion,
            new HeadlessApiConfiguration(
                legacy.Api.Enabled,
                legacy.Api.BindAddress,
                legacy.Api.Port,
                new HeadlessApiAuthenticationConfiguration(false, "/etc/mysttiq/secrets/api-token"),
                new HeadlessApiTlsConfiguration(false, "/etc/mysttiq/certs/mysttiq.pfx", "/etc/mysttiq/secrets/certificate-password")),
            legacy.Lifecycle,
            legacy.Server);
    }

    private sealed record LegacyHeadlessApiConfigurationV1(bool Enabled, string BindAddress, int Port);
    private sealed record LegacyHeadlessConfigurationV1(
        int SchemaVersion,
        LegacyHeadlessApiConfigurationV1 Api,
        HeadlessLifecycleConfiguration Lifecycle,
        HeadlessServerConfiguration Server);

    public static ServerRuntimeConfiguration ToRuntimeConfiguration(HeadlessConfiguration configuration) =>
        new(configuration.Server.ServerRoot, configuration.Server.SteamCmdPath, configuration.Server.BackupRoot, configuration.Server.RuntimeRoot);

    private static void ValidatePositive(int value, string name, ICollection<string> errors)
    {
        if (value <= 0) errors.Add($"{name} must be greater than zero.");
    }

    private static void ValidateAbsoluteLinuxPath(string? value, string name, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("/", StringComparison.Ordinal))
            errors.Add($"{name} must be an absolute Linux path.");
    }
}
