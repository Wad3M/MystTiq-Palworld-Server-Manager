using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Single authoritative discovery pipeline for the active Palworld world.
/// Resolves and decodes Level.sav once, then exposes consistent player, guild,
/// base and relationship projections to every page.
/// </summary>
public sealed class WorldDiscoverySnapshotService : IDisposable
{
    private readonly AppSettings settings;
    private readonly ActiveWorldContextService contextService;
    private readonly PalworldSaveCodec codec;
    private readonly GuildJsonMapper guildMapper = new();
    private readonly object gate = new();
    private WorldDiscoverySnapshot? cached;
    private bool disposed;

    public WorldDiscoverySnapshotService(AppSettings settings, ActiveWorldContextService contextService)
    {
        this.settings = settings;
        this.contextService = contextService;
        codec = new PalworldSaveCodec(settings);
        contextService.Changed += ContextChanged;
    }

    public event EventHandler<WorldDiscoverySnapshot>? Changed;

    public WorldDiscoverySnapshot Current(bool forceRefresh = false)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            var context = contextService.Current(forceRefresh);
            if (!forceRefresh && cached is not null && cached.Context.Generation == context.Generation)
                return cached;
            cached = Discover(context);
            return cached;
        }
    }

    public string BuildDiagnosticsReport(bool forceRefresh = true)
    {
        var snapshot = Current(forceRefresh);
        var builder = new StringBuilder();
        builder.AppendLine("=== MystTiq World Discovery Diagnostics ===");
        builder.AppendLine($"Generated (UTC): {DateTime.UtcNow:O}");
        builder.AppendLine();
        builder.AppendLine("[Active World]");
        builder.AppendLine($"World ID: {snapshot.Context.WorldId}");
        builder.AppendLine($"World Path: {snapshot.Context.WorldPath}");
        builder.AppendLine($"Resolution Source: {snapshot.Context.ResolutionSource}");
        builder.AppendLine($"Level.sav: {snapshot.Context.LevelSavePath}");
        builder.AppendLine($"Level.sav Exists: {File.Exists(snapshot.Context.LevelSavePath)}");
        builder.AppendLine($"Level.sav Size: {(File.Exists(snapshot.Context.LevelSavePath) ? new FileInfo(snapshot.Context.LevelSavePath).Length : 0)} bytes");
        builder.AppendLine();
        builder.AppendLine("[Decoder]");
        var container = File.Exists(snapshot.Context.LevelSavePath)
            ? PalworldSaveContainerDetector.Inspect(snapshot.Context.LevelSavePath)
            : new PalworldSaveContainerInfo("Unknown", -1, string.Empty, string.Empty);
        var selectedConverter = File.Exists(snapshot.Context.LevelSavePath) ? codec.FindConverterForSave(snapshot.Context.LevelSavePath) : string.Empty;
        builder.AppendLine($"Save Signature: {container.DisplaySignature}");
        builder.AppendLine($"Header Bytes: {container.HeaderHex}");
        builder.AppendLine($"Selected Decoder: {(container.IsPlm ? "PlM/Oodle" : container.IsPlz ? "Legacy PlZ" : "Unsupported/Unknown")}");
        builder.AppendLine($"Selected Converter: {selectedConverter} (exists={File.Exists(selectedConverter)})");
        builder.AppendLine($"Legacy PlZ Converter: {codec.FindConverter()} (exists={File.Exists(codec.FindConverter())})");
        builder.AppendLine($"PlM/Oodle Converter: {codec.FindPlmConverter()} (exists={File.Exists(codec.FindPlmConverter())})");
        builder.AppendLine($"Configured Python: {settings.PythonExecutable}");
        builder.AppendLine("Python Candidates: " + string.Join(", ", new[] { settings.PythonExecutable, "python", "python3", "py" }.Where(x => !string.IsNullOrWhiteSpace(x))));
        builder.AppendLine($"Decoded JSON: {snapshot.DecodedJsonPath}");
        builder.AppendLine($"Decoded JSON Exists: {File.Exists(snapshot.DecodedJsonPath)}");
        builder.AppendLine($"Decoded JSON Size: {(File.Exists(snapshot.DecodedJsonPath) ? new FileInfo(snapshot.DecodedJsonPath).Length : 0)} bytes");
        builder.AppendLine();
        builder.AppendLine("[Discovery Results]");
        builder.AppendLine($"Player Saves: {snapshot.PlayerSavePaths.Count}");
        builder.AppendLine($"Players: {snapshot.World.Players.Count}");
        builder.AppendLine($"Guilds: {snapshot.World.Guilds.Count}");
        builder.AppendLine($"Guild Rows: {snapshot.Guilds.Guilds.Count}");
        builder.AppendLine($"Bases: {snapshot.World.Bases.Count}");
        builder.AppendLine($"Base Records: {snapshot.BaseRecords.Count}");
        builder.AppendLine($"Duration: {snapshot.Duration.TotalSeconds:0.###} sec");
        if (File.Exists(snapshot.DecodedJsonPath)) AppendSchemaDiagnostics(builder, snapshot.DecodedJsonPath);
        builder.AppendLine();
        builder.AppendLine("[Pipeline Diagnostics]");
        foreach (var line in snapshot.Diagnostics) builder.AppendLine("  " + line);
        builder.AppendLine();
        builder.AppendLine("[Warnings]");
        if (snapshot.Warnings.Count == 0) builder.AppendLine("  None");
        else foreach (var line in snapshot.Warnings) builder.AppendLine("  " + line);
        return builder.ToString();
    }

    private static void AppendSchemaDiagnostics(StringBuilder builder, string jsonPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            builder.AppendLine();
            builder.AppendLine("[Decoded Schema]");
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                builder.AppendLine("Top-Level Keys:");
                foreach (var property in document.RootElement.EnumerateObject().Take(100)) builder.AppendLine("  - " + property.Name);
            }
            var candidates = new List<string>();
            ScanSchema(document.RootElement, "$", candidates, 0);
            builder.AppendLine("Guild/Base Candidate Paths:");
            if (candidates.Count == 0) builder.AppendLine("  None found by name scan");
            else foreach (var path in candidates.Take(250)) builder.AppendLine("  - " + path);
        }
        catch (Exception ex) { builder.AppendLine("Schema inspection failed: " + ex.Message); }
    }

    private static void ScanSchema(JsonElement element, string path, List<string> candidates, int depth)
    {
        if (depth > 30 || candidates.Count >= 500) return;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var childPath = path + "." + property.Name;
                if (property.Name.Contains("guild", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("group", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("basecamp", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("base", StringComparison.OrdinalIgnoreCase))
                    candidates.Add(childPath + " [" + property.Value.ValueKind + "]");
                ScanSchema(property.Value, childPath, candidates, depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                ScanSchema(item, path + "[" + index + "]", candidates, depth + 1);
                if (++index >= 50) break;
            }
        }
    }

    public void Invalidate(string reason = "World discovery invalidated")
    {
        WorldDiscoverySnapshot snapshot;
        lock (gate)
        {
            ThrowIfDisposed();
            cached = null;
            snapshot = Discover(contextService.Current(forceRefresh: true), reason);
            cached = snapshot;
        }
        Changed?.Invoke(this, snapshot);
    }

    private WorldDiscoverySnapshot Discover(ActiveWorldContext context, string reason = "")
    {
        var timer = Stopwatch.StartNew();
        var diagnostics = new List<string>();
        var warnings = new List<string>();
        var world = new WorldSnapshot { WorldName = context.WorldId };
        var guildSnapshot = new GuildWorldSnapshot
        {
            SourcePath = context.WorldPath,
            WorldPath = context.WorldPath,
            LevelSavePath = context.LevelSavePath,
            IsReadOnly = true
        };
        var playerPaths = Array.Empty<string>();
        var bases = Array.Empty<BaseDiscoveryRecord>();
        var decoded = "";
        var hash = "";

        if (!string.IsNullOrWhiteSpace(reason)) diagnostics.Add(reason);
        diagnostics.Add($"World resolution: {context.ResolutionSource}");

        if (string.IsNullOrWhiteSpace(context.WorldPath) || !Directory.Exists(context.WorldPath))
        {
            warnings.Add("No active Palworld world was resolved.");
            return Complete();
        }
        if (!File.Exists(context.LevelSavePath))
        {
            warnings.Add("The resolved world does not contain Level.sav.");
            return Complete();
        }

        try
        {
            hash = PalworldSaveCodec.HashFile(context.LevelSavePath);
            var playersDirectory = Path.Combine(context.WorldPath, "Players");
            playerPaths = Directory.Exists(playersDirectory)
                ? Directory.EnumerateFiles(playersDirectory, "*.sav", SearchOption.TopDirectoryOnly)
                    .Where(path => !path.EndsWith("_dps.sav", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
                : [];
            diagnostics.Add($"Player saves discovered: {playerPaths.Length}");

            decoded = context.LevelSavePath + ".json";
            if (!File.Exists(decoded) || File.GetLastWriteTimeUtc(decoded) < context.LevelLastWriteUtc)
                decoded = codec.Decode(context.LevelSavePath);
            if (!File.Exists(decoded)) throw new FileNotFoundException("Level.sav decoding did not produce JSON.", decoded);
            diagnostics.Add("Level.sav decoded successfully.");

            var live = new LiveWorldScanner().Scan(decoded, playerPaths);
            world = live.Snapshot;
            diagnostics.AddRange(live.Diagnostics);

            var guildDiscovery = new GuildDiscoveryEngine().DiscoverWithDiagnostics(decoded);
            new GuildDiscoveryEngine().Enrich(world, guildDiscovery.Records);
            diagnostics.AddRange(guildDiscovery.Diagnostics);
            foreach (var candidatePath in guildDiscovery.CandidatePaths.Take(50))
                diagnostics.Add("Guild candidate: " + candidatePath);
            foreach (var rejection in guildDiscovery.Rejections.Take(25))
                diagnostics.Add("Guild rejection: " + rejection);
            diagnostics.Add($"Guild records discovered: {guildDiscovery.Records.Count}");

            var baseDiscovery = new BaseDiscoveryEngine().DiscoverWithDiagnostics(decoded);
            bases = baseDiscovery.Records.ToArray();
            new BaseDiscoveryEngine().Enrich(world, bases);
            diagnostics.AddRange(baseDiscovery.Diagnostics);
            foreach (var rejection in baseDiscovery.Rejections.Take(25))
                diagnostics.Add("Base rejection: " + rejection);
            diagnostics.Add($"Base records discovered: {bases.Length}");

            MergeRelationships(world);
            AppendObjectGraphDiagnostics(world, diagnostics);
            guildSnapshot = guildMapper.Read(decoded, context.WorldPath, context.LevelSavePath);
            guildSnapshot.SourceHash = hash;
            guildSnapshot.Warnings.AddRange(warnings);
            MergeWorldProjectionIntoGuildSnapshot(world, guildSnapshot);
            warnings.AddRange(guildSnapshot.Warnings);
        }
        catch (Exception ex)
        {
            warnings.Add(ex.Message);
            guildSnapshot.Warnings.Add(ex.Message);
        }

        return Complete();

        WorldDiscoverySnapshot Complete()
        {
            timer.Stop();
            diagnostics.Add($"Discovery completed in {timer.Elapsed.TotalSeconds:0.###} seconds.");
            return new WorldDiscoverySnapshot
            {
                Context = context,
                DecodedJsonPath = decoded,
                SourceHash = hash,
                ScannedUtc = DateTime.UtcNow,
                Duration = timer.Elapsed,
                World = world,
                Guilds = guildSnapshot,
                BaseRecords = bases,
                PlayerSavePaths = playerPaths,
                Diagnostics = diagnostics.Distinct().ToArray(),
                Warnings = warnings.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray()
            };
        }
    }

    private static void MergeRelationships(WorldSnapshot world)
    {
        var players = world.Players
            .Where(x => !string.IsNullOrWhiteSpace(Normalize(x.PlayerGuid)))
            .GroupBy(x => Normalize(x.PlayerGuid), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var guilds = world.Guilds
            .Where(x => !string.IsNullOrWhiteSpace(Normalize(x.GuildId)))
            .GroupBy(x => Normalize(x.GuildId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var guild in world.Guilds)
        {
            guild.GuildId = Normalize(guild.GuildId);
            guild.LeaderPlayerGuid = Normalize(guild.LeaderPlayerGuid);
            guild.MemberPlayerGuids = guild.MemberPlayerGuids.Select(Normalize).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            guild.BaseIds = guild.BaseIds.Select(Normalize).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var member in guild.MemberPlayerGuids)
                if (players.TryGetValue(member, out var player)) player.GuildId = guild.GuildId;
        }

        foreach (var worldBase in world.Bases)
        {
            worldBase.BaseId = Normalize(worldBase.BaseId);
            worldBase.GuildId = Normalize(worldBase.GuildId);

            WorldGuildRecord? owner = null;
            if (!string.IsNullOrWhiteSpace(worldBase.GuildId))
                guilds.TryGetValue(worldBase.GuildId, out owner);

            // Some save layouts keep ownership only on the guild-side base ID
            // collection. Match that relationship after both discovery passes.
            owner ??= world.Guilds.FirstOrDefault(candidate =>
                candidate.BaseIds.Any(baseId => Normalize(baseId) == worldBase.BaseId));

            // A small/new world may contain one real player guild and one base
            // while the base owner field is omitted by the decoded schema. Use
            // the relationship only when it is unambiguous.
            if (owner is null && string.IsNullOrWhiteSpace(worldBase.GuildId) && world.Bases.Count == 1)
            {
                var credibleGuilds = world.Guilds
                    .Where(candidate => candidate.MemberPlayerGuids.Count > 0 || !string.IsNullOrWhiteSpace(candidate.LeaderPlayerGuid))
                    .ToList();
                if (credibleGuilds.Count == 1) owner = credibleGuilds[0];
            }

            if (owner is null) continue;
            worldBase.GuildId = owner.GuildId;
            if (!owner.BaseIds.Contains(worldBase.BaseId, StringComparer.OrdinalIgnoreCase))
                owner.BaseIds.Add(worldBase.BaseId);
        }
    }

    private static void MergeWorldProjectionIntoGuildSnapshot(WorldSnapshot world, GuildWorldSnapshot target)
    {
        // The Guilds page is a projection of the typed Guild collection only.
        // Base discovery must never manufacture additional Guild rows.
        var authoritativeGuildIds = world.Guilds
            .Select(x => Normalize(x.GuildId))
            .Where(x => x.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        target.Guilds.RemoveAll(x => !authoritativeGuildIds.Contains(Normalize(x.GuildId)));

        foreach (var source in world.Guilds)
        {
            var guild = target.Guilds.FirstOrDefault(x => Normalize(x.GuildId) == Normalize(source.GuildId));
            if (guild is null)
            {
                guild = new GuildRow
                {
                    GuildId = source.GuildId,
                    Name = string.IsNullOrWhiteSpace(source.GuildName) ? "Unnamed Guild" : source.GuildName,
                    LeaderUid = source.LeaderPlayerGuid,
                    GroupType = "Guild"
                };
                target.Guilds.Add(guild);
            }

            guild.Name = string.IsNullOrWhiteSpace(source.GuildName) ? guild.Name : source.GuildName;
            if (string.IsNullOrWhiteSpace(guild.LeaderUid)) guild.LeaderUid = source.LeaderPlayerGuid;

            foreach (var memberId in source.MemberPlayerGuids)
            {
                var player = world.Players.FirstOrDefault(x => Normalize(x.PlayerGuid) == Normalize(memberId));
                var member = guild.Members.FirstOrDefault(x => Normalize(x.PlayerUid) == Normalize(memberId));
                if (member is null)
                {
                    member = new GuildMemberRow { PlayerUid = memberId };
                    guild.Members.Add(member);
                }

                if (!string.IsNullOrWhiteSpace(player?.PlayerName))
                    member.PlayerName = player.PlayerName;
                else if (string.IsNullOrWhiteSpace(member.PlayerName) || member.PlayerName == member.PlayerUid)
                    member.PlayerName = "Unknown Player";

                member.IsLeader = Normalize(memberId) == Normalize(guild.LeaderUid);
                member.PlayerSaveExists = !string.IsNullOrWhiteSpace(player?.SaveFilePath);
            }

            guild.Members.RemoveAll(member =>
                !source.MemberPlayerGuids.Any(id => Normalize(id) == Normalize(member.PlayerUid)));
            guild.LeaderName = guild.Members.FirstOrDefault(x => x.IsLeader)?.PlayerName ??
                               (string.IsNullOrWhiteSpace(guild.LeaderUid) ? "Unknown" : guild.LeaderUid);
            guild.Bases.Clear();
        }

        foreach (var source in world.Bases)
        {
            var guild = target.Guilds.FirstOrDefault(x => Normalize(x.GuildId) == Normalize(source.GuildId));
            if (guild is null) continue;
            guild.Bases.Add(new GuildBaseRow
            {
                BaseId = source.BaseId,
                Name = source.DisplayName,
                Location = $"{source.X:0.##}, {source.Y:0.##}, {source.Z:0.##}",
                OwnerGuildId = source.GuildId
            });
        }
    }

    private static void AppendObjectGraphDiagnostics(WorldSnapshot world, ICollection<string> diagnostics)
    {
        var playerIds = world.Players.Select(x => Normalize(x.PlayerGuid))
            .Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var guildIds = world.Guilds.Select(x => Normalize(x.GuildId))
            .Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseIds = world.Bases.Select(x => Normalize(x.BaseId))
            .Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var guildMemberLinks = world.Guilds.Sum(x => x.MemberPlayerGuids.Count);
        var resolvedGuildMemberLinks = world.Guilds.Sum(x =>
            x.MemberPlayerGuids.Count(member => playerIds.Contains(Normalize(member))));
        var guildBaseLinks = world.Guilds.Sum(x => x.BaseIds.Count);
        var resolvedGuildBaseLinks = world.Guilds.Sum(x =>
            x.BaseIds.Count(baseId => baseIds.Contains(Normalize(baseId))));
        var resolvedBaseOwners = world.Bases.Count(x => guildIds.Contains(Normalize(x.GuildId)));

        diagnostics.Add($"World object graph: Guilds={world.Guilds.Count}; Players={world.Players.Count}; Bases={world.Bases.Count}");
        diagnostics.Add($"Relationship Guild -> Players: {resolvedGuildMemberLinks}/{guildMemberLinks} resolved");
        diagnostics.Add($"Relationship Guild -> Bases: {resolvedGuildBaseLinks}/{guildBaseLinks} resolved");
        diagnostics.Add($"Relationship Base -> Guild: {resolvedBaseOwners}/{world.Bases.Count} resolved");
    }

    private void ContextChanged(object? sender, ActiveWorldContext context)
    {
        lock (gate) cached = null;
    }


    private static string Normalize(string value) => LiveWorldScanner.NormalizeId(value);
    private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(WorldDiscoverySnapshotService)); }
    public void Dispose() { if (disposed) return; disposed = true; contextService.Changed -= ContextChanged; }
}
