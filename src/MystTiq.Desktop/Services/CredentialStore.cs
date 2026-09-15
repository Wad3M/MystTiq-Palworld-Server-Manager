using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MystTiq.Desktop.Services;

// v0.7.71.0: persists connection-profile bearer tokens across app restarts, encrypted with Windows
// DPAPI (DataProtectionScope.CurrentUser) -- decryptable only by this same Windows account on this
// same machine, with no separate password/key for the app to manage or for the user to ever type.
// Before this, ConnectionProfile deliberately kept tokens process-memory-only (see its own docstring)
// specifically to avoid persisting a secret in plain text; DPAPI removes that tradeoff, so this
// reverses that decision rather than routing around it. See memory: project_mysttiq_credential_
// storage_plan.md for the fuller design writeup, including why a plaintext-first step was skipped.
//
// Not available on non-Windows builds (DesktopLinux) -- ProtectedData.Protect/Unprotect throws
// PlatformNotSupportedException there. Every public method checks OperatingSystem.IsWindows() first
// and no-ops otherwise, so callers don't need their own platform checks; Linux keeps today's
// process-memory-only behavior unchanged, not a regression, just not yet extended.
public sealed class CredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // DPAPI's "optional entropy" -- an extra, app-specific secret mixed into the encryption so a
    // ciphertext produced by this store can't be decrypted by some other DPAPI-using app running as
    // the same Windows user. Not itself a secret that needs protecting (DPAPI's real protection is
    // the user's own master key); this only needs to be constant and specific to MystTiq.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MystTiq.Desktop.CredentialStore.v1");

    public CredentialStore()
    {
        var configRoot = GetConfigRoot();
        StoragePath = Path.Combine(configRoot, "MystTiq", "credentials.json");
    }

    public string StoragePath { get; }

    public string? TryLoad(string profileId)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(profileId))
            return null;

        try
        {
            if (!File.Exists(StoragePath)) return null;
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(StoragePath), JsonOptions);
            if (entries is null || !entries.TryGetValue(profileId, out var cipherBase64))
                return null;

            var cipher = Convert.FromBase64String(cipherBase64);
            var plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            // Corrupt store, a ciphertext from a different Windows user/machine, or any other
            // decrypt failure must not block connecting -- the caller falls back to an empty token
            // (the same as today's process-memory-only behavior) and the user re-enters it.
            return null;
        }
    }

    public void Save(string profileId, string token)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(token))
            return;

        try
        {
            var entries = LoadAll();
            var cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
            entries[profileId] = Convert.ToBase64String(cipher);
            WriteAll(entries);
        }
        catch
        {
            // Best-effort: a failed save just means the token isn't remembered next launch, not a
            // reason to interrupt an otherwise-successful connect.
        }
    }

    public void Delete(string profileId)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(profileId))
            return;

        try
        {
            var entries = LoadAll();
            if (entries.Remove(profileId))
                WriteAll(entries);
        }
        catch
        {
            // Best-effort cleanup; a leftover encrypted entry for a since-deleted profile is inert
            // (never looked up again by a real Id) rather than a live risk.
        }
    }

    private Dictionary<string, string> LoadAll()
    {
        if (!File.Exists(StoragePath)) return new Dictionary<string, string>();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(StoragePath), JsonOptions)
            ?? new Dictionary<string, string>();
    }

    private void WriteAll(Dictionary<string, string> entries)
    {
        var directory = Path.GetDirectoryName(StoragePath)!;
        Directory.CreateDirectory(directory);

        var temp = StoragePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(entries, JsonOptions));
        File.Move(temp, StoragePath, overwrite: true);
    }

    private static string GetConfigRoot()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
            return xdg;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config");
    }
}
