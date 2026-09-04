using System.Net;
using System.Text.Json;
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

public sealed class HeadlessConfigurationService
{
    public const string LinuxDefaultPath = "/etc/mysttiq/mysttiq.json";
    public static string WindowsDefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MystTiqPalworldServer", "mysttiq.json");
    public static string DefaultPath => OperatingSystem.IsWindows() ? WindowsDefaultPath : LinuxDefaultPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public HeadlessConfiguration LoadOrDefault(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
            return HeadlessConfiguration.CreateDefaultForCurrentPlatform();

        var json = File.ReadAllText(path);
        var schemaVersion = ReadSchemaVersion(json);
        if (schemaVersion == 1)
            return MigrateV2(MigrateV1Json(json));
        if (schemaVersion == 2)
            return MigrateV2(json);

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
            ValidateAbsolutePath(configuration.Api.Authentication.TokenFile, "api.authentication.tokenFile", errors);

        if (configuration.Api.Tls.Enabled)
        {
            ValidateAbsolutePath(configuration.Api.Tls.CertificatePath, "api.tls.certificatePath", errors);
            ValidateAbsolutePath(configuration.Api.Tls.CertificatePasswordFile, "api.tls.certificatePasswordFile", errors);
        }

        ValidatePositive(configuration.Lifecycle.StartupTimeoutSeconds, "lifecycle.startupTimeoutSeconds", errors);
        ValidatePositive(configuration.Lifecycle.StopTimeoutSeconds, "lifecycle.stopTimeoutSeconds", errors);
        ValidatePositive(configuration.Lifecycle.ServicePollSeconds, "lifecycle.servicePollSeconds", errors);
        ValidatePositive(configuration.Lifecycle.RecoveryBackoffSeconds, "lifecycle.recoveryBackoffSeconds", errors);
        ValidatePositive(configuration.Lifecycle.MaximumRecoveryAttempts, "lifecycle.maximumRecoveryAttempts", errors);
        ValidatePositive(configuration.Lifecycle.RecoveryWindowSeconds, "lifecycle.recoveryWindowSeconds", errors);

        ValidateAbsolutePath(configuration.FleetRoot, "fleetRoot", errors);
        if (configuration.FleetStaggerSeconds < 0)
            errors.Add("fleetStaggerSeconds must be zero or greater.");

        if (configuration.Servers is null || configuration.Servers.Count == 0)
        {
            errors.Add("servers must contain at least one server profile.");
        }
        else
        {
            if (configuration.FindServer(HeadlessConfiguration.DefaultServerProfileId) is null)
                errors.Add($"servers must include a profile with id \"{HeadlessConfiguration.DefaultServerProfileId}\".");

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var server in configuration.Servers)
            {
                if (string.IsNullOrWhiteSpace(server.Id))
                    errors.Add("Every server profile must have a non-empty id.");
                else if (!seenIds.Add(server.Id))
                    errors.Add($"Duplicate server profile id \"{server.Id}\".");

                var prefix = $"servers[{server.Id}]";
                ValidateAbsolutePath(server.ServerRoot, $"{prefix}.serverRoot", errors);
                ValidateAbsolutePath(server.SteamCmdPath, $"{prefix}.steamCmdPath", errors);
                ValidateAbsolutePath(server.BackupRoot, $"{prefix}.backupRoot", errors);
                ValidateAbsolutePath(server.RuntimeRoot, $"{prefix}.runtimeRoot", errors);

                if (server.LaunchArguments is null || server.LaunchArguments.Count == 0)
                    errors.Add($"{prefix}.launchArguments must contain at least one argument.");
            }
        }

        return new ConfigurationValidationResult(errors.Count == 0, errors);
    }

    public string SaveValidated(
        HeadlessConfiguration configuration,
        string? path = null,
        bool createRollbackCopy = true)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        path ??= DefaultPath;

        var validation = Validate(configuration);
        if (!validation.Valid)
            throw new InvalidDataException(
                "Configuration did not validate: " + string.Join("; ", validation.Errors));

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Configuration path has no parent directory.");
        Directory.CreateDirectory(directory);

        string? rollbackPath = null;
        if (createRollbackCopy && File.Exists(path))
        {
            rollbackPath = path + $".pre-api-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.bak";
            File.Copy(path, rollbackPath, overwrite: false);
        }

        var tempPath = path + $".tmp-{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(configuration, JsonOptions));
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }

        return rollbackPath ?? string.Empty;
    }

    public void WriteDefault(string? path = null, bool overwrite = false)
    {
        path ??= DefaultPath;
        if (File.Exists(path) && !overwrite)
            throw new IOException($"Configuration already exists: {path}");

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Configuration path has no parent directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(HeadlessConfiguration.CreateDefaultForCurrentPlatform(), JsonOptions));
    }


    public bool NeedsMigration(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return false;
        return ReadSchemaVersion(File.ReadAllText(path)) < HeadlessConfiguration.CurrentSchemaVersion;
    }

    public HeadlessConfiguration MigrateFile(string? path = null)
    {
        path ??= DefaultPath;
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

    // v1 -> v3: deserialize as legacy v1 shape, re-serialize as legacy v2 shape (single Server
    // block, current-platform auth/TLS defaults disabled same as before), then hand off to MigrateV2
    // so both migration paths converge on one v2->v3 wrapping step.
    private static string MigrateV1Json(string json)
    {
        var legacy = JsonSerializer.Deserialize<LegacyHeadlessConfigurationV1>(json, JsonOptions)
            ?? throw new InvalidDataException("Unable to deserialize MystTiq schema v1 configuration.");

        var defaults = HeadlessConfiguration.CreateDefaultForCurrentPlatform();
        var v2 = new LegacyHeadlessConfigurationV2(
            2,
            new HeadlessApiConfiguration(
                legacy.Api.Enabled,
                legacy.Api.BindAddress,
                legacy.Api.Port,
                defaults.Api.Authentication with { Enabled = false },
                defaults.Api.Tls with { Enabled = false }),
            legacy.Lifecycle,
            legacy.Server);
        return JsonSerializer.Serialize(v2, JsonOptions);
    }

    // v2 -> v3: the old single `Server` block becomes the sole entry in `Servers`, id "default" --
    // an existing single-server deployment upgrades with zero behavior change (every unprefixed
    // route still resolves to this profile). FleetRoot (new in v3, shared state for the
    // fleet-level singletons -- OperationCoordinator/RBAC/AuthAbuseGuard) derives from the
    // platform default's fleet path since v2 configs never had one.
    private static HeadlessConfiguration MigrateV2(string json)
    {
        var legacy = JsonSerializer.Deserialize<LegacyHeadlessConfigurationV2>(json, JsonOptions)
            ?? throw new InvalidDataException("Unable to deserialize MystTiq schema v2 configuration.");

        var defaults = HeadlessConfiguration.CreateDefaultForCurrentPlatform();
        return new HeadlessConfiguration(
            HeadlessConfiguration.CurrentSchemaVersion,
            legacy.Api,
            legacy.Lifecycle,
            [
                new HeadlessServerProfileConfiguration(
                    HeadlessConfiguration.DefaultServerProfileId,
                    "Default Server",
                    legacy.Server.ServerRoot,
                    legacy.Server.SteamCmdPath,
                    legacy.Server.BackupRoot,
                    legacy.Server.RuntimeRoot,
                    legacy.Server.LaunchArguments,
                    OperatingSystem.IsWindows() ? ServerRuntimeKind.WindowsNative : ServerRuntimeKind.LinuxNative)
            ],
            defaults.FleetRoot);
    }

    private sealed record LegacyHeadlessApiConfigurationV1(bool Enabled, string BindAddress, int Port);
    private sealed record LegacyHeadlessConfigurationV1(
        int SchemaVersion,
        LegacyHeadlessApiConfigurationV1 Api,
        HeadlessLifecycleConfiguration Lifecycle,
        HeadlessServerConfiguration Server);

    private sealed record LegacyHeadlessConfigurationV2(
        int SchemaVersion,
        HeadlessApiConfiguration Api,
        HeadlessLifecycleConfiguration Lifecycle,
        HeadlessServerConfiguration Server);

    public static ServerRuntimeConfiguration ToRuntimeConfiguration(HeadlessServerProfileConfiguration server) =>
        new(server.ServerRoot, server.SteamCmdPath, server.BackupRoot, server.RuntimeRoot);

    public static ServerRuntimeConfiguration ToRuntimeConfiguration(HeadlessConfiguration configuration) =>
        ToRuntimeConfiguration(configuration.DefaultServer);

    private static void ValidatePositive(int value, string name, ICollection<string> errors)
    {
        if (value <= 0) errors.Add($"{name} must be greater than zero.");
    }

    private static void ValidateAbsolutePath(string? value, string name, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} must be an absolute path.");
            return;
        }

        var absolute = OperatingSystem.IsWindows()
            ? Path.IsPathFullyQualified(value)
            : value.StartsWith("/", StringComparison.Ordinal);
        if (!absolute)
            errors.Add($"{name} must be an absolute path for the current platform.");
    }
}
