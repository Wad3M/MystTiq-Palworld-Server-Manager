using System.Text.Json;

namespace MystTiq.Desktop.Services;

// Desktop-local preference (which accent theme and dark/light variant to apply), not a
// server-side setting -- mirrors LocalMapPreferencesStore's exact storage shape and location, as a
// sibling file in the same MystTiq config directory.
public sealed class LocalThemePreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public LocalThemePreferencesStore()
    {
        var configRoot = GetConfigRoot();
        StoragePath = Path.Combine(configRoot, "MystTiq", "theme-preferences.json");
    }

    public string StoragePath { get; }

    public (string AccentTheme, string Variant) Load()
    {
        try
        {
            if (!File.Exists(StoragePath)) return ("Default", "Dark");
            var json = File.ReadAllText(StoragePath);
            var preferences = JsonSerializer.Deserialize<ThemePreferences>(json, JsonOptions);
            return (
                string.IsNullOrWhiteSpace(preferences?.AccentTheme) ? "Default" : preferences.AccentTheme,
                string.IsNullOrWhiteSpace(preferences?.Variant) ? "Dark" : preferences.Variant);
        }
        catch
        {
            // Corrupt/unreadable preference storage must not prevent the desktop shell from starting.
            return ("Default", "Dark");
        }
    }

    public void Save(string accentTheme, string variant)
    {
        var directory = Path.GetDirectoryName(StoragePath)!;
        Directory.CreateDirectory(directory);

        var temp = StoragePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new ThemePreferences(accentTheme, variant), JsonOptions));
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

    private sealed record ThemePreferences(string? AccentTheme, string? Variant);
}
