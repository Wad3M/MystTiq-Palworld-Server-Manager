using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.7.75.0 "Copy Player": clones one player's build/progress onto another player's own save,
// while the destination keeps its own identity. Confirmed live against a real production player
// save (read-only decode via the existing PlM/Oodle converter -- the exact same generic tool this
// codebase already uses for Level.sav, just pointed at a per-player Players/{uid}.sav file for the
// first time) that a player's own save is a flat GVAS structure at properties.SaveData.value, with
// a clean split between identity/state fields and progress fields:
//   Copied from source onto destination: PlayerCharacterMakeData (appearance), InventoryInfo,
//   UnlockedRecipeTechnologyNames, RecordData, SkinInventoryInfo, OrderedQuestArray_FullRelease.
//   Left as the destination's own, deliberately NOT overwritten: PlayerUId, IndividualId,
//   PlayerPlatform, LastOnlineDateTime (identity/session fields -- copying these would make two
//   save records claim the same identity), LastTransform (the destination shouldn't teleport to
//   the source's last position), and OtomoCharacterContainerId/PalStorageContainerId (these are
//   references to separate pal-party/storage container records, most likely stored elsewhere --
//   blindly copying just the ID could make both players point at the same container, which this
//   codebase has no way to verify is safe without decoding that separate structure too; excluded
//   rather than risking a shared-container collision).
// Deliberately NOT included in this version: the player's own in-world "character body" (Level,
// Talent_*, Rank -- these live in Level.sav's CharacterSaveParameterMap, a different file and a
// different decode target, per HeadlessPalEditService) and pal-party/storage contents themselves.
// A real, disclosed scope decision, not an oversight -- see the architecture doc.
public sealed class HeadlessPlayerCopyService(
    IServerPathProfile paths,
    IServerLifecycleService lifecycle,
    HeadlessBackupService backups,
    HeadlessActivityLogService activity,
    HeadlessPlayerGuildExplorerService explorer,
    HeadlessSaveCodecService codec,
    IOperationCoordinator coordinator,
    ServerProfileId profile)
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(15);

    // Fields copied verbatim from the source player's SaveData onto the destination's.
    private static readonly string[] CopiedFields =
    [
        "PlayerCharacterMakeData", "InventoryInfo", "UnlockedRecipeTechnologyNames",
        "RecordData", "SkinInventoryInfo", "OrderedQuestArray_FullRelease"
    ];

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, PendingCopy> pending = new(StringComparer.Ordinal);

    public async Task<HeadlessPlayerCopyPreview> PreviewAsync(string sourcePlayerId, string destinationPlayerId, CancellationToken cancellationToken)
    {
        var findings = new List<string>();
        var snapshot = await explorer.ExploreAsync(cancellationToken);
        var source = snapshot.Players.FirstOrDefault(p => string.Equals(p.PlayerId, sourcePlayerId, StringComparison.OrdinalIgnoreCase));
        var destination = snapshot.Players.FirstOrDefault(p => string.Equals(p.PlayerId, destinationPlayerId, StringComparison.OrdinalIgnoreCase));

        if (!snapshot.Available || string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath))
            findings.Add("No active world was discovered.");
        if (string.Equals(sourcePlayerId, destinationPlayerId, StringComparison.OrdinalIgnoreCase))
            findings.Add("Source and destination must be different players.");
        if (source is null) findings.Add($"Source player {sourcePlayerId} was not found.");
        else if (!source.SaveExists) findings.Add($"Source player {source.PlayerName} has no save file.");
        else if (source.Online) findings.Add($"Source player {source.PlayerName} is currently online -- wait for them to disconnect.");
        if (destination is null) findings.Add($"Destination player {destinationPlayerId} was not found.");
        else if (!destination.SaveExists) findings.Add($"Destination player {destination.PlayerName} has no save file.");
        else if (destination.Online) findings.Add($"Destination player {destination.PlayerName} is currently online -- wait for them to disconnect.");

        var status = await lifecycle.GetStatusAsync(cancellationToken);
        if (status.NativeProcessId.HasValue || status.Ready)
            findings.Add("PalServer must be stopped before copying player data.");

        string? sourcePath = null, destinationPath = null;
        if (source is { SaveExists: true } && destination is { SaveExists: true } && !string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath))
        {
            sourcePath = Path.Combine(snapshot.ActiveWorldPath, "Players", $"{sourcePlayerId.Trim()}.sav");
            destinationPath = Path.Combine(snapshot.ActiveWorldPath, "Players", $"{destinationPlayerId.Trim()}.sav");
        }

        (HeadlessSaveConverterMatch? converterMatch, string converterDetail) = (null, "No active world.");
        if (destinationPath is not null)
            (converterMatch, converterDetail) = await codec.ResolveConverterAsync(destinationPath, cancellationToken);
        if (sourcePath is not null && destinationPath is not null && converterMatch is null)
            findings.Add("Save codec unavailable: " + converterDetail);

        var canApply = findings.Count == 0 && sourcePath is not null && destinationPath is not null &&
            File.Exists(sourcePath) && File.Exists(destinationPath) && converterMatch is not null &&
            !status.NativeProcessId.HasValue && !status.Ready;

        var token = "";
        var expires = DateTimeOffset.UtcNow.Add(PreviewLifetime);
        if (canApply)
        {
            findings.Add($"Will copy from {source!.PlayerName}: {string.Join(", ", CopiedFields)}.");
            findings.Add($"{destination!.PlayerName} keeps their own identity, position, session history, and pal party/storage references unchanged.");
            findings.Add("Not included in this version: in-world character level/stats (a separate Level.sav decode target) and pal-party/storage contents themselves.");
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            lock (pending)
                pending[token] = new PendingCopy(sourcePlayerId, source.PlayerName, destinationPlayerId, destination.PlayerName,
                    sourcePath!, destinationPath!, HashFile(destinationPath!), expires);
        }

        activity.Record(canApply ? "Information" : "Warning", "Player Copy",
            $"Previewed copy {sourcePlayerId} -> {destinationPlayerId}", $"canApply={canApply}");

        return new HeadlessPlayerCopyPreview(canApply, token, sourcePlayerId, source?.PlayerName ?? sourcePlayerId,
            destinationPlayerId, destination?.PlayerName ?? destinationPlayerId, findings, expires);
    }

    public async Task<HeadlessPlayerCopyResult> ApplyAsync(string previewToken, bool confirmed, CancellationToken cancellationToken)
    {
        if (!confirmed)
            return HeadlessPlayerCopyResult.Failure("Apply requires explicit confirmation.");
        if (!await gate.WaitAsync(0, cancellationToken))
            return HeadlessPlayerCopyResult.Failure("Another player copy is already running.");

        PendingCopy? op = null;
        HeadlessWorldTransactionJournal? journal = null;
        OperationHandle? operation = null;
        string? working = null;
        string? originalCopy = null;
        try
        {
            lock (pending)
            {
                if (pending.TryGetValue(previewToken, out var found)) op = found;
                if (op is not null) pending.Remove(previewToken);
            }
            if (op is null || op.ExpiresUtc < DateTimeOffset.UtcNow)
                return HeadlessPlayerCopyResult.Failure("The preview token is missing or expired. Preview the copy again.");

            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.NativeProcessId.HasValue || status.Ready)
                return HeadlessPlayerCopyResult.Failure("Stop PalServer before copying player data.");

            if (!File.Exists(op.DestinationPath) || !HashFile(op.DestinationPath).Equals(op.DestinationHash, StringComparison.OrdinalIgnoreCase))
                return HeadlessPlayerCopyResult.Failure("The destination player's save changed since the preview. Preview the copy again.");
            if (!File.Exists(op.SourcePath))
                return HeadlessPlayerCopyResult.Failure("The source player's save no longer exists. Preview the copy again.");

            operation = await coordinator.BeginAsync(profile, "player-copy",
                "HeadlessPlayerCopyService", ["world-mutation"], cancellationToken);

            var id = Guid.NewGuid().ToString("N");
            journal = NewJournal(id, op.SourcePlayerId, op.DestinationPlayerId);
            Advance(journal, "PreviewAccepted", $"The single-use preview token for {op.SourcePlayerId} -> {op.DestinationPlayerId} was accepted.");

            var safety = await backups.CreateAsync(BackupClass.Safety, cancellationToken);
            if (!safety.Success || string.IsNullOrWhiteSpace(safety.FileName))
                throw new InvalidOperationException("Fresh safety backup failed: " + safety.Message);
            journal.SafetyBackup = safety.FileName;
            Advance(journal, "SafetyBackupCreated", $"Fresh safety backup: {safety.FileName}");

            var (converterMatch, converterDetail) = await codec.ResolveConverterAsync(op.DestinationPath, cancellationToken);
            if (converterMatch is null)
                throw new InvalidOperationException("Save codec unavailable: " + converterDetail);
            Advance(journal, "ConverterResolved", $"Using the {converterMatch.ContainerKind} converter for this save.");

            working = Path.Combine(paths.ManagerRuntimeRoot, "player-copy", id);
            Directory.CreateDirectory(working);
            originalCopy = Path.Combine(working, "Destination.original.sav");
            File.Copy(op.DestinationPath, originalCopy, true);

            var sourceDecoded = await codec.DecodeAsync(converterMatch, op.SourcePath, Path.Combine(working, "Source.decoded.json"), cancellationToken);
            var sourceRoot = JsonNode.Parse(File.ReadAllText(sourceDecoded)) ?? throw new InvalidDataException("Decoded source save JSON is empty.");
            var sourceSaveData = GetSaveDataValue(sourceRoot) ?? throw new InvalidDataException("Source save has no properties.SaveData.value.");

            var destinationInput = Path.Combine(working, "Destination.input.sav");
            File.Copy(op.DestinationPath, destinationInput, true);
            var destinationDecoded = await codec.DecodeAsync(converterMatch, destinationInput, Path.Combine(working, "Destination.decoded.json"), cancellationToken);
            var destinationRoot = JsonNode.Parse(File.ReadAllText(destinationDecoded)) ?? throw new InvalidDataException("Decoded destination save JSON is empty.");
            var destinationSaveData = GetSaveDataValue(destinationRoot) ?? throw new InvalidDataException("Destination save has no properties.SaveData.value.");

            var copiedCount = 0;
            foreach (var field in CopiedFields)
            {
                if (sourceSaveData[field] is { } value)
                {
                    destinationSaveData[field] = value.DeepClone();
                    copiedCount++;
                }
            }
            if (copiedCount == 0)
                throw new InvalidDataException("None of the expected fields were found in the source save -- the save structure may have changed.");
            Advance(journal, "Staged", $"Copied {copiedCount} field(s) from {op.SourcePlayerName} onto {op.DestinationPlayerName}'s decoded save.");

            var mutatedJson = Path.Combine(working, "Destination.mutated.json");
            File.WriteAllText(mutatedJson, destinationRoot.ToJsonString());
            var staged = await codec.EncodeAsync(converterMatch, mutatedJson, Path.Combine(working, "Destination.repaired.sav"), cancellationToken);
            if (!File.Exists(staged) || new FileInfo(staged).Length < 64)
                throw new InvalidDataException("Encoded destination save failed size validation.");
            Advance(journal, "Encoded", "Modified JSON encoded to a staged save and passed size validation.");

            var verifyJsonPath = await codec.DecodeAsync(converterMatch, staged, Path.Combine(working, "Destination.verify.json"), cancellationToken);
            var verifyRoot = JsonNode.Parse(File.ReadAllText(verifyJsonPath)) ?? throw new InvalidDataException("Verification decode was empty.");
            var verifySaveData = GetSaveDataValue(verifyRoot) ?? throw new InvalidDataException("Verification save has no properties.SaveData.value.");
            foreach (var field in CopiedFields)
            {
                if (sourceSaveData[field] is null) continue;
                if (verifySaveData[field]?.ToJsonString() != sourceSaveData[field]!.ToJsonString())
                    throw new InvalidDataException($"Verification did not find the expected {field} in the repaired save.");
            }
            Advance(journal, "Verified", "Staged save independently decoded and every copied field was verified byte-for-byte.");

            var replacement = op.DestinationPath + $".mysttiq-player-copy-{id}.tmp";
            File.Copy(staged, replacement, true);
            File.Replace(replacement, op.DestinationPath, null, true);
            if (!File.Exists(op.DestinationPath) || new FileInfo(op.DestinationPath).Length < 64)
                throw new InvalidDataException("The destination save failed final validation after replacement.");

            Advance(journal, "Committed", $"Verified save committed atomically. Result hash: {HashFile(op.DestinationPath)}");
            activity.Record("Information", "Player Copy", $"Copied {op.SourcePlayerName} onto {op.DestinationPlayerName}",
                $"source={op.SourcePlayerId}; destination={op.DestinationPlayerId}; fields={copiedCount}; backup={safety.FileName}");
            coordinator.Complete(operation.Id, $"Copied {copiedCount} field(s) from {op.SourcePlayerName} onto {op.DestinationPlayerName}.");
            return new HeadlessPlayerCopyResult(true, id, "Committed", safety.FileName, false, journal.JournalPath,
                $"{op.DestinationPlayerName} now has {op.SourcePlayerName}'s inventory, recipes, records, skins, and quest progress.");
        }
        catch (Exception ex)
        {
            var rolledBack = false;
            try
            {
                if (op is not null && originalCopy is not null && File.Exists(originalCopy))
                {
                    File.Copy(originalCopy, op.DestinationPath, true);
                    rolledBack = HashFile(op.DestinationPath).Equals(op.DestinationHash, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { rolledBack = false; }
            if (journal is not null)
            {
                journal.RolledBack = rolledBack;
                Advance(journal, rolledBack ? "RolledBack" : "Failed", ex.Message);
                activity.Record("Warning", "Player Copy", "Copy failed", $"rolledBack={rolledBack}; error={ex.GetType().Name}");
            }
            if (operation is not null) coordinator.Fail(operation.Id, ex.Message, rolledBack);
            return new HeadlessPlayerCopyResult(false, journal?.TransactionId, rolledBack ? "RolledBack" : "Failed",
                journal?.SafetyBackup, rolledBack, journal?.JournalPath, ex.Message);
        }
        finally
        {
            operation?.Dispose();
            if (working is not null) try { Directory.Delete(working, true); } catch { }
            gate.Release();
        }
    }

    private static JsonObject? GetSaveDataValue(JsonNode root) =>
        root["properties"]?["SaveData"]?["value"] as JsonObject;

    private HeadlessWorldTransactionJournal NewJournal(string id, string sourcePlayerId, string destinationPlayerId)
    {
        var path = Path.Combine(paths.ManagerRuntimeRoot, "world-transactions", "journals", $"transaction-{id}.json");
        return new HeadlessWorldTransactionJournal
        {
            TransactionId = id, Mode = "player-copy", WorldId = $"{sourcePlayerId}->{destinationPlayerId}",
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

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record PendingCopy(string SourcePlayerId, string SourcePlayerName, string DestinationPlayerId, string DestinationPlayerName,
        string SourcePath, string DestinationPath, string DestinationHash, DateTimeOffset ExpiresUtc);
}

public sealed record HeadlessPlayerCopyPreview(bool CanApply, string PreviewToken, string SourcePlayerId, string SourcePlayerName,
    string DestinationPlayerId, string DestinationPlayerName, IReadOnlyList<string> Findings, DateTimeOffset ExpiresUtc);

public sealed record HeadlessPlayerCopyResult(bool Success, string? TransactionId, string State, string? SafetyBackup, bool RolledBack, string? JournalPath, string Message)
{
    public static HeadlessPlayerCopyResult Failure(string message) => new(false, null, "Rejected", null, false, null, message);
}

public sealed record HeadlessPlayerCopyPreviewRequest(string SourcePlayerId, string DestinationPlayerId);
public sealed record HeadlessPlayerCopyApplyRequest(string PreviewToken, bool Confirmed);
