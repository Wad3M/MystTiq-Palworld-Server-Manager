using System.Text.Json;
using System.Text.RegularExpressions;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessPlayerGuildExplorerService
{
    private const string GuildType = "EPalGroupType::Guild";
    private static readonly Regex PlayerIdPattern = new(
        "^[A-Fa-f0-9]{32}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IServerPathProfile paths;
    private readonly HeadlessMonitoringService monitoring;
    private readonly HeadlessGameNameService? gameNames;

    public HeadlessPlayerGuildExplorerService(
        IServerPathProfile paths,
        HeadlessMonitoringService monitoring,
        HeadlessGameNameService? gameNames = null)
    {
        this.paths = paths;
        this.monitoring = monitoring;
        this.gameNames = gameNames;
    }

    public async Task<HeadlessPlayerGuildSnapshot> ExploreAsync(
        CancellationToken cancellationToken)
    {
        var world = ResolveActiveWorld();
        if (world is null)
        {
            return new HeadlessPlayerGuildSnapshot(
                false,
                false,
                "Unavailable",
                null,
                null,
                [],
                [],
                [],
                [],
                DateTimeOffset.UtcNow,
                "No active Palworld world containing Level.sav was discovered.",
                []);
        }

        var playerSaves = DiscoverPlayerSaves(world);
        var semanticPath = ResolveDecodedLevelJson(world);
        var guildRecords = new List<SemanticGuildRecord>();
        var semanticWarnings = new List<string>();
        var semanticAvailable = false;
        var semanticSource = "Player save filenames only";
        IReadOnlyDictionary<string, (double X, double Y)> baseCoordinates =
            new Dictionary<string, (double X, double Y)>();
        IReadOnlyList<SavedPlayerLocation> playerLocations = [];
        var palLocations = SavedPalLocations.Empty;

        if (semanticPath is not null)
        {
            try
            {
                var parsed = ParseGuilds(semanticPath);
                baseCoordinates = parsed.BaseCoordinates;
                playerLocations = parsed.PlayerLocations;
                palLocations = parsed.PalLocations;
                guildRecords.AddRange(parsed.Guilds);
                semanticWarnings.AddRange(parsed.Warnings);
                semanticAvailable = parsed.AuthoritativeRootCount > 0;
                semanticSource = semanticAvailable
                    ? $"Decoded GroupSaveDataMap: {semanticPath}"
                    : $"Decoded Level JSON found, but no authoritative GroupSaveDataMap: {semanticPath}";
            }
            catch (Exception ex)
            {
                semanticWarnings.Add($"Decoded Level JSON could not be parsed: {ex.Message}");
                semanticSource = $"Decoded Level JSON unavailable for semantic use: {semanticPath}";
            }
        }
        else
        {
            semanticWarnings.Add(
                "Level.sav.json was not found beside the active Level.sav. Player save identities are available, but guild/name semantics require decoded Level JSON.");
        }

        HeadlessPlayersSnapshot? live = null;
        try
        {
            live = await monitoring.GetPlayersAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            semanticWarnings.Add($"Live REST enrichment unavailable: {ex.Message}");
        }

        var guildByMember = new Dictionary<string, SemanticGuildRecord>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var guild in guildRecords)
        {
            foreach (var member in guild.MemberIds)
            {
                var normalized = NormalizeId(member);
                if (!string.IsNullOrWhiteSpace(normalized))
                    guildByMember[normalized] = guild;
            }
        }

        var allPlayerIds = new HashSet<string>(
            playerSaves.Keys,
            StringComparer.OrdinalIgnoreCase);

        foreach (var guild in guildRecords)
            foreach (var member in guild.MemberIds)
                if (PlayerIdPattern.IsMatch(NormalizeId(member)))
                    allPlayerIds.Add(NormalizeId(member));

        // Live REST identities may not have a stable save yet. Include them in the same
        // authoritative server-side projection so the GUI never has to merge remote paths.
        if (live is not null)
            foreach (var player in live.Players)
            {
                var id = NormalizeId(player.PlayerId);
                if (!string.IsNullOrWhiteSpace(id)) allPlayerIds.Add(id);
            }

        var players = new List<HeadlessPlayerExplorerItem>();
        foreach (var playerId in allPlayerIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
        {
            playerSaves.TryGetValue(playerId, out var save);
            guildByMember.TryGetValue(playerId, out var guild);

            var semanticName = guild is not null &&
                               guild.MemberNames.TryGetValue(playerId, out var knownName)
                ? knownName
                : string.Empty;

            var liveMatch = live?.Players.FirstOrDefault(player =>
                NormalizeId(player.PlayerId).Equals(playerId, StringComparison.OrdinalIgnoreCase));

            var name = !string.IsNullOrWhiteSpace(liveMatch?.Name)
                ? liveMatch.Name
                : !string.IsNullOrWhiteSpace(semanticName)
                    ? semanticName
                    : "Unknown Player";

            var isLeader = guild is not null &&
                           NormalizeId(guild.LeaderId).Equals(playerId, StringComparison.OrdinalIgnoreCase);

            players.Add(new HeadlessPlayerExplorerItem(
                playerId,
                name,
                guild?.GuildId ?? string.Empty,
                guild?.Name ?? "Unassigned",
                guild is null ? "Unassigned" : isLeader ? "Leader" : "Member",
                save is not null,
                save?.SizeBytes ?? 0,
                save?.LastWriteUtc,
                liveMatch is not null,
                liveMatch?.Platform ?? string.Empty,
                liveMatch?.Ping ?? string.Empty,
                BuildPlayerEvidence(save, guild, liveMatch, semanticAvailable)));
        }

        var playerById = players.ToDictionary(
            player => player.PlayerId,
            StringComparer.OrdinalIgnoreCase);

        var guilds = guildRecords
            .Select(guild =>
            {
                var leaderId = NormalizeId(guild.LeaderId);
                var leader = playerById.GetValueOrDefault(leaderId);
                var leaderPresent = leader?.SaveExists == true;
                var orphaned = string.IsNullOrWhiteSpace(leaderId) ||
                               !guild.MemberIds.Any(member =>
                                   NormalizeId(member).Equals(leaderId, StringComparison.OrdinalIgnoreCase)) ||
                               !leaderPresent;

                return new HeadlessGuildExplorerItem(
                    guild.GuildId,
                    string.IsNullOrWhiteSpace(guild.Name) ? "Unnamed Guild" : guild.Name,
                    leaderId,
                    leader?.PlayerName ?? guild.MemberNames.GetValueOrDefault(leaderId) ?? "Unknown",
                    guild.MemberIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    guild.BaseIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    orphaned ? "Orphaned / Needs Review" : "Healthy",
                    guild.MemberIds
                        .Select(NormalizeId)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    guild.BaseIds
                        .Select(NormalizeId)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray());
            })
            .OrderBy(guild => guild.GuildName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(guild => guild.GuildId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var detail = semanticAvailable
            ? $"{players.Count} player identity record(s), {guilds.Count} guild(s) from authoritative decoded GroupSaveDataMap evidence."
            : $"{players.Count} player save identity record(s). Guild semantics are unavailable until decoded Level.sav.json evidence is present.";

        // v0.6.6.0 "World Explorer 2" -- abandoned-base detection, cheap to derive since orphaned
        // guilds already carry their own BaseIds: any base belonging to a guild with no present
        // leader is a real candidate for cleanup review, not just a display label on the guild row.
        var abandonedBaseIds = guilds
            .Where(guild => guild.Health == "Orphaned / Needs Review")
            .SelectMany(guild => guild.BaseIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // v0.7.92.0: every base with a decoded coordinate, attributed to its owning guild when one
        // lists it. A coordinate-bearing base no guild claims is still returned (blank guild) so the
        // map can show it as unowned rather than silently dropping it.
        var baseLocations = new List<HeadlessBaseLocation>();
        var claimedBaseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var guild in guilds)
        {
            foreach (var baseId in guild.BaseIds)
            {
                if (!baseCoordinates.TryGetValue(baseId, out var point)) continue;
                claimedBaseIds.Add(baseId);
                baseLocations.Add(new HeadlessBaseLocation(baseId, guild.GuildId, guild.GuildName, point.X, point.Y));
            }
        }

        foreach (var (baseId, point) in baseCoordinates)
        {
            if (claimedBaseIds.Contains(baseId)) continue;
            baseLocations.Add(new HeadlessBaseLocation(baseId, string.Empty, "Unowned", point.X, point.Y));
        }

        // v0.8.11.0: owned Pals that are out in the world, named by owner and base here so the map needs no lookups.
        var names = palLocations.OnMap.Count > 0 && gameNames is not null ? gameNames.Get().Catalog : GameNameCatalog.Empty;
        var guildByBase = baseLocations.GroupBy(b => b.BaseId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().GuildName, StringComparer.OrdinalIgnoreCase);
        var pals = palLocations.OnMap.Select(pal => new HeadlessPalLocation(
                pal.InstanceId, pal.Species, pal.IsAlpha, pal.Level, pal.NickName, pal.OwnerPlayerId,
                playerById.GetValueOrDefault(pal.OwnerPlayerId)?.PlayerName ?? string.Empty,
                pal.Placement, pal.BaseId, guildByBase.GetValueOrDefault(pal.BaseId) ?? string.Empty, pal.X, pal.Y,
                // v0.8.13.0: the species' display name when the game's names are available.
                names.PalName(pal.Species) ?? string.Empty))
            .ToArray();
        var palSummary = new HeadlessPalSummary(palLocations.TotalPals, pals.Length, palLocations.InPalbox,
            palLocations.WithoutPosition, palLocations.Unplaced);

        return new HeadlessPlayerGuildSnapshot(
            true,
            semanticAvailable,
            semanticSource,
            world,
            semanticPath,
            players,
            guilds,
            abandonedBaseIds,
            semanticWarnings.Distinct().Take(100).ToArray(),
            DateTimeOffset.UtcNow,
            detail,
            baseLocations,
            playerLocations,
            pals,
            palSummary);
    }

    private string? ResolveActiveWorld()
    {
        if (!Directory.Exists(paths.SaveRoot))
            return null;

        try
        {
            return Directory.EnumerateFiles(
                    paths.SaveRoot,
                    "Level.sav",
                    SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .Select(info => info.DirectoryName)
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, PlayerSaveEvidence> DiscoverPlayerSaves(
        string worldPath)
    {
        var result = new Dictionary<string, PlayerSaveEvidence>(
            StringComparer.OrdinalIgnoreCase);
        var playersPath = Path.Combine(worldPath, "Players");

        if (!Directory.Exists(playersPath))
            return result;

        foreach (var path in Directory.EnumerateFiles(
                     playersPath,
                     "*.sav",
                     SearchOption.TopDirectoryOnly))
        {
            var id = NormalizeId(Path.GetFileNameWithoutExtension(path));
            if (!PlayerIdPattern.IsMatch(id))
                continue;

            try
            {
                var info = new FileInfo(path);
                if (info.Length <= 0)
                    continue;

                if (!result.TryGetValue(id, out var current) ||
                    info.LastWriteTimeUtc > current.LastWriteUtc)
                {
                    result[id] = new PlayerSaveEvidence(
                        info.FullName,
                        info.Length,
                        info.LastWriteTimeUtc);
                }
            }
            catch
            {
                // Ignore an individual unstable/inaccessible player save.
            }
        }

        return result;
    }

    private static string? ResolveDecodedLevelJson(string worldPath)
    {
        var candidates = new[]
        {
            Path.Combine(worldPath, "Level.sav.json"),
            Path.Combine(worldPath, "Level.json"),
            Path.Combine(worldPath, "Level.sav.decoded.json")
        };

        return candidates.FirstOrDefault(path =>
            File.Exists(path) && new FileInfo(path).Length > 0);
    }

    private static SemanticParseResult ParseGuilds(string jsonPath)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(jsonPath),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 256
            });

        var roots = new List<JsonElement>();
        var baseCampRoots = new List<JsonElement>();
        FindAuthoritativeRoots(document.RootElement, roots, baseCampRoots, 0);

        var guilds = new Dictionary<string, SemanticGuildRecord>(
            StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var baseCoordinates = ReadBaseCoordinates(baseCampRoots);

        foreach (var root in roots)
        {
            if (!TryGetMapEntries(root, out var entries))
            {
                warnings.Add("An authoritative GroupSaveDataMap root did not contain its expected value array.");
                continue;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (!TryGetObjectProperty(entry, "value", out var groupStruct))
                    continue;

                var groupType = ReadPropertyScalar(groupStruct, "GroupType");
                if (!string.Equals(groupType, GuildType, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!TryGetObjectProperty(groupStruct, "RawData", out var rawProperty) ||
                    !TryUnwrapValue(rawProperty, out var rawData) ||
                    rawData.ValueKind != JsonValueKind.Object)
                    continue;

                var rawType = ReadDirectScalar(rawData, "group_type");
                if (!string.IsNullOrWhiteSpace(rawType) &&
                    !string.Equals(rawType, GuildType, StringComparison.OrdinalIgnoreCase))
                    continue;

                var guildId = NormalizeId(ReadDirectScalar(rawData, "group_id"));
                if (!LooksLikeId(guildId))
                    continue;

                var guildName = CleanName(ReadDirectScalar(rawData, "guild_name"));
                var groupName = CleanName(ReadDirectScalar(rawData, "group_name"));
                var name = !string.IsNullOrWhiteSpace(guildName) ? guildName : groupName;
                var leader = NormalizeId(ReadDirectScalar(rawData, "admin_player_uid"));
                var baseIds = ReadGuidArray(rawData, "base_ids");
                var members = new List<string>();
                var memberNames = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

                if (TryGetDirectProperty(rawData, "players", out var players) &&
                    players.ValueKind == JsonValueKind.Array)
                {
                    foreach (var player in players.EnumerateArray())
                    {
                        if (player.ValueKind != JsonValueKind.Object)
                            continue;

                        var playerId = NormalizeId(ReadDirectScalar(player, "player_uid"));
                        if (!LooksLikeId(playerId))
                            continue;

                        members.Add(playerId);

                        if (TryGetDirectProperty(player, "player_info", out var playerInfo) &&
                            playerInfo.ValueKind == JsonValueKind.Object)
                        {
                            var playerName = CleanName(
                                ReadDirectScalar(playerInfo, "player_name"));
                            if (!string.IsNullOrWhiteSpace(playerName))
                                memberNames[playerId] = playerName;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(leader) &&
                    !members.Contains(leader, StringComparer.OrdinalIgnoreCase))
                    members.Insert(0, leader);

                var candidate = new SemanticGuildRecord(
                    guildId,
                    name,
                    leader,
                    members.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    memberNames,
                    baseIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

                if (guilds.TryGetValue(guildId, out var existing))
                    guilds[guildId] = MergeGuild(existing, candidate);
                else
                    guilds[guildId] = candidate;
            }
        }

        // v0.7.100.0: last-known player positions, for the offline dots on the map. A problem reading
        // them must never cost the guild and base data, so it degrades to "none".
        IReadOnlyList<SavedPlayerLocation> playerLocations = [];
        try { playerLocations = SavePlayerLocationReader.Read(document.RootElement); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            warnings.Add($"Player last-known positions could not be read: {ex.Message}");
        }

        // v0.8.11.0: owned Pals' positions, with the same "degrade to none" rule.
        var palLocations = SavedPalLocations.Empty;
        try { palLocations = SavePalLocationReader.Read(document.RootElement); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            warnings.Add($"Pal positions could not be read: {ex.Message}");
        }

        return new SemanticParseResult(
            roots.Count,
            guilds.Values.ToArray(),
            warnings,
            baseCoordinates,
            playerLocations,
            palLocations);
    }

    private static void FindAuthoritativeRoots(
        JsonElement element,
        List<JsonElement> roots,
        List<JsonElement> baseCampRoots,
        int depth)
    {
        if (depth > 80)
            return;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var key = NormalizeKey(property.Name);
                if (key == "groupsavedatamap")
                {
                    roots.Add(property.Value);
                    continue;
                }

                if (key == "basecampsavedata")
                {
                    baseCampRoots.Add(property.Value);
                    continue;
                }

                FindAuthoritativeRoots(property.Value, roots, baseCampRoots, depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                FindAuthoritativeRoots(item, roots, baseCampRoots, depth + 1);
        }
    }

    // v0.7.92.0: base world coordinates for the live map. BaseCampSaveData entries are keyed by base
    // id; each carries its own spawn_transform.translation (the same Unreal world units the REST
    // player list reports as location_x/location_y). Read from the decoded Level JSON already being
    // parsed for guilds, so there is no extra decode step and no new dependency.
    private static IReadOnlyDictionary<string, (double X, double Y)> ReadBaseCoordinates(
        IReadOnlyList<JsonElement> baseCampRoots)
    {
        var result = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in baseCampRoots)
        {
            if (!TryGetMapEntries(root, out var entries)) continue;
            foreach (var entry in entries.EnumerateArray())
            {
                var baseId = NormalizeId(ReadDirectScalar(entry, "key"));
                if (!LooksLikeId(baseId)) continue;
                if (!TryGetDirectProperty(entry, "value", out var baseStruct)) continue;
                if (!TryFindNestedProperty(baseStruct, "spawn_transform", 0, out var transform)) continue;
                if (!TryFindNestedProperty(transform, "translation", 0, out var translation)) continue;
                if (!double.TryParse(ReadDirectScalar(translation, "x"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)) continue;
                if (!double.TryParse(ReadDirectScalar(translation, "y"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)) continue;
                result[baseId] = (x, y);
            }
        }

        return result;
    }

    private static bool TryFindNestedProperty(JsonElement element, string name, int depth, out JsonElement found)
    {
        found = default;
        if (depth > 8 || element.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                found = property.Value;
                return true;
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            if (TryFindNestedProperty(property.Value, name, depth + 1, out found)) return true;
        }

        return false;
    }

    private static bool TryGetMapEntries(
        JsonElement root,
        out JsonElement entries)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            entries = root;
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (TryGetDirectProperty(root, "value", out entries) &&
                entries.ValueKind == JsonValueKind.Array)
                return true;

            if (TryGetDirectProperty(root, "Value", out entries) &&
                entries.ValueKind == JsonValueKind.Array)
                return true;
        }

        entries = default;
        return false;
    }

    private static bool TryGetObjectProperty(
        JsonElement element,
        string property,
        out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (TryGetDirectProperty(element, property, out value))
            return true;

        foreach (var child in element.EnumerateObject())
        {
            if (NormalizeKey(child.Name) == NormalizeKey(property))
            {
                value = child.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetDirectProperty(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryUnwrapValue(
        JsonElement element,
        out JsonElement value)
    {
        value = element;
        for (var depth = 0; depth < 8; depth++)
        {
            if (value.ValueKind != JsonValueKind.Object)
                return true;

            if (TryGetDirectProperty(value, "value", out var nested))
            {
                value = nested;
                continue;
            }

            return true;
        }

        return true;
    }

    private static string ReadPropertyScalar(
        JsonElement element,
        string property)
    {
        if (!TryGetObjectProperty(element, property, out var value))
            return string.Empty;

        return Scalar(value);
    }

    private static string ReadDirectScalar(
        JsonElement element,
        string property)
    {
        return TryGetDirectProperty(element, property, out var value)
            ? Scalar(value)
            : string.Empty;
    }

    private static string Scalar(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString() ?? string.Empty;

        if (element.ValueKind is JsonValueKind.Number or
            JsonValueKind.True or JsonValueKind.False)
            return element.ToString();

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (TryGetDirectProperty(element, "value", out var value))
                return Scalar(value);
            if (TryGetDirectProperty(element, "id", out var id))
                return Scalar(id);
            if (TryGetDirectProperty(element, "guid", out var guid))
                return Scalar(guid);
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ReadGuidArray(
        JsonElement element,
        string property)
    {
        if (!TryGetDirectProperty(element, property, out var array))
            return [];

        if (array.ValueKind == JsonValueKind.Object &&
            TryGetDirectProperty(array, "value", out var wrapped))
            array = wrapped;

        if (array.ValueKind != JsonValueKind.Array)
            return [];

        return array.EnumerateArray()
            .Select(Scalar)
            .Select(NormalizeId)
            .Where(LooksLikeId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static SemanticGuildRecord MergeGuild(
        SemanticGuildRecord left,
        SemanticGuildRecord right)
    {
        var names = new Dictionary<string, string>(
            left.MemberNames,
            StringComparer.OrdinalIgnoreCase);

        foreach (var pair in right.MemberNames)
            if (!string.IsNullOrWhiteSpace(pair.Value))
                names[pair.Key] = pair.Value;

        return new SemanticGuildRecord(
            left.GuildId,
            !string.IsNullOrWhiteSpace(left.Name) ? left.Name : right.Name,
            !string.IsNullOrWhiteSpace(left.LeaderId) ? left.LeaderId : right.LeaderId,
            left.MemberIds.Concat(right.MemberIds)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            names,
            left.BaseIds.Concat(right.BaseIds)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static string BuildPlayerEvidence(
        PlayerSaveEvidence? save,
        SemanticGuildRecord? guild,
        HeadlessPlayerSnapshot? live,
        bool semanticAvailable)
    {
        var evidence = new List<string>();
        if (save is not null) evidence.Add("Player .sav present");
        if (guild is not null && semanticAvailable) evidence.Add("Guild membership from GroupSaveDataMap");
        if (live is not null) evidence.Add("Currently online via Palworld REST");
        return evidence.Count == 0 ? "No strong evidence" : string.Join(" · ", evidence);
    }

    private static string NormalizeId(string? value) =>
        new((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

    private static bool LooksLikeId(string? value)
    {
        var normalized = NormalizeId(value);
        return normalized.Length >= 16 &&
               normalized.All(Uri.IsHexDigit);
    }

    private static string NormalizeKey(string value) =>
        new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static string CleanName(string? value)
    {
        var clean = value?.Trim() ?? string.Empty;
        return clean.Length > 128 ? clean[..128] : clean;
    }

    private sealed record PlayerSaveEvidence(
        string Path,
        long SizeBytes,
        DateTimeOffset LastWriteUtc);

    private sealed record SemanticGuildRecord(
        string GuildId,
        string Name,
        string LeaderId,
        IReadOnlyList<string> MemberIds,
        IReadOnlyDictionary<string, string> MemberNames,
        IReadOnlyList<string> BaseIds);

    private sealed record SemanticParseResult(
        int AuthoritativeRootCount,
        IReadOnlyList<SemanticGuildRecord> Guilds,
        IReadOnlyList<string> Warnings,
        IReadOnlyDictionary<string, (double X, double Y)> BaseCoordinates,
        IReadOnlyList<SavedPlayerLocation> PlayerLocations,
        SavedPalLocations PalLocations);
}

// v0.8.11.0: an owned Pal out in the world (working at a base, or in its owner's party), from the save.
public sealed record HeadlessPalLocation(
    string InstanceId,
    string Species,
    bool IsAlpha,
    int Level,
    string NickName,
    string OwnerPlayerId,
    string OwnerName,
    string Placement,
    string BaseId,
    string GuildName,
    double X,
    double Y,
    string SpeciesName = "");

// v0.8.11.0: what the save holds, so the map can say what it does not draw (Palbox Pals, unset positions).
public sealed record HeadlessPalSummary(int TotalPals, int OnMap, int InPalbox, int WithoutPosition, int Unplaced);

public sealed record HeadlessBaseLocation(
    string BaseId,
    string GuildId,
    string GuildName,
    double X,
    double Y);

public sealed record HeadlessPlayerExplorerItem(
    string PlayerId,
    string PlayerName,
    string GuildId,
    string GuildName,
    string Role,
    bool SaveExists,
    long SaveSizeBytes,
    DateTimeOffset? SaveLastWriteUtc,
    bool Online,
    string Platform,
    string Ping,
    string Evidence);

public sealed record HeadlessGuildExplorerItem(
    string GuildId,
    string GuildName,
    string LeaderPlayerId,
    string LeaderName,
    int MemberCount,
    int BaseCount,
    string Health,
    IReadOnlyList<string> MemberPlayerIds,
    IReadOnlyList<string> BaseIds);

public sealed record HeadlessPlayerGuildSnapshot(
    bool Available,
    bool SemanticAvailable,
    string SemanticSource,
    string? ActiveWorldPath,
    string? DecodedLevelJsonPath,
    IReadOnlyList<HeadlessPlayerExplorerItem> Players,
    IReadOnlyList<HeadlessGuildExplorerItem> Guilds,
    IReadOnlyList<string> AbandonedBaseIds,
    IReadOnlyList<string> Warnings,
    DateTimeOffset ObservedAt,
    string Detail,
    IReadOnlyList<HeadlessBaseLocation> BaseLocations,
    // v0.7.100.0: optional, so every other construction (the unavailable cases) is unchanged.
    IReadOnlyList<SavedPlayerLocation>? PlayerLocations = null,
    // v0.8.11.0: optional for the same reason.
    IReadOnlyList<HeadlessPalLocation>? PalLocations = null,
    HeadlessPalSummary? PalSummary = null);
