using System.Text.Json;
using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class NotificationCenterService
{
    private readonly string filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public NotificationCenterService()
    {
        filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            BrandingMigrationService.ProductFolder,
            "notifications.json");
    }

    public List<NotificationEntry> Load()
    {
        try
        {
            if (!File.Exists(filePath)) return [];
            return JsonSerializer.Deserialize<List<NotificationEntry>>(File.ReadAllText(filePath)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IEnumerable<NotificationEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var ordered = entries
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.TimestampUtc)
            .Take(100)
            .ToList();
        File.WriteAllText(filePath, JsonSerializer.Serialize(ordered, JsonOptions));
    }
}
