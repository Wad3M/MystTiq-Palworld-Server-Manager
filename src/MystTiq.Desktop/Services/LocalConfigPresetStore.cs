using System.Text.Json;

namespace MystTiq.Desktop.Services;

// Desktop-local named QoL presets (a snapshot of whatever setting values were loaded
// when the user chose to save them), not a server-side setting -- mirrors
// LocalMapPreferencesStore's exact storage shape and location, as a sibling file in the same
// MystTiq config directory. Not synced across machines/profiles, matching how connection
// profiles and map preferences already work.
public sealed class LocalConfigPresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public LocalConfigPresetStore()
    {
        var configRoot = GetConfigRoot();
        StoragePath = Path.Combine(configRoot, "MystTiq", "config-presets.json");
    }

    public string StoragePath { get; }

    public IReadOnlyList<string> LoadPresetNames()
    {
        var presets = Load();
        return presets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyDictionary<string, string> LoadPreset(string name)
    {
        var presets = Load();
        return presets.TryGetValue(name, out var values) ? values : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public void SavePreset(string name, IReadOnlyDictionary<string, string> values)
    {
        var presets = Load();
        presets[name] = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);

        var directory = Path.GetDirectoryName(StoragePath)!;
        Directory.CreateDirectory(directory);
        var temp = StoragePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(presets, JsonOptions));
        File.Move(temp, StoragePath, overwrite: true);
    }

    private Dictionary<string, Dictionary<string, string>> Load()
    {
        try
        {
            if (!File.Exists(StoragePath)) return new(StringComparer.OrdinalIgnoreCase);
            var json = File.ReadAllText(StoragePath);
            var presets = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, JsonOptions);
            return presets is null ? new(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, Dictionary<string, string>>(presets, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // Corrupt/unreadable preference storage must not prevent the desktop shell from starting.
            return new(StringComparer.OrdinalIgnoreCase);
        }
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
