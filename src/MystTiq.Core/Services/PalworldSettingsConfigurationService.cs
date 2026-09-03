using System.Text;

namespace MystTiq.Core.Services;

public sealed record PalworldSettingEntry(string Name, string DisplayName, string Category, string Value, string DefaultValue, bool IsModified);
public sealed record PalworldConfigurationSnapshot(string ConfigurationPath, string DefaultConfigurationPath, bool Exists, IReadOnlyList<PalworldSettingEntry> Settings, string Detail);
public sealed record PalworldConfigurationSaveResult(bool Success, string Message, string? BackupPath, IReadOnlyList<string> ValidationErrors);
public sealed record PalworldDefaultConfigurationRequest(
    bool ConfirmCreate,
    string ServerName,
    string ServerDescription,
    string AdminPassword,
    string ServerPassword,
    int MaximumPlayers,
    int GamePort,
    int RestPort);

public sealed class PalworldSettingsConfigurationService
{
    private const string Marker = "OptionSettings=(";
    private readonly IServerPathProfile paths;
    public PalworldSettingsConfigurationService(IServerPathProfile paths) => this.paths = paths ?? throw new ArgumentNullException(nameof(paths));

    public string ConfigurationPath => Path.Combine(paths.ConfigRoot, "PalWorldSettings.ini");
    public string DefaultConfigurationPath => Path.Combine(paths.ServerRoot, "DefaultPalWorldSettings.ini");

    public PalworldConfigurationSnapshot Load()
    {
        if (!File.Exists(ConfigurationPath))
            return new(ConfigurationPath, DefaultConfigurationPath, false, [], $"PalWorldSettings.ini was not found at {ConfigurationPath}.");

        List<ParsedSetting> active;
        try { active = ParseFile(ConfigurationPath); }
        catch (InvalidDataException ex)
        {
            // A freshly-started PalServer can leave this file present but not yet populated
            // (observed as a 1-byte file before the server's first full config write). That is
            // a legitimate transient state, not corruption, so status/config reads must degrade
            // gracefully here rather than throwing out of the aggregate status poll.
            return new(ConfigurationPath, DefaultConfigurationPath, true, [],
                $"PalWorldSettings.ini exists but does not yet contain OptionSettings; the server may still be starting or configuration is not written yet. {ex.Message}");
        }
        var defaults = File.Exists(DefaultConfigurationPath) ? ParseFile(DefaultConfigurationPath) : [];
        var defaultMap = defaults.ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var rows = active.Select(x => new PalworldSettingEntry(
            x.Name, GetDisplayName(x.Name), GetCategory(x.Name), x.Value,
            defaultMap.TryGetValue(x.Name, out var d) ? d : string.Empty,
            defaultMap.TryGetValue(x.Name, out var dv) && !string.Equals(x.Value, dv, StringComparison.Ordinal))).ToArray();
        return new(ConfigurationPath, DefaultConfigurationPath, true, rows, $"Loaded {rows.Length} active Palworld settings.");
    }

    public PalworldConfigurationSaveResult Save(IEnumerable<PalworldSettingEntry> settings)
    {
        var rows = settings?.ToArray() ?? [];
        var errors = new List<string>();
        if (!File.Exists(ConfigurationPath)) errors.Add("PalWorldSettings.ini does not exist.");
        if (rows.Length == 0) errors.Add("No Palworld settings were supplied.");
        var duplicate = rows.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null) errors.Add($"Duplicate Palworld setting: {duplicate.Key}");
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name)) errors.Add("A setting has no name.");
            if (row.Name.IndexOfAny(['=', ',', '\r', '\n']) >= 0) errors.Add($"Invalid setting name: {row.Name}");
            if ((row.Value ?? string.Empty).Contains('\r') || (row.Value ?? string.Empty).Contains('\n')) errors.Add($"{row.Name} cannot contain a line break.");
        }
        if (errors.Count > 0) return new(false, "Palworld configuration validation failed.", null, errors);

        var original = File.ReadAllText(ConfigurationPath);
        var start = original.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0) return new(false, "OptionSettings was not found in PalWorldSettings.ini.", null, ["OptionSettings marker missing."]);
        start += Marker.Length;
        int end;
        try { end = FindEnd(original, start); }
        catch (Exception ex) { return new(false, ex.Message, null, [ex.Message]); }

        var backupDirectory = Path.Combine(paths.ConfigRoot, "ConfigBackups");
        Directory.CreateDirectory(backupDirectory);
        var backup = Path.Combine(backupDirectory, $"PalWorldSettings_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.ini");
        File.Copy(ConfigurationPath, backup, true);
        var body = string.Join(",", rows.Select(x => $"{x.Name.Trim()}={(x.Value ?? string.Empty).Trim()}"));
        var replacement = original[..start] + body + original[end..];
        var temp = ConfigurationPath + $".tmp-{Guid.NewGuid():N}";
        try { File.WriteAllText(temp, replacement); File.Move(temp, ConfigurationPath, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return new(true, "PalWorldSettings.ini saved. A timestamped rollback copy was created.", backup, []);
    }

    public PalworldConfigurationSaveResult CreateDefault(PalworldDefaultConfigurationRequest request)
    {
        var errors = ValidateDefaultRequest(request);
        if (!request.ConfirmCreate) errors.Add("Explicit confirmation is required before creating server settings.");
        if (File.Exists(ConfigurationPath))
            errors.Add("PalWorldSettings.ini already exists. Use Configuration to edit the active settings.");
        if (!File.Exists(DefaultConfigurationPath))
            errors.Add($"DefaultPalWorldSettings.ini was not found at {DefaultConfigurationPath}.");
        if (errors.Count > 0)
            return new(false, "Default Palworld configuration was not created.", null, errors);

        Directory.CreateDirectory(paths.ConfigRoot);
        var created = false;
        try
        {
            File.Copy(DefaultConfigurationPath, ConfigurationPath, false);
            created = true;
            var snapshot = Load();
            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ServerName"] = Quote(request.ServerName),
                ["ServerDescription"] = Quote(request.ServerDescription),
                ["AdminPassword"] = Quote(request.AdminPassword),
                ["ServerPassword"] = Quote(request.ServerPassword),
                ["ServerPlayerMaxNum"] = request.MaximumPlayers.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["PublicPort"] = request.GamePort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["RESTAPIPort"] = request.RestPort.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            var rows = snapshot.Settings.Select(row => replacements.TryGetValue(row.Name, out var value)
                ? row with { Value = value }
                : row).ToArray();
            var result = Save(rows);
            if (result.Success)
                return result with { Message = "Default PalWorldSettings.ini created and validated. Existing REST/RCON enablement defaults were preserved." };
            File.Delete(ConfigurationPath);
            return result;
        }
        catch (Exception ex)
        {
            if (created && File.Exists(ConfigurationPath)) File.Delete(ConfigurationPath);
            return new(false, "Default Palworld configuration could not be created.", null, [ex.Message]);
        }
    }

    public int GetConfiguredGamePort(int fallback = 8211)
    {
        try
        {
            if (!File.Exists(ConfigurationPath)) return fallback;
            var value = ParseFile(ConfigurationPath).FirstOrDefault(x => x.Name.Equals("PublicPort", StringComparison.OrdinalIgnoreCase))?.Value;
            return int.TryParse(value?.Trim().Trim('"'), out var port) && port is > 0 and <= 65535 ? port : fallback;
        }
        catch { return fallback; }
    }

    private static List<ParsedSetting> ParseFile(string path)
    {
        var text = File.ReadAllText(path);
        var start = text.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0) throw new InvalidDataException($"OptionSettings was not found in {path}.");
        start += Marker.Length;
        var end = FindEnd(text, start);
        return Split(text[start..end]).Select(token =>
        {
            var equals = FindUnquotedEquals(token);
            if (equals < 1) throw new InvalidDataException($"Malformed setting: {token}");
            return new ParsedSetting(token[..equals].Trim(), token[(equals + 1)..].Trim());
        }).ToList();
    }

    private static int FindEnd(string text, int start)
    {
        var quoted = false; var escaped = false; var depth = 1;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (escaped) { escaped = false; continue; }
            if (c == '\\' && quoted) { escaped = true; continue; }
            if (c == '"') { quoted = !quoted; continue; }
            if (quoted) continue;
            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return i;
        }
        throw new InvalidDataException("OptionSettings is malformed.");
    }

    private static IEnumerable<string> Split(string body)
    {
        var b = new StringBuilder(); var quoted = false; var escaped = false; var depth = 0;
        foreach (var c in body)
        {
            if (escaped) { b.Append(c); escaped = false; continue; }
            if (c == '\\' && quoted) { b.Append(c); escaped = true; continue; }
            if (c == '"') { quoted = !quoted; b.Append(c); continue; }
            if (!quoted)
            {
                if (c is '(' or '[' or '{') depth++;
                else if (c is ')' or ']' or '}') depth--;
                else if (c == ',' && depth == 0) { yield return b.ToString(); b.Clear(); continue; }
            }
            b.Append(c);
        }
        if (b.Length > 0) yield return b.ToString();
    }

    private static int FindUnquotedEquals(string token)
    {
        var quoted = false; var escaped = false;
        for (var i = 0; i < token.Length; i++)
        {
            var c = token[i];
            if (escaped) { escaped = false; continue; }
            if (c == '\\' && quoted) { escaped = true; continue; }
            if (c == '"') { quoted = !quoted; continue; }
            if (c == '=' && !quoted) return i;
        }
        return -1;
    }

    private static List<string> ValidateDefaultRequest(PalworldDefaultConfigurationRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.ServerName)) errors.Add("Server Name is required.");
        if (request.ServerName.Length > 128) errors.Add("Server Name must be 128 characters or fewer.");
        if (request.ServerDescription.Length > 512) errors.Add("Description must be 512 characters or fewer.");
        if (request.MaximumPlayers is < 1 or > 128) errors.Add("Maximum Players must be between 1 and 128.");
        if (request.GamePort is < 1 or > 65535) errors.Add("Game Port must be between 1 and 65535.");
        if (request.RestPort is < 1 or > 65535) errors.Add("REST Port must be between 1 and 65535.");
        if (request.GamePort == request.RestPort) errors.Add("Game Port and REST Port must be different.");
        foreach (var value in new[] { request.ServerName, request.ServerDescription, request.AdminPassword, request.ServerPassword })
            if (value.Contains('\r') || value.Contains('\n')) errors.Add("Setup text values cannot contain line breaks.");
        return errors;
    }

    private static string Quote(string value) => $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static string GetDisplayName(string name) => name switch
    {
        "ServerName" => "Server Name", "ServerDescription" => "Server Description", "AdminPassword" => "Admin Password", "ServerPassword" => "Server Password",
        "ServerPlayerMaxNum" => "Maximum Players", "PublicPort" => "Public Game Port", "RESTAPIEnabled" => "Enable REST API", "RESTAPIPort" => "REST API Port",
        "RCONEnabled" => "Enable RCON", "RCONPort" => "RCON Port", "DayTimeSpeedRate" => "Daytime Speed", "NightTimeSpeedRate" => "Nighttime Speed",
        _ => SplitCamel(name)
    };

    private static string GetCategory(string name)
    {
        if (name.Contains("Password", StringComparison.OrdinalIgnoreCase) || name.Contains("Admin", StringComparison.OrdinalIgnoreCase)) return "Security";
        if (name.Contains("Port", StringComparison.OrdinalIgnoreCase) || name.Contains("Public", StringComparison.OrdinalIgnoreCase) || name.Contains("REST", StringComparison.OrdinalIgnoreCase) || name.Contains("RCON", StringComparison.OrdinalIgnoreCase)) return "Network / Admin";
        if (name.Contains("Player", StringComparison.OrdinalIgnoreCase) || name.Contains("Guild", StringComparison.OrdinalIgnoreCase)) return "Players";
        if (name.Contains("Rate", StringComparison.OrdinalIgnoreCase) || name.Contains("Speed", StringComparison.OrdinalIgnoreCase) || name.Contains("Damage", StringComparison.OrdinalIgnoreCase)) return "Gameplay";
        return "World / Other";
    }

    private static string SplitCamel(string name)
    {
        var b = new StringBuilder();
        for (var i = 0; i < name.Length; i++) { var c = name[i]; if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1])) b.Append(' '); b.Append(c); }
        return b.ToString();
    }
    private sealed record ParsedSetting(string Name, string Value);
}
