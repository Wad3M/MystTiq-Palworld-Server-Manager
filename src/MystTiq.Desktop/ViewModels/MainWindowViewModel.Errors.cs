// MystTiq v1.1.5.0: file reviewed for this release (2026-10-25).
using Avalonia.Threading;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v1.0.7.0 (roadmap Q-2): an error nobody caught is shown on every page (with where its record was written) instead of
// closing the app, and the commands that used to be posted fire-and-forget run through RunUi, which catches, records and
// clears the busy state.
public sealed partial class MainWindowViewModel
{
    private string _unexpectedErrorText = string.Empty;
    private System.Windows.Input.ICommand? _dismissUnexpectedErrorCommand;

    public string UnexpectedErrorText { get => _unexpectedErrorText; private set { if (SetField(ref _unexpectedErrorText, value)) RaisePropertyChanged(nameof(HasUnexpectedError)); } }
    public bool HasUnexpectedError => UnexpectedErrorText.Length > 0;
    public System.Windows.Input.ICommand DismissUnexpectedErrorCommand =>
        _dismissUnexpectedErrorCommand ??= new RelayCommand(() => UnexpectedErrorText = string.Empty);

    public void ShowUnexpectedError(string source, Exception error, string? recordPath)
    {
        IsBusy = false;
        UnexpectedErrorText = recordPath is null
            ? $"Something went wrong ({source}): {error.Message} No record could be written."
            : $"Something went wrong ({source}): {error.Message} The details are in {recordPath}.";
    }

    /// <summary>Runs a command's work on the UI thread; an error is recorded and shown, never lost. The record names the call.</summary>
    private void RunUi(Func<Task> work, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(work))] string name = "") =>
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try { await work(); }
            catch (Exception ex) { CrashRecorder.Record(name, ex); }
        });
}
