using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class ConfigService(AppSettings settings)
{
    private const string Marker = "OptionSettings=(";

    public string DefaultConfigFile => Path.Combine(settings.ServerRoot, "DefaultPalWorldSettings.ini");

    public ObservableCollection<SettingRow> Load()
    {
        if (!File.Exists(settings.ConfigFile))
            throw new FileNotFoundException("PalWorldSettings.ini was not found.", settings.ConfigFile);

        var active = ParseFile(settings.ConfigFile);
        // Some current dedicated-server installations do not ship the historical
        // DefaultPalWorldSettings.ini template. The active configuration is still a
        // valid source of editable settings, so use it as the comparison baseline
        // instead of failing the entire Configuration page.
        var defaults = File.Exists(DefaultConfigFile)
            ? ParseFile(DefaultConfigFile)
            : active.Select(item => new ParsedSetting(item.Name, item.Value)).ToList();
        var activeLookup = active.ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new ObservableCollection<SettingRow>();

        foreach (var item in defaults)
        {
            known.Add(item.Name);
            rows.Add(new SettingRow
            {
                Name = item.Name,
                DisplayName = GetDisplayName(item.Name),
                Category = GetCategory(item.Name),
                Description = GetDescription(item.Name),
                DefaultValue = item.Value,
                Value = activeLookup.TryGetValue(item.Name, out var activeValue)
                    ? activeValue
                    : item.Value
            });
        }

        foreach (var item in active.Where(x => !known.Contains(x.Name)))
        {
            rows.Add(new SettingRow
            {
                Name = item.Name,
                DisplayName = GetDisplayName(item.Name),
                Category = GetCategory(item.Name),
                Description = GetDescription(item.Name),
                DefaultValue = "(not present in current default file)",
                Value = item.Value
            });
        }

        foreach (var row in rows)
            row.MarkLoaded();

        return rows;
    }

    public string? TryReadAdminPassword()
    {
        if (!File.Exists(settings.ConfigFile)) return null;
        var password = ParseFile(settings.ConfigFile)
            .FirstOrDefault(x => x.Name.Equals("AdminPassword", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        if (string.IsNullOrWhiteSpace(password)) return null;
        password = password.Trim();

        if (password.Length >= 2 && password[0] == '"' && password[^1] == '"')
            password = password[1..^1];

        return password.Replace("\\\"", "\"");
    }

    public void Save(IEnumerable<SettingRow> rows)
    {
        var list = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToList();

        var duplicate = list
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
            throw new InvalidDataException($"Duplicate setting: {duplicate.Key}");

        // A blank editor cell means "use this setting's default", not an
        // invalid configuration. This is important for collection settings
        // such as DenyTechnologyList as well as optional text settings whose
        // valid default may itself be empty.
        foreach (var row in list)
        {
            if (!string.IsNullOrWhiteSpace(row.Value)) continue;

            var defaultValue = row.DefaultValue?.Trim() ?? string.Empty;
            if (!defaultValue.StartsWith("(not present", StringComparison.OrdinalIgnoreCase))
            {
                row.Value = defaultValue;
                continue;
            }

            // Custom settings have no known server default. Preserve an
            // intentionally empty value rather than blocking every save.
            row.Value = string.Empty;
        }

        var invalid = list.Where(x => !x.IsValid).ToList();
        if (invalid.Count > 0)
            throw new InvalidDataException("Correct invalid settings before saving:\n" + string.Join("\n", invalid.Take(8).Select(x => $"• {x.DisplayName}: {x.ValidationMessage}")));

        var original = File.ReadAllText(settings.ConfigFile);
        var backupDirectory = Path.Combine(Path.GetDirectoryName(settings.ConfigFile)!, "ConfigBackups");
        Directory.CreateDirectory(backupDirectory);
        var backupPath = Path.Combine(backupDirectory, $"PalWorldSettings_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.ini");
        File.Copy(settings.ConfigFile, backupPath, true);
        var start = original.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0) throw new InvalidDataException("OptionSettings not found.");
        start += Marker.Length;
        var end = FindEnd(original, start);
        var body = string.Join(",", list.Select(x => $"{x.Name.Trim()}={(x.Value ?? string.Empty).Trim()}"));

        AtomicFile.Write(settings.ConfigFile, original[..start] + body + original[end..]);
    }


    public string Export(string destination)
    {
        if (!File.Exists(settings.ConfigFile)) throw new FileNotFoundException("PalWorldSettings.ini was not found.", settings.ConfigFile);
        File.Copy(settings.ConfigFile, destination, true);
        return destination;
    }

    public void Import(string source)
    {
        _ = ParseFile(source);
        var backupDirectory = Path.Combine(Path.GetDirectoryName(settings.ConfigFile)!, "ConfigBackups");
        Directory.CreateDirectory(backupDirectory);
        if (File.Exists(settings.ConfigFile))
            File.Copy(settings.ConfigFile, Path.Combine(backupDirectory, $"PalWorldSettings_before_import_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.ini"), true);
        Directory.CreateDirectory(Path.GetDirectoryName(settings.ConfigFile)!);
        AtomicFile.Write(settings.ConfigFile, File.ReadAllText(source));
    }

    private static string GetDisplayName(string name)
    {
        var replacements = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServerName"]="Server Name", ["ServerDescription"]="Server Description", ["AdminPassword"]="Admin Password",
            ["ServerPassword"]="Server Password", ["ServerPlayerMaxNum"]="Maximum Players", ["RESTAPIEnabled"]="Enable REST API", ["RESTAPIPort"]="REST API Port",
            ["RCONEnabled"]="Enable RCON", ["RCONPort"]="RCON Port",
            ["PublicPort"]="Public Game Port", ["DayTimeSpeedRate"]="Daytime Speed", ["NightTimeSpeedRate"]="Nighttime Speed",
            ["PlayerAutoHPRegeneRate"]="Player HP Regeneration", ["PalAutoHPRegeneRate"]="Pal HP Regeneration",
            ["WorkSpeedRate"]="Work Speed", ["ItemWeightRate"]="Item Weight", ["ItemCorruptionMultiplier"]="Food / Item Spoilage"
        };
        if (replacements.TryGetValue(name, out var friendly)) return friendly;
        var builder = new StringBuilder();
        for (var i=0;i<name.Length;i++) { var ch=name[i]; if (i>0 && char.IsUpper(ch) && !char.IsUpper(name[i-1])) builder.Append(' '); builder.Append(ch); }
        return builder.ToString();
    }

    private static string GetCategory(string name)
    {
        if (name.Equals("AdminPassword", StringComparison.OrdinalIgnoreCase) || name.Contains("REST", StringComparison.OrdinalIgnoreCase) || name.Contains("RCON", StringComparison.OrdinalIgnoreCase)) return "Remote Admin";
        if (name.Contains("Password", StringComparison.OrdinalIgnoreCase) || name.Contains("Ban", StringComparison.OrdinalIgnoreCase) || name.Contains("Admin", StringComparison.OrdinalIgnoreCase)) return "Security";
        if (name.Contains("Port", StringComparison.OrdinalIgnoreCase) || name.Contains("Public", StringComparison.OrdinalIgnoreCase)) return "Network";
        if (name.Contains("Player", StringComparison.OrdinalIgnoreCase) || name.Contains("Guild", StringComparison.OrdinalIgnoreCase)) return "Players";
        if (name.Contains("BaseCamp", StringComparison.OrdinalIgnoreCase) || name.Contains("Build", StringComparison.OrdinalIgnoreCase) || name.Contains("Drop", StringComparison.OrdinalIgnoreCase) || name.Contains("Collection", StringComparison.OrdinalIgnoreCase)) return "World";
        if (name.Contains("Rate", StringComparison.OrdinalIgnoreCase) || name.Contains("Speed", StringComparison.OrdinalIgnoreCase) || name.Contains("Damage", StringComparison.OrdinalIgnoreCase) || name.Contains("Stamina", StringComparison.OrdinalIgnoreCase)) return "Gameplay";
        if (name.Contains("Network", StringComparison.OrdinalIgnoreCase) || name.Contains("Tick", StringComparison.OrdinalIgnoreCase) || name.Contains("Performance", StringComparison.OrdinalIgnoreCase)) return "Performance";
        if (name.Contains("Mod", StringComparison.OrdinalIgnoreCase) || name.Contains("Crossplay", StringComparison.OrdinalIgnoreCase)) return "Mods";
        return "Other";
    }
    private static List<ParsedSetting> ParseFile(string path)
    {
        var text = File.ReadAllText(path);
        var start = text.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0) throw new InvalidDataException($"OptionSettings was not found in {path}.");
        start += Marker.Length;
        var end = FindEnd(text, start);

        return Split(text[start..end])
            .Select(token =>
            {
                var equals = FindUnquotedEquals(token);
                if (equals < 1) throw new InvalidDataException($"Malformed setting: {token}");
                return new ParsedSetting(token[..equals].Trim(), token[(equals + 1)..].Trim());
            })
            .ToList();
    }

    private static int FindEnd(string text, int start)
    {
        var quoted = false;
        var escaped = false;
        var depth = 1;

        for (var index = start; index < text.Length; index++)
        {
            var character = text[index];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\' && quoted)
            {
                escaped = true;
                continue;
            }

            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (quoted) continue;
            if (character == '(') depth++;
            else if (character == ')' && --depth == 0) return index;
        }

        throw new InvalidDataException("OptionSettings is malformed.");
    }

    private static IEnumerable<string> Split(string body)
    {
        var builder = new StringBuilder();
        var quoted = false;
        var escaped = false;
        var depth = 0;

        foreach (var character in body)
        {
            if (escaped)
            {
                builder.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\' && quoted)
            {
                builder.Append(character);
                escaped = true;
                continue;
            }

            if (character == '"')
            {
                quoted = !quoted;
                builder.Append(character);
                continue;
            }

            if (!quoted)
            {
                if (character is '(' or '[' or '{') depth++;
                else if (character is ')' or ']' or '}') depth--;

                if (character == ',' && depth == 0)
                {
                    yield return builder.ToString().Trim();
                    builder.Clear();
                    continue;
                }
            }

            builder.Append(character);
        }

        if (builder.Length > 0)
            yield return builder.ToString().Trim();
    }

    private static int FindUnquotedEquals(string token)
    {
        var quoted = false;
        var escaped = false;

        for (var index = 0; index < token.Length; index++)
        {
            var character = token[index];
            if (escaped) { escaped = false; continue; }
            if (character == '\\' && quoted) { escaped = true; continue; }
            if (character == '"') { quoted = !quoted; continue; }
            if (character == '=' && !quoted) return index;
        }

        return -1;
    }


    private static string GetDescription(string name)
    {
        if (Descriptions.TryGetValue(name, out var description))
            return description;

        // Keep unknown/new Palworld settings readable instead of leaving the column blank.
        var builder = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (i > 0 && char.IsUpper(ch) && !char.IsUpper(name[i - 1]))
                builder.Append(' ');
            builder.Append(ch);
        }
        return builder.Length == 0 ? "Palworld server setting." : builder + ".";
    }

    private static readonly IReadOnlyDictionary<string, string> Descriptions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Difficulty"] = "Overall server difficulty preset.",
            ["DayTimeSpeedRate"] = "Multiplier controlling how quickly daytime passes.",
            ["NightTimeSpeedRate"] = "Multiplier controlling how quickly nighttime passes.",
            ["ExpRate"] = "Experience gained by players and Pals.",
            ["PalCaptureRate"] = "Multiplier applied to Pal capture probability.",
            ["PalSpawnNumRate"] = "Multiplier for the number of wild Pals spawned.",
            ["PalDamageRateAttack"] = "Damage dealt by Pals.",
            ["PalDamageRateDefense"] = "Damage received by Pals.",
            ["PlayerDamageRateAttack"] = "Damage dealt by players.",
            ["PlayerDamageRateDefense"] = "Damage received by players.",
            ["PlayerStomachDecreaceRate"] = "Rate at which player hunger decreases.",
            ["PlayerStaminaDecreaceRate"] = "Rate at which player stamina is consumed.",
            ["PlayerAutoHPRegeneRate"] = "Player health regeneration rate while active.",
            ["PlayerAutoHpRegeneRateInSleep"] = "Player health regeneration rate while sleeping.",
            ["PalStomachDecreaceRate"] = "Rate at which Pal hunger decreases.",
            ["PalStaminaDecreaceRate"] = "Rate at which Pal stamina is consumed.",
            ["PalAutoHPRegeneRate"] = "Pal health regeneration rate while active.",
            ["PalAutoHpRegeneRateInSleep"] = "Pal health regeneration rate while sleeping in a Palbox.",
            ["BuildObjectDamageRate"] = "Damage dealt to structures and build objects.",
            ["BuildObjectDeteriorationDamageRate"] = "Rate at which structures deteriorate over time.",
            ["CollectionDropRate"] = "Amount of resources dropped from gathering nodes.",
            ["CollectionObjectHpRate"] = "Health of gatherable resource objects.",
            ["CollectionObjectRespawnSpeedRate"] = "Respawn speed of gatherable resource objects.",
            ["EnemyDropItemRate"] = "Amount of items dropped by defeated enemies.",
            ["DeathPenalty"] = "Items, equipment, or Pals lost when a player dies.",
            ["bEnablePlayerToPlayerDamage"] = "Allows players to damage other players.",
            ["bEnableFriendlyFire"] = "Allows damage to friendly players or allied targets.",
            ["bEnableInvaderEnemy"] = "Enables enemy raid events against player bases.",
            ["bActiveUNKO"] = "Enables Pal feces production.",
            ["bEnableAimAssistPad"] = "Enables aim assistance for gamepad users.",
            ["bEnableAimAssistKeyboard"] = "Enables aim assistance for keyboard and mouse users.",
            ["DropItemMaxNum"] = "Maximum number of dropped item entities in the world.",
            ["DropItemMaxNum_UNKO"] = "Maximum number of Pal feces items in the world.",
            ["BaseCampMaxNum"] = "Maximum total number of bases allowed on the server.",
            ["BaseCampWorkerMaxNum"] = "Maximum number of worker Pals assigned to one base.",
            ["DropItemAliveMaxHours"] = "Hours before dropped items are removed.",
            ["bAutoResetGuildNoOnlinePlayers"] = "Automatically removes guilds with no active members after the configured period.",
            ["AutoResetGuildTimeNoOnlinePlayers"] = "Hours before an inactive guild is automatically reset.",
            ["GuildPlayerMaxNum"] = "Maximum number of players allowed in one guild.",
            ["PalEggDefaultHatchingTime"] = "Base time required to hatch Pal eggs.",
            ["WorkSpeedRate"] = "Multiplier applied to work performed at bases and crafting stations.",
            ["ItemCorruptionMultiplier"] = "Multiplier controlling food and item spoilage/corruption speed.",
            ["bIsMultiplay"] = "Enables multiplayer mode.",
            ["bIsPvP"] = "Enables PvP server behavior.",
            ["bCanPickupOtherGuildDeathPenaltyDrop"] = "Allows players to pick up death drops belonging to another guild.",
            ["bEnableNonLoginPenalty"] = "Enables penalties affecting players or bases while offline.",
            ["bEnableFastTravel"] = "Enables fast travel points.",
            ["bIsStartLocationSelectByMap"] = "Allows new players to choose a starting location from the map.",
            ["bExistPlayerAfterLogout"] = "Keeps player characters present after logout.",
            ["bEnableDefenseOtherGuildPlayer"] = "Controls protection from players belonging to other guilds.",
            ["CoopPlayerMaxNum"] = "Maximum players in a cooperative session.",
            ["ServerPlayerMaxNum"] = "Maximum simultaneous players on the dedicated server.",
            ["ServerName"] = "Public name displayed for the server.",
            ["ServerDescription"] = "Description displayed in the server browser.",
            ["AdminPassword"] = "Shared administrator password used by both REST API and RCON authentication.",
            ["ServerPassword"] = "Password players must enter to join the server.",
            ["PublicPort"] = "Public game connection port advertised by the server.",
            ["PublicIP"] = "Public IP address advertised by the server.",
            ["RCONEnabled"] = "Enables Palworld's legacy remote console service. REST remains the preferred administration interface.",
            ["RCONPort"] = "Network port used by the remote console service.",
            ["Region"] = "Region text used when publishing the server.",
            ["bUseAuth"] = "Enables platform authentication.",
            ["BanListURL"] = "URL of the ban list loaded by the server.",
            ["RESTAPIEnabled"] = "Enables Palworld's REST administration API.",
            ["RESTAPIPort"] = "Network port used by the REST administration API.",
            ["bShowPlayerList"] = "Allows the server to expose its connected-player list.",
            ["ChatPostLimitPerMinute"] = "Maximum chat messages a player may send per minute.",
            ["CrossplayPlatforms"] = "Platforms permitted to connect through crossplay.",
            ["LogFormatType"] = "Format used for server log output.",
            ["SupplyDropSpan"] = "Interval between supply-drop events.",
            ["EnablePredatorBossPal"] = "Enables predator boss Pals.",
            ["MaxBuildingLimitNum"] = "Maximum number of build objects allowed.",
            ["ServerReplicatePawnCullDistance"] = "Distance at which player and Pal pawns stop replicating to clients.",
            ["AllowConnectPlatform"] = "Restricts connections to selected client platforms."
        };

    private sealed record ParsedSetting(string Name, string Value);
}
