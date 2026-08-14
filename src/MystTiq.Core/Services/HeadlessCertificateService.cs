using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

public sealed record HeadlessCertificateCreateResult(
    string CertificatePath,
    string PasswordFile,
    string Subject,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter,
    string Thumbprint);

public sealed class HeadlessCertificateService
{
    private readonly HeadlessSecretFileService secretFiles;

    public HeadlessCertificateService(HeadlessSecretFileService? secretFiles = null)
    {
        this.secretFiles = secretFiles ?? new HeadlessSecretFileService();
    }

    public HeadlessCertificateCreateResult CreateSelfSignedServerCertificate(
        string certificatePath,
        string passwordFile,
        string bindAddress,
        string? dnsName = null,
        bool overwrite = false,
        int validityDays = 825)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindAddress);

        if (!IPAddress.TryParse(bindAddress, out var ipAddress))
            throw new ArgumentException("Certificate bind address must be a literal IP address.", nameof(bindAddress));

        if (validityDays is < 1 or > 825)
            throw new ArgumentOutOfRangeException(nameof(validityDays), "Certificate validity must be between 1 and 825 days.");

        if (File.Exists(certificatePath) && !overwrite)
            throw new IOException($"Certificate already exists: {certificatePath}");
        if (File.Exists(passwordFile) && !overwrite)
            throw new IOException($"Certificate password file already exists: {passwordFile}");

        var certificateDirectory = Path.GetDirectoryName(certificatePath)
            ?? throw new InvalidOperationException("Certificate path has no parent directory.");
        Directory.CreateDirectory(certificateDirectory);

        var password = secretFiles.GenerateBearerToken(32);
        secretFiles.WriteSecret(passwordFile, password, overwrite);

        using var rsa = RSA.Create(3072);
        var commonName = string.IsNullOrWhiteSpace(dnsName) ? ipAddress.ToString() : dnsName.Trim();
        var request = new CertificateRequest(
            $"CN={commonName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, critical: true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        var eku = new OidCollection
        {
            new Oid("1.3.6.1.5.5.7.3.1") // TLS Web Server Authentication
        };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, critical: false));

        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(ipAddress);
        san.AddDnsName("localhost");
        if (!string.IsNullOrWhiteSpace(dnsName))
            san.AddDnsName(dnsName.Trim());
        request.CertificateExtensions.Add(san.Build());

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = notBefore.AddDays(validityDays);

        using var certificate = request.CreateSelfSigned(notBefore, notAfter);
        var pfx = certificate.Export(X509ContentType.Pfx, password);
        File.WriteAllBytes(certificatePath, pfx);

        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(
                certificatePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return new HeadlessCertificateCreateResult(
            certificatePath,
            passwordFile,
            certificate.Subject,
            notBefore,
            notAfter,
            certificate.Thumbprint);
    }
}
