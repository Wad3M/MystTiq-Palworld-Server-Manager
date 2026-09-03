using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessBackupService
{
    private const string Prefix = "Palworld_";
    private readonly IServerPathProfile paths;
    private readonly IServerLifecycleService lifecycle;
    private readonly HeadlessActivityLogService activity;
    private readonly SemaphoreSlim backupGate = new(1, 1);
    private readonly string verificationPath;
    private readonly Dictionary<string, HeadlessBackupRetentionPreview> retentionPreviews = new(StringComparer.Ordinal);

    public HeadlessBackupService(
        IServerPathProfile paths,
        IServerLifecycleService lifecycle,
        HeadlessActivityLogService activity)
    {
        this.paths = paths;
        this.lifecycle = lifecycle;
        this.activity = activity;
        var stateRoot = Path.Combine(paths.ManagerRuntimeRoot, "backups");
        Directory.CreateDirectory(stateRoot);
        verificationPath = Path.Combine(stateRoot, "verification.json");
    }

    public HeadlessBackupInventory GetInventory()
    {
        Directory.CreateDirectory(paths.BackupRoot);

        var items = Directory.EnumerateFiles(paths.BackupRoot, $"{Prefix}*.zip", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new HeadlessBackupItem(
                file.Name,
                file.Length,
                file.LastWriteTimeUtc,
                IsArchiveReadable(file.FullName)))
            .ToList();

        return new HeadlessBackupInventory(
            items.Count,
            items.Sum(item => item.SizeBytes),
            items,
            DateTimeOffset.UtcNow,
            paths.BackupRoot,
            $"{items.Count} managed backup(s) in {paths.BackupRoot}.");
    }

    public async Task<HeadlessBackupOperationResult> CreateAsync(CancellationToken cancellationToken)
    {
        if (!await backupGate.WaitAsync(0, cancellationToken))
            return HeadlessBackupOperationResult.Conflict("A backup operation is already in progress.");

        try
        {
            return await CreateInternalAsync("manual", cancellationToken);
        }
        finally
        {
            backupGate.Release();
        }
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

        try
        {
            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.NativeProcessId.HasValue || status.Ready)
            {
                return HeadlessBackupOperationResult.Failure(
                    "Stop PalServer before restoring a backup.");
            }

            var archivePath = ResolveManagedBackup(fileName, requireExists: true);
            if (!IsArchiveReadable(archivePath))
                return HeadlessBackupOperationResult.Failure("The selected backup archive could not be verified.");

            string? safetyBackup = null;
            if (Directory.Exists(paths.SaveRoot) &&
                Directory.EnumerateFiles(paths.SaveRoot, "*", SearchOption.AllDirectories).Any())
            {
                var safety = await CreateInternalAsync("pre-restore", cancellationToken);
                if (!safety.Success)
                    return HeadlessBackupOperationResult.Failure(
                        "Restore aborted because the pre-restore safety backup failed: " + safety.Message);
                safetyBackup = safety.FileName;
            }

            var staging = paths.SaveRoot + $".restore-staging-{Guid.NewGuid():N}";
            var rollback = paths.SaveRoot + $".restore-rollback-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}";

            try
            {
                Directory.CreateDirectory(staging);
                ZipFile.ExtractToDirectory(archivePath, staging, overwriteFiles: true);

                if (!Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories).Any())
                    throw new InvalidDataException("The selected backup contains no save files.");

                if (Directory.Exists(paths.SaveRoot))
                    Directory.Move(paths.SaveRoot, rollback);

                Directory.Move(staging, paths.SaveRoot);

                if (Directory.Exists(rollback))
                    Directory.Delete(rollback, recursive: true);
            }
            catch
            {
                TryDeleteDirectory(staging);

                if (!Directory.Exists(paths.SaveRoot) && Directory.Exists(rollback))
                    Directory.Move(rollback, paths.SaveRoot);

                throw;
            }

            return new HeadlessBackupOperationResult(
                true,
                fileName,
                safetyBackup,
                safetyBackup is null
                    ? $"Backup restored: {fileName}"
                    : $"Backup restored: {fileName}. Safety backup: {safetyBackup}");
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

        try
        {
            var results = new List<HeadlessBackupVerificationResult>();
            foreach (var item in GetInventory().Items)
                results.Add(await VerifyInternalAsync(ResolveManagedBackup(item.FileName, true), cancellationToken));
            PersistVerification(results);
            activity.Record(results.All(x => x.Success) ? "Information" : "Warning", "Backups", "Verified all backups",
                $"count={results.Count}; passed={results.Count(x => x.Success)}");
            return new(results, DateTimeOffset.UtcNow, $"Verified {results.Count} managed backup(s).");
        }
        finally { backupGate.Release(); }
    }

    public HeadlessBackupRetentionPreview PreviewRetention(HeadlessBackupRetentionRequest request)
    {
        var keepLatest = Math.Clamp(request.KeepLatest, 1, 1000);
        var maxAgeDays = Math.Clamp(request.MaxAgeDays, 1, 3650);
        var inventory = GetInventory();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-maxAgeDays);
        var candidates = inventory.Items.Skip(keepLatest).Where(x => x.CreatedAt < cutoff)
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

    private async Task<HeadlessBackupOperationResult> CreateInternalAsync(
        string reason,
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

public sealed record HeadlessBackupItem(
    string FileName,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    bool Verified);

public sealed record HeadlessBackupInventory(
    int Count,
    long TotalSizeBytes,
    IReadOnlyList<HeadlessBackupItem> Items,
    DateTimeOffset ObservedAt,
    string RootPath,
    string Detail);

public sealed record HeadlessBackupOperationResult(
    bool Success,
    string? FileName,
    string? SafetyBackupFileName,
    string Message)
{
    public static HeadlessBackupOperationResult Failure(string message) =>
        new(false, null, null, message);

    public static HeadlessBackupOperationResult Conflict(string message) =>
        new(false, null, null, message);
}

public sealed record HeadlessBackupRestoreRequest(bool Confirmed);
public sealed record HeadlessBackupVerificationResult(bool Success, string FileName, string? Sha256, int EntryCount, long SizeBytes, DateTimeOffset VerifiedAt, string Message)
{
    public static HeadlessBackupVerificationResult Conflict(string fileName) => new(false, fileName, null, 0, 0, DateTimeOffset.UtcNow, "A backup operation is already in progress.");
}
public sealed record HeadlessBackupVerificationBatch(IReadOnlyList<HeadlessBackupVerificationResult> Results, DateTimeOffset VerifiedAt, string Message);
public sealed record HeadlessBackupRetentionRequest(int KeepLatest, int MaxAgeDays);
public sealed record HeadlessBackupRetentionItem(string FileName, long SizeBytes, DateTimeOffset CreatedAt);
public sealed record HeadlessBackupRetentionPreview(string Token, int KeepLatest, int MaxAgeDays, IReadOnlyList<HeadlessBackupRetentionItem> Items, long ReclaimBytes, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string Message);
public sealed record HeadlessBackupRetentionApplyRequest(string Token);
public sealed record HeadlessBackupRetentionApplyResult(bool Success, int DeletedCount, long ReclaimedBytes, string Message)
{
    public static HeadlessBackupRetentionApplyResult Failure(string message) => new(false, 0, 0, message);
}
