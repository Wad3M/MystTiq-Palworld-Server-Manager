using System.Text.Json;
using Discord;
using Discord.WebSocket;
using MystTiq.Core.Models;
using MystTiq.Core.Operations;
using MystTiq.Core.Providers;
using MystTiq.Core.Security;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.6.17.0 "Two-Way Discord Bot Control" -- the actual "two-way" half. Distinct from
// HeadlessNotificationRoutingService's outbound-only Discord webhook dispatch (no bot needed).
// Connects outbound to Discord's gateway (a normal WebSocket client, same shape as
// PalworldRconService's plain TCP client) so MystTiq never needs to expose a public HTTPS
// endpoint -- consistent with the local/LAN-first security posture the REST API already has.
// Guild-scoped slash commands only (no privileged MESSAGE_CONTENT intent needed). Every command
// composes directly onto the same services the REST API's own routes already call -- no new
// game-control logic here, only Discord transport plus role-mapped authorization.
public sealed class HeadlessDiscordBotService : IAsyncDisposable
{
    // v0.7.95.0: how often the live loop wakes to update the status message, presence and join/leave feed.
    // Discord allows far more, but a minute is plenty for status and keeps the bot a good API citizen.
    private static readonly TimeSpan LiveTickInterval = TimeSpan.FromSeconds(45);
    // The status message is otherwise only re-edited when its content changes; this forces an occasional
    // refresh so its "last checked" time never looks stale on a quiet server.
    private static readonly TimeSpan StatusForceRefresh = TimeSpan.FromMinutes(5);

    private readonly HeadlessActivityLogService activity;
    private readonly HeadlessBackupService backups;
    private readonly IServerLifecycleService lifecycle;
    private readonly PalworldRconService rcon;
    private readonly PlayerModerationCoordinator playerModeration;
    private readonly HeadlessMonitoringService monitoring;
    private readonly IOperationCoordinator operations;
    private readonly ServerProfileId profileId;
    private readonly IReadOnlyList<string> launchArguments;
    private readonly TimeSpan startupTimeout;
    private readonly TimeSpan stopTimeout;
    private readonly string configPath;
    private readonly object gate = new();

    private DiscordBotConfiguration config;
    private DiscordSocketClient? client;
    private DiscordBotConnectionState connectionState = DiscordBotConnectionState.NotConfigured;

    public HeadlessDiscordBotService(
        IServerPathProfile paths,
        HeadlessActivityLogService activity,
        IServerLifecycleService lifecycle,
        PalworldRconService rcon,
        PlayerModerationCoordinator playerModeration,
        HeadlessMonitoringService monitoring,
        IOperationCoordinator operations,
        ServerProfileId profileId,
        IReadOnlyList<string> launchArguments,
        TimeSpan startupTimeout,
        TimeSpan stopTimeout,
        HeadlessBackupService backups)
    {
        this.backups = backups;
        this.activity = activity;
        this.lifecycle = lifecycle;
        this.rcon = rcon;
        this.playerModeration = playerModeration;
        this.monitoring = monitoring;
        this.operations = operations;
        this.profileId = profileId;
        this.launchArguments = launchArguments;
        this.startupTimeout = startupTimeout;
        this.stopTimeout = stopTimeout;

        var root = Path.Combine(paths.ManagerRuntimeRoot, "notifications");
        Directory.CreateDirectory(root);
        configPath = Path.Combine(root, "discord-bot.json");
        config = Load();

        if (config.Enabled && !string.IsNullOrWhiteSpace(config.BotToken))
            _ = Task.Run(() => ConnectAsync(CancellationToken.None));
    }

    public DiscordBotConfigurationView GetConfigView()
    {
        lock (gate)
        {
            return new DiscordBotConfigurationView(
                config.Enabled,
                !string.IsNullOrWhiteSpace(config.BotToken),
                config.GuildId,
                config.OwnerDiscordUserId,
                config.RoleMappings,
                connectionState,
                config.StatusChannelId,
                config.EventsChannelId,
                config.ShowPresence);
        }
    }

    // The channel ids must be real snowflakes when present; blank clears the feature.
    public static string? ValidateChannelIds(DiscordBotConfiguration candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate.StatusChannelId) && !DiscordSnowflake.TryParse(candidate.StatusChannelId, out _))
            return "The status channel id must be the 17-20 digit number Discord shows for the channel (enable Developer Mode, right-click the channel, Copy Channel ID).";
        if (!string.IsNullOrWhiteSpace(candidate.EventsChannelId) && !DiscordSnowflake.TryParse(candidate.EventsChannelId, out _))
            return "The events channel id must be the 17-20 digit number Discord shows for the channel (enable Developer Mode, right-click the channel, Copy Channel ID).";
        return null;
    }

    // A saved BotToken of null/empty keeps whatever token is already stored -- the Desktop form
    // never redisplays a saved token, so "save without retyping it" must not wipe it out.
    public async Task<DiscordBotConfigurationView> SaveConfigAsync(DiscordBotConfiguration updated, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var effectiveToken = string.IsNullOrWhiteSpace(updated.BotToken) ? config.BotToken : updated.BotToken;
            var statusChannel = string.IsNullOrWhiteSpace(updated.StatusChannelId) ? null : updated.StatusChannelId.Trim();
            // The bot's one status message lives in one channel: keep its id across saves (the form never
            // sends it), but drop it when the channel changes so a fresh message is posted in the new one.
            var statusMessage = string.Equals(statusChannel, config.StatusChannelId, StringComparison.Ordinal) ? config.StatusMessageId : null;
            config = updated with
            {
                BotToken = effectiveToken,
                StatusChannelId = statusChannel,
                StatusMessageId = statusMessage,
                EventsChannelId = string.IsNullOrWhiteSpace(updated.EventsChannelId) ? null : updated.EventsChannelId.Trim()
            };
            Persist();
        }

        await DisconnectAsync();
        if (config.Enabled && !string.IsNullOrWhiteSpace(config.BotToken))
            _ = Task.Run(() => ConnectAsync(cancellationToken));
        else
            lock (gate) connectionState = DiscordBotConnectionState.NotConfigured;

        return GetConfigView();
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        DiscordBotConfiguration snapshot;
        lock (gate) snapshot = config;
        if (!snapshot.Enabled || string.IsNullOrWhiteSpace(snapshot.BotToken)) return;

        lock (gate) connectionState = DiscordBotConnectionState.Connecting;
        try
        {
            var socket = new DiscordSocketClient(new DiscordSocketConfig { GatewayIntents = GatewayIntents.Guilds });
            var consecutiveAuthFailures = 0;
            var stopping = 0;
            socket.Log += message =>
            {
                var isWarningOrWorse = message.Severity is LogSeverity.Error or LogSeverity.Critical or LogSeverity.Warning;
                if (isWarningOrWorse)
                    activity.Record(message.Severity == LogSeverity.Warning ? "Warning" : "Error", "Discord Bot", message.Source, message.Exception?.Message ?? message.Message);

                // Discord.Net's own connection manager retries a gateway 401 indefinitely (by
                // design, since a transient auth hiccup is retryable) -- but a genuinely bad token
                // never recovers, so this would otherwise hammer Discord's real API forever. Three
                // consecutive 401s is treated as "this token is bad," not "try again": mark Failed
                // and stop retrying rather than keep connecting under a known-invalid token. Closes
                // over `socket` directly (not the `client` field) since the field may not be
                // assigned yet -- a bad token can fail this fast, before StartAsync even returns.
                // Gated on Warning-or-worse severity AND source "Gateway" (the exact real shape
                // confirmed live: Source "Gateway", Message "The server responded with error 401:
                // 401: Unauthorized") -- a bare substring match against every Debug/Verbose log line
                // (session IDs, sequence numbers, latency values) would false-positive on a healthy,
                // long-running connection and tear it down for no reason.
                var unauthorized = isWarningOrWorse && string.Equals(message.Source, "Gateway", StringComparison.Ordinal) &&
                    (message.Message?.Contains("401", StringComparison.Ordinal) == true
                        || message.Exception?.Message.Contains("401", StringComparison.Ordinal) == true);
                if (unauthorized && Interlocked.Increment(ref consecutiveAuthFailures) >= 3 && Interlocked.Exchange(ref stopping, 1) == 0)
                {
                    lock (gate) connectionState = DiscordBotConnectionState.Failed;
                    activity.Record("Error", "Discord Bot", "Connection failed", "Discord rejected the bot token (401 Unauthorized) repeatedly; stopping reconnect attempts. Update the token and re-save to retry.");
                    _ = Task.Run(async () =>
                    {
                        try { await socket.LogoutAsync(); await socket.StopAsync(); } catch { /* best-effort teardown */ }
                        finally { if (ReferenceEquals(client, socket)) client = null; socket.Dispose(); }
                    });
                }
                return Task.CompletedTask;
            };
            socket.Ready += async () =>
            {
                Interlocked.Exchange(ref consecutiveAuthFailures, 0);
                lock (gate) connectionState = DiscordBotConnectionState.Connected;
                await RegisterCommandsAsync(socket, snapshot.GuildId);
                activity.Record("Information", "Discord Bot", "Connected", $"Discord bot connected as {socket.CurrentUser?.Username}.");
                StartLiveLoop(socket);
            };
            socket.Disconnected += ex =>
            {
                lock (gate) if (connectionState == DiscordBotConnectionState.Connected) connectionState = DiscordBotConnectionState.Disconnected;
                return Task.CompletedTask;
            };
            socket.SlashCommandExecuted += OnSlashCommandExecutedAsync;
            socket.AutocompleteExecuted += OnAutocompleteAsync;

            await socket.LoginAsync(TokenType.Bot, snapshot.BotToken);
            await socket.StartAsync();
            client = socket;
        }
        catch (Exception ex)
        {
            lock (gate) connectionState = DiscordBotConnectionState.Failed;
            activity.Record("Warning", "Discord Bot", "Connection failed", ex.Message);
        }
    }

    private async Task DisconnectAsync()
    {
        try { liveLoop?.Cancel(); } catch { /* already disposed */ }
        liveLoop = null;
        var current = client;
        client = null;
        if (current is null) return;
        try { await current.LogoutAsync(); await current.StopAsync(); }
        catch { /* best-effort teardown */ }
        finally { current.Dispose(); }
    }

    private async Task RegisterCommandsAsync(DiscordSocketClient socket, string? guildId)
    {
        if (string.IsNullOrWhiteSpace(guildId) || !ulong.TryParse(guildId, out var parsedGuildId)) return;
        var guild = socket.GetGuild(parsedGuildId);
        if (guild is null) return;

        var commands = new ApplicationCommandProperties[]
        {
            new SlashCommandBuilder().WithName("mysttiq-status").WithDescription("Show whether the server is running and how many players are online.").Build(),
            new SlashCommandBuilder().WithName("mysttiq-players").WithDescription("List online players.").Build(),
            new SlashCommandBuilder().WithName("mysttiq-start").WithDescription("Start the Palworld server.").Build(),
            new SlashCommandBuilder().WithName("mysttiq-stop").WithDescription("Stop the Palworld server.").Build(),
            new SlashCommandBuilder().WithName("mysttiq-restart").WithDescription("Restart the Palworld server.").Build(),
            new SlashCommandBuilder().WithName("mysttiq-broadcast").WithDescription("Broadcast a message to all online players.")
                .AddOption("message", ApplicationCommandOptionType.String, "The message to broadcast.", isRequired: true).Build(),
            new SlashCommandBuilder().WithName("mysttiq-save").WithDescription("Save the world now.").Build(),
            new SlashCommandBuilder().WithName("mysttiq-backup").WithDescription("Create a manual backup of the world.").Build(),
            // v0.7.95.0: the player option now autocompletes from the players online right now (name shown,
            // player id sent), so nobody has to copy a raw id. A typed id still works for offline players.
            new SlashCommandBuilder().WithName("mysttiq-kick").WithDescription("Kick a player.")
                .AddOption(new SlashCommandOptionBuilder().WithName("player").WithType(ApplicationCommandOptionType.String).WithDescription("Online player (start typing a name) or a player ID.").WithRequired(true).WithAutocomplete(true))
                .AddOption("reason", ApplicationCommandOptionType.String, "Reason.", isRequired: false).Build(),
            new SlashCommandBuilder().WithName("mysttiq-ban").WithDescription("Ban a player.")
                .AddOption(new SlashCommandOptionBuilder().WithName("player").WithType(ApplicationCommandOptionType.String).WithDescription("Online player (start typing a name) or a player ID.").WithRequired(true).WithAutocomplete(true))
                .AddOption("reason", ApplicationCommandOptionType.String, "Reason.", isRequired: false).Build(),
            new SlashCommandBuilder().WithName("mysttiq-unban").WithDescription("Unban a player by player ID.")
                .AddOption("player", ApplicationCommandOptionType.String, "Player ID to unban.", isRequired: true).Build()
        };
        await guild.BulkOverwriteApplicationCommandAsync(commands);
    }

    private async Task OnSlashCommandExecutedAsync(SocketSlashCommand command)
    {
        var name = command.Data.Name;
        if (!DiscordBotFormatting.SupportedCommands.Contains(name)) return;

        try
        {
            if (command.User is not SocketGuildUser guildUser)
            {
                await command.RespondAsync("This command can only be used in a server.", ephemeral: true);
                return;
            }

            DiscordBotConfiguration snapshot;
            lock (gate) snapshot = config;
            var required = DiscordBotFormatting.RequiredRole(name);
            var granted = ResolveRole(guildUser, snapshot);
            if (granted is null || granted < required)
            {
                activity.Record("Warning", "Discord Bot", $"Command denied: /{name}", "Caller's Discord roles do not map to a sufficient MystTiq role.", $"discord:{guildUser.Username}");
                await command.RespondAsync("You are not authorized to run this command.", ephemeral: true);
                return;
            }

            var reply = await ExecuteAsync(name, command, CancellationToken.None);
            activity.Record("Information", "Discord Bot", $"Command: /{name}", reply, $"discord:{guildUser.Username}");
            // Replies can contain player names and broadcast text, which players and callers choose, so
            // mentions are switched off: a name like @everyone must never ping the guild.
            await command.RespondAsync(reply, allowedMentions: AllowedMentions.None);
        }
        catch (Exception ex)
        {
            activity.Record("Warning", "Discord Bot", $"Command failed: /{name}", ex.Message);
            try { await command.RespondAsync($"Command failed: {ex.Message}", ephemeral: true); } catch { /* interaction may already be closed */ }
        }
    }

    private async Task<string> ExecuteAsync(string name, SocketSlashCommand command, CancellationToken cancellationToken)
    {
        switch (name)
        {
            case "mysttiq-status":
            {
                var status = await lifecycle.GetStatusAsync(cancellationToken);
                var players = await monitoring.GetPlayersAsync(cancellationToken);
                var playerText = players.Available ? $"{players.OnlineCount} player(s) online." : "Player count unavailable.";
                return $"Server is **{status.Phase}**. {playerText}";
            }
            case "mysttiq-players":
            {
                var snapshot = await monitoring.GetPlayersAsync(cancellationToken);
                if (!snapshot.Available) return "Player list is currently unavailable.";
                return snapshot.Players.Count == 0 ? "No players online." : "Online: " + string.Join(", ", snapshot.Players.Select(p => DiscordBotFormatting.EscapeMarkdown(p.Name)));
            }
            case "mysttiq-save":
            {
                var result = await rcon.ExecuteAsync("Save", cancellationToken);
                return result.Success ? "World save requested." : result.Message;
            }
            case "mysttiq-backup":
            {
                var result = await backups.CreateAsync(BackupClass.Manual, cancellationToken);
                return result.Success ? $"Backup created. {result.Message}" : $"Backup failed: {result.Message}";
            }
            case "mysttiq-start":
                return await RunLifecycleAsync("server-start-discord", ["lifecycle", "world-mutation"], cancellationToken,
                    () => lifecycle.StartAsync(launchArguments, startupTimeout, cancellationToken));
            case "mysttiq-stop":
                return await RunLifecycleAsync("server-stop-discord", ["lifecycle"], cancellationToken,
                    () => lifecycle.StopAsync(stopTimeout, cancellationToken));
            case "mysttiq-restart":
                return await RunLifecycleAsync("server-restart-discord", ["lifecycle", "world-mutation"], cancellationToken,
                    () => lifecycle.RestartAsync(launchArguments, startupTimeout, stopTimeout, cancellationToken));
            case "mysttiq-broadcast":
            {
                var message = GetStringOption(command, "message") ?? string.Empty;
                var result = await rcon.ExecuteAsync($"Broadcast {message}", cancellationToken);
                return result.Message;
            }
            case "mysttiq-kick":
            case "mysttiq-ban":
            case "mysttiq-unban":
            {
                var playerId = GetStringOption(command, "player") ?? string.Empty;
                var reason = GetStringOption(command, "reason");
                var action = name switch { "mysttiq-kick" => "kick", "mysttiq-ban" => "ban", _ => "unban" };
                var result = await playerModeration.ExecuteAsync(action, playerId, reason, cancellationToken);
                return result.Message;
            }
            default:
                return "Unknown command.";
        }
    }

    // Mirrors LocalManagementApiHost.RunLifecycleActionAsync's OperationCoordinator locking so a
    // Discord-triggered start/stop/restart can never race a REST- or Desktop-triggered one -- same
    // coordinator, same resource keys, just a different caller identity ("DiscordBot" vs
    // "LocalManagementApiHost").
    private async Task<string> RunLifecycleAsync(string kind, IReadOnlyList<string> resourceKeys, CancellationToken cancellationToken, Func<Task<ServerLifecycleOperationResult>> operation)
    {
        OperationHandle handle;
        try { handle = await operations.BeginAsync(profileId, kind, "DiscordBot", resourceKeys, cancellationToken); }
        catch (InvalidOperationException ex) { return $"Another lifecycle operation is already in progress: {ex.Message}"; }

        try
        {
            var result = await operation();
            if (result.Success) operations.Complete(handle.Id, result.Message);
            else operations.Fail(handle.Id, result.Message);
            return result.Message;
        }
        catch (Exception ex)
        {
            operations.Fail(handle.Id, ex.Message);
            throw;
        }
        finally { handle.Dispose(); }
    }

    // ---- v0.7.95.0: autocomplete ---------------------------------------------------------------

    // Only callers who could actually run the command are shown player names, so the suggestion list is
    // not a way for an unprivileged guild member to read who is online through the kick/ban prompt.
    private async Task OnAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {
        try
        {
            var name = interaction.Data.CommandName;
            if (name is not ("mysttiq-kick" or "mysttiq-ban")) return;
            if (interaction.User is not SocketGuildUser guildUser) return;

            DiscordBotConfiguration snapshot;
            lock (gate) snapshot = config;
            var granted = ResolveRole(guildUser, snapshot);
            if (granted is null || granted < DiscordBotFormatting.RequiredRole(name))
            {
                await interaction.RespondAsync(Array.Empty<AutocompleteResult>());
                return;
            }

            var players = await monitoring.GetPlayersAsync(CancellationToken.None);
            var suggestions = players.Available
                ? DiscordBotFormatting.SuggestPlayers(players.Players, interaction.Data.Current.Value?.ToString())
                : [];
            await interaction.RespondAsync(suggestions.Select(s => new AutocompleteResult(s.Name, s.Value)).ToList());
        }
        catch (Exception ex) { activity.Record("Warning", "Discord Bot", "Autocomplete failed", ex.Message); }
    }

    // ---- v0.7.95.0: live status, presence and join/leave feed --------------------------------------

    private CancellationTokenSource? liveLoop;

    private sealed class LiveState
    {
        public string? PreviousStateKey;
        public IReadOnlyDictionary<string, string>? PreviousPlayers;
        public string? LastStatusKey;
        public DateTimeOffset LastStatusEdit = DateTimeOffset.MinValue;
        public string? LastPresence;
        public string? LastError;
    }

    private void StartLiveLoop(DiscordSocketClient socket)
    {
        try { liveLoop?.Cancel(); } catch { /* previous loop already gone */ }
        var cts = new CancellationTokenSource();
        liveLoop = cts;
        _ = Task.Run(() => RunLiveLoopAsync(socket, cts.Token));
    }

    private async Task RunLiveLoopAsync(DiscordSocketClient socket, CancellationToken cancellationToken)
    {
        var state = new LiveState();
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await LiveTickAsync(socket, state, cancellationToken); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { ReportLiveProblem(state, ex.Message); }

            try { await Task.Delay(LiveTickInterval, cancellationToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task LiveTickAsync(DiscordSocketClient socket, LiveState state, CancellationToken cancellationToken)
    {
        DiscordBotConfiguration cfg;
        lock (gate) cfg = config;
        var hasStatus = DiscordSnowflake.TryParse(cfg.StatusChannelId, out var statusChannelId);
        var hasEvents = DiscordSnowflake.TryParse(cfg.EventsChannelId, out var eventsChannelId);
        if (!hasStatus && !hasEvents && !cfg.ShowPresence && state.LastPresence is null) return;

        var status = await lifecycle.GetStatusAsync(cancellationToken);
        var players = await monitoring.GetPlayersAsync(cancellationToken);
        var stateKey = DiscordBotFormatting.StateKey(status.Phase, status.Ready);
        var names = players.Available
            ? players.Players.Select(p => string.IsNullOrWhiteSpace(p.Name) ? p.PlayerId : p.Name).ToList()
            : [];

        var lines = new List<string>();
        var transition = DiscordBotFormatting.DescribeTransition(state.PreviousStateKey, stateKey);
        if (transition is not null) lines.Add(transition);
        if (stateKey != "Online") state.PreviousPlayers = null; // a stop or restart resets the baseline, so it is never read as everyone leaving
        else if (players.Available)
        {
            var diff = DiscordBotFormatting.DiffPresence(state.PreviousPlayers, players.Players);
            lines.AddRange(DiscordBotFormatting.PresenceLines(diff));
            state.PreviousPlayers = diff.Current;
        }

        state.PreviousStateKey = stateKey;

        if (cfg.ShowPresence)
        {
            var presence = DiscordBotFormatting.PresenceText(stateKey, names.Count, players.Available);
            if (presence != state.LastPresence)
            {
                await socket.SetGameAsync(presence, type: ActivityType.Watching);
                state.LastPresence = presence;
            }
        }
        else if (state.LastPresence is not null)
        {
            await socket.SetGameAsync(null);
            state.LastPresence = null;
        }

        if (hasEvents && lines.Count > 0)
            await PostEventsAsync(socket, eventsChannelId, lines, state);
        if (hasStatus)
            await UpdateStatusMessageAsync(socket, statusChannelId, cfg, DiscordBotFormatting.BuildStatus(status.Phase, status.Ready, players.Available, names), state);
    }

    private async Task UpdateStatusMessageAsync(DiscordSocketClient socket, ulong channelId, DiscordBotConfiguration cfg, DiscordStatusView view, LiveState state)
    {
        var now = DateTimeOffset.UtcNow;
        if (state.LastStatusKey == view.ChangeKey && now - state.LastStatusEdit < StatusForceRefresh) return;

        if (socket.GetChannel(channelId) is not IMessageChannel channel)
        {
            ReportLiveProblem(state, $"The status channel ({channelId}) was not found, or is not a text channel this bot can see.");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(view.Headline)
            .WithDescription($"{view.Body}\n\nLast checked <t:{now.ToUnixTimeSeconds()}:R>")
            .WithColor(view.StateKey switch
            {
                "Online" => Color.Green,
                "Starting" => Color.Gold,
                "Stopping" => Color.Orange,
                "Offline" or "Crashed" => Color.Red,
                _ => Color.LightGrey
            })
            .Build();

        IUserMessage? message = null;
        if (ulong.TryParse(cfg.StatusMessageId, out var messageId))
            message = await channel.GetMessageAsync(messageId) as IUserMessage;

        if (message is null)
        {
            message = await channel.SendMessageAsync(embed: embed, allowedMentions: AllowedMentions.None);
            PersistStatusMessageId(cfg.StatusChannelId, message.Id.ToString());
        }
        else
        {
            await message.ModifyAsync(m => m.Embed = embed);
        }

        state.LastStatusKey = view.ChangeKey;
        state.LastStatusEdit = now;
        state.LastError = null;
    }

    private async Task PostEventsAsync(DiscordSocketClient socket, ulong channelId, IReadOnlyList<string> lines, LiveState state)
    {
        if (socket.GetChannel(channelId) is not IMessageChannel channel)
        {
            ReportLiveProblem(state, $"The events channel ({channelId}) was not found, or is not a text channel this bot can see.");
            return;
        }

        const int MaximumLines = 15;
        var text = string.Join('\n', lines.Take(MaximumLines)) + (lines.Count > MaximumLines ? $"\n…and {lines.Count - MaximumLines} more." : string.Empty);
        await channel.SendMessageAsync(text, allowedMentions: AllowedMentions.None);
    }

    private void PersistStatusMessageId(string? channelIdText, string messageId)
    {
        lock (gate)
        {
            // Only if the channel was not changed while this tick was running.
            if (!string.Equals(config.StatusChannelId, channelIdText, StringComparison.Ordinal)) return;
            config = config with { StatusMessageId = messageId };
            Persist();
        }
    }

    // A Discord-side problem (missing channel, missing permission) is logged once per distinct message,
    // not once per tick, so a misconfigured channel cannot flood the activity log.
    private void ReportLiveProblem(LiveState state, string message)
    {
        if (string.Equals(state.LastError, message, StringComparison.Ordinal)) return;
        state.LastError = message;
        activity.Record("Warning", "Discord Bot", "Live update problem", message);
    }

    private static string? GetStringOption(SocketSlashCommand command, string name) =>
        command.Data.Options?.FirstOrDefault(o => o.Name == name)?.Value as string;

    private static MystTiqRole? ResolveRole(SocketGuildUser user, DiscordBotConfiguration cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.OwnerDiscordUserId) && user.Id.ToString() == cfg.OwnerDiscordUserId)
            return MystTiqRole.Owner;

        MystTiqRole? best = null;
        foreach (var role in user.Roles)
        {
            var mapping = cfg.RoleMappings.FirstOrDefault(m => m.DiscordRoleId == role.Id.ToString());
            if (mapping is not null && (best is null || mapping.Role > best.Value)) best = mapping.Role;
        }
        return best;
    }

    private DiscordBotConfiguration Load()
    {
        try { return File.Exists(configPath) ? JsonSerializer.Deserialize<DiscordBotConfiguration>(File.ReadAllText(configPath)) ?? DiscordBotConfiguration.Default : DiscordBotConfiguration.Default; }
        catch { return DiscordBotConfiguration.Default; }
    }

    private void Persist()
    {
        var partial = configPath + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(partial, configPath, true);
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
