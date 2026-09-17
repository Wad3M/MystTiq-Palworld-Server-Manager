using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MystTiq.Desktop.Views;

// v0.7.74.0: SafeExit/ForceExit added -- previously this dialog only offered Cancel/MinimizeToTray,
// with a text hint pointing at the tray icon's own Safe Exit/Force Exit for anyone who actually
// wanted to stop the server here, an extra round-trip reported live as unnecessary friction.
public enum ConfirmMinimizeToTrayResult { Cancel, MinimizeToTray, SafeExit, ForceExit }

// v0.7.73.0: previously closing the main window while any server was running silently cancelled
// the close and hid to tray with no confirmation at all (v0.7.11.0) -- reported live as surprising
// ("why is the server still running, I closed it"). Requested behavior: keep minimize-to-tray as
// the outcome, just ask first, mirroring the existing ConfirmCloseTabDialog pattern rather than
// inventing a new one.
public sealed partial class ConfirmMinimizeToTrayDialog : Window
{
    public ConfirmMinimizeToTrayDialog()
    {
        InitializeComponent();
    }

    public ConfirmMinimizeToTrayDialog(int runningServerCount) : this()
    {
        MessageText.Text = runningServerCount == 1
            ? "A Palworld server is still running. Closing this window won't stop it -- MystTiq will keep managing it in the background."
            : $"{runningServerCount} Palworld servers are still running. Closing this window won't stop them -- MystTiq will keep managing them in the background.";
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmMinimizeToTrayResult.Cancel);
    private void MinimizeToTray_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmMinimizeToTrayResult.MinimizeToTray);
    private void SafeExit_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmMinimizeToTrayResult.SafeExit);
    private void ForceExit_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmMinimizeToTrayResult.ForceExit);
}
