using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), BrandingMigrationService.ProductFolder);
    public string FilePath => Path.Combine(Root, "settings-v2.1.json");

    public AppSettings Load()
    {
        Directory.CreateDirectory(Root);
        if (!File.Exists(FilePath))
        {
            var defaults = new AppSettings(); Save(defaults); return defaults;
        }
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings(); }
        catch { return new AppSettings(); }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Root);
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
