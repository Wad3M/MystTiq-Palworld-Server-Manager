using System.Security.Cryptography;
using System.Text;

namespace MystTiq.Core.Services;

public sealed class HeadlessSecretFileService
{
    public string ReadRequiredSecret(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) throw new FileNotFoundException("Required MystTiq secret file was not found.", path);
        var value = File.ReadAllText(path).Trim();
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException($"MystTiq secret file is empty: {path}");
        return value;
    }

    public string GenerateBearerToken(int bytes = 32)
    {
        if (bytes < 32) throw new ArgumentOutOfRangeException(nameof(bytes), "Bearer tokens must contain at least 32 random bytes.");
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();
    }

    public void WriteSecret(string path, string value, bool overwrite = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (File.Exists(path) && !overwrite) throw new IOException($"Secret already exists: {path}");
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Secret path has no parent directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, value + Environment.NewLine, new UTF8Encoding(false));
        if (OperatingSystem.IsLinux())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public static bool FixedTimeEquals(string expected, string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
