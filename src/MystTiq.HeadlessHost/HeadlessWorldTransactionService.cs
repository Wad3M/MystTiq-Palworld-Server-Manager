using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessWorldTransactionService
{
    private const long MaximumArchiveBytes = 2L * 1024 * 1024 * 1024;
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(20);
    private readonly IServerPathProfile paths;
    private readonly IServerLifecycleService lifecycle;
    private readonly HeadlessBackupService backups;
    private readonly HeadlessActivityLogService activity;
    private readonly HeadlessWorldExplorerService explorer;
    private readonly IOperationCoordinator coordinator;
    private readonly ServerProfileId profile;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, PendingPlan> plans = new(StringComparer.Ordinal);
    private readonly string transactionRoot;

    public HeadlessWorldTransactionService(IServerPathProfile paths, IServerLifecycleService lifecycle,
        HeadlessBackupService backups, HeadlessActivityLogService activity, HeadlessWorldExplorerService explorer,
        IOperationCoordinator coordinator, ServerProfileId profile)
    {
        this.paths = paths;
        this.lifecycle = lifecycle;
        this.backups = backups;
        this.activity = activity;
        this.explorer = explorer;
        this.coordinator = coordinator;
        this.profile = profile;
        transactionRoot = Path.Combine(paths.ManagerRuntimeRoot, "world-transactions");
        Directory.CreateDirectory(transactionRoot);
    }

    public HeadlessWorldValidationReport ValidateActiveWorld()
    {
        var snapshot = explorer.Explore();
        var findings = new List<HeadlessWorldValidationFinding>();
        if (!snapshot.Available || string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath))
            findings.Add(new("Critical", "World", "Active world", snapshot.Detail, false));
        else
        {
            if (!snapshot.Integrity.RequiredFilesPresent)
                findings.Add(new("Critical", "Integrity", "Required files", "Level.sav is missing or empty.", false));
            foreach (var finding in snapshot.Integrity.Findings)
                findings.Add(new("Warning", "Integrity", "Structural check", finding, false));
            if (snapshot.FileCount >= 5000)
                findings.Add(new("Warning", "Inventory", "File limit", "Inventory reached its 5,000-file safety limit.", false));
            if (findings.Count == 0)
                findings.Add(new("Information", "Integrity", "Required files", "The active world passed structural validation.", false));
        }

        var healthy = findings.All(x => x.Severity is not "Critical" and not "Error");
        var report = new HeadlessWorldValidationReport(healthy, snapshot.ActiveWorldId, findings,
            DateTimeOffset.UtcNow, healthy ? "Active world validation passed." : "Active world validation requires attention.");
        activity.Record(healthy ? "Information" : "Warning", "World Validator", "Validated active world",
            $"world={snapshot.ActiveWorldId ?? "none"}; findings={findings.Count}; healthy={healthy}");
        return report;
    }

    public IReadOnlyList<HeadlessWorldTransactionJournal> GetHistory(int maximum = 100)
    {
        var journalRoot = Path.Combine(transactionRoot, "journals");
        if (!Directory.Exists(journalRoot)) return [];
        return Directory.EnumerateFiles(journalRoot, "*.json", SearchOption.TopDirectoryOnly)
            .Select(TryReadJournal).Where(x => x is not null).Cast<HeadlessWorldTransactionJournal>()
            .OrderByDescending(x => x.UpdatedUtc).Take(Math.Clamp(maximum, 1, 500)).ToList();
    }

    public async Task<HeadlessWorldImportPreview> AnalyzeArchiveAsync(Stream source, string mode, CancellationToken cancellationToken)
    {
        mode = NormalizeMode(mode);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var uploadRoot = Path.Combine(transactionRoot, "pending");
        Directory.CreateDirectory(uploadRoot);
        var archivePath = Path.Combine(uploadRoot, token + ".zip");
        try
        {
            await CopyBoundedAsync(source, archivePath, MaximumArchiveBytes, cancellationToken);
            var entries = InspectArchive(archivePath, mode);
            var expires = DateTimeOffset.UtcNow.Add(PreviewLifetime);
            var plan = new PendingPlan(token, mode, archivePath, entries, expires);
            lock (plans) plans[token] = plan;
            activity.Record("Information", "World Transaction", "Analyzed recovery archive",
                $"mode={mode}; entries={entries.Count}; expires={expires:O}");
            return new(true, token, mode, entries.Count, new FileInfo(archivePath).Length, expires,
                BuildPlanSteps(mode), $"Archive accepted for {DisplayMode(mode)}. Review the plan before Apply.");
        }
        catch
        {
            TryDeleteFile(archivePath);
            throw;
        }
    }

    public async Task<HeadlessWorldTransactionResult> ApplyAsync(HeadlessWorldTransactionApplyRequest request,
        CancellationToken cancellationToken) => await ApplyCoreAsync(request, null, cancellationToken);

    internal async Task<HeadlessWorldTransactionResult> ApplyCoreAsync(HeadlessWorldTransactionApplyRequest request,
        string? failureStage, CancellationToken cancellationToken)
    {
        if (!request.Confirmed)
            return HeadlessWorldTransactionResult.Failure("Apply requires explicit confirmation.");
        if (!await gate.WaitAsync(0, cancellationToken))
            return HeadlessWorldTransactionResult.Failure("Another world transaction is already running.");

        PendingPlan? plan = null;
        HeadlessWorldTransactionJournal? journal = null;
        OperationHandle? operation = null;
        string? staging = null;
        string? rollback = null;
        try
        {
            lock (plans)
            {
                if (plans.TryGetValue(request.PreviewToken, out var found)) plan = found;
                if (plan is not null) plans.Remove(request.PreviewToken);
            }
            if (plan is null || plan.ExpiresUtc < DateTimeOffset.UtcNow || !File.Exists(plan.ArchivePath))
                return HeadlessWorldTransactionResult.Failure("The preview token is missing or expired. Analyze the archive again.");

            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.NativeProcessId.HasValue || status.Ready)
                return HeadlessWorldTransactionResult.Failure("Stop PalServer before applying a world transaction.");

            var snapshot = explorer.Explore();
            if (!snapshot.Available || string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath))
                return HeadlessWorldTransactionResult.Failure("No active world is available for transactional recovery.");

            operation = await coordinator.BeginAsync(profile, "world-transaction",
                "HeadlessWorldTransactionService", ["world-mutation"], cancellationToken);

            var id = Guid.NewGuid().ToString("N");
            journal = NewJournal(id, plan.Mode, snapshot.ActiveWorldId ?? "unknown");
            Advance(journal, "PreviewAccepted", "The single-use preview token was accepted.");
            ThrowIfRequested(failureStage, "preview");

            var safety = await backups.CreateAsync(BackupClass.Safety, cancellationToken);
            if (!safety.Success || string.IsNullOrWhiteSpace(safety.FileName))
                throw new InvalidOperationException("Fresh safety backup failed: " + safety.Message);
            journal.SafetyBackup = safety.FileName;
            Advance(journal, "SafetyBackupCreated", $"Fresh safety backup: {safety.FileName}");
            ThrowIfRequested(failureStage, "backup");

            staging = snapshot.ActiveWorldPath + $".transaction-staging-{id}";
            rollback = snapshot.ActiveWorldPath + $".transaction-rollback-{id}";
            if (plan.Mode == "world-import") ExtractWorld(plan, staging);
            else
            {
                CopyDirectory(snapshot.ActiveWorldPath, staging);
                ExtractPlayerRecovery(plan, staging);
            }
            ValidateWorldDirectory(staging);
            Advance(journal, "Staged", "Candidate world was staged and validated outside the active save path.");
            ThrowIfRequested(failureStage, "staging");

            Directory.Move(snapshot.ActiveWorldPath, rollback);
            Directory.Move(staging, snapshot.ActiveWorldPath);
            Advance(journal, "Swapped", "The staged world was atomically promoted.");
            ThrowIfRequested(failureStage, "swap");

            ValidateWorldDirectory(snapshot.ActiveWorldPath);
            Advance(journal, "Validated", "Post-transaction structural validation passed.");
            ThrowIfRequested(failureStage, "validation");
            Directory.Delete(rollback, true);
            rollback = null;
            Advance(journal, "Completed", "Transaction completed successfully.");
            activity.Record("Information", "World Transaction", "Applied recovery transaction",
                $"id={id}; mode={plan.Mode}; backup={safety.FileName}; result=success");
            coordinator.Complete(operation.Id, $"{DisplayMode(plan.Mode)} completed and validated.");
            return new(true, id, "Completed", safety.FileName, false, journal.JournalPath,
                $"{DisplayMode(plan.Mode)} completed and validated.");
        }
        catch (Exception ex)
        {
            var rolledBack = false;
            try
            {
                if (rollback is not null && Directory.Exists(rollback))
                {
                    if (staging is not null && Directory.Exists(staging)) Directory.Delete(staging, true);
                    var active = explorer.Explore().ActiveWorldPath;
                    if (!string.IsNullOrWhiteSpace(active) && Directory.Exists(active)) Directory.Delete(active, true);
                    var originalPath = rollback[..rollback.IndexOf(".transaction-rollback-", StringComparison.Ordinal)];
                    Directory.Move(rollback, originalPath);
                    rollback = null;
                    rolledBack = true;
                }
            }
            catch { rolledBack = false; }
            if (journal is not null)
            {
                journal.RolledBack = rolledBack;
                Advance(journal, rolledBack ? "RolledBack" : "Failed", ex.Message);
                activity.Record("Warning", "World Transaction", "Recovery transaction failed",
                    $"id={journal.TransactionId}; rolledBack={rolledBack}; error={ex.GetType().Name}");
            }
            if (operation is not null) coordinator.Fail(operation.Id, ex.Message, rolledBack);
            return new(false, journal?.TransactionId, rolledBack ? "RolledBack" : "Failed",
                journal?.SafetyBackup, rolledBack, journal?.JournalPath, ex.Message);
        }
        finally
        {
            operation?.Dispose();
            if (staging is not null && Directory.Exists(staging)) try { Directory.Delete(staging, true); } catch { }
            if (plan is not null) TryDeleteFile(plan.ArchivePath);
            gate.Release();
        }
    }

    private static IReadOnlyList<string> BuildPlanSteps(string mode) => mode == "world-import"
        ? ["Stop PalServer", "Create fresh safety backup", "Extract into isolated staging", "Validate staged world", "Atomically swap active world", "Validate active world", "Write journal and audit"]
        : ["Stop PalServer", "Create fresh safety backup", "Clone active world into staging", "Overlay canonical player saves", "Validate staged world", "Atomically swap active world", "Validate and journal"];

    private static List<ArchiveItem> InspectArchive(string path, string mode)
    {
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count == 0) throw new InvalidDataException("The archive is empty.");
        if (archive.Entries.Count > 20000) throw new InvalidDataException("The archive exceeds the 20,000-entry limit.");
        var items = new List<ArchiveItem>();
        long expandedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var safe = NormalizeArchivePath(entry.FullName);
            expandedBytes = checked(expandedBytes + entry.Length);
            if (expandedBytes > 8L * 1024 * 1024 * 1024) throw new InvalidDataException("The archive expands beyond the 8 GiB safety limit.");
            items.Add(new(safe, entry.Length));
        }
        if (mode == "world-import" && !items.Any(x => Path.GetFileName(x.Path).Equals("Level.sav", StringComparison.OrdinalIgnoreCase) && x.Size > 0))
            throw new InvalidDataException("A world import must contain a non-empty Level.sav.");
        if (mode == "player-recovery" && !items.Any(x => IsCanonicalPlayerSave(x.Path)))
            throw new InvalidDataException("Player Recovery requires at least one canonical Players/<32-hex>.sav entry.");
        return items;
    }

    private static void ExtractWorld(PendingPlan plan, string staging)
    {
        Directory.CreateDirectory(staging);
        var level = plan.Entries.First(x => Path.GetFileName(x.Path).Equals("Level.sav", StringComparison.OrdinalIgnoreCase));
        var prefix = level.Path[..^"Level.sav".Length];
        using var archive = ZipFile.OpenRead(plan.ArchivePath);
        foreach (var entry in archive.Entries.Where(x => !string.IsNullOrEmpty(x.Name)))
        {
            var safe = NormalizeArchivePath(entry.FullName);
            if (!safe.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            WriteEntry(entry, staging, safe[prefix.Length..]);
        }
    }

    private static void ExtractPlayerRecovery(PendingPlan plan, string staging)
    {
        using var archive = ZipFile.OpenRead(plan.ArchivePath);
        foreach (var entry in archive.Entries.Where(x => !string.IsNullOrEmpty(x.Name)))
        {
            var safe = NormalizeArchivePath(entry.FullName);
            if (!IsCanonicalPlayerSave(safe)) continue;
            WriteEntry(entry, staging, "Players/" + Path.GetFileName(safe));
        }
    }

    private static void WriteEntry(ZipArchiveEntry entry, string root, string relative)
    {
        var destination = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive path escaped staging.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        entry.ExtractToFile(destination, true);
    }

    private static string NormalizeArchivePath(string value)
    {
        var normalized = value.Replace('\\', '/').TrimStart('/');
        if (Path.IsPathRooted(value) || normalized.Split('/').Any(x => x == "..") || normalized.Contains(':'))
            throw new InvalidDataException("Archive contains an unsafe path.");
        return normalized;
    }

    private static bool IsCanonicalPlayerSave(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var playerIndex = Array.FindLastIndex(parts, x => x.Equals("Players", StringComparison.OrdinalIgnoreCase));
        if (playerIndex < 0 || playerIndex != parts.Length - 2) return false;
        var name = Path.GetFileNameWithoutExtension(parts[^1]);
        return parts[^1].EndsWith(".sav", StringComparison.OrdinalIgnoreCase) && name.Length == 32 && name.All(Uri.IsHexDigit);
    }

    private void Advance(HeadlessWorldTransactionJournal journal, string state, string detail)
    {
        journal.State = state; journal.UpdatedUtc = DateTimeOffset.UtcNow;
        journal.Stages.Add(new(state, detail, journal.UpdatedUtc));
        Directory.CreateDirectory(Path.GetDirectoryName(journal.JournalPath)!);
        var temp = journal.JournalPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(journal, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, journal.JournalPath, true);
    }

    private HeadlessWorldTransactionJournal NewJournal(string id, string mode, string worldId)
    {
        var path = Path.Combine(transactionRoot, "journals", $"transaction-{id}.json");
        return new HeadlessWorldTransactionJournal
        {
            TransactionId = id, Mode = mode, WorldId = worldId, State = "Created",
            CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow,
            JournalPath = path
        };
    }

    private static HeadlessWorldTransactionJournal? TryReadJournal(string path)
    { try { return JsonSerializer.Deserialize<HeadlessWorldTransactionJournal>(File.ReadAllText(path)); } catch { return null; } }
    private static void ValidateWorldDirectory(string root)
    { var level = new FileInfo(Path.Combine(root, "Level.sav")); if (!level.Exists || level.Length == 0) throw new InvalidDataException("Candidate world has no non-empty Level.sav."); }
    private static string NormalizeMode(string mode) => mode.Trim().ToLowerInvariant() switch { "world-import" => "world-import", "player-recovery" => "player-recovery", _ => throw new ArgumentException("Mode must be world-import or player-recovery.") };
    private static string DisplayMode(string mode) => mode == "world-import" ? "World Import" : "Player Recovery";
    private static void ThrowIfRequested(string? requested, string stage) { if (string.Equals(requested, stage, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Injected failure at {stage} stage."); }
    private static void CopyDirectory(string source, string destination) { Directory.CreateDirectory(destination); foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory))); foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) { var target = Path.Combine(destination, Path.GetRelativePath(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target, true); } }
    private static async Task CopyBoundedAsync(Stream source, string destination, long maximum, CancellationToken token) { await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true); var buffer = new byte[81920]; long total = 0; int read; while ((read = await source.ReadAsync(buffer, token)) > 0) { total += read; if (total > maximum) throw new InvalidDataException("Archive exceeds the 2 GiB limit."); await output.WriteAsync(buffer.AsMemory(0, read), token); } }
    private static void TryDeleteFile(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private sealed record PendingPlan(string Token, string Mode, string ArchivePath, IReadOnlyList<ArchiveItem> Entries, DateTimeOffset ExpiresUtc);
    private sealed record ArchiveItem(string Path, long Size);
}

public sealed record HeadlessWorldValidationFinding(string Severity, string Category, string Check, string Detail, bool RepairAvailable);
public sealed record HeadlessWorldValidationReport(bool Healthy, string? WorldId, IReadOnlyList<HeadlessWorldValidationFinding> Findings, DateTimeOffset CheckedAt, string Summary);
public sealed record HeadlessWorldImportPreview(bool Accepted, string PreviewToken, string Mode, int EntryCount, long ArchiveBytes, DateTimeOffset ExpiresUtc, IReadOnlyList<string> Steps, string Summary);
public sealed record HeadlessWorldTransactionApplyRequest(string PreviewToken, bool Confirmed);
public sealed record HeadlessWorldTransactionResult(bool Success, string? TransactionId, string State, string? SafetyBackup, bool RolledBack, string? JournalPath, string Message)
{ public static HeadlessWorldTransactionResult Failure(string message) => new(false, null, "Rejected", null, false, null, message); }
public sealed record HeadlessWorldTransactionStage(string State, string Detail, DateTimeOffset TimestampUtc);
public sealed class HeadlessWorldTransactionJournal
{
    public string TransactionId { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string WorldId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? SafetyBackup { get; set; }
    public bool RolledBack { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public string JournalPath { get; set; } = string.Empty;
    public List<HeadlessWorldTransactionStage> Stages { get; set; } = [];
}
