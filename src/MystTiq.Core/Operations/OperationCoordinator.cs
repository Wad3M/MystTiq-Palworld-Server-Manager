using System.Text.Json;
using MystTiq.Core.Services;

namespace MystTiq.Core.Operations;

// Central coordinator for long-running/destructive actions. Persists one JSON
// file per operation under ManagerRuntimeRoot/operations/journals/{profile}/, reusing
// the exact atomic-temp-file-then-move pattern already proven by
// HeadlessWorldTransactionJournal (WorldTransactionJournalService.Advance) --
// just promoted to Core and made kind-agnostic instead of copied per feature.
//
// v0.6.2.0: this is now a fleet-level singleton shared by every server profile (constructed
// once against the fleet root, not any one profile's paths) so a Fleet dashboard can see
// operations across the whole fleet. Resource locks are keyed by (profile, resourceKey) so a
// "world-mutation" lock on one server never blocks another -- same-profile locking is
// unaffected (still reject-if-conflicting within that profile).
public sealed class OperationCoordinator : IOperationCoordinator
{
    private readonly IServerPathProfile paths;
    private readonly object gate = new();
    private readonly Dictionary<(ServerProfileId Profile, string Key), OperationId> resourceLocks = new();
    private readonly Dictionary<OperationId, OperationRecord> records = [];
    private readonly List<OperationId> order = [];

    public OperationCoordinator(IServerPathProfile paths)
    {
        this.paths = paths;
    }

    public Task<OperationHandle> BeginAsync(ServerProfileId profile, string kind, string source, IReadOnlyList<string> resourceKeys, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            foreach (var key in resourceKeys)
            {
                if (resourceLocks.TryGetValue((profile, NormalizeKey(key)), out var holder))
                    throw new InvalidOperationException($"Blocked: resource '{key}' on server '{profile}' is held by operation {holder} until it completes. Try again once that operation finishes.");
            }

            var id = OperationId.New();
            foreach (var key in resourceKeys) resourceLocks[(profile, NormalizeKey(key))] = id;

            var now = DateTimeOffset.UtcNow;
            var journalPath = Path.Combine(paths.ManagerRuntimeRoot, "operations", "journals", profile.Value, $"operation-{id}.json");
            var record = new OperationRecord
            {
                OperationId = id,
                ServerProfileId = profile,
                Kind = kind,
                Source = source,
                ResourceKeys = resourceKeys,
                Phase = OperationPhase.Running,
                CreatedUtc = now,
                UpdatedUtc = now,
                JournalPath = journalPath
            };
            record.Stages.Add(new OperationStage("Started", $"{kind} started by {source}.", now));
            records[id] = record;
            order.Add(id);
            Persist(record);
            return Task.FromResult(new OperationHandle(this, profile, id, resourceKeys));
        }
    }

    public void Advance(OperationId id, string state, string detail)
    {
        lock (gate)
        {
            if (!records.TryGetValue(id, out var record)) return;
            record.UpdatedUtc = DateTimeOffset.UtcNow;
            record.Stages.Add(new OperationStage(state, detail, record.UpdatedUtc));
            Persist(record);
        }
    }

    public void SetSafetyBackup(OperationId id, string fileName)
    {
        lock (gate)
        {
            if (!records.TryGetValue(id, out var record)) return;
            record.SafetyBackup = fileName;
            Persist(record);
        }
    }

    public void Complete(OperationId id, string detail)
    {
        lock (gate)
        {
            if (!records.TryGetValue(id, out var record)) return;
            record.Phase = OperationPhase.Completed;
            record.UpdatedUtc = DateTimeOffset.UtcNow;
            record.Stages.Add(new OperationStage("Committed", detail, record.UpdatedUtc));
            Persist(record);
        }
    }

    public void Fail(OperationId id, string detail, bool rolledBack = false)
    {
        lock (gate)
        {
            if (!records.TryGetValue(id, out var record)) return;
            record.Phase = rolledBack ? OperationPhase.RolledBack : OperationPhase.Failed;
            record.RolledBack = rolledBack;
            record.UpdatedUtc = DateTimeOffset.UtcNow;
            record.Stages.Add(new OperationStage(rolledBack ? "RolledBack" : "Failed", detail, record.UpdatedUtc));
            Persist(record);
        }
    }

    public IReadOnlyList<OperationRecord> ListRecent(int max = 50) => ListRecent(null, max);

    public IReadOnlyList<OperationRecord> ListRecent(ServerProfileId? profile, int max = 50)
    {
        lock (gate)
        {
            var sequence = order.AsEnumerable().Reverse().Select(id => records[id]);
            if (profile is { } filter)
                sequence = sequence.Where(r => r.ServerProfileId.Equals(filter));
            return sequence.Take(max).ToList();
        }
    }

    public OperationRecord? Find(OperationId id)
    {
        lock (gate)
        {
            return records.TryGetValue(id, out var record) ? record : null;
        }
    }

    internal void ReleaseLocks(ServerProfileId profile, OperationId id, IReadOnlyList<string> resourceKeys)
    {
        lock (gate)
        {
            foreach (var key in resourceKeys)
            {
                var normalized = (profile, NormalizeKey(key));
                if (resourceLocks.TryGetValue(normalized, out var holder) && holder.Equals(id))
                    resourceLocks.Remove(normalized);
            }
        }
    }

    private static string NormalizeKey(string key) => key.ToUpperInvariant();

    private static void Persist(OperationRecord record)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(record.JournalPath)!);
            var temp = record.JournalPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, record.JournalPath, true);
        }
        catch
        {
            // Journal persistence is best-effort disk evidence; in-memory state remains
            // authoritative for the process lifetime and a real mutation should not fail
            // over a journal write failure (e.g. a read-only runtime directory).
        }
    }
}
