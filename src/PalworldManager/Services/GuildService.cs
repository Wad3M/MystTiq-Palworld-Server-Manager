using System.Text.Json;
using PalworldManager.Models;
namespace PalworldManager.Services;
public sealed class GuildService
{
    private readonly AppSettings settings; private readonly PalworldSaveCodec codec; private readonly GuildJsonMapper mapper=new(); private readonly ActiveWorldContextService? worldContext; private readonly WorldDiscoverySnapshotService? discovery;
    private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=true,WriteIndented=true};
    public GuildService(AppSettings settings, ActiveWorldContextService? worldContext = null, WorldDiscoverySnapshotService? discovery = null){this.settings=settings;this.worldContext=worldContext;this.discovery=discovery;codec=new PalworldSaveCodec(settings);}
    public string FindWorldPath(){if(worldContext is not null)return worldContext.Current().WorldPath;var saved=Path.Combine(settings.ServerRoot??"","Pal","Saved","SaveGames","0");if(!Directory.Exists(saved))return"";return Directory.EnumerateDirectories(saved).Where(d=>File.Exists(Path.Combine(d,"Level.sav"))).OrderByDescending(d=>File.GetLastWriteTimeUtc(Path.Combine(d,"Level.sav"))).FirstOrDefault()??"";}
    public GuildWorldSnapshot LoadSnapshot(string? worldPath=null)
    {
        worldPath=string.IsNullOrWhiteSpace(worldPath)?FindWorldPath():worldPath;
        if (discovery is not null && (string.IsNullOrWhiteSpace(worldPath) || worldPath.Equals(discovery.Current().Context.WorldPath, StringComparison.OrdinalIgnoreCase)))
            return discovery.Current().Guilds;
        var empty=new GuildWorldSnapshot{SourcePath=worldPath??"",WorldPath=worldPath??""};
        if(string.IsNullOrWhiteSpace(worldPath)||!Directory.Exists(worldPath)){empty.Warnings.Add("No active Palworld world was found.");return empty;}
        var level=Path.Combine(worldPath,"Level.sav"); var json=level+".json";
        try { if(File.Exists(level)&&(!File.Exists(json)||File.GetLastWriteTimeUtc(json)<File.GetLastWriteTimeUtc(level))) json=codec.Decode(level); if(!File.Exists(json)){empty.Warnings.Add("Level.sav was found, but no decoded JSON is available and the converter is not configured.");return empty;} var snap=mapper.Read(json,worldPath,level); snap.SourceHash=File.Exists(level)?PalworldSaveCodec.HashFile(level):""; snap.IsReadOnly=true; return snap; }
        catch(Exception ex){empty.LevelSavePath=level;empty.Warnings.Add(ex.Message);return empty;}
    }
    public void SaveSnapshot(GuildWorldSnapshot snapshot)=>throw new InvalidOperationException("Direct save writing is disabled in this build. Stage and validate a repair plan first.");
    public string ExportSnapshot(GuildWorldSnapshot snapshot,string destination){Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.WriteAllText(destination,JsonSerializer.Serialize(snapshot,JsonOptions));return destination;}
}
