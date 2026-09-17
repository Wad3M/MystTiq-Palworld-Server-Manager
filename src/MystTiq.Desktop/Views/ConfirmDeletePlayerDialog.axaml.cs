using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MystTiq.Desktop.Views;

public enum ConfirmDeletePlayerResult { Cancel, Delete }

// v0.7.75.0: shown after a real HeadlessPlayerDeletionService.PreviewAsync call succeeds -- the
// findings list is the actual server-reported preview text (which guild reference will be cleaned
// up, exact file size, etc.), not a generic warning, matching how every other destructive
// Preview -> confirm -> Apply flow in this app surfaces real findings before asking for confirmation.
public sealed partial class ConfirmDeletePlayerDialog : Window
{
    public ConfirmDeletePlayerDialog()
    {
        InitializeComponent();
    }

    public ConfirmDeletePlayerDialog(string playerName, IReadOnlyList<string> findings) : this()
    {
        TitleText.Text = $"Delete {playerName}'s save?";
        FindingsList.ItemsSource = findings;
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmDeletePlayerResult.Cancel);
    private void Confirm_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmDeletePlayerResult.Delete);
}
