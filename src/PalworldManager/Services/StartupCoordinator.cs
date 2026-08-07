namespace PalworldManager.Services;

public sealed record StartupStageResult(string Name, bool Success, TimeSpan Duration, string Message);

public sealed class StartupCoordinator
{
    public async Task<IReadOnlyList<StartupStageResult>> RunAsync(
        IEnumerable<(string Name, Func<Task> Execute)> stages,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<StartupStageResult>();
        foreach (var stage in stages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timer = Stopwatch.StartNew();
            try
            {
                await stage.Execute();
                timer.Stop();
                results.Add(new StartupStageResult(stage.Name, true, timer.Elapsed, "Completed"));
                log?.Invoke($"[STARTUP] {stage.Name} completed in {timer.Elapsed.TotalMilliseconds:0} ms.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                timer.Stop();
                results.Add(new StartupStageResult(stage.Name, false, timer.Elapsed, ex.Message));
                log?.Invoke($"[STARTUP] {stage.Name} was unavailable after {timer.Elapsed.TotalMilliseconds:0} ms: {ex.Message}");
            }
        }
        return results;
    }
}
