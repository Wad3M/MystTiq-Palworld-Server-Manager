// MystTiq v1.1.1.3: file reviewed for this release (2026-10-19).
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessBackupService
{
    private const string Prefix = "Palworld_";
    private readonly IServerPathProfile paths;
    private readonly IServerLifecycleService lifecycle;
    private readonly HeadlessActivityLogService activity;
    private readonly IOperationCoordinator coordinator;
    private readonly ServerProfileId profile;
    private readonly SemaphoreSlim backupGate = new(1, 1);
    private readonly string verificationPath;
    private readonly string classificationPath;
    private readonly object classificationGate = new();
    private readonly Dictionary<string, HeadlessBackupClassificationEntry> classifications;
    private readonly Dictionary<string, HeadlessBackupRetentionPreview> retentionPreviews = new(StringComparer.Ordinal);
    // v1.0.0.4: each backup's world day, read from its own Level.sav (world-days.json), and the reader filling it in.
    private readonly HeadlessWorldClockService? worldClock;
    private readonly string worldDaysPath;
    private readonly object worldDaysGate = new();
    private readonly Dictionary<string, HeadlessBackupWorldDay> worldDays;
    private int worldDayReaderRunning;

    // v1.0.7.2 (roadmap Q-12): the scan running now (reading backups' world days, or Verify All), or the last one.
    private readonly object scanGate = new();
    private BackupScanProgress? scan;
    private CancellationTokenSource? scanCancel;
    // Only the newest scan writes its progress (Verify All takes the slot from a world-day read; that read does not
    // start while Verify All runs).
    private long scanId;
    public const int WorldDaySaveEvery = 10;

    public BackupScanProgress? GetScan() { lock (scanGate) return scan; }

    /// <summary>Stops the scan running now. False when none runs.</summary>
    public bool CancelScan()
    {
        lock (scanGate)
        {
            if (scan is not { FinishedUtc: null } || scanCancel is null) return false;
            scanCancel.Cancel();
            return true;
        }
    }

    private (CancellationTokenSource Cancel, long Id) BeginScan(string kind, int total, CancellationToken token)
    {
        lock (scanGate)
        {
            scanCancel?.Dispose();
            scanCancel = CancellationTokenSource.CreateLinkedTokenSource(token);
            scan = new BackupScanProgress(kind, 0, total, null, [], DateTimeOffset.UtcNow, null, false);
            return (scanCancel, ++scanId);
        }
    }

    private bool VerifyAllRunning()
    {
        lock (scanGate) return scan is { Kind: "Verifying backups", FinishedUtc: null };
    }

    private void StepScan(long id, string? current, bool done, string? problemFile = null, string? problem = null)
    {
        lock (scanGate)
        {
            if (scan is null || id != scanId) return;
            var problems = problem is null ? scan.Unreadable : [.. scan.Unreadable, new BackupScanProblem(problemFile ?? current ?? string.Empty, problem)];
            scan = scan with { Done = scan.Done + (done ? 1 : 0), Current = current, Unreadable = problems };
        }
    }

    private void FinishScan(long id, bool cancelled)
    {
        lock (scanGate)
            if (scan is not null && id == scanId) scan = scan with { Current = null, FinishedUtc = DateTimeOffset.UtcNow, Cancelled = cancelled };
    }

    public HeadlessBackupService(
        IServerPathProfile paths,
        IServerLifecycleService lifecycle,
        HeadlessActivityLogService activity,
        IOperationCoordinator coordinator,
        ServerProfileId profile,
        HeadlessWorldClockService? worldClock = null)
    {
        this.paths = paths;
        this.lifecycle = lifecycle;
        this.activity = activity;
        this.coordinator = coordinator;
        this.profile = profile;
        var stateRoot = Path.Combine(paths.ManagerRuntimeRoot, "backups");
        Directory.CreateDirectory(stateRoot);
        verificationPath = Path.Combine(stateRoot, "verification.json");
        classificationPath = Path.Combine(stateRoot, "classification.json");
        classifications = LoadClassifications();
        this.worldClock = worldClock;
        worldDaysPath = Path.Combine(stateRoot, "world-days.json");
        worldDays = LoadWorldDays();
    }

    public HeadlessBackupInventory GetInventory()
    {
        Directory.CreateDirectory(paths.BackupRoot);

        var items = Directory.EnumerateFiles(paths.BackupRoot, $"{Prefix}*.zip", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => WithWorldDay(new HeadlessBackupItem(
                file.Name,
                file.Length,
                file.LastWriteTimeUtc,
                IsArchiveReadable(file.FullName),
                ClassOf(file.Name))))
            .ToList();
        QueueWorldDays();

        return new HeadlessBackupInventory(
            items.Count,
            items.Sum(item => item.SizeBytes),
            items,
            DateTimeOffset.UtcNow,
            paths.BackupRoot,
            $"{items.Count} managed backup(s) in {paths.BackupRoot}.")
        {
            // v1.0.0.4: restores need to replace SaveGames; the Desktop offers to fix the Saved folder's access when they cannot.
            SaveFolderReplaceable = SaveFolderAccess.CanReplace(paths.SaveRoot),
            // v1.0.7.2 (roadmap Q-12): the scan running now, or the last one.
            Scan = GetScan(),
            SavedFolderPath = Path.GetDirectoryName(paths.SaveRoot.TrimEnd(Path.DirectorySeparatorChar)),
        };
    }

    // Unclassified/legacy backups (created before this milestone, or by manual file operations)
    // default to Manual -- the most-protected class -- so they never silently become eligible for
    // the new class-scoped retention pruning below.
    private BackupClass ClassOf(string fileName)
    {
        lock (classificationGate)
            return classifications.TryGetValue(fileName, out var entry) ? entry.Class : BackupClass.Manual;
    }

    public async Task<HeadlessBackupOperationResult> CreateAsync(BackupClass backupClass, CancellationToken cancellationToken)
    {
        if (!await backupGate.WaitAsync(0, cancellationToken))
            return HeadlessBackupOperationResult.Conflict("A backup operation is already in progress.");

        // v1.0.7.1 (roadmap Q-4): a scheduled or manual backup holds "world-mutation" while it zips the world, so it never
        // captures a save edit, a restore or a migration half done, and those are refused while it runs. A safety backup
        // is taken BY one of those operations, inside the lock it already holds, so it does not take it again.
        OperationHold? hold = null;
        if (backupClass != BackupClass.Safety)
        {
            (hold, var holdRefusal) = await OperationHold.TryBeginAsync(coordinator, profile, "backup-create", "HeadlessBackupService",
                ["world-mutation"], "A backup", cancellationToken);
            if (holdRefusal is not null)
            {
                backupGate.Release();
                return HeadlessBackupOperationResult.Conflict(holdRefusal);
            }
        }
        using var worldHold = hold;

        try
        {
            return await CreateInternalAsync(backupClass.ToString().ToLowerInvariant(), backupClass, cancellationToken);
        }
        finally
        {
            backupGate.Release();
        }
    }

    public HeadlessBackupClassificationEntry SetClass(string fileName, BackupClass backupClass, string? reason)
    {
        ResolveManagedBackup(fileName, requireExists: true);
        var entry = new HeadlessBackupClassificationEntry(backupClass, DateTimeOffset.UtcNow, reason);
        lock (classificationGate)
        {
            classifications[fileName] = entry;
            PersistClassifications();
        }
        activity.Record("Information", "Backups", "Reclassified backup", $"file={fileName}; class={backupClass}");
        return entry;
    }

    public async Task<HeadlessBackupOperationResult> DeleteAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        if (!await backupGate.WaitAsync(0, cancellationToken))
            return HeadlessBackupOperationResult.Conflict("A backup operation is already in progress.");

        try
        {
            var fullPath = ResolveManagedBackup(fileName, requireExists: true);
            await Task.Run(() => File.Delete(fullPath), cancellationToken);
            return new HeadlessBackupOperationResult(
                true,
                fileName,
                null,
                $"Backup deleted: {fileName}");
        }
        catch (Exception ex)
        {
            return HeadlessBackupOperationResult.Failure(ex.Message);
        }
        finally
        {
            backupGate.Release();
        }
    }

    public async Task<HeadlessBackupOperationResult> RestoreAsync(
        string fileName,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        if (!confirmed)
            return HeadlessBackupOperationResult.Failure("Restore requires explicit confirmation.");
        if (!await backupGate.WaitAsync(0, cancellationToken))
            return HeadlessBackupOperationResult.Conflict("A backup operation is already in progress.");

        // v0.7.7.0: unlike HeadlessWorldTransactionService/HeadlessGuildOwnershipService/
        // HeadlessBaseOwnershipService/HeadlessCharacterMigrationService (which all hold the
        // coordinator's "world-mutation" lock for their own save-mutating operations),
        // RestoreAsync previously mutated paths.SaveRoot guarded only by the local backupGate
        // above -- so a restore could run concurrently with one of those transactions on the same
        // save tree. DeleteAsync/VerifyAsync deliberately do NOT take this lock: they read or remove
        // only archives. v1.0.7.1 (Q-4): CreateAsync takes it for scheduled and manual backups (a world
        // half way through an edit must not be zipped), and not for the safety backups those
        // already-locked transactions take, so it never rejects its own caller.
        OperationHandle? operation = null;
        try
        {
            operation = await coordinator.BeginAsync(profile, "backup-restore", "HeadlessBackupService", ["world-mutation"], cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            backupGate.Release();
            return HeadlessBackupOperationResult.Conflict("Another world-mutating operation is in progress: " + ex.Message);
        }

        try
        {
            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.ServerMayBeRunning)
            {
                const string busyMessage = "Stop PalServer before restoring a backup.";
                coordinator.Fail(operation.Id, busyMessage);
                activity.Record("Warning", "Backups", "Restore refused", $"file={fileName}; {busyMessage}");
                return HeadlessBackupOperationResult.Failure(busyMessage);
            }

            // v1.0.0.4: a PalServer started outside MystTiq (double-clicked, or from a script) is not in the status above.
            var outside = ServerProcessesOutsideMystTiq();
            if (outside.Count > 0)
            {
                var outsideMessage = $"PalServer is running from this server's folder (process {string.Join(", ", outside)}), started outside MystTiq. Stop it before restoring.";
                coordinator.Fail(operation.Id, outsideMessage);
                activity.Record("Warning", "Backups", "Restore refused", $"file={fileName}; {outsideMessage}");
                return HeadlessBackupOperationResult.Failure(outsideMessage);
            }

            var archivePath = ResolveManagedBackup(fileName, requireExists: true);
            if (!IsArchiveReadable(archivePath))
            {
                const string unreadableMessage = "The selected backup archive could not be verified.";
                coordinator.Fail(operation.Id, unreadableMessage);
                return HeadlessBackupOperationResult.Failure(unreadableMessage);
            }

            // v1.0.0.4: the 2026-10-01 failures were this, not a file in use: SaveGames belonged to administrators.
            if (!SaveFolderAccess.CanReplace(paths.SaveRoot))
            {
                coordinator.Fail(operation.Id, SaveFolderAccess.NoAccessMessage);
                activity.Record("Warning", "Backups", "Restore refused", $"file={fileName}; {SaveFolderAccess.NoAccessMessage}");
                return HeadlessBackupOperationResult.Failure(SaveFolderAccess.NoAccessMessage);
            }

            string? safetyBackup = null;
            if (Directory.Exists(paths.SaveRoot) &&
                Directory.EnumerateFiles(paths.SaveRoot, "*", SearchOption.AllDirectories).Any())
            {
                var safety = await CreateInternalAsync("pre-restore", BackupClass.Safety, cancellationToken);
                if (!safety.Success)
                {
                    var safetyMessage = "Restore aborted because the pre-restore safety backup failed: " + safety.Message;
                    coordinator.Fail(operation.Id, safetyMessage);
                    return HeadlessBackupOperationResult.Failure(safetyMessage);
                }
                safetyBackup = safety.FileName;
            }

            var staging = paths.SaveRoot + $".restore-staging-{Guid.NewGuid():N}";
            var rollback = paths.SaveRoot + $".restore-rollback-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}";

            try
            {
                Directory.CreateDirectory(staging);
                // v1.0.7.4 (roadmap Q-15): checked before anything is written (size, entries, free space).
                if (ArchiveLimits.CheckFile(archivePath, staging) is { } tooBig) throw new InvalidDataException(tooBig);
                ZipFile.ExtractToDirectory(archivePath, staging, overwriteFiles: true);

                if (!Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories).Any())
                    throw new InvalidDataException("The selected backup contains no save files.");

                if (Directory.Exists(paths.SaveRoot))
                    await MoveSaveRootAsideAsync(rollback, cancellationToken);

                Directory.Move(staging, paths.SaveRoot);
            }
            catch (Exception ex)
            {
                TryDeleteDirectory(staging);

                if (!Directory.Exists(paths.SaveRoot) && Directory.Exists(rollback))
                    Directory.Move(rollback, paths.SaveRoot);

                coordinator.Fail(operation.Id, ex.Message);
                throw;
            }

            // v0.7.7.0: the restore itself has already succeeded once control reaches here --
            // SaveRoot now holds the restored content. A failure to delete the now-redundant
            // rollback copy below must not be reported as "restore failed"; previously it was,
            // because this deletion ran inside the same try/catch as the actual restore above, so
            // e.g. an AV scanner holding a file lock on the rollback copy for a moment would make a
            // successful restore return Failure() while leaving the good data already in place.
            // v1.0.6.0: a held file is retried for a moment; files that belong to administrators are named as the reason.
            string? leftoverRollback = null, leftoverReason = null;
            if (Directory.Exists(rollback) && FileRetry.TryDeleteDirectory(rollback) is { } why) { leftoverRollback = rollback; leftoverReason = why; }

            var message = safetyBackup is null
                ? $"Backup restored: {fileName}"
                : $"Backup restored: {fileName}. Safety backup: {safetyBackup}";
            if (leftoverRollback is not null)
                message += leftoverReason == "denied"
                    ? $" Note: the temporary copy of the world from before the restore ({Path.GetFileName(leftoverRollback)}) was kept: some files in it belong to administrators. Fix Save Folder Access on the Backups page lets MystTiq remove it next time; it is safe to delete."
                    : $" Note: a temporary rollback copy could not be cleaned up automatically ({Path.GetFileName(leftoverRollback)}) -- safe to delete manually.";

            // v1.0.0.4: the proof the owner asked for: the restored world's day, read from the restored Level.sav itself (this
            // also refreshes the decoded copy the Dashboard reads, which the backup carried in its old state).
            var (dayText, restoredTicks) = await DescribeRestoredWorldAsync(fileName, cancellationToken);
            message += " " + dayText;

            coordinator.Complete(operation.Id, message);
            activity.Record("Information", "Backups", "Restored backup", $"file={fileName}; safety={safetyBackup ?? "none"}; {dayText}");
            return new HeadlessBackupOperationResult(true, fileName, safetyBackup, message)
            {
                RestoredWorldDayNumber = restoredTicks is { } rt ? WorldClock.Split(rt).Day : null,
                RestoredWorldTimeText = restoredTicks is { } t2 ? WorldClock.Split(t2).Time : null,
            };
        }
        catch (Exception ex)
        {
            coordinator.Fail(operation.Id, ex.Message);
            activity.Record("Warning", "Backups", "Restore failed", $"file={fileName}; {ex.Message}");
            return HeadlessBackupOperationResult.Failure(ex.Message);
        }
        finally
        {
            operation.Dispose();
            backupGate.Release();
        }
    }

    // v1.0.0.4: the 2026-10-01 restores failed at once with "Access to the path …\SaveGames is denied": something still had a
    // file in it open (moments after the server stopped, a window, a scan). Now: up to ten tries half a second apart, and if it
    // is still held, a message that names the program and says nothing was changed.
    private async Task MoveSaveRootAsideAsync(string rollback, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Move(paths.SaveRoot, rollback);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt < 10) { await Task.Delay(500, cancellationToken); continue; }
                if (!SaveFolderAccess.CanReplace(paths.SaveRoot)) throw new IOException(SaveFolderAccess.NoAccessMessage, ex);
                var holders = FileLockers.UnderFolder(paths.SaveRoot);
                throw new IOException(holders.Count > 0
                    ? $"The save folder is in use and could not be replaced, so nothing was restored. Open in: {string.Join(", ", holders)}. Close it and restore again."
                    : $"The save folder is in use and could not be replaced, so nothing was restored. Close any program or Explorer window using {paths.SaveRoot} and restore again.", ex);
            }
        }
    }

    // A PalServer process whose executable lives under this server's folder, which the lifecycle status does not know.
    private IReadOnlyList<string> ServerProcessesOutsideMystTiq()
    {
        var root = Path.GetFullPath(paths.ServerRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var found = new List<string>();
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                if (!process.ProcessName.StartsWith("PalServer", StringComparison.OrdinalIgnoreCase)) continue;
                var path = process.MainModule?.FileName;
                if (path is not null && Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase)) found.Add(process.Id.ToString());
            }
            catch { }
            finally { process.Dispose(); }
        }
        return found;
    }

    private async Task<(string Text, long? Ticks)> DescribeRestoredWorldAsync(string fileName, CancellationToken cancellationToken)
    {
        if (worldClock is null) return (string.Empty, null);
        var level = Directory.Exists(paths.SaveRoot)
            ? Directory.EnumerateFiles(paths.SaveRoot, "Level.sav", SearchOption.AllDirectories)
                .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p.Equals("backup", StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
        if (level is null) return ("The world's day could not be read: the restored save has no Level.sav.", null);
        var result = await worldClock.RefreshAsync(Path.GetDirectoryName(level)!, cancellationToken);
        if (!result.Success || result.Ticks is null) return ($"The world's day could not be read: {result.Error}", null);
        var (day, time) = WorldClock.Split(result.Ticks.Value);
        HeadlessBackupWorldDay? expected;
        lock (worldDaysGate) worldDays.TryGetValue(fileName, out expected);
        if (expected?.Ticks is { } backupTicks && backupTicks != result.Ticks.Value)
        {
            var (bd, bt) = WorldClock.Split(backupTicks);
            return ($"The world is now Day {day} {time}; the backup was read as Day {bd} {bt}.", result.Ticks);
        }
        return (expected?.Ticks is null ? $"The world is now Day {day} {time}." : $"The world is now Day {day} {time}, as in the backup.", result.Ticks);
    }

    private HeadlessBackupItem WithWorldDay(HeadlessBackupItem item)
    {
        HeadlessBackupWorldDay? day;
        lock (worldDaysGate) worldDays.TryGetValue(item.FileName, out day);
        if (day?.Ticks is not { } ticks) return item;
        var (number, time) = WorldClock.Split(ticks);
        return item with { WorldDayNumber = number, WorldTimeText = time };
    }

    // Reads the day of every backup not read yet (newest first), one at a time in the background; a failure is retried after
    // an hour (for example once the save tools are installed).
    private void QueueWorldDays()
    {
        if (worldClock is null || Interlocked.Exchange(ref worldDayReaderRunning, 1) == 1) return;
        // v1.0.7.0 (roadmap Q-3): stops at shutdown, and a failure is logged instead of dropped.
        // v1.0.7.2 (roadmap Q-12): says how far it is ("12 of 47"), names each archive it cannot read, can be cancelled, and
        // saves the list every WorldDaySaveEvery backups and at the end, not after every one.
        _ = BackgroundWork.Shared.Run("backup world days", async token =>
        {
            CancellationTokenSource? cancel = null;
            long id = 0;
            var unsaved = 0;
            try
            {
                var pending = Directory.EnumerateFiles(paths.BackupRoot, $"{Prefix}*.zip", SearchOption.TopDirectoryOnly)
                    .Select(f => new FileInfo(f)).OrderByDescending(f => f.LastWriteTimeUtc)
                    .Where(file =>
                    {
                        HeadlessBackupWorldDay? known;
                        lock (worldDaysGate) worldDays.TryGetValue(file.Name, out known);
                        return known is null || (known.Ticks is null && DateTimeOffset.UtcNow - known.ReadUtc >= TimeSpan.FromHours(1));
                    })
                    .ToList();
                if (pending.Count == 0 || VerifyAllRunning()) return;
                (cancel, id) = BeginScan("Reading world days", pending.Count, token);
                foreach (var file in pending)
                {
                    cancel.Token.ThrowIfCancellationRequested();
                    StepScan(id, file.Name, done: false);
                    var result = await worldClock.ReadBackupAsync(file.FullName, cancel.Token);
                    StepScan(id, file.Name, done: true, file.Name, result.Ticks is null ? result.Error ?? "its world day could not be read" : null);
                    lock (worldDaysGate) worldDays[file.Name] = new HeadlessBackupWorldDay(result.Ticks, result.Error, DateTimeOffset.UtcNow);
                    if (++unsaved >= WorldDaySaveEvery) { SaveWorldDays(); unsaved = 0; }
                }
                FinishScan(id, cancelled: false);
            }
            catch (OperationCanceledException) when (cancel?.IsCancellationRequested == true)
            {
                FinishScan(id, cancelled: true);
            }
            finally
            {
                if (unsaved > 0) SaveWorldDays();
                Interlocked.Exchange(ref worldDayReaderRunning, 0);
            }
        });
    }

    private void SaveWorldDays()
    {
        lock (worldDaysGate) File.WriteAllText(worldDaysPath, JsonSerializer.Serialize(worldDays));
    }

    private Dictionary<string, HeadlessBackupWorldDay> LoadWorldDays()
    {
        try
        {
            if (File.Exists(worldDaysPath))
                return new Dictionary<string, HeadlessBackupWorldDay>(
                    JsonSerializer.Deserialize<Dictionary<string, HeadlessBackupWorldDay>>(File.ReadAllText(worldDaysPath)) ?? [], StringComparer.OrdinalIgnoreCase);
        }
        catch { }
        return new Dictionary<string, HeadlessBackupWorldDay>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<HeadlessBackupVerificationResult> VerifyAsync(string fileName, CancellationToken cancellationToken)
    {
        if (!await backupGate.WaitAsync(0, cancellationToken))
            return HeadlessBackupVerificationResult.Conflict(fileName);

        try
        {
            var result = await VerifyInternalAsync(ResolveManagedBackup(fileName, true), cancellationToken);
            PersistVerification([result]);
            activity.Record(result.Success ? "Information" : "Warning", "Backups", "Verified backup",
                $"file={result.FileName}; sha256={result.Sha256}; entries={result.EntryCount}; result={result.Success}");
            return result;
        }
        catch (Exception ex)
        {
            return new(false, fileName, null, 0, 0, DateTimeOffset.UtcNow, ex.Message);
        }
        finally { backupGate.Release(); }
    }

    public async Task<HeadlessBackupVerificationBatch> VerifyAllAsync(CancellationToken cancellationToken)
    {
        if (!await backupGate.WaitAsync(0, cancellationToken))
            return new([], DateTimeOffset.UtcNow, "A backup operation is already in progress.");

        // v1.0.7.2 (roadmap Q-12): "3 of 47" while it runs, each failing archive named, and Cancel stops it after the
        // archive in hand (the ones done so far are kept).
        var items = GetInventory().Items;
        var (cancel, id) = BeginScan("Verifying backups", items.Count, cancellationToken);
        var results = new List<HeadlessBackupVerificationResult>();
        try
        {
            foreach (var item in items)
            {
                cancel.Token.ThrowIfCancellationRequested();
                StepScan(id, item.FileName, done: false);
                var result = await VerifyInternalAsync(ResolveManagedBackup(item.FileName, true), cancel.Token);
                results.Add(result);
                StepScan(id, item.FileName, done: true, item.FileName, result.Success ? null : result.Message);
            }
            FinishScan(id, cancelled: false);
            PersistVerification(results);
            activity.Record(results.All(x => x.Success) ? "Information" : "Warning", "Backups", "Verified all backups",
                $"count={results.Count}; passed={results.Count(x => x.Success)}");
            return new(results, DateTimeOffset.UtcNow, $"Verified {results.Count} managed backup(s).");
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            FinishScan(id, cancelled: true);
            PersistVerification(results);
            activity.Record("Information", "Backups", "Verify All cancelled", $"after {results.Count} of {items.Count}");
            return new(results, DateTimeOffset.UtcNow, $"Cancelled after {results.Count} of {items.Count} backup(s).");
        }
        finally { backupGate.Release(); }
    }

    public HeadlessBackupRetentionPreview PreviewRetention(HeadlessBackupRetentionRequest request)
    {
        var keepLatest = Math.Clamp(request.KeepLatest, 1, 1000);
        var maxAgeDays = Math.Clamp(request.MaxAgeDays, 1, 3650);
        // Default to Scheduled only -- this is what actually protects Manual/Emergency/Safety
        // backups from routine pruning: the retention tool literally cannot see them unless an
        // admin explicitly opts a class in.
        var includeClasses = request.IncludeClasses is { Count: > 0 }
            ? request.IncludeClasses
            : (IReadOnlyCollection<BackupClass>)new[] { BackupClass.Scheduled };
        var inventory = GetInventory();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-maxAgeDays);
        var candidates = inventory.Items.Where(x => includeClasses.Contains(x.Class)).Skip(keepLatest).Where(x => x.CreatedAt < cutoff)
            .Select(x => new HeadlessBackupRetentionItem(x.FileName, x.SizeBytes, x.CreatedAt)).ToArray();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var preview = new HeadlessBackupRetentionPreview(token, keepLatest, maxAgeDays, candidates,
            candidates.Sum(x => x.SizeBytes), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15),
            candidates.Length == 0 ? "No backups match this retention policy." : $"{candidates.Length} backup(s) would be deleted.");
        lock (retentionPreviews) retentionPreviews[token] = preview;
        activity.Record("Information", "Backups", "Previewed retention cleanup",
            $"token={token}; keepLatest={keepLatest}; maxAgeDays={maxAgeDays}; candidates={candidates.Length}");
        return preview;
    }

    public async Task<HeadlessBackupRetentionApplyResult> ApplyRetentionAsync(string token, CancellationToken cancellationToken)
    {
        if (!await backupGate.WaitAsync(0, cancellationToken))
            return HeadlessBackupRetentionApplyResult.Failure("A backup operation is already in progress.");
        try
        {
            HeadlessBackupRetentionPreview? preview;
            lock (retentionPreviews)
            {
                retentionPreviews.Remove(token ?? string.Empty, out preview);
            }
            if (preview is null || preview.ExpiresAt < DateTimeOffset.UtcNow)
                return HeadlessBackupRetentionApplyResult.Failure("Retention preview is missing or expired. Preview again.");

            foreach (var item in preview.Items)
            {
                var info = new FileInfo(ResolveManagedBackup(item.FileName, true));
                if (info.Length != item.SizeBytes || info.LastWriteTimeUtc != item.CreatedAt.UtcDateTime)
                    return HeadlessBackupRetentionApplyResult.Failure("Backup inventory changed. Preview again before applying cleanup.");
            }

            foreach (var item in preview.Items)
                File.Delete(ResolveManagedBackup(item.FileName, true));
            activity.Record("Information", "Backups", "Applied retention cleanup",
                $"token={preview.Token}; deleted={preview.Items.Count}; bytes={preview.ReclaimBytes}");
            return new(true, preview.Items.Count, preview.ReclaimBytes, "Retention cleanup applied exactly as previewed.");
        }
        catch (Exception ex) { return HeadlessBackupRetentionApplyResult.Failure(ex.Message); }
        finally { backupGate.Release(); }
    }

    private static async Task<HeadlessBackupVerificationResult> VerifyInternalAsync(string fullPath, CancellationToken cancellationToken)
    {
        var info = new FileInfo(fullPath);
        var count = 0;
        try
        {
            using (var archive = ZipFile.OpenRead(fullPath))
            {
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var normalized = entry.FullName.Replace('\\', '/');
                    if (normalized.StartsWith('/') || normalized.Split('/').Any(x => x == ".."))
                        throw new InvalidDataException("Archive contains an unsafe path.");
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    await using var stream = entry.Open();
                    await stream.CopyToAsync(Stream.Null, cancellationToken);
                    count++;
                }
            }
            await using var input = File.OpenRead(fullPath);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken));
            return new(true, info.Name, hash, count, info.Length, DateTimeOffset.UtcNow, "Archive content and paths verified.");
        }
        catch (Exception ex)
        {
            return new(false, info.Name, null, count, info.Exists ? info.Length : 0, DateTimeOffset.UtcNow, ex.Message);
        }
    }

    private void PersistVerification(IEnumerable<HeadlessBackupVerificationResult> results)
    {
        Dictionary<string, HeadlessBackupVerificationResult> records;
        try
        {
            records = File.Exists(verificationPath)
                ? JsonSerializer.Deserialize<Dictionary<string, HeadlessBackupVerificationResult>>(File.ReadAllText(verificationPath)) ?? []
                : [];
        }
        catch { records = []; }
        foreach (var result in results) records[result.FileName] = result;
        var partial = verificationPath + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(partial, verificationPath, true);
    }

    private Dictionary<string, HeadlessBackupClassificationEntry> LoadClassifications()
    {
        try
        {
            return File.Exists(classificationPath)
                ? JsonSerializer.Deserialize<Dictionary<string, HeadlessBackupClassificationEntry>>(File.ReadAllText(classificationPath)) ?? []
                : [];
        }
        catch { return []; }
    }

    // Caller must already hold classificationGate.
    private void PersistClassifications()
    {
        var partial = classificationPath + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(classifications, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(partial, classificationPath, true);
    }

    public const int HeldSaveAttempts = 10;
    private static readonly TimeSpan HeldSavePause = TimeSpan.FromSeconds(1);

    private async Task<HeadlessBackupOperationResult> CreateInternalAsync(
        string reason,
        BackupClass backupClass,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(paths.SaveRoot) ||
            !Directory.EnumerateFiles(paths.SaveRoot, "*", SearchOption.AllDirectories).Any())
        {
            return HeadlessBackupOperationResult.Failure(
                $"No Palworld save data was found beneath {paths.SaveRoot}.");
        }

        Directory.CreateDirectory(paths.BackupRoot);

        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss-fff");
        var fileName = $"{Prefix}{stamp}.zip";
        var finalPath = Path.Combine(paths.BackupRoot, fileName);
        var partialPath = finalPath + ".partial";

        try
        {
            // v1.0.7.5 (roadmap Q-10): a save file held open (the server writing it, a scan, an editor) failed the backup at
            // once. Now up to ten tries a second apart; if it is still held, the message names the file and what holds it.
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        ZipFile.CreateFromDirectory(
                            paths.SaveRoot,
                            partialPath,
                            CompressionLevel.Fastest,
                            includeBaseDirectory: false);

                        if (!IsArchiveReadable(partialPath))
                            throw new InvalidDataException("New backup archive failed verification.");

                        File.Move(partialPath, finalPath, overwrite: false);
                    }, cancellationToken);
                    break;
                }
                catch (IOException ex) when (FileRetry.IsHeld(ex))
                {
                    if (File.Exists(partialPath)) File.Delete(partialPath);
                    if (attempt < HeldSaveAttempts) { await Task.Delay(HeldSavePause, cancellationToken); continue; }
                    var holders = FileLockers.UnderFolder(paths.SaveRoot);
                    throw new IOException($"A save file stayed in use for {HeldSaveAttempts} seconds, so no backup was made. {ex.Message}" +
                        (holders.Count > 0 ? $" Open in: {string.Join(", ", holders)}." : string.Empty) + " Try again in a moment.", ex);
                }
            }

            lock (classificationGate)
            {
                classifications[fileName] = new HeadlessBackupClassificationEntry(backupClass, DateTimeOffset.UtcNow, reason);
                PersistClassifications();
            }

            return new HeadlessBackupOperationResult(
                true,
                fileName,
                null,
                $"Backup created ({reason}): {fileName}");
        }
        catch (Exception ex)
        {
            if (File.Exists(partialPath))
                File.Delete(partialPath);

            return HeadlessBackupOperationResult.Failure(ex.Message);
        }
    }

    private string ResolveManagedBackup(string fileName, bool requireExists)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Backup filename is required.", nameof(fileName));

        var safeName = Path.GetFileName(fileName);
        if (!string.Equals(safeName, fileName, StringComparison.Ordinal) ||
            !safeName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            !safeName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected file is not a managed MystTiq Palworld backup.");
        }

        var root = Path.GetFullPath(paths.BackupRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(paths.BackupRoot, safeName));

        if (!fullPath.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidOperationException("Backup path escaped the configured backup root.");

        if (requireExists && !File.Exists(fullPath))
            throw new FileNotFoundException("The selected backup was not found.", fullPath);

        return fullPath;
    }

    private static bool IsArchiveReadable(string path)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.Contains("..", StringComparison.Ordinal))
                    return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum BackupClass { Manual, Scheduled, Emergency, Safety }

public sealed record HeadlessBackupClassificationEntry(BackupClass Class, DateTimeOffset ClassifiedUtc, string? Reason);

public sealed record HeadlessBackupItem(
    string FileName,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    bool Verified,
    BackupClass Class)
{
    // v1.0.0.4: the world's in-game day inside this backup (from its own Level.sav), once read.
    public long? WorldDayNumber { get; init; }
    public string? WorldTimeText { get; init; }
}

public sealed record HeadlessBackupWorldDay(long? Ticks, string? Error, DateTimeOffset ReadUtc);

public sealed record HeadlessBackupInventory(
    int Count,
    long TotalSizeBytes,
    IReadOnlyList<HeadlessBackupItem> Items,
    DateTimeOffset ObservedAt,
    string RootPath,
    string Detail)
{
    public bool SaveFolderReplaceable { get; init; } = true;
    public string? SavedFolderPath { get; init; }
    // v1.0.7.2 (roadmap Q-12): the scan running now, or the last one.
    public BackupScanProgress? Scan { get; init; }
}

public sealed record HeadlessBackupOperationResult(
    bool Success,
    string? FileName,
    string? SafetyBackupFileName,
    string Message)
{
    // v1.0.0.4: after a restore, the restored world's day read from its Level.sav.
    public long? RestoredWorldDayNumber { get; init; }
    public string? RestoredWorldTimeText { get; init; }

    public static HeadlessBackupOperationResult Failure(string message) =>
        new(false, null, null, message);

    // v1.0.7.1 (roadmap Q-4): refused because another operation runs (not a failure of the backup itself), so unattended
    // work can wait and try again.
    public bool IsConflict { get; init; }

    public static HeadlessBackupOperationResult Conflict(string message) =>
        new(false, null, null, message) { IsConflict = true };
}

public sealed record HeadlessBackupRestoreRequest(bool Confirmed);
public sealed record HeadlessBackupVerificationResult(bool Success, string FileName, string? Sha256, int EntryCount, long SizeBytes, DateTimeOffset VerifiedAt, string Message)
{
    public static HeadlessBackupVerificationResult Conflict(string fileName) => new(false, fileName, null, 0, 0, DateTimeOffset.UtcNow, "A backup operation is already in progress.");
}
// v1.0.7.2 (roadmap Q-12): a backup scan's progress: what it is doing, how far, the archive in hand, and the archives it
// could not read (with why).
public sealed record BackupScanProgress(string Kind, int Done, int Total, string? Current, IReadOnlyList<BackupScanProblem> Unreadable,
    DateTimeOffset StartedUtc, DateTimeOffset? FinishedUtc, bool Cancelled);
public sealed record BackupScanProblem(string FileName, string Reason);
public sealed record HeadlessBackupVerificationBatch(IReadOnlyList<HeadlessBackupVerificationResult> Results, DateTimeOffset VerifiedAt, string Message);
// IReadOnlyCollection, not IReadOnlySet -- System.Text.Json's default deserializer cannot
// populate an IReadOnlySet<T> from a JSON array (it's abstract/read-only from its perspective),
// so any caller providing includeClasses would crash the whole request with a 500.
public sealed record HeadlessBackupRetentionRequest(int KeepLatest, int MaxAgeDays, IReadOnlyCollection<BackupClass>? IncludeClasses = null);
public sealed record HeadlessBackupSetClassRequest(BackupClass Class, string? Reason);
public sealed record HeadlessBackupRetentionItem(string FileName, long SizeBytes, DateTimeOffset CreatedAt);
public sealed record HeadlessBackupRetentionPreview(string Token, int KeepLatest, int MaxAgeDays, IReadOnlyList<HeadlessBackupRetentionItem> Items, long ReclaimBytes, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string Message);
public sealed record HeadlessBackupRetentionApplyRequest(string Token);
public sealed record HeadlessBackupRetentionApplyResult(bool Success, int DeletedCount, long ReclaimedBytes, string Message)
{
    public static HeadlessBackupRetentionApplyResult Failure(string message) => new(false, 0, 0, message);
}
