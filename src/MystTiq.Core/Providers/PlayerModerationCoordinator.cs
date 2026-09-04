namespace MystTiq.Core.Providers;

/// <summary>
/// Tries each registered provider that supports the requested action, in registration order
/// (REST first, matching today's pre-v0.6.5.0 default behavior), skipping any provider that
/// reports itself Unavailable/Misconfigured and falling through to the next on a real execution
/// failure. Returns the first success, or the most recent failure if every provider fails --
/// never a silent no-op.
/// </summary>
public sealed class PlayerModerationCoordinator
{
    private readonly IReadOnlyList<IPlayerModerationProvider> providers;

    public PlayerModerationCoordinator(IEnumerable<IPlayerModerationProvider> providers) =>
        this.providers = providers.ToList();

    public async Task<IReadOnlyList<ProviderDescriptor>> GetProviderHealthAsync(CancellationToken cancellationToken)
    {
        var results = new List<ProviderDescriptor>(providers.Count);
        foreach (var provider in providers)
            results.Add(await provider.GetHealthAsync(cancellationToken));
        return results;
    }

    public async Task<PlayerModerationResult> ExecuteAsync(string action, string playerId, string? message, CancellationToken cancellationToken)
    {
        var candidates = providers.Where(p => p.SupportsAction(action)).ToList();
        if (candidates.Count == 0)
            return new PlayerModerationResult(false, false, "none", action, playerId, $"No configured provider supports the '{action}' action.");

        PlayerModerationResult? lastAttempt = null;
        foreach (var provider in candidates)
        {
            var health = await provider.GetHealthAsync(cancellationToken);
            if (health.Health is ProviderHealth.Unavailable or ProviderHealth.Misconfigured)
            {
                lastAttempt = new PlayerModerationResult(false, false, provider.ProviderId, action, playerId, $"{provider.DisplayName}: {health.Detail}");
                continue;
            }

            var result = await provider.ExecuteAsync(action, playerId, message, cancellationToken);
            if (result.Success) return result;
            lastAttempt = result;
        }

        return lastAttempt!;
    }
}
