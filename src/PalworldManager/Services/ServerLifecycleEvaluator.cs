namespace PalworldManager.Services;

/// <summary>
/// Pure lifecycle-state policy. It interprets process observations but performs no
/// process operations, making lifecycle decisions independently testable.
/// </summary>
public sealed class ServerLifecycleEvaluator
{
    public ServerLifecycleState Evaluate(
        ServerLifecycleState current,
        bool processDetected,
        DateTime lifecycleChangedUtc,
        int restartWarningSeconds,
        DateTime utcNow)
    {
        var state = current;

        if (!processDetected && state is ServerLifecycleState.Running or ServerLifecycleState.Starting)
            state = ServerLifecycleState.Crashed;
        else if (!processDetected && state == ServerLifecycleState.Stopping)
            state = ServerLifecycleState.Stopped;
        else if (processDetected && state == ServerLifecycleState.Stopped)
            state = ServerLifecycleState.Running;

        if (state == ServerLifecycleState.Starting &&
            utcNow - lifecycleChangedUtc > TimeSpan.FromSeconds(45) &&
            processDetected)
            state = ServerLifecycleState.Running;

        var shutdownHungThreshold = TimeSpan.FromSeconds(Math.Max(45, restartWarningSeconds + 45));
        if (state == ServerLifecycleState.Stopping &&
            utcNow - lifecycleChangedUtc > shutdownHungThreshold &&
            processDetected)
            state = ServerLifecycleState.Hung;

        return state;
    }

    public string Describe(ServerLifecycleState state) => state switch
    {
        ServerLifecycleState.Hung => "Palworld process remains after the shutdown timeout.",
        ServerLifecycleState.Crashed => "The managed Palworld process exited unexpectedly.",
        ServerLifecycleState.Starting => "Palworld is starting.",
        ServerLifecycleState.Stopping => "Palworld is stopping.",
        ServerLifecycleState.Running => "Palworld process is running.",
        _ => "No Palworld server process is running."
    };
}
