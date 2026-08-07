using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Reads durable transaction journals and import transaction records without
/// modifying world data. Malformed or partially-written records are skipped
/// and returned as diagnostics instead of failing the entire history view.
/// </summary>
public sealed class TransactionHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly AppSettings settings;
    private readonly SafeFileSystemService fileSystem = new();

    public TransactionHistoryService(AppSettings settings) => this.settings = settings;

    public TransactionHistorySnapshot Load(CancellationToken cancellationToken = default)
    {
        var snapshot = new TransactionHistorySnapshot();
        LoadWorldJournals(snapshot, cancellationToken);
        LoadWorldImports(snapshot, cancellationToken);
        LoadOperationReports(snapshot, cancellationToken);

        snapshot.Rows = snapshot.Rows
            .GroupBy(x => $"{x.Operation}|{x.TransactionId}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.TimestampUtc).First())
            .OrderByDescending(x => x.TimestampUtc)
            .ToList();
        return snapshot;
    }

    public string HistoryRoot => Path.Combine(settings.BackupRoot, "Transactions");

    private void LoadWorldJournals(TransactionHistorySnapshot snapshot, CancellationToken cancellationToken)
    {
        var root = Path.Combine(settings.BackupRoot, "Transactions", "Journals");
        foreach (var path in fileSystem.EnumerateFiles(root, "transaction-*.json", SearchOption.TopDirectoryOnly, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var journal = JsonSerializer.Deserialize<WorldTransactionJournal>(File.ReadAllText(path), JsonOptions);
                if (journal is null || string.IsNullOrWhiteSpace(journal.TransactionId)) continue;
                var start = journal.CreatedUtc;
                var end = journal.UpdatedUtc < start ? start : journal.UpdatedUtc;
                snapshot.Rows.Add(new TransactionHistoryRow
                {
                    TransactionId = journal.TransactionId,
                    TimestampUtc = start,
                    Operation = string.IsNullOrWhiteSpace(journal.Operation) ? "World Transaction" : journal.Operation,
                    State = journal.State.ToString(),
                    Target = FirstNonEmpty(journal.TargetId, Path.GetFileName(journal.WorldPath), journal.WorldPath),
                    BackupPath = journal.BackupPath,
                    ReportPath = journal.ReportPath,
                    SourcePath = path,
                    Duration = end - start,
                    WarningCount = journal.Errors.Count,
                    RollbackAvailable = !string.IsNullOrWhiteSpace(journal.BackupPath) && File.Exists(journal.BackupPath),
                    Details = BuildJournalDetails(journal)
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                snapshot.Diagnostics.Add($"Skipped transaction journal '{path}': {ex.Message}");
            }
        }
    }

    private void LoadWorldImports(TransactionHistorySnapshot snapshot, CancellationToken cancellationToken)
    {
        var root = Path.Combine(settings.BackupRoot, "WorldImports", "Transactions");
        foreach (var path in fileSystem.EnumerateFiles(root, "transaction.json", SearchOption.AllDirectories, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var transaction = JsonSerializer.Deserialize<WorldImportTransaction>(File.ReadAllText(path), JsonOptions);
                if (transaction is null) continue;
                var updated = File.GetLastWriteTimeUtc(path);
                snapshot.Rows.Add(new TransactionHistoryRow
                {
                    TransactionId = transaction.TransactionId.ToString("N"),
                    TimestampUtc = transaction.CreatedUtc.UtcDateTime,
                    Operation = "World Import",
                    State = transaction.State.ToString(),
                    Target = FirstNonEmpty(Path.GetFileName(transaction.OutputWorldPath), Path.GetFileName(transaction.ArchivePath), transaction.ArchivePath),
                    SourcePath = path,
                    ReportPath = Directory.Exists(transaction.ReportsRoot) ? transaction.ReportsRoot : "",
                    Duration = updated > transaction.CreatedUtc.UtcDateTime ? updated - transaction.CreatedUtc.UtcDateTime : TimeSpan.Zero,
                    WarningCount = transaction.Diagnostics.Count,
                    RollbackAvailable = false,
                    Details = BuildImportDetails(transaction)
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                snapshot.Diagnostics.Add($"Skipped world-import transaction '{path}': {ex.Message}");
            }
        }
    }

    private void LoadOperationReports(TransactionHistorySnapshot snapshot, CancellationToken cancellationToken)
    {
        var patterns = new[] { "CharacterReset_*.json", "CharacterClone_*.json", "OwnershipTransaction_*.json" };
        foreach (var pattern in patterns)
        {
            foreach (var path in fileSystem.EnumerateFiles(settings.BackupRoot, pattern, SearchOption.AllDirectories, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(path));
                    var root = document.RootElement;
                    var id = ReadString(root, "TransactionId");
                    if (string.IsNullOrWhiteSpace(id)) id = Path.GetFileNameWithoutExtension(path);
                    var operation = pattern.StartsWith("CharacterReset", StringComparison.OrdinalIgnoreCase) ? "Character Reset" :
                        pattern.StartsWith("CharacterClone", StringComparison.OrdinalIgnoreCase) ? "Character Clone" : "Ownership Transaction";
                    var success = ReadBool(root, "Success");
                    var backup = ReadString(root, "BackupPath");
                    snapshot.Rows.Add(new TransactionHistoryRow
                    {
                        TransactionId = id,
                        TimestampUtc = File.GetCreationTimeUtc(path),
                        Operation = operation,
                        State = success ? "Committed" : "Failed",
                        Target = FirstNonEmpty(ReadString(root, "PlayerName"), ReadString(root, "TargetId"), ReadString(root, "DestinationPlayer")),
                        BackupPath = backup,
                        ReportPath = path,
                        SourcePath = path,
                        Duration = TimeSpan.Zero,
                        WarningCount = success ? 0 : 1,
                        RollbackAvailable = !string.IsNullOrWhiteSpace(backup) && File.Exists(backup),
                        Details = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true })
                    });
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    snapshot.Diagnostics.Add($"Skipped operation report '{path}': {ex.Message}");
                }
            }
        }
    }

    private static string BuildJournalDetails(WorldTransactionJournal journal)
    {
        var lines = new List<string>
        {
            $"Transaction ID: {journal.TransactionId}",
            $"Operation: {journal.Operation}",
            $"State: {journal.State}",
            $"World: {journal.WorldPath}",
            $"Target: {journal.TargetId}",
            $"Backup: {journal.BackupPath}",
            $"Report: {journal.ReportPath}",
            "",
            "Stages:"
        };
        lines.AddRange(journal.Stages.Select(x => $"{x.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}  {x.State} — {x.Message}"));
        if (journal.Errors.Count > 0)
        {
            lines.Add("");
            lines.Add("Errors:");
            lines.AddRange(journal.Errors);
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildImportDetails(WorldImportTransaction transaction)
    {
        var lines = new List<string>
        {
            $"Transaction ID: {transaction.TransactionId:N}",
            $"State: {transaction.State}",
            $"Archive: {transaction.ArchivePath}",
            $"Working world: {transaction.WorkingWorldPath}",
            $"Output world: {transaction.OutputWorldPath}"
        };
        if (transaction.Diagnostics.Count > 0)
        {
            lines.Add("");
            lines.Add("Diagnostics:");
            lines.AddRange(transaction.Diagnostics);
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "—";

    private static string ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }

    private static bool ReadBool(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True;
}
