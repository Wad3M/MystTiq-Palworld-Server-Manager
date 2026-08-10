using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Creates a stable read-only temporary snapshot of a live Palworld save.
/// PalServer may briefly hold or replace Level.sav while saving; callers should
/// inspect/decode the snapshot rather than opening the active file directly.
/// </summary>
public sealed class SafeWorldSaveSnapshotService
{
    private const int DefaultAttempts = 8;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(125);

    public WorldSaveSnapshot CreateSnapshot(string sourcePath, int maxAttempts = DefaultAttempts)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("A world-save path is required.", nameof(sourcePath));
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Palworld save was not found.", sourcePath);

        maxAttempts = Math.Max(1, maxAttempts);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "MystTiq",
                "WorldSaveSnapshots",
                Guid.NewGuid().ToString("N"));
            var snapshotPath = Path.Combine(tempDirectory, Path.GetFileName(sourcePath));

            try
            {
                Directory.CreateDirectory(tempDirectory);

                var before = new FileInfo(sourcePath);
                var beforeLength = before.Length;
                var beforeWriteUtc = before.LastWriteTimeUtc;

                using (var source = new FileStream(
                           sourcePath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete,
                           128 * 1024,
                           FileOptions.SequentialScan))
                using (var destination = new FileStream(
                           snapshotPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.Read,
                           128 * 1024,
                           FileOptions.SequentialScan))
                {
                    source.CopyTo(destination);
                    destination.Flush(flushToDisk: true);
                }

                var after = new FileInfo(sourcePath);
                var snapshot = new FileInfo(snapshotPath);

                var sourceStable =
                    beforeLength == after.Length &&
                    beforeWriteUtc == after.LastWriteTimeUtc &&
                    snapshot.Length == beforeLength &&
                    snapshot.Length > 0;

                if (!sourceStable)
                {
                    TryDeleteDirectory(tempDirectory);
                    lastError = new IOException(
                        "The world save changed while MystTiq was creating a read snapshot.");
                }
                else
                {
                    return new WorldSaveSnapshot(
                        sourcePath,
                        snapshotPath,
                        after.LastWriteTimeUtc,
                        after.Length,
                        attempt,
                        attempt > 1);
                }
            }
            catch (IOException ex)
            {
                lastError = ex;
                TryDeleteDirectory(tempDirectory);
            }
            catch (UnauthorizedAccessException ex)
            {
                lastError = ex;
                TryDeleteDirectory(tempDirectory);
            }

            if (attempt < maxAttempts)
                System.Threading.Thread.Sleep(RetryDelay);
        }

        throw new IOException(
            "The active world save is currently being written by PalServer. MystTiq could not obtain a stable read snapshot yet. Wait a moment and refresh the World Inspector.",
            lastError);
    }

    public void Release(WorldSaveSnapshot? snapshot)
    {
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.SnapshotPath))
            return;

        var directory = Path.GetDirectoryName(snapshot.SnapshotPath);
        if (!string.IsNullOrWhiteSpace(directory))
            TryDeleteDirectory(directory);
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // Temporary snapshots are best-effort cleanup and contain only copied save data.
        }
    }
}
