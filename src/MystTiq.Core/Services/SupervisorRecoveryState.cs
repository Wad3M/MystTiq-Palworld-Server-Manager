using System.Text.Json;

namespace MystTiq.Core.Services;

// v0.7.115.0 (deficiency report): everything crash recovery knew lived only in memory, so a MystTiq restart
// (the Desktop sidecar restarts with the Desktop) forgot all of it:
//   - RestartHistory: the attempts inside the recovery window. Forgetting them reset the "at most N restarts
//     per window" limit on every MystTiq restart.
//   - GaveUpAtUtc: that automatic recovery had given up. Forgetting it made the next MystTiq start treat the
//     still-down server as a fresh crash and restart it again, the very thing giving up is meant to stop.
//   - PinnedNotificationId: the pinned "server is DOWN" notice. Forgetting it left that notice pinned forever,
//     because nothing could unpin it once the server came back.
// One small JSON file per server profile, written atomically. Only api-run passes a store; service-run's
// supervisor keeps its own behaviour (it exits on give-up and relies on the OS service manager).
public sealed record SupervisorRecoveryState(IReadOnlyList<DateTimeOffset> RestartHistory, DateTimeOffset? GaveUpAtUtc, string? PinnedNotificationId)
{
    public static SupervisorRecoveryState Empty { get; } = new([], null, null);
}

public sealed class SupervisorRecoveryStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object gate = new();
    private readonly string path;
    private SupervisorRecoveryState state;

    public SupervisorRecoveryStateStore(string path)
    {
        this.path = path;
        state = Load();
    }

    public static SupervisorRecoveryStateStore ForProfile(IServerPathProfile paths) =>
        new(Path.Combine(paths.ManagerRuntimeRoot, "crash-recovery", "state.json"));

    public SupervisorRecoveryState Read() { lock (gate) return state; }

    public SupervisorRecoveryState Update(Func<SupervisorRecoveryState, SupervisorRecoveryState> change)
    {
        lock (gate)
        {
            var updated = change(state);
            state = updated with { RestartHistory = updated.RestartHistory ?? [] };
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var partial = path + ".partial";
                File.WriteAllText(partial, JsonSerializer.Serialize(state, JsonOptions));
                File.Move(partial, path, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Recovery must keep working even if its state cannot be saved; memory stays authoritative.
            }
            return state;
        }
    }

    private SupervisorRecoveryState Load()
    {
        try
        {
            var loaded = File.Exists(path) ? JsonSerializer.Deserialize<SupervisorRecoveryState>(File.ReadAllText(path)) : null;
            return loaded is null ? SupervisorRecoveryState.Empty : loaded with { RestartHistory = loaded.RestartHistory ?? [] };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return SupervisorRecoveryState.Empty;
        }
    }
}
