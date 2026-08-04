namespace PalworldManager.Models;
public enum WorldImportWizardStep { SelectArchive=1, AnalyzeArchive, MapPlayers, RepairGuilds, RecoverBases, Validate, ImportAndActivate }
public sealed class WorldImportWizardSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public WorldImportWizardStep Step { get; set; } = WorldImportWizardStep.SelectArchive;
    public WorldImportTransaction? Transaction { get; set; }
    public WorldImportScanResult? Scan { get; set; }
    public WorldSnapshot? Snapshot { get; set; }
    public List<PlayerMappingRecord> PlayerMappings { get; set; } = [];
    public WorldRepairPlan RepairPlan { get; set; } = new();
    public WorldValidationReport Validation { get; set; } = new();
    public bool CanAdvance => Step switch
    {
        WorldImportWizardStep.SelectArchive => Transaction != null,
        WorldImportWizardStep.AnalyzeArchive => Scan?.IsValid == true,
        WorldImportWizardStep.MapPlayers => PlayerMappings.Count > 0 && PlayerMappings.All(x=>x.Confirmed),
        WorldImportWizardStep.RepairGuilds or WorldImportWizardStep.RecoverBases => RepairPlan.Ready,
        WorldImportWizardStep.Validate => Validation.IsValid,
        _ => false
    };
}
