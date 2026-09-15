using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MystTiq.Desktop.Views;

public enum ConfirmCloseTabResult { Cancel, LeaveRunning, StopAndClose }

// v0.7.11.0: shown when closing a tab whose server is currently running, instead of silently
// leaving it running (the previous behavior) or silently stopping it -- the user picks.
public sealed partial class ConfirmCloseTabDialog : Window
{
    public ConfirmCloseTabDialog()
    {
        InitializeComponent();
    }

    public ConfirmCloseTabDialog(string serverName) : this()
    {
        MessageText.Text = $"\"{serverName}\" is currently running. Do you want to stop it before closing this tab, or leave it running in the background?";
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmCloseTabResult.Cancel);
    private void LeaveRunning_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmCloseTabResult.LeaveRunning);
    private void StopAndClose_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmCloseTabResult.StopAndClose);
}
