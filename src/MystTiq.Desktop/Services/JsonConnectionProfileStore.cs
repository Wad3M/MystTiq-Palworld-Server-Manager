using System.Text.Json;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

public sealed class JsonConnectionProfileStore : IConnectionProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public JsonConnectionProfileStore()
    {
        var configRoot = GetConfigRoot();
        StoragePath = Path.Combine(configRoot, "MystTiq", "connections.json");
    }

    public string StoragePath { get; }

    public IReadOnlyList<ConnectionProfile> Load()
    {
        try
        {
            if (!File.Exists(StoragePath))
                return [ConnectionProfile.LocalDefault];

            var json = File.ReadAllText(StoragePath);
            var profiles = JsonSerializer.Deserialize<List<ConnectionProfile>>(json, JsonOptions) ?? [];
            if (profiles.All(p => p.Id != ConnectionProfile.LocalDefault.Id))
                profiles.Insert(0, ConnectionProfile.LocalDefault);

            return profiles;
        }
        catch
        {
            // Corrupt/unreadable profile storage must not prevent the desktop shell from starting.
            return [ConnectionProfile.LocalDefault];
        }
    }

    public void Save(IEnumerable<ConnectionProfile> profiles)
    {
        var safeProfiles = profiles
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();

        var directory = Path.GetDirectoryName(StoragePath)!;
        Directory.CreateDirectory(directory);

        var temp = StoragePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(safeProfiles, JsonOptions));
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
