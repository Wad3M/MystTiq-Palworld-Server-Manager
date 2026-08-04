using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace PalworldManager.Services.Infrastructure;

public sealed class BackgroundTaskManager
{
    private readonly ConcurrentDictionary<string, Task> _tasks = new();

    public Task RunAsync(
        string key,
        Func<CancellationToken, Task> work,
        CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(work);

        return _tasks.GetOrAdd(
            key,
            taskKey => Task.Run(
                async () =>
                {
                    try
                    {
                        await work(token).ConfigureAwait(false);
                    }
                    finally
                    {
                        _tasks.TryRemove(taskKey, out var _);
                    }
                },
                token));
    }

    public bool IsRunning(string key) =>
        !string.IsNullOrWhiteSpace(key) && _tasks.ContainsKey(key);
}
