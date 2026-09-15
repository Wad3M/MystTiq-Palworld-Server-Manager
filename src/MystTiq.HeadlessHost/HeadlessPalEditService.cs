using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MystTiq.Core.Models;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.6.15.0 "Save-Data Edit Engine Foundation": the first real capability to mutate individual
// Pal instances inside Level.sav, not just guild/base ownership records. Reuses the exact
// Preview -> Safety Backup -> Decode -> Mutate -> Encode -> Verify -> Atomic Commit shape
// HeadlessGuildOwnershipService already proved out, adapted for CharacterSaveParameterMap's real
// GVAS property wrapping (confirmed against real production save data, not assumed): a
// double-wrapped {"value":{"type":"None","value":N}} shape for ByteProperty fields
// (Level/Rank/Talent_*), single-wrapped for StrProperty/BoolProperty, and a nested
// {"type":"EnumType","value":"Enum::Member"} shape for EnumProperty (Gender). Scoped to
// scalar/identity fields only -- see the v0.6.15.0 architecture doc for what's deferred.
public sealed class HeadlessPalEditService
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
    private readonly Dictionary<string, PendingEdit> pending = new(StringComparer.Ordinal);

    public HeadlessPalEditService(IServerPathProfile paths, IServerLifecycleService lifecycle,
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

    public async Task<IReadOnlyList<PalInstanceSnapshot>> ListPalsAsync(string? ownerPlayerId, CancellationToken cancellationToken)
    {
        var snapshot = await explorer.ExploreAsync(cancellationToken);
        if (!snapshot.Available || string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath)) return [];
        var levelSavePath = Path.Combine(snapshot.ActiveWorldPath, "Level.sav");
        if (!File.Exists(levelSavePath)) return [];
        var (converterMatch, _) = await codec.ResolveConverterAsync(levelSavePath, cancellationToken);
        if (converterMatch is null) return [];

        var (root, _) = await DecodeAsync(converterMatch, levelSavePath, "list-" + Guid.NewGuid().ToString("N"), cancellationToken);
        var playersById = snapshot.Players.ToDictionary(p => Normalize(p.PlayerId), p => p.PlayerName, StringComparer.Ordinal);

        var results = new List<PalInstanceSnapshot>();
        foreach (var (key, saveParam) in EnumeratePalEntries(root))
        {
            // Ownership lives on SaveParameter's own OwnerPlayerUId, not the map entry's key.PlayerUId
            // -- confirmed against real production save data: key.PlayerUId is zero for every genuine
            // Pal entry (it's only meaningful for a player's own character-body entry, which IsPlayer
            // already filters out above).
            var ownerId = Normalize(ReadScalar(GetProperty(saveParam, "OwnerPlayerUId")));
            if (!string.IsNullOrWhiteSpace(ownerPlayerId) && !ownerId.Equals(Normalize(ownerPlayerId), StringComparison.OrdinalIgnoreCase)) continue;
            results.Add(ToSnapshot(key, saveParam, ownerId, playersById));
        }
        return results;
    }

    public async Task<HeadlessPalEditPreview> PreviewAsync(string instanceId, PalEditFieldChanges changes, CancellationToken cancellationToken)
    {
        var findings = new List<string>();
        var snapshot = await explorer.ExploreAsync(cancellationToken);
        string? levelSavePath = string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath) ? null : Path.Combine(snapshot.ActiveWorldPath, "Level.sav");
        var (converterMatch, converterDetail) = levelSavePath is null
            ? (null, "No active world.")
            : await codec.ResolveConverterAsync(levelSavePath, cancellationToken);

        if (!snapshot.Available || levelSavePath is null || !File.Exists(levelSavePath))
            findings.Add("No active world with Level.sav was discovered.");
        if (converterMatch is null)
            findings.Add("Save codec unavailable: " + converterDetail);

        PalInstanceSnapshot? current = null;
        if (findings.Count == 0)
        {
            var (root, _) = await DecodeAsync(converterMatch!, levelSavePath!, "preview-" + Guid.NewGuid().ToString("N"), cancellationToken);
            var found = FindPalEntry(root, instanceId);
            if (found is null) findings.Add($"No Pal with instance id {instanceId} was found in the active world.");
            else
            {
                var playersById = snapshot.Players.ToDictionary(p => Normalize(p.PlayerId), p => p.PlayerName, StringComparer.Ordinal);
                var ownerId = Normalize(ReadScalar(GetProperty(found.Value.SaveParam, "OwnerPlayerUId")));
                current = ToSnapshot(found.Value.Key, found.Value.SaveParam, ownerId, playersById);
            }
        }

        ValidateChanges(changes, findings);

        var canApply = findings.Count == 0 && current is not null;
        var sourceHash = canApply ? HashFile(levelSavePath!) : "";
        var expires = DateTimeOffset.UtcNow.Add(PreviewLifetime);
        if (canApply) findings.AddRange(DescribeDiff(current!, changes));

        var token = "";
        if (canApply)
        {
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            lock (pending)
                pending[token] = new PendingEdit(instanceId, changes, levelSavePath!, sourceHash, expires);
        }

        activity.Record(canApply ? "Information" : "Warning", "Pal Editor", "Previewed Pal edit",
            $"instanceId={instanceId}; canApply={canApply}");

        return new HeadlessPalEditPreview(canApply, token, instanceId, current?.NickName ?? instanceId, findings, expires);
    }

    private static void ValidateChanges(PalEditFieldChanges changes, List<string> findings)
    {
        if (changes.Level is < 1 or > 255) findings.Add("Level must be between 1 and 255.");
        if (changes.Rank is < 0 or > 5) findings.Add("Rank must be between 0 and 5.");
        if (changes.TalentHp is < 0 or > 100) findings.Add("Talent_HP must be between 0 and 100.");
        if (changes.TalentShot is < 0 or > 100) findings.Add("Talent_Shot must be between 0 and 100.");
        if (changes.TalentDefense is < 0 or > 100) findings.Add("Talent_Defense must be between 0 and 100.");
        if (changes.Gender is not null && changes.Gender is not ("Male" or "Female"))
            findings.Add("Gender must be Male or Female.");
    }

    private static IReadOnlyList<string> DescribeDiff(PalInstanceSnapshot current, PalEditFieldChanges changes)
    {
        var lines = new List<string>();
        if (changes.NickName is not null && changes.NickName != current.NickName) lines.Add($"NickName: \"{current.NickName}\" -> \"{changes.NickName}\"");
        if (changes.Level is not null && changes.Level != current.Level) lines.Add($"Level: {current.Level} -> {changes.Level}");
        if (changes.Rank is not null && changes.Rank != current.Rank) lines.Add($"Rank: {current.Rank} -> {changes.Rank}");
        if (changes.TalentHp is not null && changes.TalentHp != current.TalentHp) lines.Add($"Talent_HP: {current.TalentHp} -> {changes.TalentHp}");
        if (changes.TalentShot is not null && changes.TalentShot != current.TalentShot) lines.Add($"Talent_Shot: {current.TalentShot} -> {changes.TalentShot}");
        if (changes.TalentDefense is not null && changes.TalentDefense != current.TalentDefense) lines.Add($"Talent_Defense: {current.TalentDefense} -> {changes.TalentDefense}");
        if (changes.Gender is not null && changes.Gender != current.Gender) lines.Add($"Gender: {current.Gender} -> {changes.Gender}");
        if (changes.IsRarePal is not null && changes.IsRarePal != current.IsRarePal) lines.Add($"IsRarePal: {current.IsRarePal} -> {changes.IsRarePal}");
        if (lines.Count == 0) lines.Add("No fields would change.");
        else lines.Insert(0, "Ready:");
        return lines;
    }

    public async Task<HeadlessPalEditResult> ApplyAsync(string previewToken, bool confirmed, CancellationToken cancellationToken)
    {
        if (!confirmed) return HeadlessPalEditResult.Failure("Apply requires explicit confirmation.");
        if (!await gate.WaitAsync(0, cancellationToken)) return HeadlessPalEditResult.Failure("Another Pal edit is already running.");

        PendingEdit? op = null;
        HeadlessWorldTransactionJournal? journal = null;
        OperationHandle? operation = null;
        string? working = null;
        string? originalCopy = null;
        try
        {
            lock (pending)
            {
                if (pending.TryGetValue(previewToken, out var pendingOp)) op = pendingOp;
                if (op is not null) pending.Remove(previewToken);
            }
            if (op is null || op.ExpiresUtc < DateTimeOffset.UtcNow)
                return HeadlessPalEditResult.Failure("The preview token is missing or expired. Preview the change again.");

            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.NativeProcessId.HasValue || status.Ready)
                return HeadlessPalEditResult.Failure("Stop PalServer before applying a Pal edit.");

            if (!File.Exists(op.LevelSavePath) || !HashFile(op.LevelSavePath).Equals(op.SourceHash, StringComparison.OrdinalIgnoreCase))
                return HeadlessPalEditResult.Failure("Level.sav changed since the preview. Preview the change again.");

            operation = await coordinator.BeginAsync(profile, "pal-edit", "HeadlessPalEditService", ["world-mutation"], cancellationToken);

            var id = Guid.NewGuid().ToString("N");
            journal = NewJournal(id, op.InstanceId);
            Advance(journal, "PreviewAccepted", $"The single-use preview token for Pal {op.InstanceId} was accepted.");

            var safety = await backups.CreateAsync(BackupClass.Safety, cancellationToken);
            if (!safety.Success || string.IsNullOrWhiteSpace(safety.FileName))
                throw new InvalidOperationException("Fresh safety backup failed: " + safety.Message);
            journal.SafetyBackup = safety.FileName;
            Advance(journal, "SafetyBackupCreated", $"Fresh safety backup: {safety.FileName}");

            var (converterMatch, converterDetail) = await codec.ResolveConverterAsync(op.LevelSavePath, cancellationToken);
            if (converterMatch is null) throw new InvalidOperationException("Save codec unavailable: " + converterDetail);
            Advance(journal, "ConverterResolved", $"Using the {converterMatch.ContainerKind} converter for this save.");

            working = Path.Combine(paths.ManagerRuntimeRoot, "pal-edit", id);
            Directory.CreateDirectory(working);
            originalCopy = Path.Combine(working, "Level.original.sav");
            File.Copy(op.LevelSavePath, originalCopy, true);

            var decodeInput = Path.Combine(working, "Level.input.sav");
            File.Copy(op.LevelSavePath, decodeInput, true);
            var decodedJson = await codec.DecodeAsync(converterMatch, decodeInput, Path.Combine(working, "Level.decoded.json"), cancellationToken);
            var root = JsonNode.Parse(File.ReadAllText(decodedJson)) ?? throw new InvalidDataException("Decoded Level.sav JSON is empty.");
            var found = FindPalEntry(root, op.InstanceId)
                ?? throw new InvalidOperationException("The Pal was not found in the freshly decoded save. It may have changed since preview.");

            ApplyMutation(found.SaveParam, op.Changes);
            Advance(journal, "Staged", $"Decoded transaction staged: Pal {op.InstanceId} field changes applied.");

            var mutatedJson = Path.Combine(working, "Level.mutated.json");
            File.WriteAllText(mutatedJson, root.ToJsonString());
            var staged = await codec.EncodeAsync(converterMatch, mutatedJson, Path.Combine(working, "Level.repaired.sav"), cancellationToken);
            ValidateEncodedSave(staged, op.LevelSavePath);
            Advance(journal, "Encoded", "Modified JSON encoded to a staged Level.sav and passed size validation.");

            var verifyJsonPath = await codec.DecodeAsync(converterMatch, staged, Path.Combine(working, "Level.verify.json"), cancellationToken);
            var verifyRoot = JsonNode.Parse(File.ReadAllText(verifyJsonPath)) ?? throw new InvalidDataException("Verification decode was empty.");
            var verifyFound = FindPalEntry(verifyRoot, op.InstanceId)
                ?? throw new InvalidDataException("Verification could not find the Pal in the repaired save.");
            VerifyMutation(verifyFound.SaveParam, op.Changes);
            Advance(journal, "Verified", "Staged save independently decoded and the change was verified.");

            var replacement = op.LevelSavePath + $".mysttiq-pal-edit-{id}.tmp";
            File.Copy(staged, replacement, true);
            File.Replace(replacement, op.LevelSavePath, null, true);
            if (!File.Exists(op.LevelSavePath) || new FileInfo(op.LevelSavePath).Length < 1024)
                throw new InvalidDataException("The active Level.sav failed final validation after replacement.");

            Advance(journal, "Committed", $"Verified save committed atomically to the active world. Result hash: {HashFile(op.LevelSavePath)}");
            HeadlessSaveCodecService.RefreshExplorerSidecar(op.LevelSavePath, verifyJsonPath);
            activity.Record("Information", "Pal Editor", "Applied Pal edit", $"instanceId={op.InstanceId}; backup={safety.FileName}");
            coordinator.Complete(operation.Id, $"Pal {op.InstanceId} updated.");
            return new HeadlessPalEditResult(true, id, "Committed", safety.FileName, false, journal.JournalPath, $"Pal {op.InstanceId} updated.");
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
                activity.Record("Warning", "Pal Editor", "Pal edit failed", $"rolledBack={rolledBack}; error={ex.GetType().Name}");
            }
            if (operation is not null) coordinator.Fail(operation.Id, ex.Message, rolledBack);
            return new HeadlessPalEditResult(false, journal?.TransactionId, rolledBack ? "RolledBack" : "Failed",
                journal?.SafetyBackup, rolledBack, journal?.JournalPath, ex.Message);
        }
        finally
        {
            operation?.Dispose();
            if (working is not null) try { Directory.Delete(working, true); } catch { }
            gate.Release();
        }
    }

    private async Task<(JsonNode Root, string JsonPath)> DecodeAsync(HeadlessSaveConverterMatch converter, string levelSavePath, string workingSubdir, CancellationToken cancellationToken)
    {
        var working = Path.Combine(paths.ManagerRuntimeRoot, "pal-edit", workingSubdir);
        Directory.CreateDirectory(working);
        try
        {
            var decodeInput = Path.Combine(working, "Level.input.sav");
            File.Copy(levelSavePath, decodeInput, true);
            var decodedJson = await codec.DecodeAsync(converter, decodeInput, Path.Combine(working, "Level.decoded.json"), cancellationToken);
            var text = await File.ReadAllTextAsync(decodedJson, cancellationToken);
            var root = JsonNode.Parse(text) ?? throw new InvalidDataException("Decoded Level.sav JSON is empty.");
            return (root, decodedJson);
        }
        finally { try { Directory.Delete(working, true); } catch { } }
    }

    private static void ApplyMutation(JsonObject saveParam, PalEditFieldChanges changes)
    {
        if (changes.NickName is not null) SetStrProperty(saveParam, "NickName", changes.NickName);
        if (changes.Level is not null) SetByteProperty(saveParam, "Level", changes.Level.Value);
        if (changes.Rank is not null) SetByteProperty(saveParam, "Rank", changes.Rank.Value);
        if (changes.TalentHp is not null) SetByteProperty(saveParam, "Talent_HP", changes.TalentHp.Value);
        if (changes.TalentShot is not null) SetByteProperty(saveParam, "Talent_Shot", changes.TalentShot.Value);
        if (changes.TalentDefense is not null) SetByteProperty(saveParam, "Talent_Defense", changes.TalentDefense.Value);
        if (changes.Gender is not null) SetGenderProperty(saveParam, changes.Gender);
        if (changes.IsRarePal is not null) SetBoolProperty(saveParam, "IsRarePal", changes.IsRarePal.Value);
    }

    private static void VerifyMutation(JsonObject saveParam, PalEditFieldChanges changes)
    {
        if (changes.NickName is not null && ReadScalar(GetProperty(saveParam, "NickName")) != changes.NickName)
            throw new InvalidDataException("Verification did not find the expected NickName in the repaired save.");
        if (changes.Level is not null && ReadInt(GetProperty(saveParam, "Level")) != changes.Level)
            throw new InvalidDataException("Verification did not find the expected Level in the repaired save.");
        if (changes.Rank is not null && ReadInt(GetProperty(saveParam, "Rank")) != changes.Rank)
            throw new InvalidDataException("Verification did not find the expected Rank in the repaired save.");
        if (changes.TalentHp is not null && ReadInt(GetProperty(saveParam, "Talent_HP")) != changes.TalentHp)
            throw new InvalidDataException("Verification did not find the expected Talent_HP in the repaired save.");
        if (changes.TalentShot is not null && ReadInt(GetProperty(saveParam, "Talent_Shot")) != changes.TalentShot)
            throw new InvalidDataException("Verification did not find the expected Talent_Shot in the repaired save.");
        if (changes.TalentDefense is not null && ReadInt(GetProperty(saveParam, "Talent_Defense")) != changes.TalentDefense)
            throw new InvalidDataException("Verification did not find the expected Talent_Defense in the repaired save.");
        if (changes.Gender is not null && !GenderMatches(ReadScalar(GetProperty(saveParam, "Gender")), changes.Gender))
            throw new InvalidDataException("Verification did not find the expected Gender in the repaired save.");
        if (changes.IsRarePal is not null && ReadBool(GetProperty(saveParam, "IsRarePal")) != changes.IsRarePal)
            throw new InvalidDataException("Verification did not find the expected IsRarePal flag in the repaired save.");
    }

    // Exact-suffix comparison, not EndsWith -- "EPalGenderType::Female" ends with "Male"
    // (case-insensitively: "Fe-MALE"), so an EndsWith check against "Male" would incorrectly pass
    // for a stored value of "Female" too, making Gender="Male" verification pass unconditionally
    // regardless of what was actually written.
    private static bool GenderMatches(string stored, string expected)
    {
        var separatorIndex = stored.LastIndexOf("::", StringComparison.Ordinal);
        var storedSuffix = separatorIndex >= 0 ? stored[(separatorIndex + 2)..] : stored;
        return storedSuffix.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private HeadlessWorldTransactionJournal NewJournal(string id, string instanceId)
    {
        var path = Path.Combine(paths.ManagerRuntimeRoot, "world-transactions", "journals", $"transaction-{id}.json");
        return new HeadlessWorldTransactionJournal
        {
            TransactionId = id, Mode = "pal-edit", WorldId = instanceId, State = "Created",
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

    // --- CharacterSaveParameterMap traversal (mutable JsonNode tree), grounded against real
    // decoded production save data: entries are {key:{PlayerUId,InstanceId,DebugName}, value:{
    // RawData:{value:{object:{SaveParameter:{value:{...fields...}}}}}}}. ---

    private static IEnumerable<(JsonObject Key, JsonObject SaveParam)> EnumeratePalEntries(JsonNode root)
    {
        foreach (var mapRoot in FindAllByKey(root, "charactersaveparametermap"))
        {
            var entriesNode = mapRoot;
            if (entriesNode is JsonObject && GetProperty(entriesNode, "value") is JsonArray wrapped) entriesNode = wrapped;
            if (entriesNode is not JsonArray entries) continue;

            foreach (var entry in entries)
            {
                if (entry is not JsonObject entryObj) continue;
                var key = GetProperty(entryObj, "key") as JsonObject;
                var valueNode = GetProperty(entryObj, "value");
                if (key is null || valueNode is null) continue;

                var saveParam = ResolveSaveParameter(valueNode);
                if (saveParam is null) continue;
                if (ReadBool(GetProperty(saveParam, "IsPlayer"))) continue; // players use the same struct; skip them
                yield return (key, saveParam);
            }
        }
    }

    private static (JsonObject Key, JsonObject SaveParam)? FindPalEntry(JsonNode root, string instanceId)
    {
        var normalized = Normalize(instanceId);
        foreach (var (key, saveParam) in EnumeratePalEntries(root))
        {
            var id = Normalize(ReadScalar(GetProperty(key, "InstanceId")));
            if (id.Equals(normalized, StringComparison.OrdinalIgnoreCase)) return (key, saveParam);
        }
        return null;
    }

    private static JsonObject? ResolveSaveParameter(JsonNode entryValue)
    {
        var rawDataProperty = GetProperty(entryValue, "RawData");
        if (rawDataProperty is null) return null;
        var rawData = Unwrap(rawDataProperty) as JsonObject;
        if (rawData is null) return null;
        var objectNode = GetProperty(rawData, "object") as JsonObject;
        if (objectNode is null) return null;
        var saveParameterProperty = GetProperty(objectNode, "SaveParameter");
        if (saveParameterProperty is null) return null;
        return Unwrap(saveParameterProperty) as JsonObject;
    }

    private static PalInstanceSnapshot ToSnapshot(JsonObject key, JsonObject saveParam, string ownerId, IReadOnlyDictionary<string, string> playersById)
    {
        var characterId = ReadScalar(GetProperty(saveParam, "CharacterID"));
        var isBoss = characterId.StartsWith("BOSS_", StringComparison.OrdinalIgnoreCase);
        var gender = ReadScalar(GetProperty(saveParam, "Gender"));
        var genderShort = gender.Contains("Female", StringComparison.OrdinalIgnoreCase) ? "Female"
            : gender.Contains("Male", StringComparison.OrdinalIgnoreCase) ? "Male" : "Unknown";
        var hasOwner = !string.IsNullOrWhiteSpace(ownerId) && ownerId != "00000000000000000000000000000000";
        playersById.TryGetValue(ownerId, out var ownerName);
        var level = ReadInt(GetProperty(saveParam, "Level"));

        return new PalInstanceSnapshot(
            ReadScalar(GetProperty(key, "InstanceId")),
            characterId,
            isBoss,
            ReadScalar(GetProperty(saveParam, "NickName")),
            level > 0 ? level : 1,
            ReadInt(GetProperty(saveParam, "Rank")),
            ReadInt(GetProperty(saveParam, "Talent_HP")),
            ReadInt(GetProperty(saveParam, "Talent_Shot")),
            ReadInt(GetProperty(saveParam, "Talent_Defense")),
            genderShort,
            ReadBool(GetProperty(saveParam, "IsRarePal")),
            hasOwner ? ownerId : null,
            hasOwner ? ownerName : null);
    }

    // --- Typed property setters, matching the real wrapping shapes confirmed against production
    // save data (double-wrapped ByteProperty for Level/Rank/Talent_*, single-wrapped for
    // StrProperty/BoolProperty, nested type+value for EnumProperty). ---

    private static void SetByteProperty(JsonObject saveParam, string name, int value)
    {
        if (GetProperty(saveParam, name) is JsonObject existing && GetProperty(existing, "value") is JsonObject inner && inner.ContainsKey("value"))
            inner["value"] = value;
        else
            saveParam[name] = new JsonObject { ["id"] = null, ["value"] = new JsonObject { ["type"] = "None", ["value"] = value }, ["type"] = "ByteProperty" };
    }

    private static void SetStrProperty(JsonObject saveParam, string name, string value)
    {
        if (GetProperty(saveParam, name) is JsonObject existing)
            existing["value"] = value;
        else
            saveParam[name] = new JsonObject { ["id"] = null, ["value"] = value, ["type"] = "StrProperty" };
    }

    private static void SetBoolProperty(JsonObject saveParam, string name, bool value)
    {
        if (GetProperty(saveParam, name) is JsonObject existing)
            existing["value"] = value;
        else
            saveParam[name] = new JsonObject { ["value"] = value, ["id"] = null, ["type"] = "BoolProperty" };
    }

    private static void SetGenderProperty(JsonObject saveParam, string genderValue)
    {
        var full = $"EPalGenderType::{genderValue}";
        if (GetProperty(saveParam, "Gender") is JsonObject existing && GetProperty(existing, "value") is JsonObject inner && inner.ContainsKey("value"))
            inner["value"] = full;
        else
            saveParam["Gender"] = new JsonObject { ["id"] = null, ["value"] = new JsonObject { ["type"] = "EPalGenderType", ["value"] = full }, ["type"] = "EnumProperty" };
    }

    // --- Shared JsonNode helpers, identical to HeadlessGuildOwnershipService's own copy
    // (duplicated here rather than extracted, matching the existing convention across the guild/
    // base/character-migration ownership services). ---

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

    private static JsonNode? GetProperty(JsonNode? node, string name)
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

    private static int ReadInt(JsonNode? node) => int.TryParse(ReadScalar(node), out var v) ? v : 0;
    private static bool ReadBool(JsonNode? node) => bool.TryParse(ReadScalar(node), out var v) && v;

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

    private sealed record PendingEdit(string InstanceId, PalEditFieldChanges Changes, string LevelSavePath, string SourceHash, DateTimeOffset ExpiresUtc);
}

public sealed record HeadlessPalEditPreview(bool CanApply, string PreviewToken, string InstanceId, string PalLabel, IReadOnlyList<string> Findings, DateTimeOffset ExpiresUtc);
public sealed record HeadlessPalEditPreviewRequest(string InstanceId, PalEditFieldChanges Changes);
public sealed record HeadlessPalEditApplyRequest(string PreviewToken, bool Confirmed);
public sealed record HeadlessPalEditResult(bool Success, string? TransactionId, string State, string? SafetyBackup,
    bool RolledBack, string? JournalPath, string Message)
{
    public static HeadlessPalEditResult Failure(string message) => new(false, null, "Rejected", null, false, null, message);
}
