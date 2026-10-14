// MystTiq v1.0.8.4: file reviewed for this release (2026-10-12).
using System.Security.Cryptography;
using System.Text;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.7.4 (roadmap Q-14): the SMTP password, the webhook and Discord webhook URLs (each URL is its own password) and the
/// Discord bot token were kept in plain text, protected only by the folder's permissions. On Windows they are now encrypted
/// with DPAPI in machine scope (the service may run as LocalSystem or as the desktop's user, and both must read them;
/// a copy of the file on another computer cannot). On Linux there is no machine key store a headless service can rely on,
/// so the files holding them are made readable by their owner only (0600).
/// </summary>
public static class SecretProtector
{
    public const string Prefix = "protected:v1:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MystTiq.saved-secret.v1");

    public static bool IsProtected(string? value) => value?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    /// <summary>True when the value is a secret written in plain text that this platform would encrypt.</summary>
    public static bool NeedsProtection(string? value) => OperatingSystem.IsWindows() && !string.IsNullOrEmpty(value) && !IsProtected(value);

    public static string? Protect(string? value)
    {
        if (!NeedsProtection(value) || !OperatingSystem.IsWindows()) return value;
        var cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(value!), Entropy, DataProtectionScope.LocalMachine);
        return Prefix + Convert.ToBase64String(cipher);
    }

    /// <summary>The plain value. A value that cannot be decrypted (copied from another computer) reads as empty.</summary>
    public static string? Unprotect(string? value)
    {
        if (!IsProtected(value)) return value;
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var plain = ProtectedData.Unprotect(Convert.FromBase64String(value![Prefix.Length..]), Entropy, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException) { return null; }
    }

    /// <summary>On Linux and macOS, makes the file readable and writable by its owner only. Does nothing on Windows.</summary>
    public static void RestrictToOwner(string path)
    {
        if (OperatingSystem.IsWindows() || !File.Exists(path)) return;
        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Keeps the plain file once, beside it, before its secrets are first encrypted (in case the encryption must be undone);
    /// Server Doctor says to delete it once the channels work. Returns the copy's path, or null when one exists already.
    /// </summary>
    public static string? KeepPlainCopyOnce(string path)
    {
        var copy = path + ".before-encryption";
        if (File.Exists(copy) || !File.Exists(path)) return null;
        File.Copy(path, copy);
        return copy;
    }
}
