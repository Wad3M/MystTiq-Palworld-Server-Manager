// MystTiq v1.0.2.0: file reviewed for this release (2026-10-05).
using Avalonia.Controls;
using Avalonia.Interactivity;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.Views;

public enum ConfirmCopyPlayerResult { Cancel, Copy }

public sealed partial class ConfirmCopyPlayerDialog : Window
{
    public ConfirmCopyPlayerDialog()
    {
        InitializeComponent();
    }

    public ConfirmCopyPlayerDialog(string sourceName, string destinationName, IReadOnlyList<string> findings) : this()
    {
        TitleText.Text = Localizer.T($"Copy {sourceName}'s data onto {destinationName}?");
        FindingsList.ItemsSource = findings;
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmCopyPlayerResult.Cancel);
    private void Confirm_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmCopyPlayerResult.Copy);
}
