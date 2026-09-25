using System.Text.Json;

namespace MystTiq.Desktop.Services;

// v0.8.16.0: display preferences that apply to every tab (the theme mode is per tab, in ConnectionProfile). Today that
// is density. A sibling file of the other Desktop-local preferences, in the same MystTiq config folder.
public sealed class LocalDisplayPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public LocalDisplayPreferencesStore()
    {
        StoragePath = Path.Combine(LocalMapPreferencesStore.GetLocalConfigRoot(), "MystTiq", "display-preferences.json");
    }

    public string StoragePath { get; }

    public string LoadDensity()
    {
        try
        {
            if (!File.Exists(StoragePath)) return "Comfortable";
            var preferences = JsonSerializer.Deserialize<DisplayPreferences>(File.ReadAllText(StoragePath), JsonOptions);
            return ThemeCatalog.NormalizeDensity(preferences?.Density);
        }
        catch
        {
            // Unreadable preferences must not stop the Desktop from starting.
            return "Comfortable";
        }
    }

    public void SaveDensity(string density)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
        var temp = StoragePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new DisplayPreferences(ThemeCatalog.NormalizeDensity(density)), JsonOptions));
        File.Move(temp, StoragePath, overwrite: true);
    }

    private sealed record DisplayPreferences(string? Density);
}
