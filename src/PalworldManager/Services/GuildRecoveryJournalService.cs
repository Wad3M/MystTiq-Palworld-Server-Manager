using System.Text.Json;
namespace PalworldManager.Services;
public sealed class GuildRecoveryJournalService
{
    public string Write(string worldPath,string action,object details){var dir=Path.Combine(worldPath,"MystRecovery");Directory.CreateDirectory(dir);var path=Path.Combine(dir,$"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{action}.json");File.WriteAllText(path,JsonSerializer.Serialize(new{action,utc=DateTime.UtcNow,machine=Environment.MachineName,details},new JsonSerializerOptions{WriteIndented=true}));return path;}
    public IReadOnlyList<string> FindBackups(string backupRoot)=>Directory.Exists(backupRoot)?Directory.EnumerateDirectories(Path.Combine(backupRoot,"GuildRepair")).OrderByDescending(x=>x).ToList():[];
}
