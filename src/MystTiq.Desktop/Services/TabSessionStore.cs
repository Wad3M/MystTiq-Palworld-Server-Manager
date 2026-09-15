using System.Text.Json;

namespace MystTiq.Desktop.Services;

// v0.7.72.0: remembers which connection profiles were open as tabs, so they reopen automatically
// on next launch instead of the app always starting back at just the single default local tab.
// Reported live: this became a real gap specifically once CredentialStore (v0.7.71.0) started
// remembering bearer tokens too -- a remembered token that still requires manually re-adding the
// tab it belongs to only gets you halfway to "it just reconnects." Stores plain profile Ids, not
// secrets, so no encryption is needed here (contrast CredentialStore).
public sealed class TabSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public TabSessionStore()
    {
        var configRoot = GetConfigRoot();
        StoragePath = Path.Combine(configRoot, "MystTiq", "open-tabs.json");
    }

    public string StoragePath { get; }

    public IReadOnlyList<string> Load()
    {
        try
        {
            if (!File.Exists(StoragePath)) return [];
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(StoragePath), JsonOptions) ?? [];
        }
        catch
        {
            // Corrupt/unreadable session storage must not prevent the desktop shell from starting;
            // worst case is falling back to today's single-default-tab behavior.
            return [];
        }
    }

    public void Save(IEnumerable<string> profileIds)
    {
        try
        {
            var directory = Path.GetDirectoryName(StoragePath)!;
            Directory.CreateDirectory(directory);

            var temp = StoragePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(profileIds.ToList(), JsonOptions));
            File.Move(temp, StoragePath, overwrite: true);
        }
        catch
        {
            // Best-effort: a failed save just means the next launch falls back to the default tab,
            // not a reason to interrupt whatever the user was doing when a tab opened/closed.
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
