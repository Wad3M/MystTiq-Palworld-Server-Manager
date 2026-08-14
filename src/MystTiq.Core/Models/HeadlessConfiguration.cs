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

public sealed record HeadlessServerConfiguration(
    string ServerRoot,
    string SteamCmdPath,
    string BackupRoot,
    string RuntimeRoot,
    IReadOnlyList<string> LaunchArguments);

public sealed record HeadlessConfiguration(
    int SchemaVersion,
    HeadlessApiConfiguration Api,
    HeadlessLifecycleConfiguration Lifecycle,
    HeadlessServerConfiguration Server)
{
    public const int CurrentSchemaVersion = 2;

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
            new HeadlessServerConfiguration(
                ServerRoot: "/opt/mysttiq/palserver",
                SteamCmdPath: "/opt/mysttiq/steamcmd/steamcmd.sh",
                BackupRoot: "/opt/mysttiq/backups",
                RuntimeRoot: "/opt/mysttiq/runtime",
                LaunchArguments:
                [
                    "EpicApp=PalServer",
                    "-useperfthreads",
                    "-NoAsyncLoadingThread",
                    "-UseMultithreadForDS"
                ]));
}

public sealed record ConfigurationValidationResult(
    bool Valid,
    IReadOnlyList<string> Errors);
