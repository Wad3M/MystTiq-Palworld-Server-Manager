using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.8.3.0: the Give Item picker's catalogue of item and Pal ids for this server. Three sources, all real:
//   * the world save: every item and Pal species that occurs in it (SaveGameIdReader), so valid for the installed
//     game version by construction;
//   * the saved starter kits;
//   * ids a manual Give has delivered without an error reply before.
// Read-only. Parsing is cached until the decoded save changes, since a big world's JSON is large.
// v0.8.13.0: Name is the game's English display name when known (HeadlessGameNameService); InGameFiles marks an id that
// only the game's own name tables list (nobody on this server has had it yet). Both optional, so older callers are unchanged.
public sealed record GameIdCatalogEntry(string Kind, string Id, int WorldCount, bool InKit, bool GivenBefore, bool AlphaSeen,
    string? Name = null, bool InGameFiles = false);

public sealed record GameIdCatalogSnapshot(
    bool WorldAvailable,
    string? WorldId,
    DateTimeOffset? WorldDecodedUtc,
    int ItemCount,
    int PalCount,
    IReadOnlyList<GameIdCatalogEntry> Entries,
    string Detail,
    bool NamesAvailable = false,
    string NamesDetail = "");

public sealed class HeadlessGameIdCatalogService
{
    private readonly IServerPathProfile paths;
    private readonly HeadlessKitService kits;
    private readonly HeadlessGameNameService? names;
    private readonly object gate = new();
    private (string Path, DateTime WriteUtc, long Length)? cachedKey;
    private SaveGameIds? cachedIds;

    public HeadlessGameIdCatalogService(IServerPathProfile paths, HeadlessKitService kits, HeadlessGameNameService? names = null)
    {
        this.paths = paths;
        this.kits = kits;
        this.names = names;
    }

    public GameIdCatalogSnapshot GetCatalog()
    {
        var world = ResolveActiveWorld();
        var decoded = world is null ? null : ResolveDecodedLevelJson(world);
        SaveGameIds? ids = null;
        DateTimeOffset? decodedUtc = null;
        string? readError = null;
        if (decoded is not null)
        {
            try
            {
                var info = new FileInfo(decoded);
                decodedUtc = info.LastWriteTimeUtc;
                ids = ReadCached(info);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                readError = ex.Message;
            }
        }

        var kitEntries = kits.GetSnapshot().Config.Kits.SelectMany(k => k.Entries).ToArray();
        var given = kits.RecentlyGiven();
        var merged = Merge(ids, kitEntries, given);
        var worldId = world is null ? null : Path.GetFileName(world);
        var extra = merged.Count(e => e.WorldCount == 0);
        var (gameNames, namesStatus) = names?.Get() ?? (GameNameCatalog.Empty, HeadlessGameNameService.Unavailable("names are not set up for this server"));
        var entries = WithNames(merged, gameNames);

        string detail;
        if (ids is not null)
            detail = $"{ids.Items.Count} item(s) and {ids.Pals.Count} Pal species seen in world {worldId} " +
                     $"(save decoded {decodedUtc:yyyy-MM-dd HH:mm} UTC)" +
                     (extra > 0 ? $", plus {extra} from kits and earlier gives" : string.Empty) +
                     ". These are the installed game's own ids." +
                     (gameNames.HasNames
                         ? " Items and Pals nobody here has had yet are listed after them, from the game's own name tables."
                         : " Something nobody in this world has had yet is not listed: type its id by hand.");
        else if (readError is not null)
            detail = $"The decoded world save could not be read ({readError}). Showing ids from kits and earlier gives only.";
        else if (world is null)
            detail = "No world save was found yet. Showing ids from kits and earlier gives only.";
        else
            detail = "This world has no decoded save (Level.sav.json) yet, so its items are not known. MystTiq writes one after a " +
                     "world change such as a guild, base or Pal edit. Showing ids from kits and earlier gives only.";
        if (ids is null && gameNames.HasNames) detail += " The game's own item and Pal list is shown after them.";

        return new GameIdCatalogSnapshot(ids is not null, worldId, decodedUtc,
            entries.Count(e => e.Kind == "Item"), entries.Count(e => e.Kind == "Pal"), entries, detail,
            namesStatus.Available, namesStatus.Detail);
    }

    // v0.8.13.0 (pure, logic harness): gives every row its display name, then adds the ids only the game's name tables
    // list, marked InGameFiles. Order: items before Pals; within each, ids this server has seen (world, kits, gives)
    // before the rest; then by name (or id).
    public static IReadOnlyList<GameIdCatalogEntry> WithNames(IReadOnlyList<GameIdCatalogEntry> known, GameNameCatalog gameNames)
    {
        var rows = known.Select(e => e with { Name = e.Kind == "Pal" ? gameNames.PalName(e.Id) : gameNames.ItemName(e.Id) }).ToList();
        var have = rows.Select(e => (e.Kind, e.Id.ToUpperInvariant())).ToHashSet();
        foreach (var (id, name) in gameNames.Items)
            if (have.Add(("Item", id.ToUpperInvariant()))) rows.Add(new GameIdCatalogEntry("Item", id, 0, false, false, false, name, InGameFiles: true));
        foreach (var (id, name) in gameNames.Pals)
            if (have.Add(("Pal", id.ToUpperInvariant()))) rows.Add(new GameIdCatalogEntry("Pal", id, 0, false, false, false, name, InGameFiles: true));
        return rows
            .OrderBy(r => r.Kind == "Item" ? 0 : 1)
            .ThenBy(r => r.InGameFiles ? 1 : 0)
            .ThenBy(r => r.Name ?? r.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    // Pure (logic harness): one row per (kind, id), items first, then alphabetical. Kit and history ids are matched to
    // world ids ignoring case, so "palsphere" typed into a kit still shows as the world's "PalSphere".
    public static IReadOnlyList<GameIdCatalogEntry> Merge(SaveGameIds? world, IEnumerable<KitEntry> kitEntries, IEnumerable<KitEntry> given)
    {
        var rows = new Dictionary<(string Kind, string Key), GameIdCatalogEntry>();
        void Upsert(string kind, string id, Func<GameIdCatalogEntry, GameIdCatalogEntry> change)
        {
            var key = (kind, id.ToUpperInvariant());
            rows[key] = change(rows.TryGetValue(key, out var row) ? row : new GameIdCatalogEntry(kind, id, 0, false, false, false));
        }
        static string KindOf(string type) => type.Equals("Pal", StringComparison.OrdinalIgnoreCase) ? "Pal" : "Item";

        if (world is not null)
        {
            foreach (var (id, count) in world.Items) Upsert("Item", id, r => r with { WorldCount = r.WorldCount + count });
            foreach (var (id, count) in world.Pals) Upsert("Pal", id, r => r with { WorldCount = r.WorldCount + count, AlphaSeen = r.AlphaSeen || world.AlphaPals.Contains(id) });
        }
        foreach (var e in kitEntries.Where(e => !string.IsNullOrWhiteSpace(e.Id))) Upsert(KindOf(e.Type), e.Id.Trim(), r => r with { InKit = true });
        foreach (var e in given.Where(e => !string.IsNullOrWhiteSpace(e.Id))) Upsert(KindOf(e.Type), e.Id.Trim(), r => r with { GivenBefore = true });

        return rows.Values
            .OrderBy(r => r.Kind == "Item" ? 0 : 1)
            .ThenBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private SaveGameIds ReadCached(FileInfo info)
    {
        var key = (info.FullName, info.LastWriteTimeUtc, info.Length);
        lock (gate)
        {
            if (cachedKey == key && cachedIds is not null) return cachedIds;
        }

        byte[] bytes;
        using (var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
        }
        var ids = SaveGameIdReader.Read(bytes);
        lock (gate)
        {
            cachedKey = key;
            cachedIds = ids;
        }
        return ids;
    }

    // Same rule as the explorers: the world whose Level.sav was written most recently.
    private string? ResolveActiveWorld()
    {
        if (!Directory.Exists(paths.SaveRoot)) return null;
        try
        {
            return Directory.EnumerateFiles(paths.SaveRoot, "Level.sav", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .Select(info => info.DirectoryName)
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    private static string? ResolveDecodedLevelJson(string worldPath) =>
        new[] { "Level.sav.json", "Level.json", "Level.sav.decoded.json" }
            .Select(name => Path.Combine(worldPath, name))
            .FirstOrDefault(path => File.Exists(path) && new FileInfo(path).Length > 0);
}
