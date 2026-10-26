// MystTiq v1.1.5.1: file reviewed for this release (2026-10-26).
using MystTiq.Core.Models;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.1.4.0 (roadmap U-1, owner 2026-10-19: "add a server update card by adjusting the top bar to fit it there. A button to
/// update the server."). The Dashboard's Server update card: the installed build against Steam's public build, and one
/// button that runs the whole update here, next to the server, so it finishes even if the desktop closes: stop the server
/// (reason "Server update") if it runs, take a safety backup, run the SteamCMD update, start the server again if it was
/// running. Each step goes to the activity log. A failed step ends the update, starts the server again when it was
/// running, and raises a notification. One update at a time; another operation holding the server (a start, a backup, a
/// MOD change, a save edit) refuses it with the reason.
/// </summary>
public sealed class HeadlessServerUpdateFlowService
{
    public const string StopReason = "Server update";

    private readonly Func<CancellationToken, Task<ComponentVersionInfo>> checkBuild;
    private readonly IServerLifecycleService lifecycle;
    private readonly Func<CancellationToken, Task<ServerLifecycleOperationResult>> startServer;
    private readonly Func<CancellationToken, Task<(bool Success, string Message)>> backup;
    private readonly Func<CancellationToken, Task<(bool Success, string Message)>> update;
    private readonly TimeSpan stopTimeout;
    private readonly IOperationCoordinator? operations;
    private readonly ServerProfileId? profileId;
    private readonly Action<string, string, string>? record;
    private readonly Action<string, string>? notifyFailure;
    // v1.1.5.0 (owner 2026-10-23: "the server update button should be referring to the MystTiq version to update"): the
    // card also shows MystTiq's latest release; GitHub's answer is kept 15 minutes.
    private readonly Func<CancellationToken, Task<ComponentVersionInfo>>? checkMystTiq;
    private static readonly TimeSpan MystTiqCheckAge = TimeSpan.FromMinutes(15);
    private (DateTimeOffset At, ComponentVersionInfo Info)? mystTiqCheck;
    private readonly object gate = new();
    private ServerUpdateJob? job;

    public HeadlessServerUpdateFlowService(
        Func<CancellationToken, Task<ComponentVersionInfo>> checkBuild,
        IServerLifecycleService lifecycle,
        Func<CancellationToken, Task<ServerLifecycleOperationResult>> startServer,
        Func<CancellationToken, Task<(bool Success, string Message)>> backup,
        Func<CancellationToken, Task<(bool Success, string Message)>> update,
        TimeSpan stopTimeout,
        IOperationCoordinator? operations = null,
        ServerProfileId? profileId = null,
        Action<string, string, string>? record = null,
        Action<string, string>? notifyFailure = null,
        Func<CancellationToken, Task<ComponentVersionInfo>>? checkMystTiq = null)
    {
        this.checkMystTiq = checkMystTiq;
        this.checkBuild = checkBuild;
        this.lifecycle = lifecycle;
        this.startServer = startServer;
        this.backup = backup;
        this.update = update;
        this.stopTimeout = stopTimeout;
        this.operations = operations;
        this.profileId = profileId;
        this.record = record;
        this.notifyFailure = notifyFailure;
    }

    public ServerUpdateJob? CurrentJob { get { lock (gate) return job; } }

    /// <summary>The card: the build check (SteamCMD's answer is kept 15 minutes) and the running or last update.</summary>
    public async Task<ServerUpdateCard> GetCardAsync(CancellationToken cancellationToken)
    {
        ComponentVersionInfo check;
        try { check = await checkBuild(cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            check = new ComponentVersionInfo("Core Server", "Palworld Dedicated Server", "Unknown", "Unknown", "Unavailable",
                "SteamCMD app info (public branch)", DateTimeOffset.UtcNow, "The build check failed: " + ex.Message);
        }
        var current = CurrentJob;
        var mystTiq = await GetMystTiqCheckAsync(cancellationToken);
        return new ServerUpdateCard(check.InstalledVersion, check.LatestVersion, current is { Running: true } ? "Updating" : StateFor(check.Status),
            check.Detail, check.LastChecked, current, mystTiq?.InstalledVersion, mystTiq?.LatestVersion, mystTiq is null ? null : StateFor(mystTiq.Status), mystTiq?.Detail);
    }

    private async Task<ComponentVersionInfo?> GetMystTiqCheckAsync(CancellationToken cancellationToken)
    {
        if (checkMystTiq is null) return null;
        lock (gate)
            if (mystTiqCheck is { } kept && DateTimeOffset.UtcNow - kept.At < MystTiqCheckAge) return kept.Info;
        ComponentVersionInfo info;
        try { info = await checkMystTiq(cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            info = new ComponentVersionInfo("Core Server", "MystTiq Server Manager", "Unknown", "Unknown", "Unavailable", "GitHub", DateTimeOffset.UtcNow, "The MystTiq release check failed: " + ex.Message);
        }
        lock (gate) mystTiqCheck = (DateTimeOffset.UtcNow, info);
        return info;
    }

    /// <summary>The card's state in the English the desktop's tags read (StatusTags).</summary>
    public static string StateFor(string? status) => status switch
    {
        "UpToDate" => "Up to date",
        "UpdateAvailable" => "Update available",
        "NotInstalled" => "Not installed",
        _ => "Unavailable"
    };

    /// <summary>Starts the update in the background (tests pass background: false and call RunAsync). While one runs, that
    /// one is returned (Started false).</summary>
    public (ServerUpdateJob Job, bool Started) Begin(string? requestedBy, bool background = true)
    {
        ServerUpdateJob started;
        lock (gate)
        {
            if (job is { Running: true }) return (job, false);
            started = new ServerUpdateJob(Guid.NewGuid().ToString("N")[..12], "Checking", true, null, "Starting the server update…",
                DateTimeOffset.UtcNow, null, false, requestedBy);
            job = started;
        }
        // Only the service stopping cancels it (like the Update Center's update): half-updated server files are worse.
        if (background) _ = BackgroundWork.Shared.Run("server update", token => RunAsync(started.Id, token), BackgroundWork.Shared.Stopping);
        return (started, true);
    }

    /// <summary>The update itself.</summary>
    public async Task<ServerUpdateJob> RunAsync(string jobId, CancellationToken cancellationToken)
    {
        var wasRunning = false;
        var steps = new List<string>();
        try
        {
            wasRunning = (await lifecycle.GetStatusAsync(cancellationToken)).ServerMayBeRunning;
            Set(jobId, j => j with { WasRunning = wasRunning });
            Record("Information", "Server update started",
                (CurrentJob?.RequestedBy is { Length: > 0 } who ? $"Asked for by {who}. " : string.Empty) +
                (wasRunning ? "The server is running: it is stopped first and started again afterwards." : "The server is stopped."));

            if (wasRunning)
            {
                Phase(jobId, "Stopping", "Stopping the server for the update…");
                var (hold, refusal) = await OperationHold.TryBeginAsync(operations, profileId, "server-update-stop", nameof(HeadlessServerUpdateFlowService),
                    ["lifecycle"], "Stopping the server for its update", cancellationToken);
                if (refusal is not null) return await FinishAsync(jobId, false, refusal, startAgain: false);
                ServerLifecycleOperationResult stopped;
                using (hold)
                using (ServerStopReason.Begin(StopReason))
                {
                    stopped = await lifecycle.StopAsync(stopTimeout, cancellationToken);
                    if (stopped.Success) hold!.Succeeded(stopped.Message); else hold!.Failed(stopped.Message);
                }
                if (!stopped.Success) return await FinishAsync(jobId, false, "The server could not be stopped, so it was not updated: " + stopped.Message, startAgain: false);
                steps.Add("The server was stopped.");
            }

            Phase(jobId, "Backing up", "Taking a safety backup…");
            var (backedUp, backupMessage) = await backup(cancellationToken);
            if (!backedUp) return await FinishAsync(jobId, false, "The safety backup failed, so the server was not updated: " + backupMessage, startAgain: wasRunning);
            steps.Add("A safety backup was taken.");
            Record("Information", "Server update: safety backup", backupMessage);

            Phase(jobId, "Updating", "Updating the server files with SteamCMD… (this can take several minutes)");
            var (updated, updateMessage) = await update(cancellationToken);
            if (!updated) return await FinishAsync(jobId, false, string.Join(" ", steps.Append("The update failed: " + updateMessage)), startAgain: wasRunning);
            steps.Add(updateMessage);

            return await FinishAsync(jobId, true, string.Join(" ", steps), startAgain: wasRunning);
        }
        catch (Exception ex)
        {
            return await FinishAsync(jobId, false, string.Join(" ", steps.Append("The update stopped: " + ex.Message)), startAgain: wasRunning && ex is not OperationCanceledException);
        }
    }

    private async Task<ServerUpdateJob> FinishAsync(string jobId, bool success, string message, bool startAgain)
    {
        if (startAgain)
        {
            Phase(jobId, "Starting", "Starting the server again…");
            try
            {
                var started = await startServer(CancellationToken.None);
                message += started.Success ? " The server was started again." : " The server did not start again: " + started.Message;
                success &= started.Success;
            }
            catch (Exception ex) { message += " The server did not start again: " + ex.Message; success = false; }
        }
        Record(success ? "Information" : "Warning", success ? "Server update finished" : "Server update did not finish", message);
        if (!success) notifyFailure?.Invoke("The server update did not finish", message);
        return Set(jobId, j => j with { Running = false, Success = success, Phase = success ? "Done" : "Failed", Message = message, FinishedAt = DateTimeOffset.UtcNow });
    }

    private void Phase(string jobId, string phase, string message) => Set(jobId, j => j with { Phase = phase, Message = message });

    private ServerUpdateJob Set(string jobId, Func<ServerUpdateJob, ServerUpdateJob> change)
    {
        lock (gate)
        {
            if (job is null || job.Id != jobId) throw new InvalidOperationException("The update job was replaced.");
            job = change(job);
            return job;
        }
    }

    private void Record(string level, string title, string detail)
    {
        try { record?.Invoke(level, title, detail); } catch { /* the log never stops an update */ }
    }
}

/// <summary>v1.1.4.0 (roadmap U-1): what the Dashboard's update card shows; v1.1.5.0 adds MystTiq's own release.</summary>
public sealed record ServerUpdateCard(string Installed, string Latest, string State, string Detail, DateTimeOffset? CheckedAt, ServerUpdateJob? Job,
    string? MystTiqInstalled = null, string? MystTiqLatest = null, string? MystTiqState = null, string? MystTiqDetail = null);

/// <summary>v1.1.4.0 (roadmap U-1): one run of the update. Phase: Checking, Stopping, Backing up, Updating, Starting, Done, Failed.</summary>
public sealed record ServerUpdateJob(string Id, string Phase, bool Running, bool? Success, string Message, DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt, bool WasRunning, string? RequestedBy);
