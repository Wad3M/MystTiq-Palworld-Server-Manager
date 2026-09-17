using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MystTiq.Desktop.Views;

// v0.7.76.0: one reusable findings-list confirm dialog for the Base/Guild right-click workflow
// (transfer, wipe, guild ownership operations) -- deliberately generic rather than one bespoke
// dialog per operation type, since all of them show the same shape (title, real server-reported
// findings, a safety-backup notice, Cancel/Confirm) and only the title/button label/danger-styling
// actually differ per call site.
public sealed partial class ConfirmOperationDialog : Window
{
    public ConfirmOperationDialog()
    {
        InitializeComponent();
    }

    public ConfirmOperationDialog(string title, IReadOnlyList<string> findings, string confirmLabel, bool danger) : this()
    {
        TitleText.Text = title;
        FindingsList.ItemsSource = findings;
        ConfirmButton.Content = confirmLabel;
        ConfirmButton.Classes.Add(danger ? "danger" : "primary");
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(false);
    private void Confirm_OnClick(object? sender, RoutedEventArgs e) => Close(true);
}
