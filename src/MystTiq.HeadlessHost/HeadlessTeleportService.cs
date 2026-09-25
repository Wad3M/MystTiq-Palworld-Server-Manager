using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.7.113.0 "Teleport Points": named spots a player can teleport to from anywhere by typing a chat
// command (default "!tp <name>"), requested 2026-09-22.
//
// Why a chat command and not the game's own fast travel: the user asked for the game's own mechanic first,
// but it cannot be switched on from the server. PalWorldSettings.ini only has bEnableFastTravel and
// bEnableFastTravelOnlyBaseCamp; "fast travel from anywhere on the map" only exists as client-side mods every
// player would have to install. So, per the user's fallback, players type a chat command.
//
// How it works, and the honest constraints:
//   - Vanilla Palworld has no teleport command and players cannot run PalDefender's commands themselves. MystTiq
//     reads chat and, as the admin, runs PalDefender's `tp <UserId> <X> <Y> [Z]` over RCON, replying to the
//     player with PalDefender's `send msg <UserId> <text>`. Needs PalDefender and RCON, like Starter Kits, and
//     goes through the same IKitCommandRunner (a PalDefender-over-RCON runner, whatever its name says).
//   - Chat is read from PalDefender's own session log and from MystTiq's captured console log. PalDefender
//     replaces the game's "[CHAT] <Name> text" line with "[hh:mm:ss][info] [Chat::Global]['Name' (UserId=...,
//     IP=...)]: text", which carries the UserId tp needs. Only that form is parsed, because tp needs
//     PalDefender anyway. The format comes from another launcher's tests (SSyl/PalworldServerLauncher); nobody
//     has chatted on this machine's servers, so it is not yet observed live here.
//   - Coordinates are PalDefender's own (what `getpos` prints and `tp` takes). MystTiq never converts them;
//     "Capture" asks PalDefender for an online player's position instead.
//   - A chat line is only acted on if a player with that UserId AND that name is online in the Palworld REST
//     player list, so a player cannot teleport someone else by putting a fake header in their name. With the
//     REST API off the sender cannot be confirmed, so chat commands do nothing (Send and Capture say why).
public sealed record TeleportPoint(string Name, double X, double Y, double? Z);

public sealed record TeleportConfig(bool Enabled, string CommandPrefix, int CooldownSeconds, IReadOnlyList<TeleportPoint> Points)
{
    public static TeleportConfig Default => new(false, "!tp", 60, []);
}

public sealed record TeleportUse(DateTimeOffset AtUtc, string PlayerName, string UserId, string Point, bool Success, string Detail);

public sealed record TeleportSnapshot(TeleportConfig Config, KitProviderStatus Provider, string ChatSource, IReadOnlyList<TeleportUse> RecentUses);

public sealed record TeleportSaveResult(bool Success, string Message, TeleportConfig Config, IReadOnlyList<string> Errors);

public sealed record TeleportActionResult(bool Success, string Message, string Command, string Response);

public sealed record TeleportCaptureResult(bool Success, string Message, double? X, double? Y, double? Z, string Response);

public sealed record TeleportChatMessage(string Scope, string PlayerName, string UserId, string Text);

public sealed record TeleportSendRequest(string? PlayerId);

public sealed record TeleportCaptureRequest(string? PlayerId);

public interface IChatLineSource
{
    IReadOnlyList<string> ReadNewLines();
    string Describe();
}

// Pure parsing and command building, unit-tested in the logic harness.
public static partial class TeleportChat
{
    public const int MaximumPoints = 50;
    public const int MaximumCooldownSeconds = 3600;
    public const double MaximumCoordinate = 10_000_000;

    // The PalDefender chat header: "[hh:mm:ss][level] [Chat::Scope]['Name' (UserId=id, IP=...)]: text".
    // Regex.Match takes the leftmost header, which is the real one: anything a player types comes after it.
    [GeneratedRegex(@"\[\d{1,2}:\d{2}:\d{2}\]\[\w+\] \[Chat::(?<scope>\w*)\]\['(?<name>[^'\r\n]{1,64})' \(UserId=(?<uid>[A-Za-z0-9_\-]{3,64})(?:, IP=[^)\r\n]*)?\)\]: (?<text>.*)$")]
    private static partial Regex ChatLinePattern();

    [GeneratedRegex(@"^[A-Za-z0-9_\-]{1,32}$")]
    private static partial Regex PointNamePattern();

    [GeneratedRegex(@"^[!.][A-Za-z0-9]{1,16}$")]
    private static partial Regex PrefixPattern();

    // A number not glued to a letter, digit, underscore or dot, so "steam_76561198..." is not read as one.
    [GeneratedRegex(@"(?<![\w.])-?\d+(?:\.\d+)?(?![\w.])")]
    private static partial Regex NumberPattern();

    public static bool TryParseChatLine(string? line, out TeleportChatMessage message)
    {
        message = new(string.Empty, string.Empty, string.Empty, string.Empty);
        if (string.IsNullOrEmpty(line)) return false;
        var match = ChatLinePattern().Match(line.TrimEnd('\r'));
        if (!match.Success) return false;
        message = new(match.Groups["scope"].Value, match.Groups["name"].Value, match.Groups["uid"].Value, match.Groups["text"].Value.Trim());
        return true;
    }

    // null: not a teleport command at all. "": the bare prefix (list the points). Otherwise the point name.
    // More than one argument is treated as not a command, so ordinary chat that happens to start with the
    // prefix is ignored rather than answered.
    public static string? ParseCommand(string text, string prefix)
    {
        var tokens = (text ?? string.Empty).Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || !tokens[0].Equals(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        return tokens.Length switch { 1 => string.Empty, 2 => tokens[1], _ => null };
    }

    public static string BuildTeleportCommand(string userId, TeleportPoint point) =>
        $"tp {userId} {TeleportPointText.FormatNumber(point.X)} {TeleportPointText.FormatNumber(point.Y)}" +
        (point.Z is { } z ? " " + TeleportPointText.FormatNumber(z) : string.Empty);

    // One line, printable characters only, so a reply can never become a second RCON command.
    public static string BuildMessageCommand(string userId, string text)
    {
        var clean = new string((text ?? string.Empty).Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
        if (clean.Length > 200) clean = clean[..200];
        return $"send msg {userId} {clean}";
    }

    public static (double X, double Y, double? Z)? ParsePosition(string? response)
    {
        var numbers = NumberPattern().Matches(response ?? string.Empty)
            .Select(m => double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN)
            .Where(double.IsFinite).ToArray();
        return numbers.Length switch
        {
            < 2 => null,
            2 => (numbers[0], numbers[1], null),
            _ => (numbers[0], numbers[1], numbers[2])
        };
    }

    public static IReadOnlyList<string> Validate(TeleportConfig config)
    {
        var errors = new List<string>();
        if (!PrefixPattern().IsMatch(config.CommandPrefix ?? string.Empty))
            errors.Add("The chat command must start with ! or . followed by 1 to 16 letters or digits, e.g. !tp.");
        if (config.CooldownSeconds is < 0 or > MaximumCooldownSeconds)
            errors.Add($"The cooldown must be 0 to {MaximumCooldownSeconds} seconds.");
        if (config.Points.Count > MaximumPoints) errors.Add($"At most {MaximumPoints} teleport points are allowed.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in config.Points)
        {
            if (p.Name is null || !PointNamePattern().IsMatch(p.Name)) { errors.Add($"'{p.Name}' is not a valid point name (1 to 32 letters, digits, _ or -, no spaces)."); continue; }
            if (p.Name.Equals("list", StringComparison.OrdinalIgnoreCase)) errors.Add("'list' is reserved (it shows the points), so a point cannot be called that.");
            if (!names.Add(p.Name)) errors.Add($"Two points are called '{p.Name}'.");
            foreach (var v in new[] { p.X, p.Y, p.Z ?? 0 })
                if (!double.IsFinite(v) || Math.Abs(v) > MaximumCoordinate) { errors.Add($"Point '{p.Name}' has a coordinate out of range."); break; }
        }
        if (config.Enabled && config.Points.Count == 0) errors.Add("Add at least one point before switching teleporting on.");
        return errors;
    }
}

// Tails PalDefender's newest session log and MystTiq's captured console log for new lines. Lines already in a
// file when it is first seen are skipped (no replaying yesterday's "!tp"), unless the file was created after
// MystTiq started, which is a fresh server session. Only complete lines are consumed.
public sealed class PalDefenderChatLogSource : IChatLineSource
{
    private const int MaximumReadBytes = 1024 * 1024;
    private readonly string palDefenderLogs;
    private readonly string[] consoleLogs;
    private readonly DateTime startedUtc = DateTime.UtcNow;
    private readonly Dictionary<string, long> offsets = new(StringComparer.OrdinalIgnoreCase);
    private string? currentPalDefenderLog;

    public PalDefenderChatLogSource(IServerPathProfile paths)
    {
        palDefenderLogs = Path.Combine(paths.RuntimeBinaryRoot, "PalDefender", "Logs");
        consoleLogs =
        [
            Path.Combine(paths.LogsRoot, "MystTiq-PalServer-Console.log"),
            Path.Combine(paths.ManagerRuntimeRoot, "logs", "MystTiq-PalServer-Console.log")
        ];
    }

    public string Describe() => currentPalDefenderLog is null
        ? "Watching for PalDefender's session log (none found yet) and MystTiq's console log."
        : $"Reading chat from PalDefender's log {Path.GetFileName(currentPalDefenderLog)} and MystTiq's console log.";

    public IReadOnlyList<string> ReadNewLines()
    {
        var lines = new List<string>();
        try
        {
            if (Directory.Exists(palDefenderLogs))
            {
                var newest = Directory.GetFiles(palDefenderLogs, "*.log", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                if (newest is not null) { currentPalDefenderLog = newest; ReadFrom(newest, lines); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        foreach (var console in consoleLogs) ReadFrom(console, lines);
        return lines;
    }

    private void ReadFrom(string path, List<string> lines)
    {
        try
        {
            if (!File.Exists(path)) return;
            var length = new FileInfo(path).Length;
            if (!offsets.TryGetValue(path, out var offset))
                offset = File.GetCreationTimeUtc(path) > startedUtc ? 0 : length;
            if (length < offset) offset = 0; // rotated or truncated
            if (length == offset) { offsets[path] = offset; return; }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(offset, SeekOrigin.Begin);
            var buffer = new byte[(int)Math.Min(length - offset, MaximumReadBytes)];
            var read = stream.Read(buffer, 0, buffer.Length);
            var lastNewline = Array.LastIndexOf(buffer, (byte)'\n', Math.Max(0, read - 1));
            if (lastNewline < 0) { if (read == MaximumReadBytes) offsets[path] = offset + read; else offsets[path] = offset; return; }
            lines.AddRange(Encoding.UTF8.GetString(buffer, 0, lastNewline + 1).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')));
            offsets[path] = offset + lastNewline + 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}

public sealed class HeadlessTeleportService : IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(10);
    private const int MaximumRecentUses = 100;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string configPath;
    private readonly HeadlessActivityLogService activity;
    private readonly IKitCommandRunner runner;
    private readonly IChatLineSource chat;
    private readonly Func<CancellationToken, Task<HeadlessPlayersSnapshot>> players;
    private readonly object gate = new();
    private readonly Dictionary<string, DateTimeOffset> lastTeleport = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> recentCommands = new(StringComparer.Ordinal);
    private readonly List<TeleportUse> recentUses = [];
    private TeleportConfig config;
    private CancellationTokenSource? loopCts;
    private Task? loopTask;

    public HeadlessTeleportService(IServerPathProfile paths, HeadlessActivityLogService activity, IKitCommandRunner runner,
        IChatLineSource chat, Func<CancellationToken, Task<HeadlessPlayersSnapshot>> players)
    {
        this.activity = activity;
        this.runner = runner;
        this.chat = chat;
        this.players = players;
        var root = Path.Combine(paths.ManagerRuntimeRoot, "teleport");
        Directory.CreateDirectory(root);
        configPath = Path.Combine(root, "teleport.json");
        config = Load();
    }

    public TeleportSnapshot GetSnapshot()
    {
        lock (gate) return new(config, runner.GetStatus(), chat.Describe(), recentUses.AsEnumerable().Reverse().ToArray());
    }

    public TeleportSaveResult SaveConfig(TeleportConfig updated)
    {
        var normalized = updated with
        {
            CommandPrefix = (updated.CommandPrefix ?? string.Empty).Trim(),
            Points = (updated.Points ?? []).Select(p => p with { Name = (p.Name ?? string.Empty).Trim() }).ToArray()
        };
        var errors = TeleportChat.Validate(normalized);
        if (errors.Count > 0) return new(false, errors[0], GetSnapshot().Config, errors);
        lock (gate)
        {
            config = normalized;
            var partial = configPath + ".partial";
            File.WriteAllText(partial, JsonSerializer.Serialize(config, JsonOptions));
            File.Move(partial, configPath, true);
        }
        activity.Record("Information", "Players", "Teleport points saved", $"{normalized.Points.Count} point(s); chat command {(normalized.Enabled ? "on" : "off")} ({normalized.CommandPrefix}).");
        return new(true, "Teleport points saved.", normalized, []);
    }

    // ---- Chat loop ------------------------------------------------------------------------------------------

    public Task StartAsync(CancellationToken hostShutdownToken)
    {
        loopCts = CancellationTokenSource.CreateLinkedTokenSource(hostShutdownToken);
        loopTask = Task.Run(() => RunAsync(loopCts.Token));
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Always drained, even while switched off, so switching on later never replays old commands.
                var lines = chat.ReadNewLines();
                if (lines.Count > 0) await ProcessLinesAsync(lines, token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"Teleport chat watcher: {ex.Message}");
            }
            try { await Task.Delay(PollInterval, token); }
            catch (OperationCanceledException) { return; }
        }
    }

    public async Task ProcessLinesAsync(IReadOnlyList<string> lines, CancellationToken token)
    {
        TeleportConfig current;
        lock (gate) current = config;
        if (!current.Enabled) return;

        foreach (var line in lines)
        {
            if (!TeleportChat.TryParseChatLine(line, out var message)) continue;
            var argument = TeleportChat.ParseCommand(message.Text, current.CommandPrefix);
            if (argument is null) continue;
            if (IsDuplicate(message)) continue; // the same line seen in both logs
            await HandleCommandAsync(current, message, argument, token);
        }
    }

    private bool IsDuplicate(TeleportChatMessage message)
    {
        var now = DateTimeOffset.UtcNow;
        // Name included, so an impostor line (right UserId, wrong name) can never mark the real player's
        // command as already seen.
        var key = message.PlayerName + "\n" + message.UserId + "\n" + message.Text;
        lock (gate)
        {
            foreach (var stale in recentCommands.Where(kv => now - kv.Value > DuplicateWindow).Select(kv => kv.Key).ToArray()) recentCommands.Remove(stale);
            if (recentCommands.ContainsKey(key)) return true;
            recentCommands[key] = now;
            return false;
        }
    }

    private async Task HandleCommandAsync(TeleportConfig current, TeleportChatMessage message, string argument, CancellationToken token)
    {
        if (!runner.GetStatus().CanDeliver) return;

        // The line must belong to a player who is actually online under that exact UserId and name. Without the
        // live player list there is no way to tell a real line from one forged inside a player's name, so
        // nothing is done at all (the route smoke caught the earlier "skip the check" version teleporting an
        // impostor).
        var snapshot = await players(token);
        if (!snapshot.Available)
        {
            Record(message, argument, false, "Ignored: the live player list (Palworld REST API) is unavailable, so the sender cannot be confirmed.");
            return;
        }
        if (!snapshot.Players.Any(p =>
                string.Equals((p.UserId ?? string.Empty).Trim(), message.UserId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((p.Name ?? string.Empty).Trim(), message.PlayerName, StringComparison.Ordinal)))
        {
            Record(message, argument, false, "Ignored: no online player matches that name and UserId.");
            return;
        }

        var names = string.Join(", ", current.Points.Select(p => p.Name));
        if (argument.Length == 0 || argument.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            await ReplyAsync(message.UserId, $"Teleport points: {names}. Type {current.CommandPrefix} <name>.", token);
            return;
        }

        var point = current.Points.FirstOrDefault(p => p.Name.Equals(argument, StringComparison.OrdinalIgnoreCase));
        if (point is null)
        {
            // Recorded before the reply, so anyone who sees the reply also finds the record (the v0.7.114.0
            // gate caught a status read landing between the two).
            Record(message, argument, false, "Unknown point.");
            await ReplyAsync(message.UserId, $"No teleport point called '{argument}'. Points: {names}.", token);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var wait = 0;
        lock (gate)
        {
            if (lastTeleport.TryGetValue(message.UserId, out var last) && now - last < TimeSpan.FromSeconds(current.CooldownSeconds))
                wait = Math.Max(1, (int)Math.Ceiling(current.CooldownSeconds - (now - last).TotalSeconds));
        }
        if (wait > 0)
        {
            Record(message, point.Name, false, $"Cooldown ({wait}s left).");
            await ReplyAsync(message.UserId, $"Please wait {wait} second(s) before teleporting again.", token);
            return;
        }

        var result = await TeleportAsync(message.UserId, point, token);
        if (result.Success) lock (gate) lastTeleport[message.UserId] = now;
        Record(message, point.Name, result.Success, result.Message);
        activity.Record(result.Success ? "Information" : "Warning", "Players", result.Success ? "Player teleported (chat command)" : "Chat teleport failed",
            $"{message.PlayerName} ({message.UserId}) -> {point.Name}: {result.Message}");
        await ReplyAsync(message.UserId, result.Success ? $"Teleported to {point.Name}." : "Teleport failed. An admin can see why in MystTiq.", token);
    }

    private async Task<TeleportActionResult> TeleportAsync(string userId, TeleportPoint point, CancellationToken token)
    {
        var command = TeleportChat.BuildTeleportCommand(userId, point);
        var result = await runner.RunAsync(command, token);
        if (!result.Success) return new(false, result.Message, command, result.Response);
        if (HeadlessKitService.LooksLikeFailure(result.Response)) return new(false, $"The server replied with what looks like an error: {result.Response}", command, result.Response);
        return new(true, $"Sent to {point.Name}.", command, result.Response);
    }

    private async Task ReplyAsync(string userId, string text, CancellationToken token)
    {
        try { await runner.RunAsync(TeleportChat.BuildMessageCommand(userId, text), token); }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* a reply is best-effort */ }
    }

    private void Record(TeleportChatMessage message, string point, bool success, string detail)
    {
        lock (gate) { recentUses.Add(new(DateTimeOffset.UtcNow, message.PlayerName, message.UserId, point, success, detail)); Trim(); }
    }

    private void Trim() { if (recentUses.Count > MaximumRecentUses) recentUses.RemoveRange(0, recentUses.Count - MaximumRecentUses); }

    // ---- Admin actions -------------------------------------------------------------------------------------

    public async Task<TeleportActionResult> SendToPointAsync(string pointName, string playerId, CancellationToken token)
    {
        TeleportPoint? point;
        lock (gate) point = config.Points.FirstOrDefault(p => p.Name.Equals((pointName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
        if (point is null) return new(false, "That teleport point does not exist. Save the points first.", string.Empty, string.Empty);
        var status = runner.GetStatus();
        if (!status.CanDeliver) return new(false, status.Detail, string.Empty, string.Empty);
        var player = await FindOnlineAsync(playerId, token);
        if (player is null) return new(false, "That player is not online, and only an online player can be teleported.", string.Empty, string.Empty);
        var result = await TeleportAsync(player.UserId.Trim(), point, token);
        activity.Record(result.Success ? "Information" : "Warning", "Players", result.Success ? "Player sent to teleport point" : "Teleport to point failed", $"{player.Name} -> {point.Name}: {result.Message}");
        return result with { Message = result.Success ? $"Sent {player.Name} to {point.Name}." : result.Message };
    }

    // Asks PalDefender where an online player is standing, so a point can be saved without MystTiq having to
    // convert between coordinate systems.
    public async Task<TeleportCaptureResult> CaptureAsync(string playerId, CancellationToken token)
    {
        var status = runner.GetStatus();
        if (!status.CanDeliver) return new(false, status.Detail, null, null, null, string.Empty);
        var player = await FindOnlineAsync(playerId, token);
        if (player is null) return new(false, "That player is not online, so PalDefender cannot report their position.", null, null, null, string.Empty);
        var result = await runner.RunAsync($"getpos {player.UserId.Trim()}", token);
        if (!result.Success) return new(false, result.Message, null, null, null, result.Response);
        var position = TeleportChat.ParsePosition(result.Response);
        if (position is not { } p || HeadlessKitService.LooksLikeFailure(result.Response))
            return new(false, $"PalDefender's reply did not contain a position: {result.Response}", null, null, null, result.Response);
        return new(true, $"{player.Name} is at {TeleportPointText.FormatNumber(p.X)} {TeleportPointText.FormatNumber(p.Y)}{(p.Z is { } z ? " " + TeleportPointText.FormatNumber(z) : string.Empty)}.", p.X, p.Y, p.Z, result.Response);
    }

    private async Task<HeadlessPlayerSnapshot?> FindOnlineAsync(string playerId, CancellationToken token)
    {
        var snapshot = await players(token);
        if (!snapshot.Available) return null;
        var id = (playerId ?? string.Empty).Trim();
        return snapshot.Players.FirstOrDefault(p =>
            string.Equals((p.PlayerId ?? string.Empty).Trim(), id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals((p.UserId ?? string.Empty).Trim(), id, StringComparison.OrdinalIgnoreCase));
    }

    private TeleportConfig Load()
    {
        try
        {
            var loaded = File.Exists(configPath) ? JsonSerializer.Deserialize<TeleportConfig>(File.ReadAllText(configPath)) : null;
            return loaded is null ? TeleportConfig.Default : loaded with { Points = loaded.Points ?? [], CommandPrefix = loaded.CommandPrefix ?? "!tp" };
        }
        catch { return TeleportConfig.Default; }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (loopCts is null) return;
        loopCts.Cancel();
        if (loopTask is not null)
        {
            try { await loopTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);
}
