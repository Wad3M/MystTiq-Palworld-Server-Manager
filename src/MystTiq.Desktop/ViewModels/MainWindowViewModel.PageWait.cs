// MystTiq v1.0.7.2: file reviewed for this release (2026-10-11).
namespace MystTiq.Desktop.ViewModels;

// v1.0.7.2 (roadmap Q-13): Map and Players opened while the app was busy used to check the busy state every 500 ms for up
// to a minute, showing nothing meanwhile, and kept waiting after the owner had moved on. They now say at once what they
// are waiting for, load the moment the busy work ends (a signal, not a poll), and stop waiting when the owner leaves.
public sealed partial class MainWindowViewModel
{
    public static TimeSpan PageWaitLimit { get; set; } = TimeSpan.FromMinutes(1);

    private TaskCompletionSource _busyCleared = Completed();
    private CancellationTokenSource? _pageWait;
    private string _pageWaitText = string.Empty;

    public string PageWaitText { get => _pageWaitText; private set { if (SetField(ref _pageWaitText, value)) RaisePropertyChanged(nameof(HasPageWait)); } }
    public bool HasPageWait => PageWaitText.Length > 0;

    private static TaskCompletionSource Completed()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        done.SetResult();
        return done;
    }

    // Called whenever the busy state changes.
    private void SignalBusy(bool busy)
    {
        if (busy)
        {
            if (_busyCleared.Task.IsCompleted) _busyCleared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        else _busyCleared.TrySetResult();
    }

    // Leaving a page ends its wait.
    private void CancelPageWait() => _pageWait?.Cancel();

    /// <summary>Waits until the app is not busy. False when the owner left the page, or the wait ran out.</summary>
    private async Task<bool> WaitUntilNotBusyAsync(Models.NavigationPage page)
    {
        _pageWait?.Cancel();
        var wait = new CancellationTokenSource(PageWaitLimit);
        _pageWait = wait;
        PageWaitText = BusyReason is { Length: > 0 } reason
            ? $"Waiting for \"{reason.TrimEnd('…', '.')}\" to finish, then this page loads."
            : "Waiting for the current task to finish, then this page loads.";
        try
        {
            while (IsBusy) await _busyCleared.Task.WaitAsync(wait.Token);
            return SelectedPage == page;
        }
        catch (OperationCanceledException) { return false; }
        finally
        {
            if (ReferenceEquals(_pageWait, wait))
            {
                _pageWait = null;
                PageWaitText = string.Empty;
            }
            wait.Dispose();
        }
    }
}
