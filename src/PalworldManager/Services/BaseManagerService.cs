using PalworldManager.Services.Infrastructure;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class BaseManagerService
{
    private readonly AppSettings settings;
    private readonly GuildService guildService;
    private readonly PalworldSaveCodec codec;
    private readonly WorldDiscoverySnapshotService? discovery;
    public BaseManagerService(AppSettings settings, ActiveWorldContextService? worldContext = null, WorldDiscoverySnapshotService? discovery = null)
    {
        this.settings = settings;
        this.discovery = discovery;
        guildService = new GuildService(settings, worldContext, discovery);
        codec = new PalworldSaveCodec(settings);
    }

    public BaseManagerSummary Scan(string worldPath)
    {
        var shared = discovery?.Current(forceRefresh: true);
        if (shared is not null)
        {
            worldPath = shared.Context.WorldPath;
            if (!shared.IsResolved) throw new DirectoryNotFoundException("No active Palworld world was found.");
            return BuildSummary(shared);
        }

        if (string.IsNullOrWhiteSpace(worldPath) || !Directory.Exists(worldPath))
            throw new DirectoryNotFoundException("No active Palworld world was found.");
        var guildSnapshot = guildService.LoadSnapshot(worldPath);
        var level = Path.Combine(worldPath, "Level.sav");
        var fallback = new WorldDiscoverySnapshot
        {
            Context = new ActiveWorldContext(worldPath, Path.GetFileName(worldPath), level, File.GetLastWriteTimeUtc(level), new FileInfo(level).Length, "Base Manager fallback", 0),
            DecodedJsonPath = guildSnapshot.DecodedJsonPath,
            SourceHash = File.Exists(level) ? PalworldSaveCodec.HashFile(level) : "",
            Guilds = guildSnapshot,
            BaseRecords = !string.IsNullOrWhiteSpace(guildSnapshot.DecodedJsonPath) && File.Exists(guildSnapshot.DecodedJsonPath)
                ? new BaseDiscoveryEngine().Discover(guildSnapshot.DecodedJsonPath)
                : []
        };
        return BuildSummary(fallback);
    }

    private BaseManagerSummary BuildSummary(WorldDiscoverySnapshot shared)
    {
        var summary = new BaseManagerSummary
        {
            WorldPath = shared.Context.WorldPath,
            SourceHash = shared.SourceHash,
            CodecAvailable = !string.IsNullOrWhiteSpace(codec.FindConverter()),
            Warnings = shared.Warnings.Concat(shared.Guilds.Warnings).Distinct().ToList()
        };
        var guildNames = shared.Guilds.Guilds
            .Where(g => !string.IsNullOrWhiteSpace(g.GuildId))
            .GroupBy(g => Normalize(g.GuildId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

        foreach (var record in shared.BaseRecords)
        {
            var guildId = Normalize(record.GuildId);
            summary.Bases.Add(new BaseManagerRow
            {
                BaseId = Normalize(record.BaseId),
                Name = record.Name,
                InternalName = string.IsNullOrWhiteSpace(record.InternalName) ? record.Name : record.InternalName,
                GuildId = guildId,
                GuildName = guildNames.GetValueOrDefault(guildId, string.IsNullOrWhiteSpace(guildId) ? "Unassigned" : "Unknown guild"),
                PalboxId = Normalize(record.PalboxId),
                X = record.X, Y = record.Y, Z = record.Z,
                SourcePath = record.SourcePath,
                Health = string.IsNullOrWhiteSpace(guildId) || !guildNames.ContainsKey(guildId) ? "Orphaned" : "Healthy"
            });
        }

        foreach (var guild in shared.Guilds.Guilds)
        foreach (var baseRow in guild.Bases)
        {
            var id = Normalize(baseRow.BaseId);
            if (summary.Bases.Any(b => b.BaseId.Equals(id, StringComparison.OrdinalIgnoreCase))) continue;
            var guildId = Normalize(string.IsNullOrWhiteSpace(baseRow.OwnerGuildId) ? guild.GuildId : baseRow.OwnerGuildId);
            summary.Bases.Add(new BaseManagerRow
            {
                BaseId = id,
                Name = baseRow.Name,
                InternalName = baseRow.Name,
                GuildId = guildId,
                GuildName = guildNames.GetValueOrDefault(guildId, guild.Name),
                SourcePath = "Shared world relationship",
                Health = string.IsNullOrWhiteSpace(guildId) || !guildNames.ContainsKey(guildId) ? "Orphaned" : "Healthy"
            });
        }

        summary.Bases = summary.Bases.GroupBy(b => b.BaseId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()).ToList();

        // Resolve user-facing names only after ownership relationships are known.
        // This keeps Palworld's internal/localized template value available for
        // diagnostics while presenting a stable friendly label in the UI.
        foreach (var ownerGroup in summary.Bases
                     .GroupBy(b => string.IsNullOrWhiteSpace(b.GuildId) ? "__UNASSIGNED__" : b.GuildId,
                         StringComparer.OrdinalIgnoreCase))
        {
            var ordinal = 0;
            foreach (var baseRow in ownerGroup.OrderBy(b => b.BaseId, StringComparer.OrdinalIgnoreCase))
            {
                ordinal++;
                baseRow.Name = BaseDisplayNameResolver.Resolve(baseRow.InternalName, baseRow.GuildName, baseRow.BaseId, ordinal);
            }
        }

        summary.Bases = summary.Bases.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (summary.Bases.Count == 0)
            summary.Warnings.Add("The shared world discovery snapshot found no base records.");
        return summary;
    }

    public string ExportCsv(BaseManagerSummary summary, IEnumerable<BaseManagerRow> rows)
    {
        var root = Path.Combine(settings.BackupRoot, "BaseManager", "Reports");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"base-inventory-{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        static string Q(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        var lines = new List<string> { "Base Name,Base ID,Guild,Guild ID,Palbox ID,X,Y,Z,Health,Source" };
        lines.AddRange(rows.Select(b => string.Join(',', Q(b.Name), Q(b.BaseId), Q(b.GuildName), Q(b.GuildId), Q(b.PalboxId), b.X.ToString(System.Globalization.CultureInfo.InvariantCulture), b.Y.ToString(System.Globalization.CultureInfo.InvariantCulture), b.Z.ToString(System.Globalization.CultureInfo.InvariantCulture), Q(b.Health), Q(b.SourcePath))));
        File.WriteAllLines(path, lines, Encoding.UTF8);
        return path;
    }

    public string CreateSafetyBackup(string worldPath)
    {
        var root = Path.Combine(settings.BackupRoot, "BaseManager");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"BaseManager_{Path.GetFileName(worldPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        ZipFile.CreateFromDirectory(worldPath, path, CompressionLevel.Optimal, false);
        using var archive = ZipFile.OpenRead(path);
        if (archive.GetEntry("Level.sav") is null) throw new InvalidDataException("Backup verification failed: Level.sav is missing.");
        return path;
    }

    public string SaveOwnershipPlan(BaseManagerSummary summary, BaseManagerRow row, string targetGuildId)
    {
        targetGuildId = Normalize(targetGuildId);
        if (string.IsNullOrWhiteSpace(targetGuildId)) throw new InvalidOperationException("Enter a destination guild ID.");
        var level = Path.Combine(summary.WorldPath, "Level.sav");
        if (!File.Exists(level) || !PalworldSaveCodec.HashFile(level).Equals(summary.SourceHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Level.sav changed after the scan. Refresh before preparing a plan.");
        var root = Path.Combine(settings.BackupRoot, "BaseManager", "Plans");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"base-ownership-{DateTime.Now:yyyyMMdd_HHmmss}-{Short(row.BaseId)}.json");
        var payload = new
        {
            Version = ApplicationVersion.Version, CreatedUtc = DateTime.UtcNow, summary.WorldPath, summary.SourceHash,
            Base = new { row.BaseId, row.Name, CurrentGuildId = row.GuildId, row.GuildName, row.PalboxId, row.X, row.Y, row.Z },
            ProposedGuildId = targetGuildId,
            SafetyBoundary = "Preview only. Apply through Guild & Base Recovery after full backup and round-trip validation."
        };
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        return path;
    }

    private static string Normalize(string value) => new string((value ?? "").Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
    private static string Short(string value) { var id = Normalize(value); return id[..Math.Min(8, id.Length)]; }
}
