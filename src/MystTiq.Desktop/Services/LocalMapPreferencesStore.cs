using System.Text.Json;

namespace MystTiq.Desktop.Services;

// v0.6.16.0: Desktop-local preference (which machine's map background image to show, if any),
// not a server-side setting -- mirrors JsonConnectionProfileStore's exact storage shape and
// location, as a sibling file in the same MystTiq config directory.
public sealed class LocalMapPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public LocalMapPreferencesStore()
    {
        var configRoot = GetConfigRoot();
        StoragePath = Path.Combine(configRoot, "MystTiq", "map-preferences.json");
    }

    public string StoragePath { get; }

    // v0.7.20.0: exposed so MapPresetService can extract bundled preset images alongside this
    // same config root, rather than duplicating the cross-platform resolution logic below.
    public static string GetLocalConfigRoot() => GetConfigRoot();

    // True once any map background choice has been saved, including choosing "no background".
    // Distinguishes a first run from a deliberate Clear Background.
    public bool HasSavedPreference => File.Exists(StoragePath);

    public string? LoadBackgroundImagePath()
    {
        try
        {
            if (!File.Exists(StoragePath)) return null;
            var json = File.ReadAllText(StoragePath);
            var preferences = JsonSerializer.Deserialize<MapPreferences>(json, JsonOptions);
            return string.IsNullOrWhiteSpace(preferences?.BackgroundImagePath) ? null : preferences.BackgroundImagePath;
        }
        catch
        {
            // Corrupt/unreadable preference storage must not prevent the desktop shell from starting.
            return null;
        }
    }

    public void SaveBackgroundImagePath(string? path)
    {
        var directory = Path.GetDirectoryName(StoragePath)!;
        Directory.CreateDirectory(directory);

        var temp = StoragePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new MapPreferences(path), JsonOptions));
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

    private sealed record MapPreferences(string? BackgroundImagePath);
}
