using System.Text.Json;
using PalworldManager.Models;
namespace PalworldManager.Services;
public sealed class GuildRepairExecutor
{
    private readonly AppSettings settings; private readonly PalworldSaveCodec codec; private readonly GuildTransactionService tx;
    public GuildRepairExecutor(AppSettings settings){this.settings=settings;codec=new PalworldSaveCodec(settings);tx=new GuildTransactionService(settings);}
    public GuildRepairResult Execute(GuildWorldSnapshot snapshot,GuildRepairPlan plan)
    {
        tx.ValidatePlan(snapshot,plan);if(IsServerRunning())throw new InvalidOperationException("Stop PalServer.exe before modifying Level.sav.");
        var backup=tx.CreateBackup(snapshot.WorldPath);var temp=Path.Combine(Path.GetTempPath(),"MystTiqPalworldServer","GuildRepair",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try {var sourceJson=snapshot.DecodedJsonPath;var workJson=Path.Combine(temp,"Level.sav.json");File.Copy(sourceJson,workJson,true);new GuildJsonRepairService().Apply(workJson,plan);var workSav=Path.Combine(temp,"Level.sav");codec.Encode(workJson,workSav);var verifyJson=codec.Decode(workSav);var verify=new GuildJsonMapper().Read(verifyJson,temp,workSav);foreach(var op in plan.Operations){var g=verify.Guilds.FirstOrDefault(x=>x.GuildId.Equals(op.GuildId,StringComparison.OrdinalIgnoreCase))??throw new InvalidOperationException("Post-write verification could not find the repaired guild.");if((op.Type is GuildRepairOperationType.ClaimOrphanedGuild or GuildRepairOperationType.TransferLeadership) && !g.LeaderUid.Equals(op.PlayerUid,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Post-write verification found the wrong guild leader.");}var target=snapshot.LevelSavePath;var replacement=target+".myst-new";File.Copy(workSav,replacement,true);File.Move(replacement,target,true);var report=Path.Combine(snapshot.WorldPath,$"MystGuildRepairReport_{DateTime.Now:yyyyMMdd_HHmmss}.json");File.WriteAllText(report,JsonSerializer.Serialize(new{success=true,completedUtc=DateTime.UtcNow,backup,sourceHash=snapshot.SourceHash,resultHash=PalworldSaveCodec.HashFile(target),plan},new JsonSerializerOptions{WriteIndented=true}));return new GuildRepairResult{Success=true,BackupPath=backup,ReportPath=report,Message="Guild repair verified and applied."};}
        catch{tx.Rollback(backup,snapshot.WorldPath);throw;}finally{try{Directory.Delete(temp,true);}catch{}}
    }
    private static bool IsServerRunning()=>System.Diagnostics.Process.GetProcessesByName("PalServer").Length>0;
}
