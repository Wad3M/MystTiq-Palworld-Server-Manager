// MystTiq v1.0.9.2: file reviewed for this release (2026-10-12).
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

/// <summary>v1.0.7.2 (roadmap Q-8): the steps a server start goes through, in the words the Dashboard shows.</summary>
public static class StartupStage
{
    public const string CheckingSettings = "Checking settings";
    public const string Launching = "Launching PalServer";
    public const string WaitingForGamePort = "Waiting for the game port";
}

/// <summary>
/// v1.0.7.2 (roadmap Q-8): a start used to say only "Starting" until it was running or had failed, so a slow start and a
/// stuck one looked the same, and a failed start did not say how far it got. The platform lifecycle services move this
/// through the stages; every status read during the start carries the current stage and since when, and a start that does
/// not succeed names the stage it stopped at.
/// </summary>
public sealed class StartupProgress
{
    private readonly object gate = new();
    private readonly Func<DateTimeOffset> clock;
    private string? stage;
    private DateTimeOffset? stageSince;
    private DateTimeOffset? began;

    public StartupProgress(Func<DateTimeOffset>? clock = null) => this.clock = clock ?? (() => DateTimeOffset.UtcNow);

    public string? Current { get { lock (gate) return stage; } }

    public void Begin()
    {
        lock (gate)
        {
            began = clock();
            stage = StartupStage.CheckingSettings;
            stageSince = began;
        }
    }

    public void Enter(string next)
    {
        lock (gate)
        {
            if (began is null) return;
            stage = next;
            stageSince = clock();
        }
    }

    public void End()
    {
        lock (gate) { stage = null; stageSince = null; began = null; }
    }

    /// <summary>The status with the start's stage on it, while a start runs.</summary>
    public ServerLifecycleSnapshot Decorate(ServerLifecycleSnapshot snapshot)
    {
        lock (gate)
            return stage is null ? snapshot : snapshot with { StartupStage = stage, StartupStageSinceUtc = stageSince, StartupBeganUtc = began };
    }

    /// <summary>A start that did not succeed says which stage it stopped at (a duplicate start is not a failed one).</summary>
    public ServerLifecycleOperationResult Conclude(ServerLifecycleOperationResult result)
    {
        string? at;
        lock (gate) at = stage;
        return result.Success || result.ExitCode == HeadlessExitCode.AlreadyRunning || at is null
            ? result
            : result with { Message = $"The start stopped at \"{at}\": {result.Message}" };
    }
}
