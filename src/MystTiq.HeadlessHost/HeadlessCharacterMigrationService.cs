using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MystTiq.Core.Migration;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.6.3.0 character/account migration (e.g. Xbox -> Steam): repoints a source player's guild
// membership/leadership to an already-existing destination player identity within the same live
// world. Structurally mirrors HeadlessGuildOwnershipService's Preview -> Safety Backup ->
// Server-side Transaction -> Validate -> Journal/Audit shape exactly (same journal store, same
// OperationCoordinator "world-mutation" key, same atomic-replace/rollback/re-verify discipline).
//
// Scope decision (see docs/architecture/v0.6.3.0-*.md): this ships real, working identity/
// guild-reference migration only. Full level/XP/inventory/equipped-item/Pal transplant between
// source and destination character needs decoding an individual player .sav file's internal
// structure, which nothing in this codebase does today (HeadlessSaveCodecService/
// HeadlessGuildOwnershipService only ever decode Level.sav) -- that is explicitly deferred, not
// silently dropped.
public sealed class HeadlessCharacterMigrationService
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(15);

    private readonly IServerPathProfile paths;
    private readonly IServerLifecycleService lifecycle;
    private readonly HeadlessBackupService backups;
    private readonly HeadlessActivityLogService activity;
    private readonly HeadlessPlayerGuildExplorerService explorer;
    private readonly HeadlessSaveCodecService codec;
    private readonly IOperationCoordinator coordinator;
    private readonly ServerProfileId profile;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, PendingMigration> pending = new(StringComparer.Ordinal);

    public HeadlessCharacterMigrationService(IServerPathProfile paths, IServerLifecycleService lifecycle,
        HeadlessBackupService backups, HeadlessActivityLogService activity, HeadlessPlayerGuildExplorerService explorer,
        HeadlessSaveCodecService codec, IOperationCoordinator coordinator, ServerProfileId profile)
    {
        this.paths = paths;
        this.lifecycle = lifecycle;
        this.backups = backups;
        this.activity = activity;
        this.explorer = explorer;
        this.codec = codec;
        this.coordinator = coordinator;
        this.profile = profile;
    }

    public async Task<HeadlessCharacterMigrationPreview> PreviewAsync(string sourcePlayerId, string destinationPlayerId, CancellationToken cancellationToken)
    {
        var snapshot = await explorer.ExploreAsync(cancellationToken);
        // Blockers actually gate canApply; findings is blockers plus informational-only lines
        // (like the identity-match description) returned to the caller for display. Conflating
        // the two would make canApply false whenever there's ANY findings text, even purely
        // informational -- confirmed as a real bug via live testing against a real save.
        var blockers = new List<string>();
        var findings = new List<string>();
        var source = snapshot.Players.FirstOrDefault(p => string.Equals(p.PlayerId, sourcePlayerId, StringComparison.OrdinalIgnoreCase));
        var destination = snapshot.Players.FirstOrDefault(p => string.Equals(p.PlayerId, destinationPlayerId, StringComparison.OrdinalIgnoreCase));
        string? levelSavePath = string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath) ? null : Path.Combine(snapshot.ActiveWorldPath, "Level.sav");
        var (converterMatch, converterDetail) = levelSavePath is null
            ? (null, "No active world.")
            : await codec.ResolveConverterAsync(levelSavePath, cancellationToken);

        if (!snapshot.Available || string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath))
            blockers.Add("No active world with Level.sav was discovered.");
        if (!snapshot.SemanticAvailable)
            blockers.Add("Decoded GroupSaveDataMap evidence is unavailable; guild identity cannot be verified.");
        if (string.Equals(sourcePlayerId, destinationPlayerId, StringComparison.OrdinalIgnoreCase))
            blockers.Add("Source and destination must be different players.");
        if (source is null)
            blockers.Add($"Source player {sourcePlayerId} was not found.");
        if (destination is null)
            blockers.Add($"Destination player {destinationPlayerId} was not found.");

        PlayerMappingAssessment? assessment = null;
        if (source is not null && destination is not null)
        {
            assessment = PlayerMappingEngine.Assess(
                new PlayerMappingIdentity(source.PlayerId, source.PlayerName, source.Platform),
                new PlayerMappingIdentity(destination.PlayerId, destination.PlayerName, destination.Platform));
            findings.Add($"Identity match: {assessment.Method} (confidence {assessment.Confidence:P0}) -- {assessment.Explanation}");

            if (string.IsNullOrWhiteSpace(source.GuildId))
                blockers.Add("Source player has no guild membership to migrate; there is nothing to change.");
            if (!string.IsNullOrWhiteSpace(destination.GuildId))
                blockers.Add($"Destination player already belongs to guild {destination.GuildName}; resolve this conflict manually before migrating.");
        }
        if (converterMatch is null)
            blockers.Add("Save codec unavailable: " + converterDetail);

        findings.InsertRange(0, blockers);
        var canApply = blockers.Count == 0 && levelSavePath is not null && File.Exists(levelSavePath) &&
            source is not null && destination is not null && !string.IsNullOrWhiteSpace(source.GuildId);
        var sourceHash = canApply ? HashFile(levelSavePath!) : "";
        var expires = DateTimeOffset.UtcNow.Add(PreviewLifetime);

        if (canApply)
            findings.Add($"Ready: {destination!.PlayerName} ({destination.PlayerId}) will take over {source!.PlayerName}'s guild membership in {source.GuildName}" +
                (string.Equals(source.Role, "Leader", StringComparison.OrdinalIgnoreCase) ? " as leader." : "."));

        var token = "";
        if (canApply)
        {
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            lock (pending)
                pending[token] = new PendingMigration(sourcePlayerId, source!.PlayerName, destinationPlayerId, destination!.PlayerName,
                    source.GuildId, levelSavePath!, sourceHash, expires);
        }

        activity.Record(canApply ? "Information" : "Warning", "Character Migration",
            $"Previewed migration {sourcePlayerId} -> {destinationPlayerId}", $"canApply={canApply}");

        return new HeadlessCharacterMigrationPreview(canApply, token, sourcePlayerId, source?.PlayerName ?? sourcePlayerId,
            destinationPlayerId, destination?.PlayerName ?? destinationPlayerId, assessment?.Method.ToString(), assessment?.Confidence ?? 0,
            findings, expires);
    }

    public async Task<HeadlessCharacterMigrationResult> ApplyAsync(HeadlessCharacterMigrationApplyRequest request, CancellationToken cancellationToken)
    {
        if (!request.Confirmed)
            return HeadlessCharacterMigrationResult.Failure("Apply requires explicit confirmation.");
        if (!await gate.WaitAsync(0, cancellationToken))
            return HeadlessCharacterMigrationResult.Failure("Another character migration is already running.");

        PendingMigration? op = null;
        HeadlessWorldTransactionJournal? journal = null;
        OperationHandle? operation = null;
        string? working = null;
        string? originalCopy = null;
        try
        {
            lock (pending)
            {
                if (pending.TryGetValue(request.PreviewToken, out var found)) op = found;
                if (op is not null) pending.Remove(request.PreviewToken);
            }
            if (op is null || op.ExpiresUtc < DateTimeOffset.UtcNow)
                return HeadlessCharacterMigrationResult.Failure("The preview token is missing or expired. Preview the migration again.");

            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.NativeProcessId.HasValue || status.Ready)
                return HeadlessCharacterMigrationResult.Failure("Stop PalServer before applying a character migration.");

            if (!File.Exists(op.LevelSavePath) || !HashFile(op.LevelSavePath).Equals(op.SourceHash, StringComparison.OrdinalIgnoreCase))
                return HeadlessCharacterMigrationResult.Failure("Level.sav changed since the preview. Preview the migration again.");

            operation = await coordinator.BeginAsync(profile, "character-migration",
                "HeadlessCharacterMigrationService", ["world-mutation"], cancellationToken);

            var id = Guid.NewGuid().ToString("N");
            journal = NewJournal(id, op.GuildId);
            Advance(journal, "PreviewAccepted", $"The single-use preview token for {op.SourcePlayerId} -> {op.DestinationPlayerId} was accepted.");

            var safety = await backups.CreateAsync(BackupClass.Safety, cancellationToken);
            if (!safety.Success || string.IsNullOrWhiteSpace(safety.FileName))
                throw new InvalidOperationException("Fresh safety backup failed: " + safety.Message);
            journal.SafetyBackup = safety.FileName;
            Advance(journal, "SafetyBackupCreated", $"Fresh safety backup: {safety.FileName}");

            var (converterMatch, converterDetail) = await codec.ResolveConverterAsync(op.LevelSavePath, cancellationToken);
            if (converterMatch is null)
                throw new InvalidOperationException("Save codec unavailable: " + converterDetail);
            Advance(journal, "ConverterResolved", $"Using the {converterMatch.ContainerKind} converter for this save.");

            working = Path.Combine(paths.ManagerRuntimeRoot, "character-migration", id);
            Directory.CreateDirectory(working);
            originalCopy = Path.Combine(working, "Level.original.sav");
            File.Copy(op.LevelSavePath, originalCopy, true);

            var decodeInput = Path.Combine(working, "Level.input.sav");
            File.Copy(op.LevelSavePath, decodeInput, true);
            var decodedJson = await codec.DecodeAsync(converterMatch, decodeInput, Path.Combine(working, "Level.decoded.json"), cancellationToken);
            var root = JsonNode.Parse(File.ReadAllText(decodedJson)) ?? throw new InvalidDataException("Decoded Level.sav JSON is empty.");
            var rawData = FindGuildRawData(root, op.GuildId)
                ?? throw new InvalidOperationException("Source player's guild was not found in the freshly decoded save. It may have changed since preview.");

            ApplyMigrationMutation(rawData, op.SourcePlayerId, op.DestinationPlayerId);
            Advance(journal, "Staged", $"Decoded transaction staged: guild {op.GuildId} references repointed from {op.SourcePlayerId} to {op.DestinationPlayerId}.");

            var mutatedJson = Path.Combine(working, "Level.mutated.json");
            File.WriteAllText(mutatedJson, root.ToJsonString());
            var staged = await codec.EncodeAsync(converterMatch, mutatedJson, Path.Combine(working, "Level.repaired.sav"), cancellationToken);
            ValidateEncodedSave(staged, op.LevelSavePath);
            Advance(journal, "Encoded", "Modified JSON encoded to a staged Level.sav and passed size validation.");

            var verifyJsonPath = await codec.DecodeAsync(converterMatch, staged, Path.Combine(working, "Level.verify.json"), cancellationToken);
            var verifyRoot = JsonNode.Parse(File.ReadAllText(verifyJsonPath)) ?? throw new InvalidDataException("Verification decode was empty.");
            var verifyRawData = FindGuildRawData(verifyRoot, op.GuildId)
                ?? throw new InvalidDataException("Verification could not find the guild in the repaired save.");
            VerifyMigrationMutation(verifyRawData, op.SourcePlayerId, op.DestinationPlayerId);
            Advance(journal, "Verified", "Staged save independently decoded and the migration was verified.");

            var replacement = op.LevelSavePath + $".mysttiq-character-migration-{id}.tmp";
            File.Copy(staged, replacement, true);
            File.Replace(replacement, op.LevelSavePath, null, true);
            if (!File.Exists(op.LevelSavePath) || new FileInfo(op.LevelSavePath).Length < 1024)
                throw new InvalidDataException("The active Level.sav failed final validation after replacement.");

            Advance(journal, "Committed", $"Verified save committed atomically to the active world. Result hash: {HashFile(op.LevelSavePath)}");
            activity.Record("Information", "Character Migration", $"Migrated {op.SourcePlayerName} -> {op.DestinationPlayerName}",
                $"sourcePlayer={op.SourcePlayerId}; destinationPlayer={op.DestinationPlayerId}; guild={op.GuildId}; backup={safety.FileName}");
            coordinator.Complete(operation.Id, $"{op.SourcePlayerName}'s guild membership migrated to {op.DestinationPlayerName}.");
            return new HeadlessCharacterMigrationResult(true, id, "Committed", safety.FileName, false, journal.JournalPath,
                op.SourcePlayerId, $"{op.SourcePlayerName}'s guild membership migrated to {op.DestinationPlayerName}.");
        }
        catch (Exception ex)
        {
            var rolledBack = false;
            try
            {
                if (op is not null && originalCopy is not null && File.Exists(originalCopy))
                {
                    File.Copy(originalCopy, op.LevelSavePath, true);
                    rolledBack = HashFile(op.LevelSavePath).Equals(op.SourceHash, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { rolledBack = false; }
            if (journal is not null)
            {
                journal.RolledBack = rolledBack;
                Advance(journal, rolledBack ? "RolledBack" : "Failed", ex.Message);
                activity.Record("Warning", "Character Migration", "Migration failed", $"rolledBack={rolledBack}; error={ex.GetType().Name}");
            }
            if (operation is not null) coordinator.Fail(operation.Id, ex.Message, rolledBack);
            return new HeadlessCharacterMigrationResult(false, journal?.TransactionId, rolledBack ? "RolledBack" : "Failed",
                journal?.SafetyBackup, rolledBack, journal?.JournalPath, op?.SourcePlayerId, ex.Message);
        }
        finally
        {
            operation?.Dispose();
            if (working is not null) try { Directory.Delete(working, true); } catch { }
            gate.Release();
        }
    }

    // Post-commit only: what happens to the source character's save file after a successful,
    // journaled migration. Keep/Archive/Delete are pure file operations, safe once the
    // pre-migration safety backup already exists. Reset (writing a fresh/blank player .sav) is
    // not implemented this pass -- it needs a player-save encoder this codebase doesn't have yet.
    public async Task<HeadlessCharacterDispositionResult> DisposeSourceCharacterAsync(string sourcePlayerId, CharacterDisposition disposition, CancellationToken cancellationToken)
    {
        if (disposition == CharacterDisposition.Reset)
            return HeadlessCharacterDispositionResult.Failure("Reset is not yet supported -- it needs a fresh-character save encoder this build does not have. Use Keep, Archive, or Delete.");

        var snapshot = await explorer.ExploreAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath))
            return HeadlessCharacterDispositionResult.Failure("No active world was discovered.");

        var playersDir = Path.Combine(snapshot.ActiveWorldPath, "Players");
        var savePath = Path.Combine(playersDir, $"{sourcePlayerId}.sav");
        var companionPath = Path.Combine(playersDir, $"{sourcePlayerId}_dps.sav");

        if (disposition == CharacterDisposition.Keep)
        {
            activity.Record("Information", "Character Migration", "Source character kept", $"player={sourcePlayerId}");
            return new HeadlessCharacterDispositionResult(true, "Kept", "Source character save left in place.");
        }

        if (!File.Exists(savePath))
            return HeadlessCharacterDispositionResult.Failure($"Source player save {savePath} was not found.");

        if (disposition == CharacterDisposition.Archive)
        {
            var archiveDir = Path.Combine(paths.ManagerRuntimeRoot, "character-migration", "archive");
            Directory.CreateDirectory(archiveDir);
            var stamped = $"{sourcePlayerId}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
            File.Move(savePath, Path.Combine(archiveDir, $"{stamped}.sav"), true);
            if (File.Exists(companionPath))
                File.Move(companionPath, Path.Combine(archiveDir, $"{stamped}_dps.sav"), true);
            activity.Record("Information", "Character Migration", "Source character archived", $"player={sourcePlayerId}; archiveDir={archiveDir}");
            return new HeadlessCharacterDispositionResult(true, "Archived", $"Source character save moved to {archiveDir}.");
        }

        // Delete
        File.Delete(savePath);
        if (File.Exists(companionPath)) File.Delete(companionPath);
        activity.Record("Warning", "Character Migration", "Source character deleted", $"player={sourcePlayerId}");
        return new HeadlessCharacterDispositionResult(true, "Deleted", "Source character save removed. The pre-migration safety backup retains a copy.");
    }

    private static void ApplyMigrationMutation(JsonObject guildRawData, string sourcePlayerId, string destinationPlayerId)
    {
        var currentLeader = Normalize(ReadScalar(GetProperty(guildRawData, "admin_player_uid")));
        if (currentLeader.Equals(Normalize(sourcePlayerId), StringComparison.OrdinalIgnoreCase))
            SetScalarProperty(guildRawData, "admin_player_uid", destinationPlayerId);

        foreach (var member in GetMembers(guildRawData).OfType<JsonObject>())
        {
            var uid = Normalize(ReadScalar(GetProperty(member, "player_uid")));
            if (uid.Equals(Normalize(sourcePlayerId), StringComparison.OrdinalIgnoreCase))
                SetScalarProperty(member, "player_uid", destinationPlayerId);
        }
    }

    private static void VerifyMigrationMutation(JsonObject guildRawData, string sourcePlayerId, string destinationPlayerId)
    {
        var stillHasSource = GetMembers(guildRawData).OfType<JsonObject>().Any(member =>
            Normalize(ReadScalar(GetProperty(member, "player_uid"))).Equals(Normalize(sourcePlayerId), StringComparison.OrdinalIgnoreCase));
        if (stillHasSource)
            throw new InvalidDataException("Verification still found the source player referenced in the repaired guild.");

        var hasDestination = GetMembers(guildRawData).OfType<JsonObject>().Any(member =>
            Normalize(ReadScalar(GetProperty(member, "player_uid"))).Equals(Normalize(destinationPlayerId), StringComparison.OrdinalIgnoreCase));
        var leader = Normalize(ReadScalar(GetProperty(guildRawData, "admin_player_uid")));
        var isLeader = leader.Equals(Normalize(destinationPlayerId), StringComparison.OrdinalIgnoreCase);
        if (!hasDestination && !isLeader)
            throw new InvalidDataException("Verification did not find the destination player referenced in the repaired guild.");
    }

    private HeadlessWorldTransactionJournal NewJournal(string id, string guildId)
    {
        var path = Path.Combine(paths.ManagerRuntimeRoot, "world-transactions", "journals", $"transaction-{id}.json");
        return new HeadlessWorldTransactionJournal
        {
            TransactionId = id, Mode = "character-migration", WorldId = guildId, State = "Created",
            CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow, JournalPath = path
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

    private static void ValidateEncodedSave(string output, string original)
    {
        if (!File.Exists(output)) throw new InvalidDataException("The save encoder did not create Level.sav.");
        var length = new FileInfo(output).Length;
        if (length < 1024) throw new InvalidDataException("The encoded Level.sav is unexpectedly small.");
        var originalLength = new FileInfo(original).Length;
        if (originalLength > 0 && length > originalLength * 8) throw new InvalidDataException("The encoded Level.sav is unexpectedly larger than the source save.");
    }

    // --- Mutable (JsonNode) GroupSaveDataMap traversal, duplicated from
    // HeadlessGuildOwnershipService's own copy rather than shared -- matches this codebase's
    // existing per-service-duplication convention for these small structural walkers. ---

    private static JsonObject? FindGuildRawData(JsonNode root, string guildId)
    {
        foreach (var mapRoot in FindAllByKey(root, "groupsavedatamap"))
        {
            var entriesNode = mapRoot;
            if (entriesNode is JsonObject && GetProperty(entriesNode, "value") is JsonArray wrapped)
                entriesNode = wrapped;
            if (entriesNode is not JsonArray entries) continue;

            foreach (var entry in entries)
            {
                if (entry is null) continue;
                var groupStruct = (GetProperty(entry, "value") ?? entry) as JsonObject;
                if (groupStruct is null) continue;
                var groupType = ReadScalar(GetProperty(groupStruct, "GroupType"));
                if (!string.Equals(groupType, "EPalGroupType::Guild", StringComparison.OrdinalIgnoreCase)) continue;

                var rawDataProperty = GetProperty(groupStruct, "RawData");
                if (rawDataProperty is null) continue;
                var rawData = Unwrap(rawDataProperty) as JsonObject;
                if (rawData is null) continue;

                var id = Normalize(ReadScalar(GetProperty(rawData, "group_id")));
                if (id.Equals(Normalize(guildId), StringComparison.OrdinalIgnoreCase)) return rawData;
            }
        }
        return null;
    }

    private static List<JsonNode> FindAllByKey(JsonNode node, string key, int depth = 0)
    {
        var results = new List<JsonNode>();
        if (depth > 80) return results;
        if (node is JsonObject obj)
        {
            foreach (var pair in obj)
            {
                if (pair.Value is null) continue;
                if (NormalizeKey(pair.Key) == key) results.Add(pair.Value);
                else results.AddRange(FindAllByKey(pair.Value, key, depth + 1));
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is null) continue;
                results.AddRange(FindAllByKey(item, key, depth + 1));
            }
        }
        return results;
    }

    private static JsonNode? GetProperty(JsonNode node, string name)
    {
        if (node is not JsonObject obj) return null;
        if (obj.TryGetPropertyValue(name, out var exact) && exact is not null) return exact;
        foreach (var pair in obj)
            if (pair.Value is not null && string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        return null;
    }

    private static JsonNode Unwrap(JsonNode node)
    {
        var current = node;
        for (var i = 0; i < 8; i++)
        {
            if (current is not JsonObject) return current;
            var inner = GetProperty(current, "value");
            if (inner is null) return current;
            current = inner;
        }
        return current;
    }

    private static string ReadScalar(JsonNode? node)
    {
        if (node is null) return "";
        if (node is JsonValue value)
        {
            try { return value.GetValue<string>(); }
            catch { try { return value.ToJsonString().Trim('"'); } catch { return ""; } }
        }
        if (node is JsonObject)
        {
            var inner = GetProperty(node, "value") ?? GetProperty(node, "id") ?? GetProperty(node, "guid");
            if (inner is not null) return ReadScalar(inner);
        }
        return "";
    }

    private static void SetScalarProperty(JsonObject obj, string name, string value)
    {
        if (obj.TryGetPropertyValue(name, out var existing) && existing is JsonObject wrapper && wrapper.ContainsKey("value"))
            wrapper["value"] = value;
        else
            obj[name] = value;
    }

    private static JsonArray GetMembers(JsonObject rawData)
    {
        var membersNode = GetProperty(rawData, "players");
        if (membersNode is JsonArray array) return array;
        if (membersNode is JsonObject wrapper && GetProperty(wrapper, "value") is JsonArray wrapped) return wrapped;
        return [];
    }

    private static string NormalizeKey(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string Normalize(string value) =>
        new string((value ?? "").Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();

    private static string HashFile(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private sealed record PendingMigration(string SourcePlayerId, string SourcePlayerName, string DestinationPlayerId,
        string DestinationPlayerName, string GuildId, string LevelSavePath, string SourceHash, DateTimeOffset ExpiresUtc);
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum CharacterDisposition { Keep, Archive, Delete, Reset }

public sealed record HeadlessCharacterMigrationPreview(bool CanApply, string PreviewToken, string SourcePlayerId, string SourcePlayerName,
    string DestinationPlayerId, string DestinationPlayerName, string? MatchMethod, double MatchConfidence,
    IReadOnlyList<string> Findings, DateTimeOffset ExpiresUtc);
public sealed record HeadlessCharacterMigrationPreviewRequest(string SourcePlayerId, string DestinationPlayerId);
public sealed record HeadlessCharacterMigrationApplyRequest(string PreviewToken, bool Confirmed);
public sealed record HeadlessCharacterMigrationResult(bool Success, string? TransactionId, string State, string? SafetyBackup,
    bool RolledBack, string? JournalPath, string? SourcePlayerId, string Message)
{
    public static HeadlessCharacterMigrationResult Failure(string message) => new(false, null, "Rejected", null, false, null, null, message);
}
public sealed record HeadlessCharacterDispositionRequest(CharacterDisposition Disposition);
public sealed record HeadlessCharacterDispositionResult(bool Success, string State, string Message)
{
    public static HeadlessCharacterDispositionResult Failure(string message) => new(false, "Rejected", message);
}
