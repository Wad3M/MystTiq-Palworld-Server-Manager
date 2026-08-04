using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class ProductionWorldImportPipeline
{
    private readonly WorldImportService importer;
    private readonly WorldImportTransactionService transactions;
    private readonly Plm1SaveDecoder decoder;
    private readonly LiveWorldScanner scanner = new();
    private readonly BaseDiscoveryEngine bases = new();
    private readonly GuildDiscoveryEngine guilds = new();
    private readonly PlayerMappingEngine mappingEngine = new();
    private readonly WorldRepairPlanner repairPlanner = new();
    private readonly RepairPreviewService previewService = new();
    private readonly RealWorldRelationshipValidator validator = new();

    public ProductionWorldImportPipeline(WorldImportService importer, WorldImportTransactionService transactions, IPalworldSaveCodec codec)
    { this.importer=importer; this.transactions=transactions; decoder=new Plm1SaveDecoder(codec); }

    public async Task<ProductionImportResult> AnalyzeAsync(string archivePath, IEnumerable<WorldPlayerRecord> destinationPlayers, CancellationToken cancellationToken)
    {
        var transaction=transactions.Create(archivePath); var scan=importer.Scan(archivePath);
        if(!scan.IsValid) throw new InvalidDataException("Archive did not pass safe world-import validation: "+string.Join(" ",scan.Warnings));
        transaction.State=WorldImportTransactionState.ArchiveAnalyzed; transactions.Save(transaction);
        var staged=importer.Stage(scan); transaction.WorkingWorldPath=staged; transaction.State=WorldImportTransactionState.Extracted; transactions.Save(transaction);
        var level=Directory.EnumerateFiles(staged,"Level.sav",SearchOption.AllDirectories).OrderBy(x=>x.Count(c=>c==Path.DirectorySeparatorChar)).FirstOrDefault() ?? throw new FileNotFoundException("Staged world does not contain Level.sav.");
        var decoded=await decoder.DecodeAsync(level,transaction.DecodedRoot,cancellationToken);
        transaction.State=WorldImportTransactionState.SaveDecoded; transactions.Save(transaction);
        var playerFiles=Directory.Exists(Path.Combine(Path.GetDirectoryName(level)!,"Players"))?Directory.EnumerateFiles(Path.Combine(Path.GetDirectoryName(level)!,"Players"),"*.sav"):[];
        var live=scanner.Scan(decoded.JsonPath,playerFiles); bases.Enrich(live.Snapshot,bases.Discover(decoded.JsonPath)); guilds.Enrich(live.Snapshot,guilds.Discover(decoded.JsonPath));
        var mappings=mappingEngine.Suggest(live.Snapshot.Players,destinationPlayers).ToList(); var plan=repairPlanner.Create(live.Snapshot,mappings); var preview=previewService.Build(live.Snapshot,mappings,plan); var validation=validator.Validate(live.Snapshot);
        var result=new ProductionImportResult { TransactionId=transaction.TransactionId,OutputWorldPath=transaction.OutputWorldPath,Snapshot=live.Snapshot,Preview=preview,Validation=validation };
        result.Diagnostics.AddRange(decoded.Codec.Diagnostics.Where(x=>!string.IsNullOrWhiteSpace(x))); result.Diagnostics.AddRange(live.Diagnostics);
        transaction.State=result.ReadyForActivation?WorldImportTransactionState.ReadyToActivate:WorldImportTransactionState.AwaitingPlayerMapping; transactions.Save(transaction);
        WriteReport(transaction,result,mappings,plan); return result;
    }

    private static void WriteReport(WorldImportTransaction transaction, ProductionImportResult result, IEnumerable<PlayerMappingRecord> mappings, WorldRepairPlan plan)
    {
        Directory.CreateDirectory(transaction.ReportsRoot);
        var report=new { transaction.TransactionId, transaction.ArchivePath, result.ReadyForActivation, Counts=new { Players=result.Snapshot.Players.Count,Guilds=result.Snapshot.Guilds.Count,Bases=result.Snapshot.Bases.Count }, Mappings=mappings, RepairPlan=plan, Preview=result.Preview, Validation=result.Validation, result.Diagnostics };
        File.WriteAllText(Path.Combine(transaction.ReportsRoot,"production-import-analysis.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
    }
}
