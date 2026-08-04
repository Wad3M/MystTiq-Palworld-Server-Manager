using System.Collections.Concurrent;

namespace PalworldManager.Services.Infrastructure;

public sealed class PageOperationCoordinator : IDisposable
{
    private sealed class ActiveOperation
    {
        public required string Name { get; init; }
        public required DateTime StartedUtc { get; init; }
        public required CancellationTokenSource Cancellation { get; init; }
    }

    private readonly ConcurrentDictionary<string, ActiveOperation> active = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    public event Action<OperationProgress>? ProgressChanged;

    public IReadOnlyCollection<string> ActiveKeys => active.Keys.ToArray();

    public bool IsRunning(string key) =>
        !string.IsNullOrWhiteSpace(key) && active.ContainsKey(key);

    public async Task<bool> RunAsync(
        string key,
        string operationName,
        Func<PageOperationContext, Task> work,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(work);

        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var operation = new ActiveOperation
        {
            Name = operationName,
            StartedUtc = DateTime.UtcNow,
            Cancellation = linked
        };

        if (!active.TryAdd(key, operation))
        {
            linked.Dispose();
            return false;
        }

        Publish(key, operation, "Starting", 0, OperationState.Running);
        var context = new PageOperationContext(
            key,
            operationName,
            linked.Token,
            (step, percent) => Publish(key, operation, step, percent, OperationState.Running));

        try
        {
            await work(context);
            linked.Token.ThrowIfCancellationRequested();
            Publish(key, operation, "Complete", 100, OperationState.Completed);
            return true;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            Publish(key, operation, "Cancelled", null, OperationState.Cancelled);
            return false;
        }
        catch (Exception ex)
        {
            Publish(key, operation, "Failed", null, OperationState.Failed, ex.Message);
            throw;
        }
        finally
        {
            active.TryRemove(key, out _);
            linked.Dispose();
        }
    }

    public bool Cancel(string key)
    {
        if (!active.TryGetValue(key, out var operation)) return false;
        operation.Cancellation.Cancel();
        return true;
    }

    private void Publish(
        string key,
        ActiveOperation operation,
        string step,
        int? percent,
        OperationState state,
        string? error = null)
    {
        var now = DateTime.UtcNow;
        ProgressChanged?.Invoke(new OperationProgress(
            key,
            operation.Name,
            step,
            percent is null ? null : Math.Clamp(percent.Value, 0, 100),
            state,
            operation.StartedUtc,
            now,
            now - operation.StartedUtc,
            state == OperationState.Running,
            error));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var operation in active.Values)
            operation.Cancellation.Cancel();
        foreach (var operation in active.Values)
            operation.Cancellation.Dispose();
        active.Clear();
    }
}

public sealed class PageOperationContext
{
    private readonly Action<string, int?> report;

    internal PageOperationContext(
        string key,
        string operationName,
        CancellationToken cancellationToken,
        Action<string, int?> report)
    {
        Key = key;
        OperationName = operationName;
        CancellationToken = cancellationToken;
        this.report = report;
    }

    public string Key { get; }
    public string OperationName { get; }
    public CancellationToken CancellationToken { get; }

    public void Report(string step, int? percent = null)
    {
        CancellationToken.ThrowIfCancellationRequested();
        report(string.IsNullOrWhiteSpace(step) ? "Working" : step.Trim(), percent);
    }
}
