using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class BackupSourceLockedException(
    string relativePath,
    int attempts,
    Exception? innerException)
    : IOException(
        $"The save file '{relativePath}' remained locked after {attempts} attempts.",
        innerException)
{
    public string RelativePath { get; } = relativePath;
    public int Attempts { get; } = attempts;
}

public sealed class BackupService(AppSettings settings)
{
    private const int CopyAttempts = 12;
    private static readonly TimeSpan CopyRetryDelay = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan StabilityPollDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StabilityTimeout = TimeSpan.FromSeconds(30);


    public BackupInventorySummary GetInventorySummary()
    {
        Directory.CreateDirectory(settings.BackupRoot);

        var archives = Directory.EnumerateFiles(settings.BackupRoot, "*.zip", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .ToList();

        var server = archives.Count(file =>
            file.DirectoryName is not null &&
            Path.GetFullPath(file.DirectoryName).TrimEnd(Path.DirectorySeparatorChar)
                .Equals(Path.GetFullPath(settings.BackupRoot).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) &&
            file.Name.StartsWith("Palworld_", StringComparison.OrdinalIgnoreCase));
        var worldArchives = archives.Count(file => IsUnderFolder(file.FullName, "WorldArchives"));
        var repair = archives.Count(file => IsUnderFolder(file.FullName, "RepairCenter"));
        var mods = archives.Count(file => IsUnderFolder(file.FullName, "UE4SS-Runtimes") || IsUnderFolder(file.FullName, "Mods"));
        var other = Math.Max(0, archives.Count - server - worldArchives - repair - mods);
        var verified = List().Count(row => row.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase));
        var retentionCandidates = PreviewRetention().Count;

        return new BackupInventorySummary(
            archives.Count,
            server,
            worldArchives,
            repair,
            mods,
            other,
            archives.Sum(file => file.Length),
            verified,
            retentionCandidates);
    }

    public List<BackupRow> PreviewRetention()
    {
        return List().Skip(Math.Max(1, settings.BackupRetention)).ToList();
    }

    public int ApplyRetentionPreview(IEnumerable<string> backupPaths)
    {
        var deleted = 0;
        foreach (var path in backupPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                Delete(path);
                deleted++;
            }
            catch (FileNotFoundException)
            {
                // A previous cleanup or external process already removed it.
            }
        }

        return deleted;
    }

    private static bool IsUnderFolder(string path, string folderName)
    {
        var parts = Path.GetFullPath(path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => part.Equals(folderName, StringComparison.OrdinalIgnoreCase));
    }

    public List<BackupRow> List()
    {
        Directory.CreateDirectory(settings.BackupRoot);

        return Directory
            .EnumerateFiles(settings.BackupRoot, "Palworld_*.zip")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTimeUtc)
            .Select(file =>
            {
                var manifest = TryReadManifest(file.FullName);
                var status = manifest is null
                    ? "Unverified"
                    : manifest.ArchiveLength == file.Length && manifest.Verified
                        ? "Verified"
                        : "Needs verification";
                var verifiedAt = manifest?.VerifiedAtLocal is DateTime verified
                    ? verified.ToString("g")
                    : "Never";

                return new BackupRow(
                    file.FullName,
                    file.CreationTime,
                    Math.Round(file.Length / 1024d / 1024d, 2),
                    status,
                    verifiedAt);
            })
            .ToList();
    }

    public async Task<string> CreateAsync(
        ApiClient? api,
        bool saveFirst,
        CancellationToken token,
        bool applyRetention = true)
    {
        if (!Directory.Exists(settings.SaveRoot))
        {
            throw new InvalidOperationException(
                "No Palworld world save was found. Start the dedicated server once and allow it to create a world before making a backup.");
        }

        if (saveFirst && api is not null)
        {
            await api.SaveAsync(token);
            await WaitForSaveFilesStableAsync(token);
        }

        Directory.CreateDirectory(settings.BackupRoot);
        EnsureBackupDriveHasSpace();

        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
        var snapshot = Path.Combine(settings.BackupRoot, $".snapshot_{stamp}");
        var partial = Path.Combine(settings.BackupRoot, $"Palworld_{stamp}.zip.partial");
        var completed = Path.Combine(settings.BackupRoot, $"Palworld_{stamp}.zip");

        try
        {
            await CopyDirectoryWithRetriesAsync(
                settings.SaveRoot,
                snapshot,
                token);

            await Task.Run(() =>
            {
                ZipFile.CreateFromDirectory(
                    snapshot,
                    partial,
                    CompressionLevel.Fastest,
                    includeBaseDirectory: false);

                ValidateArchive(partial, fullyReadEntries: true);
                File.Move(partial, completed);

                var verification = VerifyArchiveCore(completed, fullyReadEntries: true);
                WriteManifest(completed, verification, saveFirst);
            }, token);
        }
        catch
        {
            TryDeleteFile(partial);
            TryDeleteManagedBackup(completed);
            throw;
        }
        finally
        {
            TryDeleteDirectory(snapshot);
            TryDeleteFile(partial);
        }

        if (applyRetention)
        {
            foreach (var oldBackup in List().Skip(Math.Max(1, settings.BackupRetention)))
            {
                TryDeleteManagedBackup(oldBackup.FilePath);
            }
        }

        return completed;
    }

    public void Delete(string backup)
    {
        if (string.IsNullOrWhiteSpace(backup))
            throw new ArgumentException("A backup path is required.", nameof(backup));

        var backupRoot = Path.GetFullPath(settings.BackupRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(backup);

        if (!fullPath.StartsWith(backupRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected file is outside the configured backup folder.");

        if (!Path.GetExtension(fullPath).Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullPath).StartsWith("Palworld_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected file is not a managed Palworld backup.");
        }

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The selected backup no longer exists.", fullPath);

        File.Delete(fullPath);
        TryDeleteFile(ManifestPath(fullPath));
    }

    public async Task<string> RestoreAsync(
        string backup,
        ServerService server,
        CancellationToken token)
    {
        if (server.IsRunning())
            throw new InvalidOperationException("Stop the server before restoring a backup.");

        var backupRoot = Path.GetFullPath(settings.BackupRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var fullBackupPath = Path.GetFullPath(backup);

        if (!fullBackupPath.StartsWith(backupRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected backup is outside the configured backup folder.");

        if (!File.Exists(fullBackupPath))
            throw new FileNotFoundException("The selected backup was not found.", fullBackupPath);

        var verification = await VerifyAsync(fullBackupPath, token);
        if (!verification.Verified)
            throw new InvalidDataException("The selected backup did not pass verification: " + verification.Summary);

        // A restore always makes a verified safety backup of the current world first.
        // Retention is deliberately deferred so the safety copy cannot delete the backup
        // the user is currently restoring.
        string? safetyBackup = null;
        if (Directory.Exists(settings.SaveRoot) &&
            Directory.EnumerateFiles(settings.SaveRoot, "*", SearchOption.AllDirectories).Any())
        {
            safetyBackup = await CreateAsync(null, saveFirst: false, token: token, applyRetention: false);
        }

        var staging = settings.SaveRoot + ".restore_staging";
        var rollback = settings.SaveRoot +
                       $".restore_rollback_{DateTime.Now:yyyyMMdd_HHmmss_fff}";

        await Task.Run(() =>
        {
            TryDeleteDirectory(staging);
            Directory.CreateDirectory(staging);

            try
            {
                ZipFile.ExtractToDirectory(fullBackupPath, staging, overwriteFiles: true);

                if (!Directory.EnumerateFiles(
                        staging,
                        "*",
                        SearchOption.AllDirectories).Any())
                {
                    throw new InvalidDataException(
                        "The selected backup contains no files.");
                }

                if (Directory.Exists(settings.SaveRoot))
                    Directory.Move(settings.SaveRoot, rollback);

                Directory.Move(staging, settings.SaveRoot);
                TryDeleteDirectory(rollback);
            }
            catch
            {
                TryDeleteDirectory(staging);

                if (!Directory.Exists(settings.SaveRoot) &&
                    Directory.Exists(rollback))
                {
                    Directory.Move(rollback, settings.SaveRoot);
                }

                throw;
            }
        }, token);

        return safetyBackup ?? "No previous world existed; no safety backup was required.";
    }

    private async Task WaitForSaveFilesStableAsync(CancellationToken token)
    {
        var deadline = DateTime.UtcNow + StabilityTimeout;
        IReadOnlyDictionary<string, FileSignature>? previous = null;
        var consecutiveStableChecks = 0;

        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();

            var current = CaptureSaveFileSignatures();

            if (previous is not null && SignaturesMatch(previous, current))
            {
                consecutiveStableChecks++;

                // Require two matching intervals so the directory has remained
                // unchanged for roughly two seconds.
                if (consecutiveStableChecks >= 2)
                    return;
            }
            else
            {
                consecutiveStableChecks = 0;
            }

            previous = current;
            await Task.Delay(StabilityPollDelay, token);
        }

        // Continue with the retrying snapshot copier. Some Palworld builds keep
        // timestamps active longer even though files are readable.
    }

    private IReadOnlyDictionary<string, FileSignature> CaptureSaveFileSignatures()
    {
        var result = new Dictionary<string, FileSignature>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory.EnumerateFiles(
                     settings.SaveRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            try
            {
                var file = new FileInfo(path);
                var relative = Path.GetRelativePath(settings.SaveRoot, path);
                result[relative] = new FileSignature(
                    file.Length,
                    file.LastWriteTimeUtc.Ticks);
            }
            catch (FileNotFoundException)
            {
                // A transient file disappeared during enumeration. The next
                // poll will produce the authoritative directory snapshot.
            }
        }

        return result;
    }

    private static bool SignaturesMatch(
        IReadOnlyDictionary<string, FileSignature> left,
        IReadOnlyDictionary<string, FileSignature> right)
    {
        if (left.Count != right.Count)
            return false;

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var other) ||
                pair.Value != other)
            {
                return false;
            }
        }

        return true;
    }

    private static async Task CopyDirectoryWithRetriesAsync(
        string sourceRoot,
        string destinationRoot,
        CancellationToken token)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var directory in Directory.EnumerateDirectories(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(sourceRoot, directory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, relative));
        }

        foreach (var sourceFile in Directory.EnumerateFiles(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(sourceRoot, sourceFile);
            var destinationFile = Path.Combine(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);

            await CopyFileWithRetriesAsync(
                sourceFile,
                destinationFile,
                relative,
                token);
        }
    }

    private static async Task CopyFileWithRetriesAsync(
        string sourceFile,
        string destinationFile,
        string relativePath,
        CancellationToken token)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= CopyAttempts; attempt++)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                await using var source = new FileStream(
                    sourceFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 128 * 1024,
                    options: FileOptions.Asynchronous |
                             FileOptions.SequentialScan);

                await using var destination = new FileStream(
                    destinationFile,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 128 * 1024,
                    options: FileOptions.Asynchronous |
                             FileOptions.SequentialScan);

                await source.CopyToAsync(destination, 128 * 1024, token);
                await destination.FlushAsync(token);
                return;
            }
            catch (IOException exception) when (attempt < CopyAttempts)
            {
                lastError = exception;
                TryDeleteFile(destinationFile);
                await Task.Delay(CopyRetryDelay, token);
            }
            catch (UnauthorizedAccessException exception)
                when (attempt < CopyAttempts)
            {
                lastError = exception;
                TryDeleteFile(destinationFile);
                await Task.Delay(CopyRetryDelay, token);
            }
            catch (Exception exception)
            {
                lastError = exception;
                break;
            }
        }

        TryDeleteFile(destinationFile);

        throw new BackupSourceLockedException(
            relativePath,
            CopyAttempts,
            lastError);
    }

    private void EnsureBackupDriveHasSpace()
    {
        var sourceBytes = Directory
            .EnumerateFiles(settings.SaveRoot, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                try { return new FileInfo(path).Length; }
                catch { return 0L; }
            })
            .Sum();

        var backupRoot = Path.GetFullPath(settings.BackupRoot);
        var driveRoot = Path.GetPathRoot(backupRoot);

        if (string.IsNullOrWhiteSpace(driveRoot))
            return;

        var drive = new DriveInfo(driveRoot);

        // Snapshot + ZIP can temporarily need close to twice the source size.
        var requiredBytes = Math.Max(sourceBytes * 2, 256L * 1024 * 1024);

        if (drive.AvailableFreeSpace < requiredBytes)
        {
            throw new IOException(
                $"Not enough free space on {drive.Name}. " +
                $"At least {requiredBytes / 1024d / 1024d:N0} MB is required.");
        }
    }

    public async Task<BackupVerificationResult> VerifyAsync(
        string backup,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(backup))
            throw new ArgumentException("A backup path is required.", nameof(backup));

        var backupRoot = Path.GetFullPath(settings.BackupRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(backup);

        if (!fullPath.StartsWith(backupRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected file is outside the configured backup folder.");
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The selected backup was not found.", fullPath);

        var existingManifest = TryReadManifest(fullPath);
        var result = await Task.Run(
            () => VerifyArchiveCore(fullPath, fullyReadEntries: true),
            token);

        if (existingManifest is { Verified: true } &&
            !string.IsNullOrWhiteSpace(existingManifest.ArchiveSha256) &&
            !existingManifest.ArchiveSha256.Equals(result.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The backup archive checksum no longer matches its previous verified manifest. " +
                "The file may have been modified or corrupted.");
        }

        WriteManifest(
            fullPath,
            result,
            saveFirst: existingManifest?.SaveRequestedBeforeSnapshot ?? false);
        return result;
    }

    private static BackupVerificationResult VerifyArchiveCore(
        string archivePath,
        bool fullyReadEntries)
    {
        var details = ValidateArchive(archivePath, fullyReadEntries);
        var info = new FileInfo(archivePath);

        using var sha = SHA256.Create();
        using var stream = new FileStream(
            archivePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.SequentialScan);
        var hash = Convert.ToHexString(sha.ComputeHash(stream));

        return new BackupVerificationResult(
            true,
            $"Verified {details.FileCount:N0} files ({details.UncompressedBytes / 1024d / 1024d:N2} MB uncompressed).",
            hash,
            info.Length,
            details.FileCount,
            details.UncompressedBytes,
            DateTime.Now);
    }

    private static ArchiveValidationDetails ValidateArchive(
        string archivePath,
        bool fullyReadEntries)
    {
        using var archive = ZipFile.OpenRead(archivePath);

        if (archive.Entries.Count == 0)
            throw new InvalidDataException("The backup archive is empty.");

        if (!archive.Entries.Any(entry => entry.Length > 0))
            throw new InvalidDataException(
                "The backup archive contains no non-empty files.");

        var archiveRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PalworldBackupValidation"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        long totalBytes = 0;
        var fileCount = 0;

        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(archiveRoot, entry.FullName));
            if (!target.StartsWith(archiveRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The backup archive contains an unsafe file path.");

            if (string.IsNullOrEmpty(entry.Name))
                continue;

            fileCount++;
            totalBytes += entry.Length;

            if (!fullyReadEntries)
                continue;

            // Read every compressed entry to EOF. This catches truncated/corrupt ZIP
            // data that merely opening the central directory would not detect.
            using var entryStream = entry.Open();
            var buffer = new byte[64 * 1024];
            while (entryStream.Read(buffer, 0, buffer.Length) > 0) { }
        }

        var hasLevel = archive.Entries.Any(entry =>
            entry.FullName.EndsWith("Level.sav", StringComparison.OrdinalIgnoreCase));
        var hasRecognizableSaveData = hasLevel || archive.Entries.Any(entry =>
            entry.FullName.EndsWith("LevelMeta.sav", StringComparison.OrdinalIgnoreCase) ||
            entry.FullName.EndsWith("WorldOption.sav", StringComparison.OrdinalIgnoreCase) ||
            entry.FullName.EndsWith("UserOption.sav", StringComparison.OrdinalIgnoreCase));

        if (!hasRecognizableSaveData)
        {
            throw new InvalidDataException(
                "The archive does not appear to contain recognizable Palworld save data.");
        }

        if (!hasLevel)
        {
            throw new InvalidDataException(
                "The archive does not contain Level.sav, so it cannot be treated as a complete world backup.");
        }

        return new ArchiveValidationDetails(fileCount, totalBytes);
    }

    private static string ManifestPath(string archivePath) => archivePath + ".manifest.json";

    private static BackupManifest? TryReadManifest(string archivePath)
    {
        var path = ManifestPath(archivePath);
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static void WriteManifest(
        string archivePath,
        BackupVerificationResult verification,
        bool saveFirst)
    {
        var manifest = new BackupManifest
        {
            FormatVersion = 1,
            ArchiveFile = Path.GetFileName(archivePath),
            ArchiveSha256 = verification.Sha256,
            ArchiveLength = verification.ArchiveLength,
            FileCount = verification.FileCount,
            UncompressedBytes = verification.UncompressedBytes,
            Verified = verification.Verified,
            VerifiedAtLocal = verification.VerifiedAt,
            SaveRequestedBeforeSnapshot = saveFirst
        };

        var path = ManifestPath(archivePath);
        var temporary = path + ".partial";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, overwrite: true);
    }

    private static void TryDeleteManagedBackup(string archivePath)
    {
        TryDeleteFile(archivePath);
        TryDeleteFile(ManifestPath(archivePath));
    }

    private sealed class BackupManifest
    {
        public int FormatVersion { get; set; }
        public string ArchiveFile { get; set; } = "";
        public string ArchiveSha256 { get; set; } = "";
        public long ArchiveLength { get; set; }
        public int FileCount { get; set; }
        public long UncompressedBytes { get; set; }
        public bool Verified { get; set; }
        public DateTime? VerifiedAtLocal { get; set; }
        public bool SaveRequestedBeforeSnapshot { get; set; }
    }

    private readonly record struct ArchiveValidationDetails(
        int FileCount,
        long UncompressedBytes);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch { }
    }

    private readonly record struct FileSignature(long Length, long LastWriteTicks);
}

public sealed record BackupVerificationResult(
    bool Verified,
    string Summary,
    string Sha256,
    long ArchiveLength,
    int FileCount,
    long UncompressedBytes,
    DateTime VerifiedAt);
