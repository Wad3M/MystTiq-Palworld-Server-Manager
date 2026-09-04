using MystTiq.Core.Providers;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

/// <summary>
/// Second real IPlayerModerationProvider (v0.6.5.0 Provider Framework), using Palworld's Source
/// RCON KickPlayer/BanPlayer admin commands. Before this, a server with REST disabled and only
/// RCON enabled -- a common real-world configuration -- had no working kick/ban path at all;
/// PlayerModerationCoordinator now falls back here automatically.
/// </summary>
public sealed class RconPlayerModerationProvider : IPlayerModerationProvider
{
    private readonly PalworldRconService rcon;
    private readonly HeadlessActivityLogService activity;

    public RconPlayerModerationProvider(PalworldRconService rcon, HeadlessActivityLogService activity)
    {
        this.rcon = rcon;
        this.activity = activity;
    }

    public string ProviderId => "rcon";
    public string DisplayName => "Palworld RCON";

    public bool SupportsAction(string action) => (action ?? string.Empty).Trim().ToLowerInvariant() is "kick" or "ban";

    public Task<ProviderDescriptor> GetHealthAsync(CancellationToken cancellationToken)
    {
        var status = rcon.GetStatus();
        var health = !status.Enabled ? ProviderHealth.Unavailable
            : !status.PasswordConfigured ? ProviderHealth.Misconfigured
            : ProviderHealth.Healthy;
        return Task.FromResult(new ProviderDescriptor(ProviderId, DisplayName, health, status.Detail));
    }

    public async Task<PlayerModerationResult> ExecuteAsync(string action, string playerId, string? message, CancellationToken cancellationToken)
    {
        var normalizedAction = (action ?? string.Empty).Trim().ToLowerInvariant();
        playerId = (playerId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(playerId))
            return new PlayerModerationResult(false, false, ProviderId, normalizedAction, playerId, "A player UserID/SteamID is required.");
        if (!SupportsAction(normalizedAction))
            return new PlayerModerationResult(false, false, ProviderId, normalizedAction, playerId, $"Palworld RCON does not expose a '{normalizedAction}' command.");

        var command = normalizedAction == "kick" ? $"KickPlayer {playerId}" : $"BanPlayer {playerId}";
        var result = await rcon.ExecuteAsync(command, cancellationToken);
        if (!result.Success)
        {
            activity.Record("Error", "Players", $"Player {normalizedAction} failed (RCON)", $"{playerId}: {result.Message}");
            return new PlayerModerationResult(false, true, ProviderId, normalizedAction, playerId, result.Message);
        }

        var detail = string.IsNullOrWhiteSpace(result.Response)
            ? $"[RCON] {command} sent for {playerId}; the server returned no text."
            : $"[RCON] {result.Response}";
        activity.Record("Success", "Players", $"Player {normalizedAction} (RCON)", $"{playerId}: {detail}");
        return new PlayerModerationResult(true, true, ProviderId, normalizedAction, playerId, detail);
    }
}
