namespace PalworldManager.Services;

public static class BrandingMigrationService
{
    public const string ProductFolder = "MystTiqPalworldServer";

    public static void MigrateLegacyApplicationData()
    {
        TryMigrate(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PalworldServerManager"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ProductFolder));
        TryMigrate(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PalworldServerManager"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductFolder));
        TryMigrate(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MystServerTools"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductFolder));
    }

    private static void TryMigrate(string source, string destination)
    {
        try
        {
            if (!Directory.Exists(source)) return;
            Directory.CreateDirectory(destination);
            CopyMissing(source, destination);
        }
        catch
        {
            // Rebranding must never prevent the manager from starting. Legacy data remains untouched.
        }
    }

    private static void CopyMissing(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!File.Exists(target)) File.Copy(file, target, false);
        }
    }
}
