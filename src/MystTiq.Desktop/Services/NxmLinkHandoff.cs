// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
using System.Runtime.Versioning;
using Microsoft.Win32;
using MystTiq.Core.Services;

namespace MystTiq.Desktop.Services;

// v1.0.5.0 (roadmap M-1; the owner's Nexus account is free): Nexus gives free accounts files only through the site's
// "Mod Manager Download" button, which opens an nxm:// link with whatever program handles them. When the owner chooses
// MystTiq for that (NxmProtocolRegistration, off until they press it), the browser starts MystTiq with the link; a second
// MystTiq hands it to the running one through this per-user inbox and exits. The running one only fills the link in and
// says so: the install still waits for the owner's click and confirmation.
public sealed class NxmLinkHandoff(string? root = null)
{
    public string Inbox { get; } = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MystTiq", "nxm-inbox");
    private static readonly TimeSpan MaximumAge = TimeSpan.FromMinutes(15);

    // The nxm:// link among a process's arguments, if any.
    public static string? FindLink(IEnumerable<string> args) =>
        args.FirstOrDefault(a => a.StartsWith("nxm://", StringComparison.OrdinalIgnoreCase));

    public bool Deliver(string link)
    {
        if (!NexusModsLinks.TryParseNxm(link, out _, out _)) return false;
        Directory.CreateDirectory(Inbox);
        var name = $"{DateTime.UtcNow.Ticks:D19}-{Guid.NewGuid():N}";
        var temp = Path.Combine(Inbox, name + ".tmp");
        File.WriteAllText(temp, link);
        File.Move(temp, Path.Combine(Inbox, name + ".nxm"));
        return true;
    }

    // Links waiting in the inbox, oldest first; each is removed once read. Stale or malformed ones are dropped.
    public IReadOnlyList<string> TakeAll()
    {
        if (!Directory.Exists(Inbox)) return [];
        var links = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Inbox, "*.nxm").Order(StringComparer.Ordinal))
        {
            try
            {
                var info = new FileInfo(file);
                var text = info.Length <= 4096 ? File.ReadAllText(file).Trim() : string.Empty;
                File.Delete(file);
                if (DateTime.UtcNow - info.LastWriteTimeUtc <= MaximumAge && NexusModsLinks.TryParseNxm(text, out _, out _)) links.Add(text);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return links;
    }
}

// The per-user nxm:// handler (HKCU\Software\Classes\nxm), set only when the owner presses the button and removed only
// when it is MystTiq's. Another manager (Vortex, Mod Organizer) holding it is named first, so it is not replaced unknowingly.
[SupportedOSPlatform("windows")]
public static class NxmProtocolRegistration
{
    private const string KeyPath = @"Software\Classes\nxm";

    public static string CommandFor(string exePath) => $"\"{exePath}\" \"%1\"";

    public static string? CurrentCommand()
    {
        using var command = Registry.CurrentUser.OpenSubKey(KeyPath + @"\shell\open\command");
        if (command?.GetValue(null) is string mine) return mine;
        using var machine = Registry.ClassesRoot.OpenSubKey(@"nxm\shell\open\command");
        return machine?.GetValue(null) as string;
    }

    public static bool IsMine(string exePath) => string.Equals(CurrentCommand(), CommandFor(exePath), StringComparison.OrdinalIgnoreCase);

    public static void Register(string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(null, "URL:NXM Protocol");
        key.SetValue("URL Protocol", string.Empty);
        using var command = key.CreateSubKey(@"shell\open\command");
        command.SetValue(null, CommandFor(exePath));
    }

    public static bool Unregister(string exePath)
    {
        using var command = Registry.CurrentUser.OpenSubKey(KeyPath + @"\shell\open\command");
        if (!string.Equals(command?.GetValue(null) as string, CommandFor(exePath), StringComparison.OrdinalIgnoreCase)) return false;
        command!.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(KeyPath, throwOnMissingSubKey: false);
        return true;
    }
}

// The MOD browser's folders and GitHub repositories, kept beside the other desktop preferences.
public sealed class LocalModBrowserPreferencesStore(string? path = null)
{
    public string StoragePath { get; } = path ?? Path.Combine(LocalMapPreferencesStore.GetLocalConfigRoot(), "MystTiq", "mod-browser.json");

    public sealed record Preferences(List<string> Folders, List<string> GitHubRepositories);

    public Preferences Load()
    {
        try
        {
            if (File.Exists(StoragePath) && System.Text.Json.JsonSerializer.Deserialize<Preferences>(File.ReadAllText(StoragePath)) is { } saved)
                return new(saved.Folders ?? [], saved.GitHubRepositories ?? []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { }
        return new([FolderModSource.DefaultDownloadsFolder()], []);
    }

    public void Save(Preferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
        var temp = StoragePath + ".tmp";
        File.WriteAllText(temp, System.Text.Json.JsonSerializer.Serialize(preferences, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, StoragePath, overwrite: true);
    }
}
