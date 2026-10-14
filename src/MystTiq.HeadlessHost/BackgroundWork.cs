// MystTiq v1.0.8.1: file reviewed for this release (2026-10-11).
using System.Collections.Concurrent;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.7.0 (roadmap Q-3): work the service starts and does not wait for (refreshing the public address, reading backup
/// world days, decoding the world clock, Discord's connection, outside notification sends, component checks) used to be
/// started with a bare <c>Task.Run</c>: its errors were dropped, often by an empty catch, nothing stopped it at shutdown and
/// nothing said it was running. It now runs here, by name:
/// <list type="bullet">
/// <item>a failure is logged once (again only when the message changes, or after the work has succeeded since);</item>
/// <item>every run gets the service's stopping token, which is cancelled at shutdown;</item>
/// <item>shutdown waits for running work up to a limit and names whatever is still running.</item>
/// </list>
/// </summary>
public sealed class BackgroundWork
{
    /// <summary>The service's own tracker.</summary>
    public static BackgroundWork Shared { get; } = new();

    private readonly ConcurrentDictionary<long, (string Name, Task Task)> running = new();
    private readonly ConcurrentDictionary<string, string> lastFailure = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource stopping = new();
    private long next;

    /// <summary>Where failures are written (the service sets the activity log): name, message.</summary>
    public Action<string, string>? FailureLog { get; set; }

    public CancellationToken Stopping => stopping.Token;

    /// <summary>The names of the work running now.</summary>
    public IReadOnlyList<string> Running => running.Values.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

    /// <summary>Starts named work. It receives a token cancelled at shutdown (and by <paramref name="cancellation"/>, when given).</summary>
    public Task Run(string name, Func<CancellationToken, Task> work, CancellationToken cancellation = default)
    {
        var id = Interlocked.Increment(ref next);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = Task.Run(async () =>
        {
            await gate.Task;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token, cancellation);
            try
            {
                await work(linked.Token);
                lastFailure.TryRemove(name, out _);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
            catch (Exception ex) { Report(name, ex); }
            finally { running.TryRemove(id, out _); }
        });
        running[id] = (name, task);
        gate.SetResult();
        return task;
    }

    /// <summary>Cancels running work and waits for it up to <paramref name="timeout"/>; returns what is still running.</summary>
    public async Task<IReadOnlyList<string>> StopAsync(TimeSpan timeout)
    {
        stopping.Cancel();
        var tasks = running.Values.Select(r => r.Task).ToArray();
        if (tasks.Length > 0) await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(timeout));
        return Running;
    }

    private void Report(string name, Exception error)
    {
        var message = error.Message;
        if (lastFailure.TryGetValue(name, out var previous) && previous == message) return;
        lastFailure[name] = message;
        try { FailureLog?.Invoke(name, message); }
        catch (Exception) { /* the log failing must not hide the next failure */ }
    }
}

/// <summary>v1.0.7.0 (roadmap Q-3): one run per key at a time (an automation rule's run).</summary>
public sealed class SingleRunGate<TKey> where TKey : notnull
{
    private readonly HashSet<TKey> running = [];
    private readonly object gate = new();

    /// <summary>True when the key was free and is now taken; false while a run for it is still in progress.</summary>
    public bool TryEnter(TKey key) { lock (gate) return running.Add(key); }

    public void Exit(TKey key) { lock (gate) running.Remove(key); }

    public bool IsRunning(TKey key) { lock (gate) return running.Contains(key); }
}
