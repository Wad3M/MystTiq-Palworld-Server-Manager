using System.Diagnostics;
using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class WorldValidatorService
{
    public WorldValidatorReport Validate(WorldDiscoverySnapshot snapshot)
    {
        var timer = Stopwatch.StartNew();
        var report = new WorldValidatorReport
        {
            WorldId = snapshot.Context.WorldId,
            WorldPath = snapshot.Context.WorldPath,
            ScannedUtc = DateTime.UtcNow,
            PlayerCount = snapshot.World.Players.Count(x => !IsPlaceholderPlayer(x.PlayerGuid)),
            GuildCount = snapshot.World.Guilds.Count,
            BaseCount = snapshot.World.Bases.Count
        };

        if (!snapshot.IsResolved)
        {
            Add(report, "World", "Active world", WorldValidationSeverity.Critical,
                "No active Palworld world could be resolved.", "", "Resolve the active world in Settings or World Inspector.", false);
            report.Duration = timer.Elapsed;
            return report;
        }

        Add(report, "Decoder", "Level.sav decode", File.Exists(snapshot.DecodedJsonPath) ? WorldValidationSeverity.Healthy : WorldValidationSeverity.Critical,
            File.Exists(snapshot.DecodedJsonPath) ? "The active Level.sav decoded successfully." : "Decoded world JSON is unavailable.",
            snapshot.Context.WorldId,
            File.Exists(snapshot.DecodedJsonPath) ? "No action required." : "Run Palworld Save Tools diagnostics and repair the decoder.", false);

        ValidatePlayers(snapshot, report);
        ValidateGuilds(snapshot, report);
        ValidateBases(snapshot, report);
        ValidateRelationships(snapshot, report);

        foreach (var warning in snapshot.Warnings.Distinct(StringComparer.OrdinalIgnoreCase))
            Add(report, "Discovery", "Pipeline warning", WorldValidationSeverity.Warning, warning, "", "Review World Discovery Diagnostics.", false);

        if (report.CriticalCount == 0 && report.WarningCount == 0)
            Add(report, "World", "Overall integrity", WorldValidationSeverity.Healthy,
                "No ownership or relationship problems were detected in the active world.", snapshot.Context.WorldId, "No action required.", false);

        report.Duration = timer.Elapsed;
        return report;
    }

    private static void ValidatePlayers(WorldDiscoverySnapshot snapshot, WorldValidatorReport report)
    {
        var realPlayers = snapshot.World.Players.Where(x => !IsPlaceholderPlayer(x.PlayerGuid)).ToList();
        if (realPlayers.Count == 0)
        {
            Add(report, "Players", "Player inventory", WorldValidationSeverity.Warning,
                "No players were discovered in the active world.", "", "Verify the Players folder and decoded CharacterSaveParameterMap.", false);
            return;
        }

        Add(report, "Players", "Player inventory", WorldValidationSeverity.Healthy,
            $"{realPlayers.Count} player record(s) and {snapshot.PlayerSavePaths.Count} player save(s) were discovered.", "", "No action required.", false);

        foreach (var player in realPlayers)
        {
            if (string.IsNullOrWhiteSpace(player.PlayerGuid))
                Add(report, "Players", "Player identifier", WorldValidationSeverity.Warning,
                    $"Player '{player.PlayerName}' has no valid Player UID.", player.PlayerName, "Inspect the player record before repair operations.", false);
            else if (string.IsNullOrWhiteSpace(player.SaveFilePath) || !File.Exists(player.SaveFilePath))
                Add(report, "Players", "Player save", WorldValidationSeverity.Warning,
                    $"Player '{Display(player.PlayerName, player.PlayerGuid)}' does not have a matched save file.", player.PlayerGuid,
                    "Review player recovery or repair the Player UID mapping.", true);
        }
    }

    private static void ValidateGuilds(WorldDiscoverySnapshot snapshot, WorldValidatorReport report)
    {
        if (snapshot.World.Guilds.Count == 0)
        {
            Add(report, "Guilds", "Guild inventory", WorldValidationSeverity.Warning,
                "No Guild-type records were discovered.", "", "Review GroupSaveDataMap in World Explorer.", false);
            return;
        }

        Add(report, "Guilds", "Guild inventory", WorldValidationSeverity.Healthy,
            $"{snapshot.World.Guilds.Count} authoritative Guild record(s) were discovered.", "", "No action required.", false);

        var players = snapshot.World.Players.Where(x => !IsPlaceholderPlayer(x.PlayerGuid))
            .GroupBy(x => x.PlayerGuid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var guild in snapshot.World.Guilds)
        {
            var guildName = Display(guild.GuildName, guild.GuildId);
            if (string.IsNullOrWhiteSpace(guild.LeaderPlayerGuid) || !players.ContainsKey(guild.LeaderPlayerGuid))
                Add(report, "Guilds", "Guild leader", WorldValidationSeverity.Critical,
                    $"Guild '{guildName}' has no valid leader relationship.", guild.GuildId,
                    "Stage a Transfer Leadership or Repair Mappings transaction.", true);
            else
                Add(report, "Guilds", "Guild leader", WorldValidationSeverity.Healthy,
                    $"Guild '{guildName}' has a valid leader.", guild.GuildId, "No action required.", false);

            foreach (var member in guild.MemberPlayerGuids.Distinct(StringComparer.OrdinalIgnoreCase))
                if (!players.ContainsKey(member))
                    Add(report, "Guilds", "Guild membership", WorldValidationSeverity.Warning,
                        $"Guild '{guildName}' references missing player {member}.", member,
                        "Stage a player-to-guild mapping repair.", true);
        }
    }

    private static void ValidateBases(WorldDiscoverySnapshot snapshot, WorldValidatorReport report)
    {
        if (snapshot.World.Bases.Count == 0)
        {
            Add(report, "Bases", "Base inventory", WorldValidationSeverity.Warning,
                "No authoritative BaseCamp records were discovered.", "", "Review BaseCampSaveData in World Explorer.", false);
            return;
        }

        Add(report, "Bases", "Base inventory", WorldValidationSeverity.Healthy,
            $"{snapshot.World.Bases.Count} authoritative Base record(s) were discovered.", "", "No action required.", false);

        var guilds = snapshot.World.Guilds.Where(x => !string.IsNullOrWhiteSpace(x.GuildId))
            .GroupBy(x => x.GuildId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var guildNames = snapshot.World.Guilds.Where(x => !string.IsNullOrWhiteSpace(x.GuildId))
            .ToDictionary(x => x.GuildId, x => x.GuildName, StringComparer.OrdinalIgnoreCase);
        var ordinal = 0;
        foreach (var worldBase in snapshot.World.Bases)
        {
            ordinal++;
            guildNames.TryGetValue(worldBase.GuildId, out var guildName);
            var name = BaseDisplayNameResolver.Resolve(worldBase.DisplayName, guildName, worldBase.BaseId, ordinal);
            if (string.IsNullOrWhiteSpace(worldBase.GuildId) || !guilds.ContainsKey(worldBase.GuildId))
                Add(report, "Bases", "Base ownership", WorldValidationSeverity.Critical,
                    $"Base '{name}' is not linked to a valid Guild.", worldBase.BaseId,
                    "Stage a Transfer Ownership or Repair Mappings transaction.", true);
            else
                Add(report, "Bases", "Base ownership", WorldValidationSeverity.Healthy,
                    $"Base '{name}' is linked to a valid Guild.", worldBase.BaseId, "No action required.", false);
        }
    }

    private static void ValidateRelationships(WorldDiscoverySnapshot snapshot, WorldValidatorReport report)
    {
        var bases = snapshot.World.Bases.Where(x => !string.IsNullOrWhiteSpace(x.BaseId))
            .GroupBy(x => x.BaseId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var guild in snapshot.World.Guilds)
        {
            foreach (var baseId in guild.BaseIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!bases.TryGetValue(baseId, out var worldBase))
                    Add(report, "Relationships", "Guild to Base", WorldValidationSeverity.Warning,
                        $"Guild '{Display(guild.GuildName, guild.GuildId)}' references Base {baseId}, but the Base record is missing.", baseId,
                        "Review BaseCampSaveData or repair the Guild-to-Base mapping.", true);
                else if (!string.Equals(worldBase.GuildId, guild.GuildId, StringComparison.OrdinalIgnoreCase))
                    Add(report, "Relationships", "Bidirectional ownership", WorldValidationSeverity.Critical,
                        $"Guild and Base ownership disagree for Base '{Display(worldBase.DisplayName, baseId)}'.", baseId,
                        "Stage a Guild-to-Base mapping repair.", true);
            }
        }
    }

    private static void Add(WorldValidatorReport report, string category, string check, WorldValidationSeverity severity,
        string message, string entityId, string action, bool repairAvailable)
        => report.Findings.Add(new WorldValidationFindingRow
        {
            Category = category,
            Check = check,
            Severity = severity,
            Message = message,
            EntityId = entityId,
            RecommendedAction = action,
            RepairAvailable = repairAvailable
        });

    private static bool IsPlaceholderPlayer(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray());
        return normalized.Length == 0 || normalized.All(c => c == '0');
    }

    private static string Display(string? name, string fallback)
        => string.IsNullOrWhiteSpace(name) ? fallback : name;
}
