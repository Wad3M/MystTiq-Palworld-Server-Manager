// MystTiq v0.9.1.0: file reviewed for this release (2026-09-28).
using Avalonia.Controls;
using Avalonia.Interactivity;
using MystTiq.Desktop.Services;

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
        TitleText.Text = Localizer.T(title);
        FindingsList.ItemsSource = findings.Select(Localizer.T).ToList();
        ConfirmButton.Content = Localizer.T(confirmLabel);
        ConfirmButton.Classes.Add(danger ? "danger" : "primary");
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(false);
    private void Confirm_OnClick(object? sender, RoutedEventArgs e) => Close(true);
}
