using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// Guild/Base ownership-repair platform the desktop UI currently marks "BACKEND REQUIRED".
// Ports the legacy WPF app's OwnershipEngineService / GuildRepairExecutor / GuildJsonRepairService
// mutation logic onto the shared headless architecture, following the same
// Preview -> Safety Backup -> Server-side Transaction -> Validate -> Journal/Audit shape as
// HeadlessWorldTransactionService. Journals are written into the same world-transactions
// journal store so existing Transaction Center history picks them up without changes.
public sealed class HeadlessGuildOwnershipService
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
    private readonly Dictionary<string, PendingOperation> pending = new(StringComparer.Ordinal);

    public HeadlessGuildOwnershipService(IServerPathProfile paths, IServerLifecycleService lifecycle,
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

    public async Task<HeadlessGuildOwnershipPreview> PreviewAsync(string operationTypeName, string guildId, string playerId, CancellationToken cancellationToken)
    {
        var operationType = NormalizeOperationType(operationTypeName);
        var snapshot = await explorer.ExploreAsync(cancellationToken);
        var findings = new List<string>();
        var guild = snapshot.Guilds.FirstOrDefault(g => string.Equals(g.GuildId, guildId, StringComparison.OrdinalIgnoreCase));
        var player = snapshot.Players.FirstOrDefault(p => string.Equals(p.PlayerId, playerId, StringComparison.OrdinalIgnoreCase));
        string? levelSavePath = string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath) ? null : Path.Combine(snapshot.ActiveWorldPath, "Level.sav");
        var (converterMatch, converterDetail) = levelSavePath is null
            ? (null, "No active world.")
            : await codec.ResolveConverterAsync(levelSavePath, cancellationToken);

        if (!snapshot.Available || string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath))
            findings.Add("No active world with Level.sav was discovered.");
        if (!snapshot.SemanticAvailable)
            findings.Add("Decoded GroupSaveDataMap evidence is unavailable; guild identity cannot be verified.");
        if (guild is null)
            findings.Add($"Guild {guildId} was not found.");
        if (player is null)
            findings.Add($"Player {playerId} was not found.");
        else if (operationType != GuildOwnershipOperationType.RemoveBrokenMember && !player.SaveExists)
            findings.Add($"Player {playerId} has no save file yet; they must join the server at least once before this change applies.");

        if (guild is not null)
        {
            switch (operationType)
            {
                case GuildOwnershipOperationType.ClaimOrphanedGuild when guild.Health != "Orphaned / Needs Review":
                    findings.Add($"Guild {guild.GuildName} is not orphaned. Claim is only offered for orphaned guilds.");
                    break;
                case GuildOwnershipOperationType.TransferLeadership when player is not null &&
                    string.Equals(guild.LeaderPlayerId, player.PlayerId, StringComparison.OrdinalIgnoreCase):
                    findings.Add($"{player.PlayerName} is already the leader of {guild.GuildName}.");
                    break;
                case GuildOwnershipOperationType.AddPlayerToGuild when player is not null &&
                    guild.MemberPlayerIds.Any(id => string.Equals(id, player.PlayerId, StringComparison.OrdinalIgnoreCase)):
                    findings.Add($"{player.PlayerName} is already a member of {guild.GuildName}.");
                    break;
                case GuildOwnershipOperationType.RemoveBrokenMember when player is not null && player.SaveExists:
                    findings.Add($"{player.PlayerName} has a valid save file. Remove Broken Member is only offered for a dangling reference with no matching save.");
                    break;
                case GuildOwnershipOperationType.RemoveBrokenMember when player is not null &&
                    !guild.MemberPlayerIds.Any(id => string.Equals(id, player.PlayerId, StringComparison.OrdinalIgnoreCase)):
                    findings.Add($"{playerId} is not currently a member of {guild.GuildName}.");
                    break;
            }
        }
        if (converterMatch is null)
            findings.Add("Save codec unavailable: " + converterDetail);

        var canApply = findings.Count == 0 && levelSavePath is not null && File.Exists(levelSavePath);
        var sourceHash = canApply ? HashFile(levelSavePath!) : "";
        var expires = DateTimeOffset.UtcNow.Add(PreviewLifetime);

        if (canApply)
            findings.Add(DescribeReady(operationType, guild!, player!));

        var token = "";
        if (canApply)
        {
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            lock (pending)
                pending[token] = new PendingOperation(operationType, guildId, playerId, player!.PlayerName, levelSavePath!, sourceHash, expires);
        }

        activity.Record(canApply ? "Information" : "Warning", "Guild Ownership", $"Previewed {DisplayName(operationType)}",
            $"guild={guildId}; player={playerId}; canApply={canApply}");

        return new HeadlessGuildOwnershipPreview(canApply, token, operationTypeName, guildId, guild?.GuildName ?? "",
            playerId, player?.PlayerName ?? playerId, findings, expires);
    }

    private static GuildOwnershipOperationType NormalizeOperationType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "claim" or "claimorphanedguild" or "claim-orphaned-guild" => GuildOwnershipOperationType.ClaimOrphanedGuild,
        "transfer" or "transferleadership" or "transfer-leadership" => GuildOwnershipOperationType.TransferLeadership,
        "add-player" or "addplayer" or "addplayertoguild" or "add-player-to-guild" => GuildOwnershipOperationType.AddPlayerToGuild,
        "remove-broken-member" or "removebrokenmember" => GuildOwnershipOperationType.RemoveBrokenMember,
        _ => throw new ArgumentException("operationType must be claim, transfer-leadership, add-player, or remove-broken-member.")
    };

    public async Task<HeadlessGuildOwnershipResult> ApplyAsync(HeadlessGuildOwnershipApplyRequest request, CancellationToken cancellationToken)
    {
        if (!request.Confirmed)
            return HeadlessGuildOwnershipResult.Failure("Apply requires explicit confirmation.");
        if (!await gate.WaitAsync(0, cancellationToken))
            return HeadlessGuildOwnershipResult.Failure("Another guild ownership transaction is already running.");

        PendingOperation? op = null;
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
                return HeadlessGuildOwnershipResult.Failure("The preview token is missing or expired. Preview the change again.");

            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.NativeProcessId.HasValue || status.Ready)
                return HeadlessGuildOwnershipResult.Failure("Stop PalServer before applying an ownership change.");

            if (!File.Exists(op.LevelSavePath) || !HashFile(op.LevelSavePath).Equals(op.SourceHash, StringComparison.OrdinalIgnoreCase))
                return HeadlessGuildOwnershipResult.Failure("Level.sav changed since the preview. Preview the change again.");

            operation = await coordinator.BeginAsync(profile, "guild-ownership",
                "HeadlessGuildOwnershipService", ["world-mutation"], cancellationToken);

            var id = Guid.NewGuid().ToString("N");
            journal = NewJournal(id, op.OperationType, op.GuildId);
            Advance(journal, "PreviewAccepted", $"The single-use preview token for guild {op.GuildId} was accepted.");

            var safety = await backups.CreateAsync(BackupClass.Safety, cancellationToken);
            if (!safety.Success || string.IsNullOrWhiteSpace(safety.FileName))
                throw new InvalidOperationException("Fresh safety backup failed: " + safety.Message);
            journal.SafetyBackup = safety.FileName;
            Advance(journal, "SafetyBackupCreated", $"Fresh safety backup: {safety.FileName}");

            var (converterMatch, converterDetail) = await codec.ResolveConverterAsync(op.LevelSavePath, cancellationToken);
            if (converterMatch is null)
                throw new InvalidOperationException("Save codec unavailable: " + converterDetail);
            Advance(journal, "ConverterResolved", $"Using the {converterMatch.ContainerKind} converter for this save.");

            working = Path.Combine(paths.ManagerRuntimeRoot, "guild-ownership", id);
            Directory.CreateDirectory(working);
            originalCopy = Path.Combine(working, "Level.original.sav");
            File.Copy(op.LevelSavePath, originalCopy, true);

            var decodeInput = Path.Combine(working, "Level.input.sav");
            File.Copy(op.LevelSavePath, decodeInput, true);
            var decodedJson = await codec.DecodeAsync(converterMatch, decodeInput, Path.Combine(working, "Level.decoded.json"), cancellationToken);
            var root = JsonNode.Parse(File.ReadAllText(decodedJson)) ?? throw new InvalidDataException("Decoded Level.sav JSON is empty.");
            var rawData = FindGuildRawData(root, op.GuildId)
                ?? throw new InvalidOperationException("Guild was not found in the freshly decoded save. It may have changed since preview.");

            ApplyMutation(op.OperationType, rawData, op.PlayerId, op.PlayerName);
            Advance(journal, "Staged", $"Decoded transaction staged: {DescribeMutation(op.OperationType, op.PlayerName, op.GuildId)}");

            var mutatedJson = Path.Combine(working, "Level.mutated.json");
            File.WriteAllText(mutatedJson, root.ToJsonString());
            var staged = await codec.EncodeAsync(converterMatch, mutatedJson, Path.Combine(working, "Level.repaired.sav"), cancellationToken);
            ValidateEncodedSave(staged, op.LevelSavePath);
            Advance(journal, "Encoded", "Modified JSON encoded to a staged Level.sav and passed size validation.");

            var verifyJsonPath = await codec.DecodeAsync(converterMatch, staged, Path.Combine(working, "Level.verify.json"), cancellationToken);
            var verifyRoot = JsonNode.Parse(File.ReadAllText(verifyJsonPath)) ?? throw new InvalidDataException("Verification decode was empty.");
            var verifyRawData = FindGuildRawData(verifyRoot, op.GuildId)
                ?? throw new InvalidDataException("Verification could not find the guild in the repaired save.");
            VerifyMutation(op.OperationType, verifyRawData, op.PlayerId);
            Advance(journal, "Verified", "Staged save independently decoded and the change was verified.");

            var replacement = op.LevelSavePath + $".mysttiq-guild-ownership-{id}.tmp";
            File.Copy(staged, replacement, true);
            File.Replace(replacement, op.LevelSavePath, null, true);
            if (!File.Exists(op.LevelSavePath) || new FileInfo(op.LevelSavePath).Length < 1024)
                throw new InvalidDataException("The active Level.sav failed final validation after replacement.");

            Advance(journal, "Committed", $"Verified save committed atomically to the active world. Result hash: {HashFile(op.LevelSavePath)}");
            activity.Record("Information", "Guild Ownership", $"Applied {DisplayName(op.OperationType)}",
                $"guild={op.GuildId}; player={op.PlayerId}; backup={safety.FileName}");
            coordinator.Complete(operation.Id, DescribeSuccess(op.OperationType, op.PlayerName, op.GuildId));
            return new HeadlessGuildOwnershipResult(true, id, "Committed", safety.FileName, false, journal.JournalPath,
                DescribeSuccess(op.OperationType, op.PlayerName, op.GuildId));
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
                activity.Record("Warning", "Guild Ownership", $"{DisplayName(op?.OperationType ?? GuildOwnershipOperationType.ClaimOrphanedGuild)} failed",
                    $"rolledBack={rolledBack}; error={ex.GetType().Name}");
            }
            if (operation is not null) coordinator.Fail(operation.Id, ex.Message, rolledBack);
            return new HeadlessGuildOwnershipResult(false, journal?.TransactionId, rolledBack ? "RolledBack" : "Failed",
                journal?.SafetyBackup, rolledBack, journal?.JournalPath, ex.Message);
        }
        finally
        {
            operation?.Dispose();
            if (working is not null) try { Directory.Delete(working, true); } catch { }
            gate.Release();
        }
    }

    private static void ApplyMutation(GuildOwnershipOperationType operationType, JsonObject rawData, string playerId, string playerName)
    {
        switch (operationType)
        {
            case GuildOwnershipOperationType.ClaimOrphanedGuild:
            case GuildOwnershipOperationType.TransferLeadership:
                SetScalarProperty(rawData, "admin_player_uid", playerId);
                EnsureMember(rawData, playerId, playerName);
                break;
            case GuildOwnershipOperationType.AddPlayerToGuild:
                EnsureMember(rawData, playerId, playerName);
                break;
            case GuildOwnershipOperationType.RemoveBrokenMember:
                RemoveMember(rawData, playerId);
                break;
        }
    }

    private static void VerifyMutation(GuildOwnershipOperationType operationType, JsonObject rawData, string playerId)
    {
        switch (operationType)
        {
            case GuildOwnershipOperationType.ClaimOrphanedGuild:
            case GuildOwnershipOperationType.TransferLeadership:
                var leader = Normalize(ReadScalar(GetProperty(rawData, "admin_player_uid")));
                if (!leader.Equals(Normalize(playerId), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Verification did not find the target player as the repaired guild's leader.");
                break;
            case GuildOwnershipOperationType.AddPlayerToGuild:
                if (!ContainsMember(rawData, playerId))
                    throw new InvalidDataException("Verification did not find the target player among the repaired guild's members.");
                break;
            case GuildOwnershipOperationType.RemoveBrokenMember:
                if (ContainsMember(rawData, playerId))
                    throw new InvalidDataException("Verification still found the broken member reference in the repaired save.");
                break;
        }
    }

    private static string DisplayName(GuildOwnershipOperationType type) => type switch
    {
        GuildOwnershipOperationType.ClaimOrphanedGuild => "Claim Orphaned Guild",
        GuildOwnershipOperationType.TransferLeadership => "Transfer Leadership",
        GuildOwnershipOperationType.AddPlayerToGuild => "Add Player to Guild",
        GuildOwnershipOperationType.RemoveBrokenMember => "Remove Broken Member",
        _ => type.ToString()
    };

    private static string DescribeReady(GuildOwnershipOperationType type, HeadlessGuildExplorerItem guild, HeadlessPlayerExplorerItem player) => type switch
    {
        GuildOwnershipOperationType.ClaimOrphanedGuild => $"Ready: {player.PlayerName} ({player.PlayerId}) will become leader of {guild.GuildName}, preserving the guild ID and its {guild.BaseCount} base(s).",
        GuildOwnershipOperationType.TransferLeadership => $"Ready: leadership of {guild.GuildName} will transfer from {guild.LeaderName} to {player.PlayerName} ({player.PlayerId}).",
        GuildOwnershipOperationType.AddPlayerToGuild => $"Ready: {player.PlayerName} ({player.PlayerId}) will be added to {guild.GuildName} as a member.",
        GuildOwnershipOperationType.RemoveBrokenMember => $"Ready: the dangling member reference {player.PlayerName} ({player.PlayerId}) -- no matching save file -- will be removed from {guild.GuildName}.",
        _ => "Ready."
    };

    private static string DescribeMutation(GuildOwnershipOperationType type, string playerName, string guildId) => type switch
    {
        GuildOwnershipOperationType.ClaimOrphanedGuild => $"{playerName} set as leader of guild {guildId}.",
        GuildOwnershipOperationType.TransferLeadership => $"{playerName} set as leader of guild {guildId}.",
        GuildOwnershipOperationType.AddPlayerToGuild => $"{playerName} added as a member of guild {guildId}.",
        GuildOwnershipOperationType.RemoveBrokenMember => $"Broken member reference {playerName} removed from guild {guildId}.",
        _ => "Guild updated."
    };

    private static string DescribeSuccess(GuildOwnershipOperationType type, string playerName, string guildId) => type switch
    {
        GuildOwnershipOperationType.ClaimOrphanedGuild => $"{playerName} is now the leader of guild {guildId}.",
        GuildOwnershipOperationType.TransferLeadership => $"Leadership of guild {guildId} transferred to {playerName}.",
        GuildOwnershipOperationType.AddPlayerToGuild => $"{playerName} was added to guild {guildId}.",
        GuildOwnershipOperationType.RemoveBrokenMember => $"The broken member reference for {playerName} was removed from guild {guildId}.",
        _ => "Guild ownership change applied."
    };

    private HeadlessWorldTransactionJournal NewJournal(string id, GuildOwnershipOperationType operationType, string guildId)
    {
        var mode = operationType switch
        {
            GuildOwnershipOperationType.ClaimOrphanedGuild => "guild-ownership-claim",
            GuildOwnershipOperationType.TransferLeadership => "guild-ownership-transfer",
            GuildOwnershipOperationType.AddPlayerToGuild => "guild-ownership-add-player",
            GuildOwnershipOperationType.RemoveBrokenMember => "guild-ownership-remove-broken-member",
            _ => "guild-ownership"
        };
        var path = Path.Combine(paths.ManagerRuntimeRoot, "world-transactions", "journals", $"transaction-{id}.json");
        return new HeadlessWorldTransactionJournal
        {
            TransactionId = id, Mode = mode, WorldId = guildId, State = "Created",
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

    // --- Mutable (JsonNode) GroupSaveDataMap traversal, mirroring the read-only
    // structural understanding HeadlessPlayerGuildExplorerService already uses. ---

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

    private static void EnsureMember(JsonObject rawData, string playerId, string? playerName)
    {
        var members = GetMembers(rawData);
        var exists = members.OfType<JsonObject>().Any(member =>
            Normalize(ReadScalar(GetProperty(member, "player_uid"))).Equals(Normalize(playerId), StringComparison.OrdinalIgnoreCase));
        if (!exists)
            // Mirrors the real palworld-save-tools/PlM group.py "players" struct schema: the
            // binary writer requires player_info.last_online_real_time and a role to be present,
            // not just player_uid/player_name. 0 is used for last_online_real_time because this
            // member is being added administratively, not from an observed login. role 2 matches
            // an ordinary (non-leader) member in existing save data; ClaimOrphanedGuild/
            // TransferLeadership additionally set admin_player_uid separately for leadership.
            members.Add(new JsonObject
            {
                ["player_uid"] = playerId,
                ["player_info"] = new JsonObject
                {
                    ["last_online_real_time"] = 0L,
                    ["player_name"] = playerName ?? playerId
                },
                ["role"] = 2
            });
    }

    private static void RemoveMember(JsonObject rawData, string playerId)
    {
        var members = GetMembers(rawData);
        var normalized = Normalize(playerId);
        for (var i = members.Count - 1; i >= 0; i--)
        {
            if (members[i] is JsonObject member &&
                Normalize(ReadScalar(GetProperty(member, "player_uid"))).Equals(normalized, StringComparison.OrdinalIgnoreCase))
                members.RemoveAt(i);
        }
    }

    private static bool ContainsMember(JsonObject rawData, string playerId) =>
        GetMembers(rawData).OfType<JsonObject>().Any(member =>
            Normalize(ReadScalar(GetProperty(member, "player_uid"))).Equals(Normalize(playerId), StringComparison.OrdinalIgnoreCase));

    private static JsonArray GetMembers(JsonObject rawData)
    {
        var membersNode = GetProperty(rawData, "players");
        if (membersNode is JsonArray array) return array;
        if (membersNode is JsonObject wrapper && GetProperty(wrapper, "value") is JsonArray wrapped) return wrapped;
        var created = new JsonArray();
        rawData["players"] = created;
        return created;
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

    private sealed record PendingOperation(GuildOwnershipOperationType OperationType, string GuildId, string PlayerId,
        string PlayerName, string LevelSavePath, string SourceHash, DateTimeOffset ExpiresUtc);
}

public enum GuildOwnershipOperationType { ClaimOrphanedGuild, TransferLeadership, AddPlayerToGuild, RemoveBrokenMember }

public sealed record HeadlessGuildOwnershipPreview(bool CanApply, string PreviewToken, string OperationType, string GuildId,
    string GuildName, string PlayerId, string PlayerName, IReadOnlyList<string> Findings, DateTimeOffset ExpiresUtc);
public sealed record HeadlessGuildOwnershipPreviewRequest(string OperationType, string GuildId, string PlayerId);
public sealed record HeadlessGuildOwnershipApplyRequest(string PreviewToken, bool Confirmed);
public sealed record HeadlessGuildOwnershipResult(bool Success, string? TransactionId, string State, string? SafetyBackup,
    bool RolledBack, string? JournalPath, string Message)
{
    public static HeadlessGuildOwnershipResult Failure(string message) => new(false, null, "Rejected", null, false, null, message);
}
