// MystTiq v1.0.4.0: file reviewed for this release (2026-10-05).
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MystTiq.Core.Models;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed record InventoryEditRequest(string PlayerId, string Action, string ItemId, int Count = 1, int? SlotIndex = null);
public sealed record InventoryView(bool Available, string PlayerId, string? ContainerId, int Capacity, IReadOnlyList<InventoryEdits.Slot> Slots, string Detail);
public sealed record InventoryEditPreview(bool CanApply, string? Token, IReadOnlyList<string> Findings, string Summary, DateTimeOffset? ExpiresUtc);
public sealed record InventoryEditApplyRequest(string Token, bool Confirmed);
public sealed record InventoryEditResult(bool Success, string State, string? SafetyBackup, bool RolledBack, string? JournalPath, string Message);

/// <summary>
/// v1.0.4.0 (roadmap S-1 remove an item, S-2 add an item; owner decision D-2: guarded edits only). The same guarded shape
/// as every MystTiq world edit (HeadlessPalEditService): a single-use preview, PalServer stopped, the world unchanged since
/// the preview, a fresh safety backup that is checked before anything is touched, decode, the one change (InventoryEdits),
/// encode, an independent decode that must show exactly that change and nothing else in the container, then an atomic
/// replace; any failure puts the original back. Only Level.sav changes; the player's own save is only read, for the id
/// of their main inventory.
/// </summary>
public sealed class HeadlessInventoryEditService
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
    private readonly Dictionary<string, Pending> pending = new(StringComparer.Ordinal);

    private sealed record Pending(InventoryEditRequest Request, string LevelSavePath, string PlayerSavePath, string SourceHash, DateTimeOffset ExpiresUtc);

    public HeadlessInventoryEditService(IServerPathProfile paths, IServerLifecycleService lifecycle, HeadlessBackupService backups,
        HeadlessActivityLogService activity, HeadlessPlayerGuildExplorerService explorer, HeadlessSaveCodecService codec,
        IOperationCoordinator coordinator, ServerProfileId profile)
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

    public async Task<InventoryView> GetInventoryAsync(string playerId, CancellationToken cancellationToken)
    {
        var located = await LocateAsync(playerId, cancellationToken);
        if (located.Problem is not null) return new(false, playerId, null, 0, [], located.Problem);
        var working = NewWorking("view");
        try
        {
            var (level, player) = await DecodeBothAsync(located.Converter!, located.LevelSavePath!, located.PlayerSavePath!, working, cancellationToken);
            var containerId = InventoryEdits.CommonContainerId(player);
            var container = containerId is null ? null : InventoryEdits.FindContainer(level, containerId);
            if (container is null) return new(false, playerId, containerId, 0, [], "The player's main inventory was not found in the world.");
            var slots = InventoryEdits.ReadSlots(container);
            return new(true, playerId, containerId, InventoryEdits.Capacity(container), slots, $"{slots.Count} of {InventoryEdits.Capacity(container)} slots used.");
        }
        finally { TryDelete(working); }
    }

    public async Task<InventoryEditPreview> PreviewAsync(InventoryEditRequest request, CancellationToken cancellationToken)
    {
        var findings = new List<string>();
        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("remove" or "add")) findings.Add("Action must be remove or add.");
        if (string.IsNullOrWhiteSpace(request.ItemId)) findings.Add("Name the item (its id, for example PalSphere).");
        if (action == "add" && request.Count is < 1 or > 9999) findings.Add("Add between 1 and 9999.");
        var status = await lifecycle.GetStatusAsync(cancellationToken);
        if (status.ServerMayBeRunning)
            findings.Add("Stop PalServer first: a save is only edited while the server is stopped, so no player is online and the game cannot overwrite it.");
        if (findings.Count > 0) return new(false, null, findings, "Not possible.", null);

        var located = await LocateAsync(request.PlayerId, cancellationToken);
        if (located.Problem is not null) return new(false, null, [located.Problem], "Not possible.", null);
        var working = NewWorking("preview");
        try
        {
            var (level, player) = await DecodeBothAsync(located.Converter!, located.LevelSavePath!, located.PlayerSavePath!, working, cancellationToken);
            var containerId = InventoryEdits.CommonContainerId(player);
            var container = containerId is null ? null : InventoryEdits.FindContainer(level, containerId);
            if (container is null) return new(false, null, ["The player's main inventory was not found in the world."], "Not possible.", null);
            string summary;
            try
            {
                var trial = (JsonObject)container.DeepClone();
                summary = action == "remove"
                    ? DescribeRemove(InventoryEdits.Remove(trial, request.ItemId.Trim(), request.SlotIndex))
                    : DescribeAdd(InventoryEdits.Add(level, trial, request.ItemId.Trim(), request.Count));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
            {
                return new(false, null, [ex.Message], "Not possible.", null);
            }
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var expires = DateTimeOffset.UtcNow + PreviewLifetime;
            lock (pending) pending[token] = new(request with { ItemId = request.ItemId.Trim(), Action = action! }, located.LevelSavePath!, located.PlayerSavePath!, HashFile(located.LevelSavePath!), expires);
            return new(true, token, ["A fresh safety backup is taken and checked first; the change is verified before it replaces the world."], summary, expires);
        }
        finally { TryDelete(working); }
    }

    public async Task<InventoryEditResult> ApplyAsync(string token, bool confirmed, CancellationToken cancellationToken)
    {
        if (!confirmed) return new(false, "Refused", null, false, null, "Apply requires explicit confirmation.");
        if (!await gate.WaitAsync(0, cancellationToken)) return new(false, "Refused", null, false, null, "Another inventory edit is already running.");
        Pending? op = null;
        HeadlessWorldTransactionJournal? journal = null;
        OperationHandle? operation = null;
        string? working = null, originalCopy = null;
        try
        {
            lock (pending) { if (pending.Remove(token, out var p)) op = p; }
            if (op is null || op.ExpiresUtc < DateTimeOffset.UtcNow)
                return new(false, "Refused", null, false, null, "The preview is missing or expired. Preview the change again.");
            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.ServerMayBeRunning)
                return new(false, "Refused", null, false, null, "Stop PalServer before applying an inventory edit.");
            if (!File.Exists(op.LevelSavePath) || !HashFile(op.LevelSavePath).Equals(op.SourceHash, StringComparison.OrdinalIgnoreCase))
                return new(false, "Refused", null, false, null, "Level.sav changed since the preview. Preview the change again.");

            operation = await coordinator.BeginAsync(profile, "inventory-edit", nameof(HeadlessInventoryEditService), ["world-mutation"], cancellationToken);
            var id = Guid.NewGuid().ToString("N");
            journal = NewJournal(id, op.Request);
            Advance(journal, "PreviewAccepted", $"{op.Request.Action} {op.Request.ItemId} for player {op.Request.PlayerId}.");

            var safety = await backups.CreateAsync(BackupClass.Safety, cancellationToken);
            if (!safety.Success || string.IsNullOrWhiteSpace(safety.FileName))
                throw new InvalidOperationException("Fresh safety backup failed: " + safety.Message);
            var check = await backups.VerifyAsync(safety.FileName, cancellationToken);
            if (!check.Success) throw new InvalidOperationException("The safety backup did not pass its check: " + check.Message);
            journal.SafetyBackup = safety.FileName;
            Advance(journal, "SafetyBackupVerified", $"Fresh safety backup {safety.FileName}, checked.");

            var (converter, detail) = await codec.ResolveConverterAsync(op.LevelSavePath, cancellationToken);
            if (converter is null) throw new InvalidOperationException("Save codec unavailable: " + detail);
            working = NewWorking(id);
            originalCopy = Path.Combine(working, "Level.original.sav");
            File.Copy(op.LevelSavePath, originalCopy, true);
            var (level, player) = await DecodeBothAsync(converter, op.LevelSavePath, op.PlayerSavePath, working, cancellationToken);
            var containerId = InventoryEdits.CommonContainerId(player) ?? throw new InvalidDataException("The player's inventory id was not found.");
            var container = InventoryEdits.FindContainer(level, containerId) ?? throw new InvalidDataException("The player's inventory was not found in the world.");
            var before = InventoryEdits.ReadSlots(container);
            var changed = op.Request.Action == "remove"
                ? InventoryEdits.Remove(container, op.Request.ItemId, op.Request.SlotIndex)
                : InventoryEdits.Add(level, container, op.Request.ItemId, op.Request.Count);
            Advance(journal, "Staged", op.Request.Action == "remove" ? DescribeRemove(changed) : DescribeAdd(changed));

            var mutated = Path.Combine(working, "Level.mutated.json");
            File.WriteAllText(mutated, level.ToJsonString());
            var staged = await codec.EncodeAsync(converter, mutated, Path.Combine(working, "Level.edited.sav"), cancellationToken);
            if (!File.Exists(staged) || new FileInfo(staged).Length < 1024) throw new InvalidDataException("The encoded Level.sav is missing or too small.");
            var verifyJson = await codec.DecodeAsync(converter, staged, Path.Combine(working, "Level.verify.json"), cancellationToken);
            var verifyRoot = JsonNode.Parse(File.ReadAllText(verifyJson)) ?? throw new InvalidDataException("Verification decode was empty.");
            var after = InventoryEdits.ReadSlots(InventoryEdits.FindContainer(verifyRoot, containerId) ?? throw new InvalidDataException("Verification: the inventory is gone."));
            VerifyOnlyThisChanged(before, after, changed, op.Request.Action);
            Advance(journal, "Verified", "The edited save, decoded again, shows exactly this change and nothing else in the inventory.");

            var replacement = op.LevelSavePath + $".mysttiq-inventory-{id}.tmp";
            File.Copy(staged, replacement, true);
            File.Replace(replacement, op.LevelSavePath, null, true);
            Advance(journal, "Committed", $"Committed. Result hash {HashFile(op.LevelSavePath)}.");
            HeadlessSaveCodecService.RefreshExplorerSidecar(op.LevelSavePath, verifyJson);
            var message = (op.Request.Action == "remove" ? DescribeRemove(changed) : DescribeAdd(changed)) + $" Safety backup: {safety.FileName}.";
            activity.Record("Information", "Inventory", op.Request.Action == "remove" ? "Item removed from a player" : "Item added to a player",
                $"player={op.Request.PlayerId}; {message}");
            coordinator.Complete(operation.Id, message);
            return new(true, "Committed", safety.FileName, false, journal.JournalPath, message);
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
                activity.Record("Warning", "Inventory", "Inventory edit failed", $"rolledBack={rolledBack}; error={ex.Message}");
            }
            if (operation is not null) coordinator.Fail(operation.Id, ex.Message, rolledBack);
            return new(false, rolledBack ? "RolledBack" : "Failed", journal?.SafetyBackup, rolledBack, journal?.JournalPath, ex.Message);
        }
        finally
        {
            operation?.Dispose();
            if (working is not null) TryDelete(working);
            gate.Release();
        }
    }

    // Exactly the one slot differs: removed, or added; every other slot is identical.
    public static void VerifyOnlyThisChanged(IReadOnlyList<InventoryEdits.Slot> before, IReadOnlyList<InventoryEdits.Slot> after, InventoryEdits.Slot changed, string action)
    {
        var expected = action == "remove"
            ? before.Where(s => s.SlotIndex != changed.SlotIndex).ToList()
            : before.Append(changed).OrderBy(s => s.SlotIndex).ToList();
        if (!expected.SequenceEqual(after.OrderBy(s => s.SlotIndex)))
            throw new InvalidDataException("Verification: the inventory after the edit is not exactly the inventory before plus this one change.");
    }

    private static string DescribeRemove(InventoryEdits.Slot s) => $"Removed {s.Count} × {s.ItemId} (slot {s.SlotIndex}).";
    private static string DescribeAdd(InventoryEdits.Slot s) => $"Added {s.Count} × {s.ItemId} (slot {s.SlotIndex}).";

    private sealed record Located(string? LevelSavePath, string? PlayerSavePath, HeadlessSaveConverterMatch? Converter, string? Problem);

    private async Task<Located> LocateAsync(string playerId, CancellationToken cancellationToken)
    {
        var snapshot = await explorer.ExploreAsync(cancellationToken);
        if (!snapshot.Available || string.IsNullOrWhiteSpace(snapshot.ActiveWorldPath)) return new(null, null, null, "The active world could not be read.");
        var id = (playerId ?? string.Empty).Trim().Replace("-", string.Empty);
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[0-9A-Fa-f]{32}$")) return new(null, null, null, "The player id must be the 32-character id from the save.");
        var level = Path.Combine(snapshot.ActiveWorldPath, "Level.sav");
        var player = Path.Combine(snapshot.ActiveWorldPath, "Players", id.ToUpperInvariant() + ".sav");
        if (!File.Exists(level)) return new(null, null, null, "Level.sav was not found.");
        if (!File.Exists(player)) return new(null, null, null, $"No save for player {id} in this world.");
        var (converter, detail) = await codec.ResolveConverterAsync(level, cancellationToken);
        return converter is null ? new(null, null, null, "Save codec unavailable: " + detail) : new(level, player, converter, null);
    }

    private async Task<(JsonNode Level, JsonNode Player)> DecodeBothAsync(HeadlessSaveConverterMatch converter, string levelSavePath, string playerSavePath, string working, CancellationToken cancellationToken)
    {
        var levelCopy = Path.Combine(working, "Level.input.sav");
        File.Copy(levelSavePath, levelCopy, true);
        var playerCopy = Path.Combine(working, "Player.input.sav");
        File.Copy(playerSavePath, playerCopy, true);
        var levelJson = await codec.DecodeAsync(converter, levelCopy, Path.Combine(working, "Level.decoded.json"), cancellationToken);
        var (playerConverter, detail) = await codec.ResolveConverterAsync(playerCopy, cancellationToken);
        var playerJson = await codec.DecodeAsync(playerConverter ?? throw new InvalidOperationException("Player save codec unavailable: " + detail),
            playerCopy, Path.Combine(working, "Player.decoded.json"), cancellationToken);
        return (JsonNode.Parse(File.ReadAllText(levelJson)) ?? throw new InvalidDataException("Decoded Level.sav is empty."),
                JsonNode.Parse(File.ReadAllText(playerJson)) ?? throw new InvalidDataException("Decoded player save is empty."));
    }

    private string NewWorking(string name)
    {
        var dir = Path.Combine(paths.ManagerRuntimeRoot, "inventory-edit", name + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private HeadlessWorldTransactionJournal NewJournal(string id, InventoryEditRequest request)
    {
        return new HeadlessWorldTransactionJournal
        {
            TransactionId = id, Mode = "inventory-" + request.Action, WorldId = request.PlayerId, State = "Created",
            CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow,
            JournalPath = Path.Combine(paths.ManagerRuntimeRoot, "world-transactions", "journals", $"transaction-{id}.json")
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

    private static void TryDelete(string dir) { try { Directory.Delete(dir, true); } catch { } }
}
