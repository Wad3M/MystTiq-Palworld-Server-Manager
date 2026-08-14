using System.Net;
using System.Text.Json;
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

public sealed class HeadlessRemoteApiEnrollmentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly HeadlessConfigurationService configurationService;

    public HeadlessRemoteApiEnrollmentService(HeadlessConfigurationService? configurationService = null)
    {
        this.configurationService = configurationService ?? new HeadlessConfigurationService();
    }

    public HeadlessConfiguration EnableRemoteApi(
        string configurationPath,
        string bindAddress,
        int port,
        string tokenFile,
        string certificatePath,
        string certificatePasswordFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindAddress);

        if (!IPAddress.TryParse(bindAddress, out var ipAddress))
            throw new ArgumentException("Remote API bind address must be a literal IP address.", nameof(bindAddress));
        if (IPAddress.IsLoopback(ipAddress))
            throw new ArgumentException("Remote API enrollment requires a non-loopback bind address.", nameof(bindAddress));
        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Remote API port must be between 1024 and 65535.");

        var configuration = configurationService.LoadOrDefault(configurationPath);
        var updated = configuration with
        {
            Api = configuration.Api with
            {
                Enabled = true,
                BindAddress = bindAddress,
                Port = port,
                Authentication = configuration.Api.Authentication with
                {
                    Enabled = true,
                    TokenFile = tokenFile
                },
                Tls = configuration.Api.Tls with
                {
                    Enabled = true,
                    CertificatePath = certificatePath,
                    CertificatePasswordFile = certificatePasswordFile
                }
            }
        };

        var validation = configurationService.Validate(updated);
        if (!validation.Valid)
            throw new InvalidDataException(
                "Remote API configuration did not validate: " +
                string.Join("; ", validation.Errors));

        var directory = Path.GetDirectoryName(configurationPath)
            ?? throw new InvalidOperationException("Configuration path has no parent directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(configurationPath, JsonSerializer.Serialize(updated, JsonOptions));

        return updated;
    }

    public HeadlessConfiguration DisableRemoteApi(string configurationPath)
    {
        var configuration = configurationService.LoadOrDefault(configurationPath);
        var defaults = HeadlessConfiguration.CreateLinuxDefault();

        var updated = configuration with
        {
            Api = configuration.Api with
            {
                BindAddress = defaults.Api.BindAddress,
                Port = defaults.Api.Port,
                Authentication = configuration.Api.Authentication with { Enabled = false },
                Tls = configuration.Api.Tls with { Enabled = false }
            }
        };

        File.WriteAllText(configurationPath, JsonSerializer.Serialize(updated, JsonOptions));
        return updated;
    }
}
