using System.Security.Cryptography;
using System.Text.Json;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.7.75.0 "Delete Player Completely": removes a player's own save file
// (Players/{PlayerUId}.sav), then -- as a genuinely separate, independent operation, not nested
// inside this one -- reuses HeadlessGuildOwnershipService's own already-proven RemoveBrokenMember
// operation to clean up their now-dangling guild reference, if any. Also clears their
// HeadlessPlayerRegistryService history.
//
// Deliberately two independent operations rather than one combined transaction: nesting a second
// full Preview/Apply lifecycle (with its own OperationCoordinator "world-mutation" acquisition)
// inside this one's own held lock would deadlock against a non-reentrant coordinator. Running them
// sequentially -- delete the file and fully release this operation's own lock, THEN separately
// invoke guild cleanup, which acquires its own lock fresh -- is simpler and exactly matches what a
// human admin would naturally do if they noticed the broken reference afterward, using the same
// code path RemoveBrokenMember already uses for that. Base ownership is deliberately NOT
// special-cased here: a base whose owning guild becomes ownerless as a side effect already surfaces
// through this codebase's existing orphan-detection/Base Recovery flow, so nothing new is needed.
public sealed class HeadlessPlayerDeletionService(
    IServerPathProfile paths,
    IServerLifecycleService lifecycle,
    HeadlessBackupService backups,
    HeadlessActivityLogService activity,
    HeadlessPlayerGuildExplorerService explorer,
    HeadlessPlayerRegistryService registry,
    HeadlessGuildOwnershipService guildOwnership,
    IOperationCoordinator coordinator,
    ServerProfileId profile)
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(15);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, PendingDeletion> pending = new(StringComparer.Ordinal);

    public async Task<HeadlessPlayerDeletionPreview> PreviewAsync(string playerId, CancellationToken cancellationToken)
    {
        var findings = new List<string>();
        var snapshot = await explorer.ExploreAsync(cancellationToken);
        var player = snapshot.Players.FirstOrDefault(p => string.Equals(p.PlayerId, playerId, StringComparison.OrdinalIgnoreCase));

        if (!snapshot.Available || string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath))
            findings.Add("No active world was discovered.");
        if (player is null)
            findings.Add($"Player {playerId} was not found.");
        else
        {
            if (player.Online)
                findings.Add("This player is currently online. Kick or wait for them to disconnect before deleting their save.");
            if (!player.SaveExists)
                findings.Add("This player has no save file on disk -- there is nothing to delete.");
            if (!string.IsNullOrWhiteSpace(player.GuildId))
                findings.Add($"Player is a member of guild {player.GuildName} ({player.GuildId}) -- their broken membership reference will be cleaned up as a separate follow-up step after deletion.");
        }

        var status = await lifecycle.GetStatusAsync(cancellationToken);
        if (status.NativeProcessId.HasValue || status.Ready)
            findings.Add("PalServer must be stopped before deleting a player save.");

        var savePath = player is not null && !string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath)
            ? Path.Combine(snapshot.ActiveWorldPath, "Players", $"{Normalize(playerId)}.sav")
            : null;

        var canApply = player is { SaveExists: true, Online: false } && savePath is not null && File.Exists(savePath) &&
            !status.NativeProcessId.HasValue && !status.Ready;

        var token = "";
        var expires = DateTimeOffset.UtcNow.Add(PreviewLifetime);
        if (canApply)
        {
            findings.Add($"Ready: {player!.PlayerName}'s save file ({new FileInfo(savePath!).Length:N0} bytes) will be permanently deleted, and their registry history cleared.");
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            lock (pending)
                pending[token] = new PendingDeletion(playerId, player.PlayerName, player.GuildId, savePath!, HashFile(savePath!), expires);
        }

        activity.Record(canApply ? "Information" : "Warning", "Player Deletion",
            $"Previewed deletion of {playerId}", $"canApply={canApply}");

        return new HeadlessPlayerDeletionPreview(canApply, token, playerId, player?.PlayerName ?? playerId, findings, expires);
    }

    public async Task<HeadlessPlayerDeletionResult> ApplyAsync(string previewToken, bool confirmed, CancellationToken cancellationToken)
    {
        if (!confirmed)
            return HeadlessPlayerDeletionResult.Failure("Apply requires explicit confirmation.");
        if (!await gate.WaitAsync(0, cancellationToken))
            return HeadlessPlayerDeletionResult.Failure("Another player deletion is already running.");

        PendingDeletion? op = null;
        HeadlessWorldTransactionJournal? journal = null;
        OperationHandle? operation = null;
        try
        {
            lock (pending)
            {
                if (pending.TryGetValue(previewToken, out var found)) op = found;
                if (op is not null) pending.Remove(previewToken);
            }
            if (op is null || op.ExpiresUtc < DateTimeOffset.UtcNow)
                return HeadlessPlayerDeletionResult.Failure("The preview token is missing or expired. Preview the deletion again.");

            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.NativeProcessId.HasValue || status.Ready)
                return HeadlessPlayerDeletionResult.Failure("Stop PalServer before deleting a player save.");

            if (!File.Exists(op.SavePath) || !HashFile(op.SavePath).Equals(op.SourceHash, StringComparison.OrdinalIgnoreCase))
                return HeadlessPlayerDeletionResult.Failure("The player's save file changed since the preview. Preview the deletion again.");

            operation = await coordinator.BeginAsync(profile, "player-deletion",
                "HeadlessPlayerDeletionService", ["world-mutation"], cancellationToken);

            var id = Guid.NewGuid().ToString("N");
            journal = NewJournal(id, op.PlayerId);
            Advance(journal, "PreviewAccepted", $"The single-use preview token for player {op.PlayerId} was accepted.");

            var safety = await backups.CreateAsync(BackupClass.Safety, cancellationToken);
            if (!safety.Success || string.IsNullOrWhiteSpace(safety.FileName))
                throw new InvalidOperationException("Fresh safety backup failed: " + safety.Message);
            journal.SafetyBackup = safety.FileName;
            Advance(journal, "SafetyBackupCreated", $"Fresh safety backup: {safety.FileName}");

            File.Delete(op.SavePath);
            if (File.Exists(op.SavePath))
                throw new InvalidOperationException("The save file still exists after deletion was attempted.");
            Advance(journal, "Committed", $"Deleted {op.SavePath}.");

            registry.Forget(op.PlayerId);
            Advance(journal, "RegistryCleared", $"Cleared registry history for {op.PlayerId}.");

            activity.Record("Information", "Player Deletion", $"Deleted player {op.PlayerName} ({op.PlayerId})",
                $"backup={safety.FileName}; hadGuild={!string.IsNullOrWhiteSpace(op.GuildId)}");
            coordinator.Complete(operation.Id, $"Deleted {op.PlayerName}'s save and registry history.");
            // Complete() only marks the record done -- it does NOT release the "world-mutation"
            // lock; only Dispose() does (see OperationHandle). Caught live: the guild cleanup
            // below tried to acquire that same key while this operation still technically held
            // it, and was correctly rejected as a conflict. Dispose explicitly here, before that
            // follow-up call, rather than waiting for the `finally` block -- Dispose is idempotent
            // (guarded by its own `disposed` flag) so the `finally`'s own operation?.Dispose() below
            // remains safe to keep as a catch-all for every other exit path.
            operation.Dispose();

            // Genuinely separate follow-up operation -- see the class-level comment for why this
            // isn't nested inside the transaction above. Best-effort: a failure here leaves a
            // real, already-disclosed dangling guild reference that RemoveBrokenMember can still
            // clean up later exactly as it always could; it must never make the deletion itself
            // look like it failed, since the player's save really is gone at this point.
            string? guildCleanupDetail = null;
            if (!string.IsNullOrWhiteSpace(op.GuildId))
            {
                try
                {
                    var guildPreview = await guildOwnership.PreviewAsync("remove-broken-member", op.GuildId, op.PlayerId, cancellationToken);
                    if (guildPreview.CanApply)
                    {
                        var guildResult = await guildOwnership.ApplyAsync(
                            new HeadlessGuildOwnershipApplyRequest(guildPreview.PreviewToken, true), cancellationToken);
                        guildCleanupDetail = guildResult.Success
                            ? $"Broken guild membership reference also cleaned up (guild {op.GuildId})."
                            : $"Save deleted, but the follow-up guild-reference cleanup failed: {guildResult.Message}. Use Guild Ownership Operations' Remove Broken Member manually.";
                    }
                    else
                    {
                        guildCleanupDetail = "Save deleted. The guild-reference cleanup could not run automatically -- use Guild Ownership Operations' Remove Broken Member manually if needed.";
                    }
                }
                catch (Exception ex)
                {
                    guildCleanupDetail = $"Save deleted, but the follow-up guild-reference cleanup threw: {ex.Message}. Use Guild Ownership Operations' Remove Broken Member manually.";
                }
            }

            return new HeadlessPlayerDeletionResult(true, id, "Committed", safety.FileName, journal.JournalPath,
                guildCleanupDetail ?? $"{op.PlayerName}'s save and registry history were deleted.");
        }
        catch (Exception ex)
        {
            if (journal is not null)
            {
                Advance(journal, "Failed", ex.Message);
                activity.Record("Warning", "Player Deletion", "Deletion failed", $"error={ex.GetType().Name}");
            }
            if (operation is not null) coordinator.Fail(operation.Id, ex.Message);
            return new HeadlessPlayerDeletionResult(false, journal?.TransactionId, "Failed", journal?.SafetyBackup, journal?.JournalPath, ex.Message);
        }
        finally
        {
            operation?.Dispose();
            gate.Release();
        }
    }

    private HeadlessWorldTransactionJournal NewJournal(string id, string playerId)
    {
        var path = Path.Combine(paths.ManagerRuntimeRoot, "world-transactions", "journals", $"transaction-{id}.json");
        return new HeadlessWorldTransactionJournal
        {
            TransactionId = id, Mode = "player-deletion", WorldId = playerId,
            State = "Created", CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow, JournalPath = path
        };
    }

    private static void Advance(HeadlessWorldTransactionJournal journal, string state, string detail)
    {
        journal.State = state; journal.UpdatedUtc = DateTimeOffset.UtcNow;
        journal.Stages.Add(new(state, detail, journal.UpdatedUtc));
        Directory.CreateDirectory(Path.GetDirectoryName(journal.JournalPath)!);
        var temp = journal.JournalPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(journal, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, journal.JournalPath, true);
    }

    private static string Normalize(string value) => value.Trim();

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record PendingDeletion(string PlayerId, string PlayerName, string GuildId, string SavePath, string SourceHash, DateTimeOffset ExpiresUtc);
}

public sealed record HeadlessPlayerDeletionPreview(bool CanApply, string PreviewToken, string PlayerId, string PlayerName,
    IReadOnlyList<string> Findings, DateTimeOffset ExpiresUtc);

public sealed record HeadlessPlayerDeletionResult(bool Success, string? TransactionId, string State, string? SafetyBackup, string? JournalPath, string Message)
{
    public static HeadlessPlayerDeletionResult Failure(string message) => new(false, null, "Rejected", null, null, message);
}

public sealed record HeadlessPlayerDeletionApplyRequest(string PreviewToken, bool Confirmed);
