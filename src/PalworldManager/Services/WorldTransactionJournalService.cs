using System.Text;
using System.Text.Json;
using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Durable audit journal for every world-changing transaction. Journals are
/// written outside the active world and survive both successful commits and
/// rollback/failure paths.
/// </summary>
public sealed class WorldTransactionJournalService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string root;

    public WorldTransactionJournalService(AppSettings settings)
    {
        root = Path.Combine(settings.BackupRoot, "Transactions", "Journals");
        Directory.CreateDirectory(root);
    }

    public WorldTransactionJournal Create(string transactionId, string operation, string worldPath, string targetId, string sourceHash)
    {
        var journal = new WorldTransactionJournal
        {
            TransactionId = transactionId,
            Operation = operation,
            WorldPath = worldPath,
            TargetId = targetId,
            SourceHash = sourceHash
        };
        Advance(journal, WorldTransactionState.Prepared, "Transaction prepared; active save hash captured.");
        return journal;
    }

    public void Advance(WorldTransactionJournal journal, WorldTransactionState state, string message)
    {
        journal.State = state;
        journal.UpdatedUtc = DateTime.UtcNow;
        journal.Stages.Add(new WorldTransactionStage { State = state, Message = message, TimestampUtc = journal.UpdatedUtc });
        Save(journal);
    }

    public void Fail(WorldTransactionJournal journal, Exception error, bool rolledBack)
    {
        journal.Errors.Add(error.ToString());
        Advance(journal, rolledBack ? WorldTransactionState.RolledBack : WorldTransactionState.Failed,
            rolledBack ? "Transaction failed and the original Level.sav was restored." : "Transaction failed before rollback could be confirmed.");
    }

    public string Save(WorldTransactionJournal journal)
    {
        var path = Path.Combine(root, $"transaction-{journal.TransactionId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(journal, JsonOptions), new UTF8Encoding(false));
        return path;
    }
}
