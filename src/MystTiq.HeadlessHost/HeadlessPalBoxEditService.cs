// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MystTiq.Core.Models;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed record PalBoxEditRequest(string PlayerId, string Action, string? Species = null, string? InstanceId = null);
public sealed record PalBoxView(bool Available, string PlayerId, string? ContainerId, int Capacity, IReadOnlyList<PalBoxEdits.Pal> Pals, IReadOnlyList<string> AddableSpecies, string Detail);
public sealed record PalBoxEditPreview(bool CanApply, string? Token, IReadOnlyList<string> Findings, string Summary, DateTimeOffset? ExpiresUtc);
public sealed record PalBoxEditApplyRequest(string Token, bool Confirmed);
public sealed record PalBoxEditResult(bool Success, string State, string? SafetyBackup, bool RolledBack, string? JournalPath, string Message);

/// <summary>
/// v1.0.6.0 (roadmap S-3 add and remove a Pal; owner decisions D-2 guarded edits only, D-7). The same guarded shape as the
/// v1.0.4.0 inventory edit (HeadlessInventoryEditService): a single-use preview, PalServer stopped, the world unchanged
/// since the preview, a fresh safety backup checked before anything is touched, decode, the one change (PalBoxEdits),
/// encode, an independent decode that must show exactly that Pal added or removed in the world's characters, the Pal box
/// and the guild's members and nothing else there, then an atomic replace; any failure puts the original back. Only
/// Level.sav changes; the player's own save is only read, for their id and the id of their Pal box.
/// </summary>
public sealed class HeadlessPalBoxEditService
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

    private sealed record Pending(PalBoxEditRequest Request, string LevelSavePath, string PlayerSavePath, string SourceHash, string NewInstanceId, DateTimeOffset ExpiresUtc);

    public HeadlessPalBoxEditService(IServerPathProfile paths, IServerLifecycleService lifecycle, HeadlessBackupService backups,
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

    public async Task<PalBoxView> GetPalBoxAsync(string playerId, CancellationToken cancellationToken)
    {
        var located = await LocateAsync(playerId, cancellationToken);
        if (located.Problem is not null) return new(false, playerId, null, 0, [], [], located.Problem);
        var working = NewWorking("view");
        try
        {
            var (level, player) = await DecodeBothAsync(located.Converter!, located.LevelSavePath!, located.PlayerSavePath!, working, cancellationToken);
            var containerId = PalBoxEdits.PalBoxContainerId(player);
            var container = containerId is null ? null : PalBoxEdits.FindContainer(level, containerId);
            if (container is null) return new(false, playerId, containerId, 0, [], [], "The player's Pal box was not found in the world.");
            var pals = PalBoxEdits.ReadPalBox(level, containerId!);
            var capacity = PalBoxEdits.Capacity(container);
            return new(true, playerId, containerId, capacity, pals, PalBoxEdits.AddableSpecies(level, containerId!), $"{pals.Count} Pals in the box ({capacity} places).");
        }
        finally { TryDelete(working); }
    }

    public async Task<PalBoxEditPreview> PreviewAsync(PalBoxEditRequest request, CancellationToken cancellationToken)
    {
        var findings = new List<string>();
        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("remove" or "add")) findings.Add("Action must be remove or add.");
        if (action == "add" && string.IsNullOrWhiteSpace(request.Species)) findings.Add("Name the Pal (its id, for example SheepBall).");
        if (action == "remove" && !Guid.TryParse(request.InstanceId, out _)) findings.Add("Pick the Pal to remove.");
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
            var uid = PalBoxEdits.PlayerUid(player);
            var containerId = PalBoxEdits.PalBoxContainerId(player);
            if (uid is null || containerId is null) return new(false, null, ["The player's id or Pal box was not found in their save."], "Not possible.", null);
            var newId = Guid.NewGuid().ToString();
            string summary;
            try
            {
                var trial = level.DeepClone();
                summary = action == "remove"
                    ? DescribeRemove(PalBoxEdits.Remove(trial, uid, containerId, request.InstanceId!))
                    : DescribeAdd(PalBoxEdits.Add(trial, uid, containerId, request.Species!.Trim(), newId, DateTime.UtcNow));
            }
            catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException)
            {
                return new(false, null, [ex.Message], "Not possible.", null);
            }
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var expires = DateTimeOffset.UtcNow + PreviewLifetime;
            lock (pending) pending[token] = new(request with { Action = action!, Species = request.Species?.Trim() }, located.LevelSavePath!, located.PlayerSavePath!, HashFile(located.LevelSavePath!), newId, expires);
            return new(true, token, ["A fresh safety backup is taken and checked first; the change is verified before it replaces the world."], summary, expires);
        }
        finally { TryDelete(working); }
    }

    public async Task<PalBoxEditResult> ApplyAsync(string token, bool confirmed, CancellationToken cancellationToken)
    {
        if (!confirmed) return new(false, "Refused", null, false, null, "Apply requires explicit confirmation.");
        if (!await gate.WaitAsync(0, cancellationToken)) return new(false, "Refused", null, false, null, "Another Pal box edit is already running.");
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
                return new(false, "Refused", null, false, null, "Stop PalServer before applying a Pal box edit.");
            if (!File.Exists(op.LevelSavePath) || !HashFile(op.LevelSavePath).Equals(op.SourceHash, StringComparison.OrdinalIgnoreCase))
                return new(false, "Refused", null, false, null, "Level.sav changed since the preview. Preview the change again.");

            operation = await coordinator.BeginAsync(profile, "palbox-edit", nameof(HeadlessPalBoxEditService), ["world-mutation"], cancellationToken);
            var id = Guid.NewGuid().ToString("N");
            journal = NewJournal(id, op.Request);
            Advance(journal, "PreviewAccepted", $"{op.Request.Action} {op.Request.Species ?? op.Request.InstanceId} for player {op.Request.PlayerId}.");

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
            var uid = PalBoxEdits.PlayerUid(player) ?? throw new InvalidDataException("The player's id was not found.");
            var containerId = PalBoxEdits.PalBoxContainerId(player) ?? throw new InvalidDataException("The player's Pal box id was not found.");
            var guild = PalBoxEdits.GuildOf(level, uid) ?? throw new InvalidDataException("The player is not in a guild in this world.");
            var before = PalBoxEdits.Measure(level, containerId, guild);
            var changed = op.Request.Action == "remove"
                ? PalBoxEdits.Remove(level, uid, containerId, op.Request.InstanceId!)
                : PalBoxEdits.Add(level, uid, containerId, op.Request.Species!, op.NewInstanceId, DateTime.UtcNow);
            Advance(journal, "Staged", op.Request.Action == "remove" ? DescribeRemove(changed) : DescribeAdd(changed));

            var mutated = Path.Combine(working, "Level.mutated.json");
            File.WriteAllText(mutated, level.ToJsonString());
            var staged = await codec.EncodeAsync(converter, mutated, Path.Combine(working, "Level.edited.sav"), cancellationToken);
            if (!File.Exists(staged) || new FileInfo(staged).Length < 1024) throw new InvalidDataException("The encoded Level.sav is missing or too small.");
            var verifyJson = await codec.DecodeAsync(converter, staged, Path.Combine(working, "Level.verify.json"), cancellationToken);
            var verifyRoot = JsonNode.Parse(File.ReadAllText(verifyJson)) ?? throw new InvalidDataException("Verification decode was empty.");
            PalBoxEdits.VerifyOnlyThisChanged(before, PalBoxEdits.Measure(verifyRoot, containerId, guild), changed, op.Request.Action);
            Advance(journal, "Verified", "The edited save, decoded again, shows exactly this Pal added or removed, and nothing else changed in the characters, the Pal box or the guild.");

            var replacement = op.LevelSavePath + $".mysttiq-palbox-{id}.tmp";
            File.Copy(staged, replacement, true);
            FileRetry.Replace(replacement, op.LevelSavePath);
            Advance(journal, "Committed", $"Committed. Result hash {HashFile(op.LevelSavePath)}.");
            HeadlessSaveCodecService.RefreshExplorerSidecar(op.LevelSavePath, verifyJson);
            var message = (op.Request.Action == "remove" ? DescribeRemove(changed) : DescribeAdd(changed)) + $" Safety backup: {safety.FileName}.";
            activity.Record("Information", "Pals", op.Request.Action == "remove" ? "Pal removed from a player" : "Pal added to a player", $"player={op.Request.PlayerId}; {message}");
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
                activity.Record("Warning", "Pals", "Pal box edit failed", $"rolledBack={rolledBack}; error={ex.Message}");
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

    private static string DescribeRemove(PalBoxEdits.Pal p) => $"Removed {p.Species} (level {p.Level}) from the Pal box (place {p.SlotIndex}).";
    private static string DescribeAdd(PalBoxEdits.Pal p) => $"Added {p.Species} (level {p.Level}) to the Pal box (place {p.SlotIndex}).";

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
        var dir = Path.Combine(paths.ManagerRuntimeRoot, "palbox-edit", name + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private HeadlessWorldTransactionJournal NewJournal(string id, PalBoxEditRequest request) => new()
    {
        TransactionId = id, Mode = "palbox-" + request.Action, WorldId = request.PlayerId, State = "Created",
        CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow,
        JournalPath = Path.Combine(paths.ManagerRuntimeRoot, "world-transactions", "journals", $"transaction-{id}.json")
    };

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
