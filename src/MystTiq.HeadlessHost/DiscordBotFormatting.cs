using MystTiq.Core.Models;
using MystTiq.Core.Security;

namespace MystTiq.HeadlessHost;

// v0.7.95.0: the pure, Discord-free half of the bot's live features -- what the status message says, the
// join/leave and lifecycle diffs, autocomplete suggestions, and which role each command needs. Kept apart
// from HeadlessDiscordBotService so all of it is unit-tested in the logic harness (the gateway/message
// plumbing itself cannot be exercised without a real bot token).
public sealed record DiscordStatusView(string StateKey, string Headline, string Body, string ChangeKey);

public sealed record PresenceDiff(IReadOnlyList<string> Joined, IReadOnlyList<string> Left, IReadOnlyDictionary<string, string> Current);

public static class DiscordBotFormatting
{
    public const int MaximumListedPlayers = 20;
    public const int MaximumSuggestions = 25;

    // ---- Server state ----------------------------------------------------------------------------

    // Online means the process is up AND the game port is confirmed; a running process without it is
    // still starting. Mirrors the Desktop's own reading of the same status.
    public static string StateKey(ServerLifecyclePhase phase, bool ready) => phase switch
    {
        ServerLifecyclePhase.Running => ready ? "Online" : "Starting",
        ServerLifecyclePhase.Starting => "Starting",
        ServerLifecyclePhase.Stopping => "Stopping",
        ServerLifecyclePhase.Stopped => "Offline",
        ServerLifecyclePhase.Crashed => "Crashed",
        _ => "Unknown"
    };

    public static DiscordStatusView BuildStatus(ServerLifecyclePhase phase, bool ready, bool playersAvailable, IReadOnlyList<string> playerNames)
    {
        var state = StateKey(phase, ready);
        var headline = state switch
        {
            "Online" => "🟢 Server online",
            "Starting" => "🟡 Server starting",
            "Stopping" => "🟠 Server stopping",
            "Offline" => "🔴 Server offline",
            "Crashed" => "⚠️ Server crashed",
            _ => "⚪ Server status unknown"
        };

        string body;
        if (state != "Online") body = "No players while the server is not online.";
        else if (!playersAvailable) body = "Player list unavailable right now.";
        else if (playerNames.Count == 0) body = "**0 players online**";
        else
        {
            var listed = playerNames.Take(MaximumListedPlayers).Select(EscapeMarkdown).ToList();
            var more = playerNames.Count - listed.Count;
            body = $"**{playerNames.Count} player{(playerNames.Count == 1 ? string.Empty : "s")} online**\n" + string.Join(", ", listed) + (more > 0 ? $" and {more} more" : string.Empty);
        }

        // The change key deliberately leaves out any timestamp, so an idle server is not re-edited every tick.
        return new(state, headline, body, state + "|" + body);
    }

    public static string PresenceText(string stateKey, int playerCount, bool playersAvailable) => stateKey switch
    {
        "Online" => playersAvailable ? $"{playerCount} player{(playerCount == 1 ? string.Empty : "s")} online" : "Server online",
        "Starting" => "Server starting",
        "Stopping" => "Server stopping",
        "Crashed" => "Server crashed",
        "Offline" => "Server offline",
        _ => "Status unknown"
    };

    // One line per real change of state, or null when nothing worth announcing changed. Stopping and
    // Unknown are transitional/uninformative, so they are never announced on their own.
    public static string? DescribeTransition(string? previousStateKey, string currentStateKey)
    {
        if (previousStateKey is null || previousStateKey == currentStateKey) return null;
        return currentStateKey switch
        {
            "Online" => "✅ Server is online.",
            "Starting" => "⏳ Server is starting.",
            "Offline" when previousStateKey is "Online" or "Starting" or "Stopping" => "🛑 Server stopped.",
            "Crashed" => "⚠️ Server crashed.",
            _ => null
        };
    }

    // ---- Join / leave ----------------------------------------------------------------------------

    // previous == null is the first observation: it only establishes a baseline, so nothing is announced.
    // The caller must not call this with a failed (unavailable) player read, which would look like
    // everyone leaving at once.
    public static PresenceDiff DiffPresence(IReadOnlyDictionary<string, string>? previous, IReadOnlyList<HeadlessPlayerSnapshot> current)
    {
        var now = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var player in current)
        {
            var key = !string.IsNullOrWhiteSpace(player.PlayerId) ? player.PlayerId.Trim() : (player.UserId ?? string.Empty).Trim();
            if (key.Length == 0) continue;
            now[key] = string.IsNullOrWhiteSpace(player.Name) ? key : player.Name.Trim();
        }

        if (previous is null) return new([], [], now);
        var joined = now.Where(p => !previous.ContainsKey(p.Key)).Select(p => p.Value).ToList();
        var left = previous.Where(p => !now.ContainsKey(p.Key)).Select(p => p.Value).ToList();
        return new(joined, left, now);
    }

    public static IReadOnlyList<string> PresenceLines(PresenceDiff diff) =>
        diff.Joined.Select(n => $"➕ **{EscapeMarkdown(n)}** joined.").Concat(diff.Left.Select(n => $"➖ **{EscapeMarkdown(n)}** left.")).ToList();

    // ---- Autocomplete ----------------------------------------------------------------------------

    public static IReadOnlyList<(string Name, string Value)> SuggestPlayers(IReadOnlyList<HeadlessPlayerSnapshot> players, string? typed)
    {
        var needle = (typed ?? string.Empty).Trim();
        var matches = players
            .Where(p => !string.IsNullOrWhiteSpace(p.PlayerId) && p.PlayerId.Trim().Length <= 100)
            .Where(p => needle.Length == 0
                        || (p.Name ?? string.Empty).Contains(needle, StringComparison.OrdinalIgnoreCase)
                        || p.PlayerId.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
            .Take(MaximumSuggestions)
            .ToList();

        // Two players can share a display name, so a short id tail keeps the two suggestions apart.
        var duplicated = matches.GroupBy(p => p.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return matches.Select(p =>
        {
            var name = string.IsNullOrWhiteSpace(p.Name) ? p.PlayerId : p.Name;
            var label = duplicated.Contains(p.Name ?? string.Empty) ? $"{name} (#{p.PlayerId.Trim()[^Math.Min(4, p.PlayerId.Trim().Length)..]})" : name;
            return (label.Length > 100 ? label[..100] : label, p.PlayerId.Trim());
        }).ToList();
    }

    // ---- Commands and ids ------------------------------------------------------------------------

    public static readonly string[] SupportedCommands =
    [
        "mysttiq-status", "mysttiq-players", "mysttiq-start", "mysttiq-stop", "mysttiq-restart", "mysttiq-broadcast",
        "mysttiq-save", "mysttiq-backup", "mysttiq-kick", "mysttiq-ban", "mysttiq-unban"
    ];

    public static MystTiqRole RequiredRole(string commandName) => commandName switch
    {
        "mysttiq-status" or "mysttiq-players" => MystTiqRole.Viewer,
        "mysttiq-start" or "mysttiq-stop" or "mysttiq-restart" or "mysttiq-broadcast" or "mysttiq-save" or "mysttiq-backup" => MystTiqRole.Operator,
        "mysttiq-kick" or "mysttiq-ban" or "mysttiq-unban" => MystTiqRole.Admin,
        _ => MystTiqRole.Owner
    };

    // Discord snowflakes are 17-20 digit numbers; anything else typed into a channel-id box is refused
    // rather than passed to the gateway.
    public static bool TryParseSnowflake(string? text, out ulong id) => DiscordSnowflake.TryParse(text, out id);

    // Player names are chosen by players, so they are escaped before being put into a Discord message.
    public static string EscapeMarkdown(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var builder = new System.Text.StringBuilder(text.Length + 8);
        foreach (var ch in text)
        {
            if (ch is '\\' or '*' or '_' or '~' or '`' or '|' or '>' or '#' or '[' or ']') builder.Append('\\');
            builder.Append(ch is '\r' or '\n' ? ' ' : ch);
        }

        return builder.ToString();
    }
}
