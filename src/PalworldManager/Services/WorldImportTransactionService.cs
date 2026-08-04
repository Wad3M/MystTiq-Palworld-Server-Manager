using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class WorldImportTransactionService
{
    private readonly AppSettings settings;
    public WorldImportTransactionService(AppSettings settings) => this.settings = settings;

    public WorldImportTransaction Create(string archivePath)
    {
        var id = Guid.NewGuid();
        var root = Path.Combine(settings.BackupRoot, "WorldImports", "Transactions", id.ToString("N"));
        var tx = new WorldImportTransaction
        {
            TransactionId = id,
            ArchivePath = archivePath,
            StagingRoot = root,
            WorkingWorldPath = Path.Combine(root, "Working"),
            OutputWorldPath = Path.Combine(root, "Output"),
            State = WorldImportTransactionState.Created
        };
        Directory.CreateDirectory(tx.WorkingWorldPath);
        Directory.CreateDirectory(tx.OutputWorldPath);
        Save(tx);
        return tx;
    }

    public void Save(WorldImportTransaction tx)
    {
        Directory.CreateDirectory(tx.StagingRoot);
        var temp = Path.Combine(tx.StagingRoot, "transaction.json.tmp");
        var final = Path.Combine(tx.StagingRoot, "transaction.json");
        File.WriteAllText(temp, JsonSerializer.Serialize(tx, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, final, true);
    }

    public void Fail(WorldImportTransaction tx, Exception error)
    {
        tx.State = WorldImportTransactionState.Failed;
        tx.Diagnostics.Add(error.ToString());
        Save(tx);
    }
}
