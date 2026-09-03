using System.Text.Json;
using System.Text.RegularExpressions;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed record PlayerWarningRecord(string Message, DateTimeOffset CreatedAt, string Actor);
public sealed record PlayerMetadataRecord(string PlayerId, string Notes, IReadOnlyList<PlayerWarningRecord> Warnings, DateTimeOffset UpdatedAt);
public sealed record PlayerNotesRequest(string Notes);
public sealed record PlayerWarningRequest(string Message);

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

    private Dictionary<string, PlayerMetadataRecord> Load()
    {
        if (!File.Exists(path)) return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, PlayerMetadataRecord>>(File.ReadAllText(path))
                   ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex) { throw new InvalidDataException("Player metadata store is malformed.", ex); }
    }

    private void Save(Dictionary<string, PlayerMetadataRecord> store)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + $".tmp-{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

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
