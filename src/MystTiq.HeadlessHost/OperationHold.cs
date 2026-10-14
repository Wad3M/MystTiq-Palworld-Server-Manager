// MystTiq v1.1.0.2: file reviewed for this release (2026-10-13).
using MystTiq.Core.Operations;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.7.1 (roadmap Q-4): the operation coordinator decides every conflict. Work that used to check "is the server
/// running?" and then go ahead (a SteamCMD update, a MOD change, a PalDefender update) or that held no lock at all (a
/// scheduled or manual backup) now holds the coordinator's lock for its whole run, so a start (or a save edit) arriving
/// in between is refused with the reason, and the work itself is refused while one of those runs. Each hold is an
/// operation in the history like any other.
/// </summary>
public sealed class OperationHold : IDisposable
{
    private readonly IOperationCoordinator? operations;
    private readonly OperationHandle? handle;
    private bool finished;

    private OperationHold(IOperationCoordinator? operations, OperationHandle? handle)
    {
        this.operations = operations;
        this.handle = handle;
    }

    /// <summary>
    /// Takes the given locks, or returns why not. Without a coordinator (a harness, a one-off command) it holds nothing and
    /// always succeeds, as before.
    /// </summary>
    public static async Task<(OperationHold? Hold, string? Refusal)> TryBeginAsync(
        IOperationCoordinator? operations, ServerProfileId? profile, string kind, string source, IReadOnlyList<string> resourceKeys,
        string what, CancellationToken cancellationToken)
    {
        if (operations is null || profile is null) return (new OperationHold(null, null), null);
        try
        {
            var handle = await operations.BeginAsync(profile.Value, kind, source, resourceKeys, cancellationToken);
            return (new OperationHold(operations, handle), null);
        }
        catch (InvalidOperationException ex)
        {
            return (null, $"{what} is refused while another operation on this server is in progress. {ex.Message}");
        }
    }

    public void Succeeded(string detail)
    {
        if (handle is null || finished) return;
        finished = true;
        operations!.Complete(handle.Id, detail);
    }

    public void Failed(string detail)
    {
        if (handle is null || finished) return;
        finished = true;
        operations!.Fail(handle.Id, detail);
    }

    public void Dispose()
    {
        if (handle is null) return;
        if (!finished) Succeeded("Finished.");
        handle.Dispose();
    }
}
