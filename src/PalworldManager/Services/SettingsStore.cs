using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly ApplicationPathService paths = ApplicationPathService.Current;
    public string Root => paths.DataRoot;
    public string FilePath => Path.Combine(paths.SettingsRoot, "settings-v2.1.json");

    public AppSettings Load()
    {
        paths.EnsureApplicationDirectories();
        if (!File.Exists(FilePath))
        {
            var defaults = new AppSettings(); paths.ApplyWorkspaceDefaults(defaults); Save(defaults); return defaults;
        }
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
            paths.ApplyWorkspaceDefaults(settings);
            if (paths.IsPortable) Save(settings);
            return settings;
        }
        catch
        {
            var defaults = new AppSettings();
            paths.ApplyWorkspaceDefaults(defaults);
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        paths.EnsureApplicationDirectories();
        AtomicFile.Write(FilePath, JsonSerializer.Serialize(settings, Options));
    }
}

public static class AtomicFile
{
    public static void Write(string path,string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=path+".tmp";
        File.WriteAllText(temp,content);
        if(File.Exists(path)) File.Replace(temp,path,path+$".bak_{DateTime.Now:yyyyMMdd_HHmmss_fff}");
        else File.Move(temp,path);
    }
}
