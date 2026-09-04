namespace MystTiq.HeadlessHost;

// In-memory, single-instance only (per the milestone's deferred list -- persisted/distributed
// rate-limit state is out of scope). Reuses the existing activity/audit sink rather than
// building a new one.
public sealed class HeadlessAuthAbuseGuardService
{
    private const int MaxFailures = 8;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly HeadlessActivityLogService activity;
    private readonly object gate = new();
    private readonly Dictionary<string, (List<DateTimeOffset> Failures, DateTimeOffset? LockedUntilUtc)> state = new(StringComparer.Ordinal);

    public HeadlessAuthAbuseGuardService(HeadlessActivityLogService activity) => this.activity = activity;

    public bool IsLockedOut(string remoteIp)
    {
        lock (gate)
        {
            if (!state.TryGetValue(remoteIp, out var entry)) return false;
            return entry.LockedUntilUtc.HasValue && entry.LockedUntilUtc.Value > DateTimeOffset.UtcNow;
        }
    }

    public void RecordFailure(string remoteIp)
    {
        lock (gate)
        {
            if (!state.TryGetValue(remoteIp, out var entry))
                entry = (new List<DateTimeOffset>(), null);

            var now = DateTimeOffset.UtcNow;
            var failures = entry.Failures;
            failures.Add(now);
            failures.RemoveAll(x => now - x > FailureWindow);

            DateTimeOffset? lockedUntilUtc = entry.LockedUntilUtc;
            if (failures.Count >= MaxFailures)
            {
                lockedUntilUtc = now + LockoutDuration;
                activity.Record("Critical", "Auth", "Remote IP locked out after repeated authentication failures", $"ip={remoteIp}; failures={failures.Count}; lockedUntil={lockedUntilUtc:O}");
            }
            state[remoteIp] = (failures, lockedUntilUtc);
        }
    }

    public void RecordSuccess(string remoteIp)
    {
        lock (gate) state.Remove(remoteIp);
    }
}
