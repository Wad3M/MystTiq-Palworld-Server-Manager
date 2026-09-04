namespace MystTiq.Core.Models;

public sealed record HeadlessApiAuthenticationConfiguration(
    bool Enabled,
    string TokenFile);

public sealed record HeadlessApiTlsConfiguration(
    bool Enabled,
    string CertificatePath,
    string CertificatePasswordFile);

public sealed record HeadlessApiConfiguration(
    bool Enabled,
    string BindAddress,
    int Port,
    HeadlessApiAuthenticationConfiguration Authentication,
    HeadlessApiTlsConfiguration Tls);

public sealed record HeadlessLifecycleConfiguration(
    int StartupTimeoutSeconds,
    int StopTimeoutSeconds,
    int ServicePollSeconds,
    int RecoveryBackoffSeconds,
    int MaximumRecoveryAttempts,
    int RecoveryWindowSeconds);

// Kept for backward-compatible schema-v1/v2 migration only -- superseded by
// HeadlessServerProfileConfiguration (schema v3, multi-server fleet).
public sealed record HeadlessServerConfiguration(
    string ServerRoot,
    string SteamCmdPath,
    string BackupRoot,
    string RuntimeRoot,
    IReadOnlyList<string> LaunchArguments);

// v0.6.2.0 runtime-provider seam: two real implementations (Windows/Linux native), each
// wrapping the existing WindowsServerLifecycleService/LinuxServerLifecycleService unchanged.
// Docker/Wine are intentionally not added as enum members yet -- see docs/architecture
// v0.6.2.0 implementation note for why (need real Docker/Wine environments to verify against).
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ServerRuntimeKind { WindowsNative, LinuxNative }

public sealed record HeadlessServerProfileConfiguration(
    string Id,
    string Name,
    string ServerRoot,
    string SteamCmdPath,
    string BackupRoot,
    string RuntimeRoot,
    IReadOnlyList<string> LaunchArguments,
    ServerRuntimeKind Runtime = ServerRuntimeKind.WindowsNative);

public sealed record HeadlessConfiguration(
    int SchemaVersion,
    HeadlessApiConfiguration Api,
    HeadlessLifecycleConfiguration Lifecycle,
    IReadOnlyList<HeadlessServerProfileConfiguration> Servers,
    string FleetRoot,
    int FleetStaggerSeconds = 5)
{
    public const int CurrentSchemaVersion = 3;
    public const string DefaultServerProfileId = "default";

    public HeadlessServerProfileConfiguration? FindServer(string id) =>
        Servers.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    // Computed convenience, not persisted state -- must not round-trip into the JSON file.
    [System.Text.Json.Serialization.JsonIgnore]
    public HeadlessServerProfileConfiguration DefaultServer =>
        FindServer(DefaultServerProfileId) ?? Servers[0];

    public static HeadlessConfiguration CreateDefaultForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? CreateWindowsDefault() : CreateLinuxDefault();

    public static HeadlessConfiguration CreateWindowsDefault()
    {
        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var root = Path.Combine(common, "MystTiqPalworldServer");
        return new(
            CurrentSchemaVersion,
            new HeadlessApiConfiguration(
                Enabled: true,
                BindAddress: "127.0.0.1",
                Port: 8213,
                Authentication: new HeadlessApiAuthenticationConfiguration(false, Path.Combine(root, "secrets", "api-token")),
                Tls: new HeadlessApiTlsConfiguration(false, Path.Combine(root, "certs", "mysttiq.pfx"), Path.Combine(root, "secrets", "certificate-password"))),
            new HeadlessLifecycleConfiguration(90, 30, 5, 10, 5, 300),
            [
                new HeadlessServerProfileConfiguration(
                    DefaultServerProfileId,
                    "Default Server",
                    @"C:\GameServers\Palworld\Server",
                    @"C:\GameServers\Palworld\SteamCMD\steamcmd.exe",
                    @"C:\GameServers\Palworld\Server\Backups",
                    Path.Combine(root, "runtime"),
                    ["-useperfthreads", "-NoAsyncLoadingThread", "-UseMultithreadForDS", "-stdout", "-FullStdOutLogOutput", "-logformat=text"],
                    ServerRuntimeKind.WindowsNative)
            ],
            Path.Combine(root, "fleet"));
    }

    public static HeadlessConfiguration CreateLinuxDefault() =>
        new(
            CurrentSchemaVersion,
            new HeadlessApiConfiguration(
                Enabled: true,
                BindAddress: "127.0.0.1",
                Port: 8213,
                Authentication: new HeadlessApiAuthenticationConfiguration(
                    Enabled: false,
                    TokenFile: "/etc/mysttiq/secrets/api-token"),
                Tls: new HeadlessApiTlsConfiguration(
                    Enabled: false,
                    CertificatePath: "/etc/mysttiq/certs/mysttiq.pfx",
                    CertificatePasswordFile: "/etc/mysttiq/secrets/certificate-password")),
            new HeadlessLifecycleConfiguration(
                StartupTimeoutSeconds: 90,
                StopTimeoutSeconds: 30,
                ServicePollSeconds: 5,
                RecoveryBackoffSeconds: 10,
                MaximumRecoveryAttempts: 5,
                RecoveryWindowSeconds: 300),
            [
                new HeadlessServerProfileConfiguration(
                    DefaultServerProfileId,
                    "Default Server",
                    ServerRoot: "/opt/mysttiq/palserver",
                    SteamCmdPath: "/opt/mysttiq/steamcmd/steamcmd.sh",
                    BackupRoot: "/opt/mysttiq/backups",
                    RuntimeRoot: "/opt/mysttiq/runtime",
                    LaunchArguments:
                    [
                        "-useperfthreads",
                        "-NoAsyncLoadingThread",
                        "-UseMultithreadForDS",
                        "-log",
                        "-logformat=text"
                    ],
                    Runtime: ServerRuntimeKind.LinuxNative)
            ],
            "/opt/mysttiq/fleet");
}

public sealed record ConfigurationValidationResult(
    bool Valid,
    IReadOnlyList<string> Errors);
