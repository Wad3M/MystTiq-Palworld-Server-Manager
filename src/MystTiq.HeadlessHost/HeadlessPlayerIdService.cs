// MystTiq v1.1.5.1: file reviewed for this release (2026-10-26).
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MystTiq.Core.Models;
using MystTiq.Core.Operations;
using MystTiq.Core.Providers;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.1.0.2 (roadmap F-2), owner request 2026-10-13: "write a test for the MystTiq to run when a user connects to ensure
/// that the correct ID is calculated", and when a player is not given their character "the system should automatically
/// migrate to the new user, backing up and cleaning up the list".
/// <list type="bullet">
/// <item><b>Join check.</b> (v1.1.1.0: every account, Steam or not.) Every Steam player's join is checked once per session: the player ID the game assigned against
/// the ID their character lives under (learned from earlier joins, else the pre-1.0 Steam formula when that character
/// exists). Each check is recorded (correct, first seen, given a new ID by the game, moved).</item>
/// <item><b>Learned IDs.</b> The ID each account's character lives under is kept per account, so the identity guard
/// compares with the game's own assignment instead of a formula the game no longer uses.</item>
/// <item><b>Automatic move.</b> An account given a new ID while its character lives under the old one is queued; when
/// nobody is online the server is stopped, a safety backup taken, the character moved (<see cref="PlayerIdMove"/>),
/// the result decoded again and verified, the old character file removed (v1.1.0.3: the safety backup holds it), the
/// account's level history cleared, and the server started again. Anything unexpected refuses the move and alerts.</item>
/// </list>
/// </summary>
public sealed class HeadlessPlayerIdService
{
    private const int MaximumChecks = 200;
    private readonly IServerPathProfile paths;
    private readonly IServerLifecycleService lifecycle;
    private readonly HeadlessBackupService backups;
    private readonly HeadlessActivityLogService activity;
    private readonly HeadlessNotificationService notifications;
    private readonly HeadlessSaveCodecService codec;
    private readonly IOperationCoordinator coordinator;
    private readonly ServerProfileId profile;
    private readonly Func<CancellationToken, Task<ServerLifecycleOperationResult>> startServer;
    private readonly Func<CancellationToken, Task<ServerLifecycleOperationResult>> stopServer;
    private readonly Action<string[]> forgetLevelHistory;
    private readonly HeadlessPlayerRegistryService? registry;
    private readonly string ledgerPath;
    private readonly object gate = new();
    private readonly HashSet<string> checkedThisSession = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim moving = new(1, 1);
    private PlayerIdLedger ledger;

    public HeadlessPlayerIdService(IServerPathProfile paths, IServerLifecycleService lifecycle, HeadlessBackupService backups,
        HeadlessActivityLogService activity, HeadlessNotificationService notifications, HeadlessSaveCodecService codec,
        IOperationCoordinator coordinator, ServerProfileId profile,
        Func<CancellationToken, Task<ServerLifecycleOperationResult>> startServer,
        Func<CancellationToken, Task<ServerLifecycleOperationResult>> stopServer,
        Action<string[]>? forgetLevelHistory = null, HeadlessPlayerRegistryService? registry = null)
    {
        this.paths = paths; this.lifecycle = lifecycle; this.backups = backups; this.activity = activity;
        this.notifications = notifications; this.codec = codec; this.coordinator = coordinator; this.profile = profile;
        this.startServer = startServer; this.stopServer = stopServer;
        this.forgetLevelHistory = forgetLevelHistory ?? (_ => { });
        this.registry = registry;
        var root = Path.Combine(paths.ManagerRuntimeRoot, "players");
        Directory.CreateDirectory(root);
        ledgerPath = Path.Combine(root, "player-ids.json");
        ledger = SavedFiles.Load(ledgerPath, "player ID ledger", () => new PlayerIdLedger());
    }

    public PlayerIdLedger Snapshot() { lock (gate) return ledger with { Known = new(ledger.Known, StringComparer.OrdinalIgnoreCase), Checks = [.. ledger.Checks], Pending = [.. ledger.Pending] }; }

    public bool AutoMove { get { lock (gate) return ledger.AutoMove; } }

    public PlayerIdLedger SaveSettings(bool autoMove)
    {
        lock (gate) { ledger = ledger with { AutoMove = autoMove }; Persist(); }
        activity.Record("Information", "Players", "Player IDs", $"Automatic character move {(autoMove ? "on" : "off")}.");
        return Snapshot();
    }

    /// <summary>
    /// The ID this account's character lives under: learned, else the pre-1.0 Steam formula, else (v1.1.1.0) the player
    /// list; null if unknown.
    /// </summary>
    public string? ExpectedFor(string userId) => KnownCharacterFor(userId) ?? SteamPlayerUid.FromUserId(userId);

    // The ID of the character this account already has: learned from an earlier join, else the pre-1.0 Steam formula when
    // that character exists, else (v1.1.1.0) the player list. The list covers accounts with no formula (Xbox and other
    // non-Steam gdk_ accounts) and accounts seen before MystTiq learned their ID; with several, the earliest-seen
    // character is the account's own (a later one is what the game's new ID made).
    private string? KnownCharacterFor(string userId)
    {
        string? learned;
        lock (gate) ledger.Known.TryGetValue(userId, out learned);
        if (learned is not null && CharacterExists(learned)) return learned;
        var formula = SteamPlayerUid.FromUserId(userId);
        if (formula is not null && CharacterExists(formula)) return formula;
        return registry?.Snapshot()
            .Where(r => r.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.FirstSeenUtc)
            .Select(r => NormalizeId(r.PlayerId))
            .FirstOrDefault(id => id is not null && CharacterExists(id));
    }

    /// <summary>
    /// v1.1.1.0: links an account to the character that is theirs, for when MystTiq cannot tell (an account whose ID the
    /// game changed before MystTiq saw it). When the account was last given another ID that has a character, that ID's
    /// character is replaced by this one with the usual move (queued; run when nobody is online, or with Restart server
    /// and move character); otherwise the next join is checked against it.
    /// </summary>
    public PlayerIdMoveResult Link(string userId, string playerId)
    {
        userId = (userId ?? string.Empty).Trim();
        var id = NormalizeId(playerId);
        if (userId.Length == 0) return PlayerIdMoveResult.Failure("Enter the account's user ID (for example steam_7656… or gdk_…).");
        if (id is null) return PlayerIdMoveResult.Failure("Enter the character's player ID: 32 letters and digits (0-9, A-F).");
        if (!CharacterExists(id)) return PlayerIdMoveResult.Failure($"No world has a character file for {id[..8]}.");
        string message;
        lock (gate)
        {
            var last = ledger.Checks.LastOrDefault(c => c.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase));
            var name = last?.Name ?? userId;
            var current = last?.Assigned;
            ledger.Known[userId] = id;
            ledger.Pending.RemoveAll(p => p.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase));
            if (current is not null && !current.Equals(id, StringComparison.OrdinalIgnoreCase) && CharacterExists(current))
            {
                ledger.Pending.Add(new PlayerIdMoveRequest(userId, name, id, current, DateTimeOffset.UtcNow));
                message = $"{name} ({userId}) is linked to {id[..8]}. The game last gave them {current[..8]}, so that character is replaced by this one " +
                    (ledger.AutoMove ? "when nobody is online (a safety backup is taken first)." : "with Restart server and move character (a safety backup is taken first).");
            }
            else
                message = $"{name} ({userId}) is linked to {id[..8]}. Their next join is checked against it.";
            checkedThisSession.Remove(userId);
            Persist();
        }
        activity.Record("Information", "Players", "Player IDs", message);
        return new PlayerIdMoveResult(true, message);
    }

    /// <summary>Kick text for a player whose character is waiting to be moved.</summary>
    public string KickReason(string userId)
    {
        lock (gate)
            return ledger.AutoMove && ledger.Pending.Any(p => p.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                ? "MystTiq: your character is being moved to your new player ID. Join again in a few minutes."
                : "MystTiq: the server did not load your character. Please wait for the administrator and join again.";
    }

    /// <summary>The join check: records each Steam player once per session and queues a move when the game gave a new ID.</summary>
    public IReadOnlyList<PlayerIdCheck> Observe(HeadlessPlayersSnapshot players)
    {
        var recorded = new List<PlayerIdCheck>();
        if (!players.Available) return recorded;
        var online = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var player in players.Players)
        {
            var userId = (player.UserId ?? string.Empty).Trim();
            if (userId.Length == 0) continue;
            online.Add(userId);
            var assigned = NormalizeId(player.PlayerId);
            if (assigned is null) continue;  // still loading or on the new-character screen: checked once a character exists
            lock (gate) if (!checkedThisSession.Add(userId)) continue;

            var known = KnownCharacterFor(userId);
            var check = PlayerIdJudge.Judge(userId, assigned, known);
            var name = string.IsNullOrWhiteSpace(player.Name) ? userId : player.Name;
            var entry = new PlayerIdCheck(DateTimeOffset.UtcNow, name, userId, assigned, known ?? assigned, check);
            lock (gate)
            {
                if (check is PlayerIdResult.Correct or PlayerIdResult.FirstSeen) ledger.Known[userId] = assigned;
                if (check == PlayerIdResult.NewIdFromGame && !ledger.Pending.Any(p => p.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase)))
                    ledger.Pending.Add(new PlayerIdMoveRequest(userId, name, known!, assigned, DateTimeOffset.UtcNow));
                ledger.Checks.Add(entry);
                if (ledger.Checks.Count > MaximumChecks) ledger.Checks.RemoveRange(0, ledger.Checks.Count - MaximumChecks);
                Persist();
            }
            recorded.Add(entry);
            if (check == PlayerIdResult.NewIdFromGame)
            {
                var message = $"{name} ({userId}) was given a new player ID by the game ({assigned[..8]}) while their character is under {known![..8]}. " +
                    (AutoMove ? "MystTiq moves the character to the new ID when nobody is online, with a safety backup first." : "Move it on Players > Player IDs.");
                activity.Record("Warning", "Players", "Player ID check", message);
                notifications.Create("Critical", "Player given a new ID by the game", message, pinned: true);
            }
        }
        lock (gate) checkedThisSession.RemoveWhere(id => !online.Contains(id));
        return recorded;
    }

    /// <summary>Starts the queued moves in the background when automatic moves are on and nobody is online.</summary>
    public void RunPendingWhenIdle(HeadlessPlayersSnapshot players)
    {
        bool due;
        lock (gate) due = ledger.AutoMove && ledger.Pending.Count > 0;
        if (!due || !players.Available || players.OnlineCount > 0 || moving.CurrentCount == 0) return;
        _ = BackgroundWork.Shared.Run("player ID move", token => RunPendingAsync(null, token));
    }

    /// <summary>Stops the server if it runs, moves every queued character (or only <paramref name="userId"/>'s), starts it again.</summary>
    public async Task<PlayerIdMoveResult> RunPendingAsync(string? userId, CancellationToken cancellationToken)
    {
        if (!await moving.WaitAsync(0, cancellationToken)) return PlayerIdMoveResult.Failure("A character move is already running.");
        OperationHandle? lifecycleLock = null;
        try
        {
            List<PlayerIdMoveRequest> queue;
            lock (gate) queue = ledger.Pending.Where(p => userId is null || p.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (queue.Count == 0) return PlayerIdMoveResult.Failure(userId is null ? "No character is waiting to be moved." : $"No move is waiting for {userId}.");

            lifecycleLock = await coordinator.BeginAsync(profile, "player-id-move", nameof(HeadlessPlayerIdService), ["lifecycle"], cancellationToken);
            var wasRunning = (await lifecycle.GetStatusAsync(cancellationToken)).ServerMayBeRunning;
            if (wasRunning)
            {
                var stopped = await stopServer(cancellationToken);
                if (!stopped.Success) throw new InvalidOperationException("The server could not be stopped for the move: " + stopped.Message);
            }
            var results = new List<string>();
            var allMoved = true;
            foreach (var request in queue)
            {
                var result = await MoveAsync(request, cancellationToken);
                results.Add(result.Message);
                allMoved &= result.Success;
            }
            if (wasRunning)
            {
                var started = await startServer(cancellationToken);
                results.Add(started.Success ? "The server was started again." : "The server did not start again: " + started.Message);
            }
            coordinator.Complete(lifecycleLock.Id, string.Join(" ", results));
            return new PlayerIdMoveResult(allMoved, string.Join(" ", results));
        }
        catch (Exception ex)
        {
            if (lifecycleLock is not null) coordinator.Fail(lifecycleLock.Id, ex.Message);
            activity.Record("Warning", "Players", "Character move", ex.Message);
            notifications.Create("Critical", "Character move did not run", ex.Message, pinned: true);
            return PlayerIdMoveResult.Failure(ex.Message);
        }
        finally
        {
            lifecycleLock?.Dispose();
            moving.Release();
        }
    }

    /// <summary>One move as a transaction (the server must be stopped).</summary>
    private async Task<PlayerIdMoveResult> MoveAsync(PlayerIdMoveRequest request, CancellationToken cancellationToken)
    {
        OperationHandle? operation = null;
        HeadlessWorldTransactionJournal? journal = null;
        string? working = null, levelPath = null, levelOriginal = null, toPath = null, toOriginal = null, fromPath = null, fromOriginal = null;
        var replaced = false;
        try
        {
            if ((await lifecycle.GetStatusAsync(cancellationToken)).ServerMayBeRunning) throw new InvalidOperationException("PalServer is still running.");
            var world = WorldOf(request.FromPlayerId) ?? throw new InvalidOperationException($"No world has a character file for {request.FromPlayerId[..8]}.");
            levelPath = Path.Combine(world, "Level.sav");
            fromPath = Path.Combine(world, "Players", request.FromPlayerId + ".sav");
            toPath = Path.Combine(world, "Players", request.ToPlayerId + ".sav");

            operation = await coordinator.BeginAsync(profile, "player-id-move", nameof(HeadlessPlayerIdService), ["world-mutation"], cancellationToken);
            var id = Guid.NewGuid().ToString("N");
            journal = new HeadlessWorldTransactionJournal
            {
                TransactionId = id, Mode = "player-id-move", WorldId = $"{request.FromPlayerId}->{request.ToPlayerId}", State = "Created",
                CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow,
                JournalPath = Path.Combine(paths.ManagerRuntimeRoot, "world-transactions", "journals", $"transaction-{id}.json")
            };
            Advance(journal, "Accepted", $"{request.Name} ({request.UserId}): {request.FromPlayerId[..8]} -> {request.ToPlayerId[..8]}.");

            var safety = await backups.CreateAsync(BackupClass.Safety, cancellationToken);
            if (!safety.Success || string.IsNullOrWhiteSpace(safety.FileName)) throw new InvalidOperationException("Fresh safety backup failed: " + safety.Message);
            journal.SafetyBackup = safety.FileName;
            Advance(journal, "SafetyBackupCreated", $"Fresh safety backup: {safety.FileName}");

            var (converter, converterDetail) = await codec.ResolveConverterAsync(levelPath, cancellationToken);
            if (converter is null) throw new InvalidOperationException("Save codec unavailable: " + converterDetail);
            working = Path.Combine(paths.ManagerRuntimeRoot, "player-id-move", id);
            Directory.CreateDirectory(working);
            levelOriginal = Path.Combine(working, "Level.original.sav");
            File.Copy(levelPath, levelOriginal, true);
            if (File.Exists(toPath)) { toOriginal = Path.Combine(working, "To.original.sav"); File.Copy(toPath, toOriginal, true); }
            fromOriginal = Path.Combine(working, "From.original.sav");
            File.Copy(fromPath, fromOriginal, true);

            var level = await DecodeAsync(converter, levelOriginal, Path.Combine(working, "Level.json"), cancellationToken);
            var player = await DecodeAsync(converter, fromPath, Path.Combine(working, "From.json"), cancellationToken);
            var plan = PlayerIdMove.Check(level, request.FromPlayerId, request.ToPlayerId);
            if (!plan.CanMove) throw new InvalidOperationException(plan.Problem);
            var worldReferences = PlayerIdMove.MoveWorld(level, request.FromPlayerId, request.ToPlayerId);
            var fileReferences = PlayerIdMove.MoveCharacterFile(player, request.FromPlayerId, request.ToPlayerId);
            var characters = plan.Characters - plan.ThrowawayBodies;
            Advance(journal, "Staged", $"Removed the character under the new ID ({plan.ThrowawayBodies} character, {plan.ThrowawayGuilds} guild); rewrote {worldReferences} world references and {fileReferences} in the character file.");

            var stagedLevel = await EncodeAsync(converter, level, Path.Combine(working, "Level.moved"), cancellationToken);
            var stagedPlayer = await EncodeAsync(converter, player, Path.Combine(working, "To.moved"), cancellationToken);
            var problem = PlayerIdMove.Verify(
                await DecodeAsync(converter, stagedLevel, Path.Combine(working, "Level.verify.json"), cancellationToken),
                await DecodeAsync(converter, stagedPlayer, Path.Combine(working, "To.verify.json"), cancellationToken),
                request.FromPlayerId, request.ToPlayerId, worldReferences, characters, fileReferences);
            if (problem is not null) throw new InvalidDataException(problem);
            Advance(journal, "Verified", "The staged world and character file were decoded again and checked.");

            replaced = true;
            File.Copy(stagedLevel, levelPath + $".mysttiq-player-id-{id}.tmp", true);
            File.Replace(levelPath + $".mysttiq-player-id-{id}.tmp", levelPath, null, true);
            File.Copy(stagedPlayer, toPath, true);
            // v1.1.0.3 (roadmap F-3, owner 2026-10-13: "it should cleanup the old files so as to not cause possible
            // issues"): the old character file is removed once the world and the moved file are in place; the safety
            // backup taken first holds it and the replaced one. Nothing is kept beside the world any more.
            File.Delete(fromPath);
            var companion = Path.Combine(world, "Players", request.FromPlayerId + "_dps.sav");
            if (File.Exists(companion)) File.Delete(companion);
            Advance(journal, "Committed", $"Moved. The old character file was removed; the safety backup {safety.FileName} holds it and the replaced one.");

            lock (gate)
            {
                ledger.Known[request.UserId] = request.ToPlayerId;
                ledger.Pending.RemoveAll(p => p.UserId.Equals(request.UserId, StringComparison.OrdinalIgnoreCase));
                ledger.Checks.Add(new PlayerIdCheck(DateTimeOffset.UtcNow, request.Name, request.UserId, request.ToPlayerId, request.FromPlayerId, PlayerIdResult.Moved));
                Persist();
            }
            forgetLevelHistory([request.FromPlayerId, request.ToPlayerId]);
            registry?.MergeInto(request.FromPlayerId, request.ToPlayerId);
            var message = $"{request.Name}'s character was moved from {request.FromPlayerId[..8]} to {request.ToPlayerId[..8]} (safety backup {safety.FileName}).";
            activity.Record("Information", "Players", "Character moved", message);
            notifications.Create("Information", "Character moved to the new player ID", message);
            coordinator.Complete(operation.Id, message);
            return new PlayerIdMoveResult(true, message);
        }
        catch (Exception ex)
        {
            var rolledBack = false;
            if (replaced)
            {
                try
                {
                    if (levelOriginal is not null && levelPath is not null) File.Copy(levelOriginal, levelPath, true);
                    if (toPath is not null) { if (toOriginal is not null) File.Copy(toOriginal, toPath, true); else if (File.Exists(toPath)) File.Delete(toPath); }
                    if (fromPath is not null && fromOriginal is not null && !File.Exists(fromPath)) File.Copy(fromOriginal, fromPath, true);
                    rolledBack = true;
                }
                catch { rolledBack = false; }
            }
            if (journal is not null) { journal.RolledBack = rolledBack; Advance(journal, rolledBack ? "RolledBack" : "Failed", ex.Message); }
            if (operation is not null) coordinator.Fail(operation.Id, ex.Message, rolledBack);
            var message = $"{request.Name}'s character was not moved: {ex.Message}";
            activity.Record("Warning", "Players", "Character move", message);
            notifications.Create("Critical", "Character move refused", message, pinned: true);
            return PlayerIdMoveResult.Failure(message);
        }
        finally
        {
            operation?.Dispose();
            if (working is not null) try { Directory.Delete(working, true); } catch { }
        }
    }

    /// <summary>
    /// v1.1.0.3 (roadmap F-3): removes what earlier moves left behind. v1.1.0.2 kept the old and replaced character files
    /// in runtime\character-migration\archive; each is removed when a MystTiq backup holds that character file (it can be
    /// restored from there). The player list's records naming no character are merged into the account's current ID.
    /// Returns how many files and records were cleared.
    /// </summary>
    public int CleanUpOldEntries()
    {
        var cleared = 0;
        var archive = Path.Combine(paths.ManagerRuntimeRoot, "character-migration", "archive");
        try
        {
            if (Directory.Exists(archive))
            {
                var held = PlayerFilesInBackups();
                foreach (var file in Directory.EnumerateFiles(archive, "*.sav"))
                {
                    var name = Path.GetFileName(file);
                    var playerId = name.Length >= 32 ? name[..32] : string.Empty;
                    if (NormalizeId(playerId) is null || !held.Contains(playerId)) continue;
                    File.Delete(file);
                    cleared++;
                }
                if (!Directory.EnumerateFileSystemEntries(archive).Any()) Directory.Delete(archive);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { activity.Record("Warning", "Players", "Player ID clean-up", ex.Message); }
        if (registry is not null)
        {
            Dictionary<string, string> current;
            lock (gate) current = new(ledger.Known, StringComparer.OrdinalIgnoreCase);
            cleared += registry.CleanUp(CharacterExists, current);
        }
        if (cleared > 0) activity.Record("Information", "Players", "Player ID clean-up", $"Cleared {cleared} old character file(s) and player list record(s) that named no character.");
        return cleared;
    }

    // The player IDs whose character file is held in a MystTiq backup (a ZIP under the backup folder).
    private HashSet<string> PlayerFilesInBackups()
    {
        var held = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(paths.BackupRoot)) return held;
        foreach (var zip in Directory.EnumerateFiles(paths.BackupRoot, "*.zip", SearchOption.AllDirectories))
        {
            try
            {
                using var archive = System.IO.Compression.ZipFile.OpenRead(zip);
                foreach (var entry in archive.Entries)
                {
                    var parts = entry.FullName.Replace('\\', '/').Split('/');
                    if (parts.Length >= 2 && parts[^2].Equals("Players", StringComparison.OrdinalIgnoreCase) && entry.Name.EndsWith(".sav", StringComparison.OrdinalIgnoreCase))
                        held.Add(Path.GetFileNameWithoutExtension(entry.Name));
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { /* an unreadable backup holds nothing */ }
        }
        return held;
    }

    private async Task<JsonNode> DecodeAsync(HeadlessSaveConverterMatch converter, string sav, string json, CancellationToken token)
    {
        var path = await codec.DecodeAsync(converter, sav, json, token);
        return JsonNode.Parse(File.ReadAllText(path)) ?? throw new InvalidDataException($"Decoded {Path.GetFileName(sav)} is empty.");
    }

    private async Task<string> EncodeAsync(HeadlessSaveConverterMatch converter, JsonNode node, string stem, CancellationToken token)
    {
        File.WriteAllText(stem + ".json", node.ToJsonString());
        var staged = await codec.EncodeAsync(converter, stem + ".json", stem + ".sav", token);
        if (!File.Exists(staged) || new FileInfo(staged).Length < 64) throw new InvalidDataException($"Encoded {Path.GetFileName(staged)} failed size validation.");
        return staged;
    }

    /// <summary>A 32-hex player ID in upper case, or null for "None" / empty.</summary>
    public static string? NormalizeId(string? playerId)
    {
        var id = (playerId ?? string.Empty).Replace("-", string.Empty).Trim();
        return id.Length == 32 && id.All(Uri.IsHexDigit) ? id.ToUpperInvariant() : null;
    }

    private bool CharacterExists(string playerId) => WorldOf(playerId) is not null;

    // The world folder holding Players/<playerId>.sav (never MystTiq's backup copies).
    private string? WorldOf(string playerId)
    {
        try
        {
            if (!Directory.Exists(paths.SaveRoot)) return null;
            var file = Directory.EnumerateFiles(paths.SaveRoot, playerId + ".sav", SearchOption.AllDirectories)
                .FirstOrDefault(f => string.Equals(Path.GetFileName(Path.GetDirectoryName(f)), "Players", StringComparison.OrdinalIgnoreCase) &&
                                     !f.Contains(Path.DirectorySeparatorChar + "backup" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            return file is null ? null : Path.GetDirectoryName(Path.GetDirectoryName(file));
        }
        catch { return null; }
    }

    private void Persist()
    {
        try { SavedFiles.SaveText(ledgerPath, JsonSerializer.Serialize(ledger)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* kept in memory; written on the next change */ }
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
}

[JsonConverter(typeof(JsonStringEnumConverter<PlayerIdResult>))]
public enum PlayerIdResult { Correct, FirstSeen, NewIdFromGame, Moved }

/// <summary>v1.1.0.2: the join check's rule, pure so the logic harness covers it.</summary>
public static class PlayerIdJudge
{
    /// <summary>
    /// <paramref name="known"/> is the ID the account's character lives under (null: no character yet). The same ID is
    /// correct; no known character means a first sighting (a new player, or one already moved); a different ID while the
    /// known character exists means the game gave the account a new ID.
    /// </summary>
    public static PlayerIdResult Judge(string userId, string assigned, string? known) =>
        known is null ? PlayerIdResult.FirstSeen
        : string.Equals(assigned, known, StringComparison.OrdinalIgnoreCase) ? PlayerIdResult.Correct
        : PlayerIdResult.NewIdFromGame;
}

public sealed record PlayerIdCheck(DateTimeOffset At, string Name, string UserId, string Assigned, string Expected, PlayerIdResult Result);
public sealed record PlayerIdMoveRequest(string UserId, string Name, string FromPlayerId, string ToPlayerId, DateTimeOffset Since);
public sealed record PlayerIdLedger
{
    public bool AutoMove { get; init; } = true;
    public Dictionary<string, string> Known { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<PlayerIdCheck> Checks { get; init; } = [];
    public List<PlayerIdMoveRequest> Pending { get; init; } = [];
}
public sealed record PlayerIdMoveResult(bool Success, string Message)
{
    public static PlayerIdMoveResult Failure(string message) => new(false, message);
}
public sealed record PlayerIdSettingsRequest(bool AutoMove);
public sealed record PlayerIdMoveCommand(string UserId);
public sealed record PlayerIdLinkRequest(string UserId, string PlayerId);
