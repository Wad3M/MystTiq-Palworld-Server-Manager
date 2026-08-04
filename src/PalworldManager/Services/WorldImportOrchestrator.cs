using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class WorldImportOrchestrator
{
    private readonly WorldImportService importer;
    private readonly WorldImportTransactionService transactions;
    private readonly IPalworldSaveCodec codec;
    private readonly PlayerMappingEngine mappings = new();
    private readonly WorldRepairPlanner repairs = new();
    private readonly WorldRelationshipValidator relationships = new();

    public WorldImportOrchestrator(WorldImportService importer, WorldImportTransactionService transactions, IPalworldSaveCodec codec)
    { this.importer=importer; this.transactions=transactions; this.codec=codec; }

    public WorldImportWizardSession Begin(string archivePath)
    {
        var tx=transactions.Create(archivePath); var scan=importer.Scan(archivePath);
        tx.State=WorldImportTransactionState.ArchiveAnalyzed; transactions.Save(tx);
        return new WorldImportWizardSession { Transaction=tx, Scan=scan, Step=WorldImportWizardStep.AnalyzeArchive };
    }

    public void SetSnapshot(WorldImportWizardSession session, WorldSnapshot snapshot, IEnumerable<WorldPlayerRecord> destinationPlayers)
    {
        session.Snapshot=snapshot;
        session.PlayerMappings=mappings.Suggest(snapshot.Players,destinationPlayers).ToList();
        session.Step=WorldImportWizardStep.MapPlayers;
    }

    public void BuildRepairPlan(WorldImportWizardSession session)
    {
        if(session.Snapshot is null) throw new InvalidOperationException("World snapshot has not been created.");
        var mappingIssues=mappings.Validate(session.PlayerMappings);
        session.RepairPlan=repairs.Create(session.Snapshot,session.PlayerMappings);
        session.RepairPlan.RemainingIssues.AddRange(mappingIssues);
        session.Step=WorldImportWizardStep.RepairGuilds;
    }

    public void Validate(WorldImportWizardSession session)
    {
        if(session.Snapshot is null) throw new InvalidOperationException("World snapshot has not been created.");
        var issues=relationships.Validate(session.Snapshot);
        session.Validation=new WorldValidationReport { Errors=issues.Where(x=>x.BlocksActivation).ToList(), Warnings=issues.Where(x=>!x.BlocksActivation).ToList() };
        session.Step=WorldImportWizardStep.Validate;
    }
}
