// MystTiq v1.1.5.0: file reviewed for this release (2026-10-23).
using System.Text.Json;
using System.Text.RegularExpressions;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// Data/annotation only in this pass -- not auto-enforced against any live provider hook (no
// automatic kick/ban/mute follows from setting a flag).
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum PlayerFlag { None, Watchlisted, Banned, Trusted }

public sealed record PlayerWarningRecord(string Message, DateTimeOffset CreatedAt, string Actor);
public sealed record PlayerMetadataRecord(string PlayerId, string Notes, IReadOnlyList<PlayerWarningRecord> Warnings, DateTimeOffset UpdatedAt, PlayerFlag Flag = PlayerFlag.None);
public sealed record PlayerNotesRequest(string Notes);
public sealed record PlayerWarningRequest(string Message);
public sealed record PlayerFlagRequest(PlayerFlag Flag);

public sealed class HeadlessPlayerMetadataService
{
    private static readonly Regex SafePlayerId = new("^[A-Za-z0-9:_-]{1,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly object gate = new();
    private readonly string path;
    private readonly HeadlessActivityLogService activity;

    public HeadlessPlayerMetadataService(IServerPathProfile paths, HeadlessActivityLogService activity)
    {
        path = Path.Combine(paths.ManagerRuntimeRoot, "players", "player-metadata.json");
        this.activity = activity;
    }

    public PlayerMetadataRecord Get(string playerId)
    {
        var id = NormalizeId(playerId);
        lock (gate)
        {
            var store = Load();
            return store.TryGetValue(id, out var record)
                ? record
                : new PlayerMetadataRecord(id, string.Empty, [], DateTimeOffset.UtcNow);
        }
    }

    public PlayerMetadataRecord SaveNotes(string playerId, string? notes)
    {
        var id = NormalizeId(playerId);
        var value = NormalizeText(notes, 4000, "Notes");
        lock (gate)
        {
            var store = Load();
            var current = store.TryGetValue(id, out var record) ? record : new PlayerMetadataRecord(id, string.Empty, [], DateTimeOffset.UtcNow);
            var updated = current with { Notes = value, UpdatedAt = DateTimeOffset.UtcNow };
            store[id] = updated;
            Save(store);
            activity.Record("Information", "Players", "Player notes updated", $"playerId={id}; characters={value.Length}");
            return updated;
        }
    }

    public PlayerMetadataRecord AddWarning(string playerId, string? message)
    {
        var id = NormalizeId(playerId);
        var value = NormalizeText(message, 1000, "Warning");
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Warning message cannot be empty.");
        lock (gate)
        {
            var store = Load();
            var current = store.TryGetValue(id, out var record) ? record : new PlayerMetadataRecord(id, string.Empty, [], DateTimeOffset.UtcNow);
            var warnings = current.Warnings.Append(new PlayerWarningRecord(value, DateTimeOffset.UtcNow, "mysttiq-operator")).TakeLast(100).ToArray();
            var updated = current with { Warnings = warnings, UpdatedAt = DateTimeOffset.UtcNow };
            store[id] = updated;
            Save(store);
            activity.Record("Warning", "Players", "Player warning added", $"playerId={id}; warningCount={warnings.Length}");
            return updated;
        }
    }

    public PlayerMetadataRecord SetFlag(string playerId, PlayerFlag flag)
    {
        var id = NormalizeId(playerId);
        lock (gate)
        {
            var store = Load();
            var current = store.TryGetValue(id, out var record) ? record : new PlayerMetadataRecord(id, string.Empty, [], DateTimeOffset.UtcNow);
            var updated = current with { Flag = flag, UpdatedAt = DateTimeOffset.UtcNow };
            store[id] = updated;
            Save(store);
            activity.Record("Information", "Players", "Player flag updated", $"playerId={id}; flag={flag}");
            return updated;
        }
    }

    // v1.0.7.0 (Q-1): unreadable player notes and flags fall back to their last good copy, else changes are refused.
    private Dictionary<string, PlayerMetadataRecord> Load() =>
        new(SavedFiles.Load(path, "player notes and flags", () => new Dictionary<string, PlayerMetadataRecord>()), StringComparer.OrdinalIgnoreCase);

    // Callers hold the service's lock, so one writer at a time.
    private void Save(Dictionary<string, PlayerMetadataRecord> store) => SavedFiles.Save(path, store);

    private static string NormalizeId(string? value)
    {
        var id = (value ?? string.Empty).Trim();
        if (!SafePlayerId.IsMatch(id)) throw new ArgumentException("Player ID contains unsupported characters or length.");
        return id;
    }

    private static string NormalizeText(string? value, int maximum, string label)
    {
        var text = (value ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (text.Length > maximum) throw new ArgumentException($"{label} cannot exceed {maximum} characters.");
        return text;
    }
}
