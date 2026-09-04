using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// Base/Palbox ownership repair, following the same Preview -> Safety Backup ->
// Server-side Transaction -> Validate -> Journal/Audit -> Refresh GUI pattern
// HeadlessGuildOwnershipService already proved for guild leadership. Base
// ownership has a materially different, more scattered shape: a base's real
// save representation is not one record but a guild-level `base_ids` pointer
// plus a "group_id_belong_to" tag denormalized across every structure/
// container/worker object placed at that base (confirmed against a real save:
// 132 separate tagged records for one base). v0.5.4.0 adds Recover Base (the
// legacy app's DeleteBaseAndOwnedObjects operation) on the same detection
// logic as Transfer Ownership, removing the tagged objects and the base_ids
// entry outright instead of reassigning them.
public sealed class HeadlessBaseOwnershipService
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
    private readonly Dictionary<string, PendingBaseTransfer> pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PendingBaseRecovery> pendingRecovery = new(StringComparer.Ordinal);

    public HeadlessBaseOwnershipService(IServerPathProfile paths, IServerLifecycleService lifecycle,
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

    public async Task<HeadlessBaseOwnershipPreview> PreviewTransferAsync(string baseId, string targetGuildId, CancellationToken cancellationToken)
    {
        var snapshot = await explorer.ExploreAsync(cancellationToken);
        var findings = new List<string>();
        var normalizedBaseId = Normalize(baseId);
        var sourceGuild = snapshot.Guilds.FirstOrDefault(g => g.BaseIds.Any(id => Normalize(id).Equals(normalizedBaseId, StringComparison.OrdinalIgnoreCase)));
        var targetGuild = snapshot.Guilds.FirstOrDefault(g => string.Equals(g.GuildId, targetGuildId, StringComparison.OrdinalIgnoreCase));
        string? levelSavePath = string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath) ? null : Path.Combine(snapshot.ActiveWorldPath, "Level.sav");
        var (converterMatch, converterDetail) = levelSavePath is null
            ? (null, "No active world.")
            : await codec.ResolveConverterAsync(levelSavePath, cancellationToken);

        if (!snapshot.Available || string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath))
            findings.Add("No active world with Level.sav was discovered.");
        if (!snapshot.SemanticAvailable)
            findings.Add("Decoded GroupSaveDataMap evidence is unavailable; base/guild identity cannot be verified.");
        if (sourceGuild is null)
            findings.Add($"Base {baseId} is not currently referenced by any guild's base_ids.");
        if (targetGuild is null)
            findings.Add($"Target guild {targetGuildId} was not found.");
        if (sourceGuild is not null && targetGuild is not null && string.Equals(sourceGuild.GuildId, targetGuild.GuildId, StringComparison.OrdinalIgnoreCase))
            findings.Add($"Base {baseId} already belongs to {targetGuild.GuildName}.");
        if (converterMatch is null)
            findings.Add("Save codec unavailable: " + converterDetail);

        var canApply = findings.Count == 0 && levelSavePath is not null && File.Exists(levelSavePath);
        var sourceHash = canApply ? HashFile(levelSavePath!) : "";
        var expires = DateTimeOffset.UtcNow.Add(PreviewLifetime);

        if (canApply)
            findings.Add($"Ready: base {baseId} will transfer from {sourceGuild!.GuildName} to {targetGuild!.GuildName}. Every structure/container/worker record tagged to this base will have its owning-guild tag updated to match.");

        var token = "";
        if (canApply)
        {
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            lock (pending)
                pending[token] = new PendingBaseTransfer(baseId, sourceGuild!.GuildId, sourceGuild.GuildName, targetGuild!.GuildId, targetGuild.GuildName, levelSavePath!, sourceHash, expires);
        }

        activity.Record(canApply ? "Information" : "Warning", "Base Ownership", "Previewed Transfer Base Ownership",
            $"base={baseId}; targetGuild={targetGuildId}; canApply={canApply}");

        return new HeadlessBaseOwnershipPreview(canApply, token, baseId, sourceGuild?.GuildId ?? "", sourceGuild?.GuildName ?? "",
            targetGuildId, targetGuild?.GuildName ?? "", findings, expires);
    }

    public async Task<HeadlessBaseOwnershipResult> ApplyTransferAsync(HeadlessBaseOwnershipApplyRequest request, CancellationToken cancellationToken)
    {
        if (!request.Confirmed)
            return HeadlessBaseOwnershipResult.Failure("Apply requires explicit confirmation.");
        if (!await gate.WaitAsync(0, cancellationToken))
            return HeadlessBaseOwnershipResult.Failure("Another base ownership transaction is already running.");

        PendingBaseTransfer? op = null;
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
                return HeadlessBaseOwnershipResult.Failure("The preview token is missing or expired. Preview the transfer again.");

            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.NativeProcessId.HasValue || status.Ready)
                return HeadlessBaseOwnershipResult.Failure("Stop PalServer before applying an ownership change.");

            if (!File.Exists(op.LevelSavePath) || !HashFile(op.LevelSavePath).Equals(op.SourceHash, StringComparison.OrdinalIgnoreCase))
                return HeadlessBaseOwnershipResult.Failure("Level.sav changed since the preview. Preview the transfer again.");

            operation = await coordinator.BeginAsync(profile, "base-ownership-transfer",
                "HeadlessBaseOwnershipService", ["world-mutation"], cancellationToken);

            var id = Guid.NewGuid().ToString("N");
            journal = NewJournal(id, op.BaseId, op.TargetGuildId);
            Advance(journal, "PreviewAccepted", $"The single-use preview token for base {op.BaseId} was accepted.");

            var safety = await backups.CreateAsync(BackupClass.Safety, cancellationToken);
            if (!safety.Success || string.IsNullOrWhiteSpace(safety.FileName))
                throw new InvalidOperationException("Fresh safety backup failed: " + safety.Message);
            journal.SafetyBackup = safety.FileName;
            Advance(journal, "SafetyBackupCreated", $"Fresh safety backup: {safety.FileName}");

            var (converterMatch, converterDetail) = await codec.ResolveConverterAsync(op.LevelSavePath, cancellationToken);
            if (converterMatch is null)
                throw new InvalidOperationException("Save codec unavailable: " + converterDetail);
            Advance(journal, "ConverterResolved", $"Using the {converterMatch.ContainerKind} converter for this save.");

            working = Path.Combine(paths.ManagerRuntimeRoot, "base-ownership", id);
            Directory.CreateDirectory(working);
            originalCopy = Path.Combine(working, "Level.original.sav");
            File.Copy(op.LevelSavePath, originalCopy, true);

            var decodeInput = Path.Combine(working, "Level.input.sav");
            File.Copy(op.LevelSavePath, decodeInput, true);
            var decodedJson = await codec.DecodeAsync(converterMatch, decodeInput, Path.Combine(working, "Level.decoded.json"), cancellationToken);
            var root = JsonNode.Parse(File.ReadAllText(decodedJson)) ?? throw new InvalidDataException("Decoded Level.sav JSON is empty.");

            var sourceRawData = FindGuildRawData(root, op.SourceGuildId)
                ?? throw new InvalidOperationException("The source guild was not found in the freshly decoded save. It may have changed since preview.");
            var targetRawData = FindGuildRawData(root, op.TargetGuildId)
                ?? throw new InvalidOperationException("The target guild was not found in the freshly decoded save. It may have changed since preview.");

            if (!RemoveFromBaseIds(sourceRawData, op.BaseId))
                throw new InvalidOperationException("Base ID was not found in the source guild's base_ids. It may have changed since preview.");
            AddToBaseIds(targetRawData, op.BaseId);

            var retagged = RetagBaseObjects(root, op.BaseId, op.TargetGuildId);
            Advance(journal, "Staged",
                $"Decoded transaction staged: base {op.BaseId} moved from {op.SourceGuildName} to {op.TargetGuildName}; {retagged} owning-guild tag(s) updated.");

            var mutatedJson = Path.Combine(working, "Level.mutated.json");
            File.WriteAllText(mutatedJson, root.ToJsonString());
            var staged = await codec.EncodeAsync(converterMatch, mutatedJson, Path.Combine(working, "Level.repaired.sav"), cancellationToken);
            ValidateEncodedSave(staged, op.LevelSavePath);
            Advance(journal, "Encoded", "Modified JSON encoded to a staged Level.sav and passed size validation.");

            var verifyJsonPath = await codec.DecodeAsync(converterMatch, staged, Path.Combine(working, "Level.verify.json"), cancellationToken);
            var verifyRoot = JsonNode.Parse(File.ReadAllText(verifyJsonPath)) ?? throw new InvalidDataException("Verification decode was empty.");
            var verifySourceRawData = FindGuildRawData(verifyRoot, op.SourceGuildId)
                ?? throw new InvalidDataException("Verification could not find the source guild in the repaired save.");
            var verifyTargetRawData = FindGuildRawData(verifyRoot, op.TargetGuildId)
                ?? throw new InvalidDataException("Verification could not find the target guild in the repaired save.");
            if (ContainsBaseId(verifySourceRawData, op.BaseId))
                throw new InvalidDataException("Verification found the base still listed under the source guild's base_ids.");
            if (!ContainsBaseId(verifyTargetRawData, op.BaseId))
                throw new InvalidDataException("Verification did not find the base under the target guild's base_ids.");
            var verifyRetagged = CountRetaggedBaseObjects(verifyRoot, op.BaseId, op.TargetGuildId);
            if (verifyRetagged == 0 && retagged > 0)
                throw new InvalidDataException("Verification did not find any owning-guild tags updated to the target guild for this base.");
            Advance(journal, "Verified", $"Staged save independently decoded; base_ids moved and {verifyRetagged} owning-guild tag(s) confirmed.");

            var replacement = op.LevelSavePath + $".mysttiq-base-ownership-{id}.tmp";
            File.Copy(staged, replacement, true);
            File.Replace(replacement, op.LevelSavePath, null, true);
            if (!File.Exists(op.LevelSavePath) || new FileInfo(op.LevelSavePath).Length < 1024)
                throw new InvalidDataException("The active Level.sav failed final validation after replacement.");

            Advance(journal, "Committed", $"Verified save committed atomically to the active world. Result hash: {HashFile(op.LevelSavePath)}");
            activity.Record("Information", "Base Ownership", "Applied Transfer Base Ownership",
                $"base={op.BaseId}; from={op.SourceGuildId}; to={op.TargetGuildId}; retagged={retagged}; backup={safety.FileName}");
            coordinator.Complete(operation.Id, $"Base {op.BaseId} transferred from {op.SourceGuildName} to {op.TargetGuildName}.");
            return new HeadlessBaseOwnershipResult(true, id, "Committed", safety.FileName, false, journal.JournalPath,
                $"Base {op.BaseId} transferred from {op.SourceGuildName} to {op.TargetGuildName} ({retagged} owning-guild tag(s) updated).");
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
                activity.Record("Warning", "Base Ownership", "Transfer Base Ownership failed",
                    $"rolledBack={rolledBack}; error={ex.GetType().Name}");
            }
            if (operation is not null) coordinator.Fail(operation.Id, ex.Message, rolledBack);
            return new HeadlessBaseOwnershipResult(false, journal?.TransactionId, rolledBack ? "RolledBack" : "Failed",
                journal?.SafetyBackup, rolledBack, journal?.JournalPath, ex.Message);
        }
        finally
        {
            operation?.Dispose();
            if (working is not null) try { Directory.Delete(working, true); } catch { }
            gate.Release();
        }
    }

    public async Task<HeadlessBaseRecoveryPreview> PreviewRecoveryAsync(string baseId, CancellationToken cancellationToken)
    {
        var snapshot = await explorer.ExploreAsync(cancellationToken);
        var findings = new List<string>();
        var normalizedBaseId = Normalize(baseId);
        var owningGuild = snapshot.Guilds.FirstOrDefault(g => g.BaseIds.Any(id => Normalize(id).Equals(normalizedBaseId, StringComparison.OrdinalIgnoreCase)));
        string? levelSavePath = string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath) ? null : Path.Combine(snapshot.ActiveWorldPath, "Level.sav");
        var (converterMatch, converterDetail) = levelSavePath is null
            ? (null, "No active world.")
            : await codec.ResolveConverterAsync(levelSavePath, cancellationToken);

        if (!snapshot.Available || string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath))
            findings.Add("No active world with Level.sav was discovered.");
        if (!snapshot.SemanticAvailable)
            findings.Add("Decoded GroupSaveDataMap evidence is unavailable; base/guild identity cannot be verified.");
        if (owningGuild is null)
            findings.Add($"Base {baseId} is not currently referenced by any guild's base_ids.");
        if (converterMatch is null)
            findings.Add("Save codec unavailable: " + converterDetail);

        int? objectCount = null;
        if (findings.Count == 0 && levelSavePath is not null && File.Exists(levelSavePath))
        {
            try
            {
                objectCount = await CountBaseObjectsAsync(converterMatch!, levelSavePath, baseId, cancellationToken);
                if (objectCount == 0)
                    findings.Add($"No structure/container/worker records tagged to base {baseId} were found in the decoded save.");
            }
            catch (Exception ex) { findings.Add("Object count preview failed: " + ex.Message); }
        }

        var canApply = findings.Count == 0 && levelSavePath is not null && File.Exists(levelSavePath);
        var sourceHash = canApply ? HashFile(levelSavePath!) : "";
        var expires = DateTimeOffset.UtcNow.Add(PreviewLifetime);

        if (canApply)
            findings.Add($"Ready: base {baseId} owned by {owningGuild!.GuildName} will be permanently removed, along with {objectCount} structure/container/worker record(s) tagged to it. This cannot be undone except by restoring the safety backup created during Apply.");

        var token = "";
        if (canApply)
        {
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            lock (pendingRecovery)
                pendingRecovery[token] = new PendingBaseRecovery(baseId, owningGuild!.GuildId, owningGuild.GuildName, levelSavePath!, sourceHash, expires);
        }

        activity.Record(canApply ? "Information" : "Warning", "Base Ownership", "Previewed Recover Base",
            $"base={baseId}; canApply={canApply}; objectCount={objectCount}");

        return new HeadlessBaseRecoveryPreview(canApply, token, baseId, owningGuild?.GuildId ?? "", owningGuild?.GuildName ?? "", objectCount, findings, expires);
    }

    public async Task<HeadlessBaseOwnershipResult> ApplyRecoveryAsync(HeadlessBaseRecoveryApplyRequest request, CancellationToken cancellationToken)
    {
        if (!request.Confirmed)
            return HeadlessBaseOwnershipResult.Failure("Apply requires explicit confirmation.");
        if (!await gate.WaitAsync(0, cancellationToken))
            return HeadlessBaseOwnershipResult.Failure("Another base ownership transaction is already running.");

        PendingBaseRecovery? op = null;
        HeadlessWorldTransactionJournal? journal = null;
        OperationHandle? operation = null;
        string? working = null;
        string? originalCopy = null;
        try
        {
            lock (pendingRecovery)
            {
                if (pendingRecovery.TryGetValue(request.PreviewToken, out var found)) op = found;
                if (op is not null) pendingRecovery.Remove(request.PreviewToken);
            }
            if (op is null || op.ExpiresUtc < DateTimeOffset.UtcNow)
                return HeadlessBaseOwnershipResult.Failure("The preview token is missing or expired. Preview the recovery again.");

            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.NativeProcessId.HasValue || status.Ready)
                return HeadlessBaseOwnershipResult.Failure("Stop PalServer before applying base recovery.");

            if (!File.Exists(op.LevelSavePath) || !HashFile(op.LevelSavePath).Equals(op.SourceHash, StringComparison.OrdinalIgnoreCase))
                return HeadlessBaseOwnershipResult.Failure("Level.sav changed since the preview. Preview the recovery again.");

            operation = await coordinator.BeginAsync(profile, "base-recovery",
                "HeadlessBaseOwnershipService", ["world-mutation"], cancellationToken);

            var id = Guid.NewGuid().ToString("N");
            journal = NewJournal(id, op.BaseId, "recovery");
            Advance(journal, "PreviewAccepted", $"The single-use preview token for base {op.BaseId} was accepted.");

            var safety = await backups.CreateAsync(BackupClass.Safety, cancellationToken);
            if (!safety.Success || string.IsNullOrWhiteSpace(safety.FileName))
                throw new InvalidOperationException("Fresh safety backup failed: " + safety.Message);
            journal.SafetyBackup = safety.FileName;
            Advance(journal, "SafetyBackupCreated", $"Fresh safety backup: {safety.FileName}");

            var (converterMatch, converterDetail) = await codec.ResolveConverterAsync(op.LevelSavePath, cancellationToken);
            if (converterMatch is null)
                throw new InvalidOperationException("Save codec unavailable: " + converterDetail);
            Advance(journal, "ConverterResolved", $"Using the {converterMatch.ContainerKind} converter for this save.");

            working = Path.Combine(paths.ManagerRuntimeRoot, "base-recovery", id);
            Directory.CreateDirectory(working);
            originalCopy = Path.Combine(working, "Level.original.sav");
            File.Copy(op.LevelSavePath, originalCopy, true);

            var decodeInput = Path.Combine(working, "Level.input.sav");
            File.Copy(op.LevelSavePath, decodeInput, true);
            var decodedJson = await codec.DecodeAsync(converterMatch, decodeInput, Path.Combine(working, "Level.decoded.json"), cancellationToken);
            var root = JsonNode.Parse(File.ReadAllText(decodedJson)) ?? throw new InvalidDataException("Decoded Level.sav JSON is empty.");

            var sourceRawData = FindGuildRawData(root, op.GuildId)
                ?? throw new InvalidOperationException("The owning guild was not found in the freshly decoded save. It may have changed since preview.");

            if (!RemoveFromBaseIds(sourceRawData, op.BaseId))
                throw new InvalidOperationException("Base ID was not found in the owning guild's base_ids. It may have changed since preview.");

            var removed = RemoveBaseObjects(root, op.BaseId);
            Advance(journal, "Staged",
                $"Decoded transaction staged: base {op.BaseId} removed from {op.GuildName}'s base_ids; {removed} structure/container/worker record(s) deleted.");

            var mutatedJson = Path.Combine(working, "Level.mutated.json");
            File.WriteAllText(mutatedJson, root.ToJsonString());
            var staged = await codec.EncodeAsync(converterMatch, mutatedJson, Path.Combine(working, "Level.repaired.sav"), cancellationToken);
            ValidateEncodedSave(staged, op.LevelSavePath);
            Advance(journal, "Encoded", "Modified JSON encoded to a staged Level.sav and passed size validation.");

            var verifyJsonPath = await codec.DecodeAsync(converterMatch, staged, Path.Combine(working, "Level.verify.json"), cancellationToken);
            var verifyRoot = JsonNode.Parse(File.ReadAllText(verifyJsonPath)) ?? throw new InvalidDataException("Verification decode was empty.");
            var verifySourceRawData = FindGuildRawData(verifyRoot, op.GuildId)
                ?? throw new InvalidDataException("Verification could not find the owning guild in the repaired save.");
            if (ContainsBaseId(verifySourceRawData, op.BaseId))
                throw new InvalidDataException("Verification found the base still listed under the owning guild's base_ids.");
            var remaining = CountBaseObjects(verifyRoot, op.BaseId);
            if (remaining > 0)
                throw new InvalidDataException($"Verification found {remaining} remaining record(s) still tagged to base {op.BaseId}.");
            Advance(journal, "Verified", $"Staged save independently decoded; base_ids entry and all {removed} tagged record(s) confirmed removed.");

            var replacement = op.LevelSavePath + $".mysttiq-base-recovery-{id}.tmp";
            File.Copy(staged, replacement, true);
            File.Replace(replacement, op.LevelSavePath, null, true);
            if (!File.Exists(op.LevelSavePath) || new FileInfo(op.LevelSavePath).Length < 1024)
                throw new InvalidDataException("The active Level.sav failed final validation after replacement.");

            Advance(journal, "Committed", $"Verified save committed atomically to the active world. Result hash: {HashFile(op.LevelSavePath)}");
            activity.Record("Information", "Base Ownership", "Applied Recover Base",
                $"base={op.BaseId}; guild={op.GuildId}; removed={removed}; backup={safety.FileName}");
            coordinator.Complete(operation.Id, $"Base {op.BaseId} and {removed} owned record(s) were permanently removed from {op.GuildName}.");
            return new HeadlessBaseOwnershipResult(true, id, "Committed", safety.FileName, false, journal.JournalPath,
                $"Base {op.BaseId} and {removed} owned record(s) were permanently removed from {op.GuildName}.");
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
                activity.Record("Warning", "Base Ownership", "Recover Base failed",
                    $"rolledBack={rolledBack}; error={ex.GetType().Name}");
            }
            if (operation is not null) coordinator.Fail(operation.Id, ex.Message, rolledBack);
            return new HeadlessBaseOwnershipResult(false, journal?.TransactionId, rolledBack ? "RolledBack" : "Failed",
                journal?.SafetyBackup, rolledBack, journal?.JournalPath, ex.Message);
        }
        finally
        {
            operation?.Dispose();
            if (working is not null) try { Directory.Delete(working, true); } catch { }
            gate.Release();
        }
    }

    private async Task<int> CountBaseObjectsAsync(HeadlessSaveConverterMatch converterMatch, string levelSavePath, string baseId, CancellationToken cancellationToken)
    {
        var temp = Path.Combine(paths.ManagerRuntimeRoot, "base-recovery-preview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var staged = Path.Combine(temp, "Level.preview.sav");
            File.Copy(levelSavePath, staged, true);
            var decodedJson = await codec.DecodeAsync(converterMatch, staged, Path.Combine(temp, "Level.preview.json"), cancellationToken);
            var root = JsonNode.Parse(File.ReadAllText(decodedJson)) ?? throw new InvalidDataException("Decoded Level.sav JSON is empty.");
            return CountBaseObjects(root, baseId);
        }
        finally { try { Directory.Delete(temp, true); } catch { } }
    }

    // --- whole-tree object removal: every ARRAY ELEMENT whose subtree contains an object
    // with base_camp_id_belong_to == baseId is deleted outright. Unlike RetagBaseObjects
    // (which only overwrites a sibling field's value and can never corrupt structure),
    // deletion must never remove a keyed property from a fixed-shape object -- some record
    // shapes (e.g. MapObjectSaveData) carry the ownership tag several levels below the
    // record's own array element, nested under Model.value.RawData.value. Removing that
    // inner "value" key directly leaves a structurally required object empty and breaks
    // the save encoder. Only whole discardable array entries are ever removed. ---

    private static int RemoveBaseObjects(JsonNode? root, string baseId)
    {
        var removed = 0;
        RemoveBaseObjectsFrom(root, baseId, ref removed);
        return removed;
    }

    private static void RemoveBaseObjectsFrom(JsonNode? node, string baseId, ref int removed)
    {
        if (node is JsonArray array)
        {
            for (var i = array.Count - 1; i >= 0; i--)
            {
                var child = array[i];
                if (child is null) continue;
                if (ContainsBaseTag(child, baseId)) { array.RemoveAt(i); removed++; }
                else RemoveBaseObjectsFrom(child, baseId, ref removed);
            }
        }
        else if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(x => x.Key).ToList())
                if (obj[key] is { } child) RemoveBaseObjectsFrom(child, baseId, ref removed);
        }
    }

    private static bool ContainsBaseTag(JsonNode? node, string baseId)
    {
        var found = false;
        Walk(node, n => { if (!found && IsOwnedByBase(n, baseId)) found = true; });
        return found;
    }

    private static int CountBaseObjects(JsonNode? root, string baseId)
    {
        var count = 0;
        Walk(root, node => { if (IsOwnedByBase(node, baseId)) count++; });
        return count;
    }

    private static bool IsOwnedByBase(JsonNode node, string baseId)
    {
        if (node is not JsonObject obj) return false;
        if (!obj.TryGetPropertyValue("base_camp_id_belong_to", out var campRef) || campRef is not JsonValue campValue) return false;
        return TryString(campValue, out var campText) && Normalize(campText).Equals(Normalize(baseId), StringComparison.OrdinalIgnoreCase);
    }

    // --- base_ids array mutation (guild RawData level) ---

    private static bool RemoveFromBaseIds(JsonObject guildRawData, string baseId)
    {
        var array = GetBaseIdsArray(guildRawData);
        var normalized = Normalize(baseId);
        for (var i = array.Count - 1; i >= 0; i--)
        {
            if (array[i] is JsonValue value && TryString(value, out var text) && Normalize(text).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                array.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    private static void AddToBaseIds(JsonObject guildRawData, string baseId)
    {
        var array = GetBaseIdsArray(guildRawData);
        var normalized = Normalize(baseId);
        var exists = array.Any(item => item is JsonValue value && TryString(value, out var text) && Normalize(text).Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (!exists) array.Add(PreserveGuidFormatting(array, baseId));
    }

    private static bool ContainsBaseId(JsonObject guildRawData, string baseId)
    {
        var normalized = Normalize(baseId);
        return GetBaseIdsArray(guildRawData).Any(item => item is JsonValue value && TryString(value, out var text) && Normalize(text).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonArray GetBaseIdsArray(JsonObject guildRawData)
    {
        if (guildRawData.TryGetPropertyValue("base_ids", out var existing) && existing is JsonArray array) return array;
        var created = new JsonArray();
        guildRawData["base_ids"] = created;
        return created;
    }

    private static string PreserveGuidFormatting(JsonArray existingArray, string compact)
    {
        var sample = existingArray.OfType<JsonValue>().Select(v => TryString(v, out var s) ? s : "").FirstOrDefault(s => s.Contains('-'));
        var normalized = Normalize(compact);
        if (sample is not null && normalized.Length == 32)
            return $"{normalized[..8]}-{normalized[8..12]}-{normalized[12..16]}-{normalized[16..20]}-{normalized[20..]}".ToLowerInvariant();
        return compact;
    }

    // --- whole-tree retagging: every object with base_camp_id_belong_to == baseId
    // gets its group_id_belong_to (if present) set to the new owning guild. ---

    private static int RetagBaseObjects(JsonNode root, string baseId, string newGuildId)
    {
        var changed = 0;
        Walk(root, node =>
        {
            if (node is not JsonObject obj) return;
            if (!obj.TryGetPropertyValue("base_camp_id_belong_to", out var campRef) || campRef is not JsonValue campValue) return;
            if (!TryString(campValue, out var campText) || !Normalize(campText).Equals(Normalize(baseId), StringComparison.OrdinalIgnoreCase)) return;
            if (obj.TryGetPropertyValue("group_id_belong_to", out var groupRef) && groupRef is JsonValue groupValue && TryString(groupValue, out var groupText))
            {
                obj["group_id_belong_to"] = PreserveGuidFormatting(groupText, newGuildId);
                changed++;
            }
        });
        return changed;
    }

    private static int CountRetaggedBaseObjects(JsonNode root, string baseId, string guildId)
    {
        var count = 0;
        Walk(root, node =>
        {
            if (node is not JsonObject obj) return;
            if (!obj.TryGetPropertyValue("base_camp_id_belong_to", out var campRef) || campRef is not JsonValue campValue) return;
            if (!TryString(campValue, out var campText) || !Normalize(campText).Equals(Normalize(baseId), StringComparison.OrdinalIgnoreCase)) return;
            if (obj.TryGetPropertyValue("group_id_belong_to", out var groupRef) && groupRef is JsonValue groupValue &&
                TryString(groupValue, out var groupText) && Normalize(groupText).Equals(Normalize(guildId), StringComparison.OrdinalIgnoreCase))
                count++;
        });
        return count;
    }

    private static string PreserveGuidFormatting(string original, string compact)
    {
        var normalized = Normalize(compact);
        if (original.Contains('-') && normalized.Length == 32)
            return $"{normalized[..8]}-{normalized[8..12]}-{normalized[12..16]}-{normalized[16..20]}-{normalized[20..]}".ToLowerInvariant();
        return compact;
    }

    private static void Walk(JsonNode? node, Action<JsonNode> visitor)
    {
        if (node is null) return;
        visitor(node);
        if (node is JsonObject obj)
            foreach (var pair in obj) Walk(pair.Value, visitor);
        else if (node is JsonArray array)
            foreach (var item in array) Walk(item, visitor);
    }

    // --- guild RawData lookup, matching HeadlessGuildOwnershipService's structural understanding ---

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

    private static bool TryString(JsonValue value, out string text)
    {
        try { text = value.GetValue<string>(); return true; }
        catch { text = ""; return false; }
    }

    private static string NormalizeKey(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string Normalize(string value) =>
        new string((value ?? "").Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();

    private HeadlessWorldTransactionJournal NewJournal(string id, string baseId, string targetGuildId)
    {
        var path = Path.Combine(paths.ManagerRuntimeRoot, "world-transactions", "journals", $"transaction-{id}.json");
        var mode = targetGuildId == "recovery" ? "base-recovery" : "base-ownership-transfer";
        var worldId = targetGuildId == "recovery" ? $"{baseId}->deleted" : $"{baseId}->{targetGuildId}";
        return new HeadlessWorldTransactionJournal
        {
            TransactionId = id, Mode = mode, WorldId = worldId, State = "Created",
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

    private static string HashFile(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private sealed record PendingBaseTransfer(string BaseId, string SourceGuildId, string SourceGuildName, string TargetGuildId,
        string TargetGuildName, string LevelSavePath, string SourceHash, DateTimeOffset ExpiresUtc);
    private sealed record PendingBaseRecovery(string BaseId, string GuildId, string GuildName,
        string LevelSavePath, string SourceHash, DateTimeOffset ExpiresUtc);
}

public sealed record HeadlessBaseOwnershipPreview(bool CanApply, string PreviewToken, string BaseId, string SourceGuildId,
    string SourceGuildName, string TargetGuildId, string TargetGuildName, IReadOnlyList<string> Findings, DateTimeOffset ExpiresUtc);
public sealed record HeadlessBaseOwnershipTransferRequest(string BaseId, string TargetGuildId);
public sealed record HeadlessBaseOwnershipApplyRequest(string PreviewToken, bool Confirmed);
public sealed record HeadlessBaseRecoveryPreview(bool CanApply, string PreviewToken, string BaseId, string GuildId,
    string GuildName, int? ObjectCount, IReadOnlyList<string> Findings, DateTimeOffset ExpiresUtc);
public sealed record HeadlessBaseRecoveryRequest(string BaseId);
public sealed record HeadlessBaseRecoveryApplyRequest(string PreviewToken, bool Confirmed);
public sealed record HeadlessBaseOwnershipResult(bool Success, string? TransactionId, string State, string? SafetyBackup,
    bool RolledBack, string? JournalPath, string Message)
{
    public static HeadlessBaseOwnershipResult Failure(string message) => new(false, null, "Rejected", null, false, null, message);
}
