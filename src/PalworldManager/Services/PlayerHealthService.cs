using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class PlayerHealthService
{
    private readonly AppSettings settings;

    public PlayerHealthService(AppSettings settings) => this.settings = settings;

    public PlayerHealthReport Analyze(PlayerRow player, IReadOnlyCollection<PlayerRow> allPlayers)
    {
        var checks = new List<PlayerHealthCheckRow>();
        var duplicates = new List<string>();
        var repairs = new List<string>();

        AddCheck(checks, "Character save", File.Exists(player.SavePath), "Confirmed",
            File.Exists(player.SavePath) ? "Player save file exists." : "No matching player save file was found.",
            "Discover saves again or use Reset Character only after a verified backup.", 25);

        var hasStableId = !string.IsNullOrWhiteSpace(player.UserId) || !string.IsNullOrWhiteSpace(player.SteamId) || !string.IsNullOrWhiteSpace(player.PlayerId);
        AddCheck(checks, "Player identity", hasStableId, "Confirmed",
            hasStableId ? "At least one stable player identifier is available." : "No UserID, SteamID, or PlayerID is available.",
            "Allow the player to connect once so MystTiq can capture a stable identity.", 20);

        var saveName = string.IsNullOrWhiteSpace(player.SavePath) ? "" : Path.GetFileNameWithoutExtension(player.SavePath);
        var saveMatchesPlayerId = string.IsNullOrWhiteSpace(saveName) || string.IsNullOrWhiteSpace(player.PlayerId) || Normalize(saveName) == Normalize(player.PlayerId);
        AddCheck(checks, "Save mapping", saveMatchesPlayerId, "Likely",
            saveMatchesPlayerId ? "Save filename and known PlayerID do not conflict." : "Save filename does not match the known PlayerID.",
            "Review Player Mapping and compare the save before applying recovery actions.", 15);

        var worldRoot = FindActiveWorldRoot();
        var levelExists = !string.IsNullOrWhiteSpace(worldRoot) && File.Exists(Path.Combine(worldRoot, "Level.sav"));
        AddCheck(checks, "World registration source", levelExists, "Confirmed",
            levelExists ? "Active Level.sav is present for registration checks." : "Active Level.sav was not found.",
            "Verify the configured server root and active world.", 15);

        var duplicateUser = CountMatches(allPlayers, player, p => p.UserId) > 1;
        var duplicateSteam = CountMatches(allPlayers, player, p => p.SteamId) > 1;
        var duplicatePlayer = CountMatches(allPlayers, player, p => p.PlayerId) > 1;
        if (duplicateUser) duplicates.Add("Duplicate UserID detected.");
        if (duplicateSteam) duplicates.Add("Duplicate SteamID detected.");
        if (duplicatePlayer) duplicates.Add("Duplicate PlayerID detected.");
        var noDuplicates = !duplicateUser && !duplicateSteam && !duplicatePlayer;
        AddCheck(checks, "Duplicate identifiers", noDuplicates, "Confirmed",
            noDuplicates ? "No duplicate stable identifiers were found in the known-player database." : string.Join(" ", duplicates),
            "Use Player Comparison before removing or merging records.", 15);

        var sourceHealthy = !player.Source.Contains("orphan", StringComparison.OrdinalIgnoreCase) && !player.SaveStatus.Contains("missing", StringComparison.OrdinalIgnoreCase);
        AddCheck(checks, "Discovery state", sourceHealthy, "Likely",
            sourceHealthy ? $"Record source is {player.Source}; save state is {player.SaveStatus}." : $"Record source is {player.Source}; save state is {player.SaveStatus}.",
            "Refresh players and run Discover Saves to reconcile stale records.", 10);

        foreach (var failed in checks.Where(c => c.Status != "Healthy")) repairs.Add(failed.Recommendation);
        var score = checks.Sum(c => c.Status == "Healthy" ? Weight(c.Component) : 0);
        var overall = score >= 90 ? "Healthy" : score >= 70 ? "Review" : score >= 40 ? "Warning" : "Critical";
        return new PlayerHealthReport
        {
            PlayerKey = BuildKey(player), DisplayName = player.Name, Score = score, OverallStatus = overall,
            Checks = checks, DuplicateFindings = duplicates, RepairRecommendations = repairs.Distinct().ToList()
        };
    }

    public List<PlayerComparisonRow> Compare(PlayerRow source, PlayerRow destination)
    {
        static string Result(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ? "Same" : "Different";
        return
        [
            new("Character name", source.Name, destination.Name, Result(source.Name, destination.Name)),
            new("Platform", source.Platform, destination.Platform, Result(source.Platform, destination.Platform)),
            new("Level", source.Level, destination.Level, Result(source.Level, destination.Level)),
            new("Guild/Source", source.Source, destination.Source, Result(source.Source, destination.Source)),
            new("Buildings", source.BuildingCount, destination.BuildingCount, Result(source.BuildingCount, destination.BuildingCount)),
            new("Save status", source.SaveStatus, destination.SaveStatus, Result(source.SaveStatus, destination.SaveStatus)),
            new("UserID", source.UserId, destination.UserId, Result(source.UserId, destination.UserId)),
            new("SteamID", source.SteamId, destination.SteamId, Result(source.SteamId, destination.SteamId)),
            new("PlayerID", source.PlayerId, destination.PlayerId, Result(source.PlayerId, destination.PlayerId))
        ];
    }

    public string ExportHtml(PlayerRow player, PlayerHealthReport report, PlayerAdministrationSummary administration, IReadOnlyList<string> timeline, string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);
        var safe = string.Concat(player.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var path = Path.Combine(destinationFolder, $"PlayerReport_{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        static string H(string? value) => System.Net.WebUtility.HtmlEncode(value ?? "");
        var rows = string.Join(Environment.NewLine, report.Checks.Select(c => $"<tr><td>{H(c.Component)}</td><td>{H(c.Status)}</td><td>{H(c.Confidence)}</td><td>{H(c.Detail)}</td><td>{H(c.Recommendation)}</td></tr>"));
        var events = string.Join(Environment.NewLine, timeline.Select(item => $"<li>{H(item)}</li>"));
        var html = "<!doctype html><html><head><meta charset='utf-8'><title>MystTiq Player Report</title>" +
                   "<style>body{font-family:Segoe UI;background:#0b1118;color:#dce8f5;padding:24px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #2a394b;padding:8px;text-align:left}th{background:#152232}h1,h2{color:#9fc4ea}</style></head><body>" +
                   $"<h1>MystTiq Player Report</h1><h2>{H(player.Name)}</h2><p>Generated: {DateTime.Now:G}</p>" +
                   $"<p>Health: <strong>{report.Score}% — {H(report.OverallStatus)}</strong></p>" +
                   $"<p>UserID: {H(player.UserId)}<br>SteamID: {H(player.SteamId)}<br>PlayerID: {H(player.PlayerId)}<br>Platform: {H(player.Platform)}<br>Level: {H(player.Level)}<br>Last seen: {H(player.LastSeen)}</p>" +
                   $"<p>Admin: {administration.IsAdmin} &nbsp; Whitelisted: {administration.IsWhitelisted} &nbsp; Banned: {administration.IsBanned} &nbsp; Notes: {administration.NoteCount} &nbsp; Active warnings: {administration.ActiveWarningCount}</p>" +
                   "<h2>Health checks</h2><table><tr><th>Component</th><th>Status</th><th>Confidence</th><th>Detail</th><th>Recommendation</th></tr>" + rows + "</table>" +
                   "<h2>Timeline</h2><ul>" + events + "</ul></body></html>";
        File.WriteAllText(path, html, new UTF8Encoding(false));
        return path;
    }

    private string FindActiveWorldRoot()
    {
        var root = Path.Combine(settings.ServerRoot, "Pal", "Saved", "SaveGames", "0");
        if (!Directory.Exists(root)) return "";
        return Directory.GetDirectories(root).OrderByDescending(Directory.GetLastWriteTimeUtc).FirstOrDefault() ?? "";
    }

    private static int CountMatches(IEnumerable<PlayerRow> players, PlayerRow selected, Func<PlayerRow, string> selector)
    {
        var value = selector(selected);
        return string.IsNullOrWhiteSpace(value) ? 0 : players.Count(p => selector(p).Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildKey(PlayerRow row) => !string.IsNullOrWhiteSpace(row.UserId) ? "user:" + row.UserId : !string.IsNullOrWhiteSpace(row.SteamId) ? "steam:" + row.SteamId : "player:" + row.PlayerId;
    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static int Weight(string component) => component switch { "Character save" => 25, "Player identity" => 20, "Save mapping" => 15, "World registration source" => 15, "Duplicate identifiers" => 15, _ => 10 };
    private static void AddCheck(List<PlayerHealthCheckRow> list, string component, bool healthy, string confidence, string detail, string recommendation, int _) =>
        list.Add(new(component, healthy ? "Healthy" : "Issue", confidence, detail, recommendation));
}
