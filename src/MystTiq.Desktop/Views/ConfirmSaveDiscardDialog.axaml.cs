using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MystTiq.Desktop.Views;

public enum ConfirmSaveDiscardResult { Cancel, Discard, Save }

// v0.7.87.0: shown when navigating away from Configuration with unsaved Palworld setting changes,
// instead of silently discarding them (the previous behavior) -- the user picks, mirroring
// ConfirmCloseTabDialog's own Cancel/alternative/primary shape.
public sealed partial class ConfirmSaveDiscardDialog : Window
{
    public ConfirmSaveDiscardDialog()
    {
        InitializeComponent();
    }

    public ConfirmSaveDiscardDialog(string dirtySummary) : this()
    {
        MessageText.Text = $"{dirtySummary}. Save them before leaving Configuration, or discard them?";
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmSaveDiscardResult.Cancel);
    private void Discard_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmSaveDiscardResult.Discard);
    private void Save_OnClick(object? sender, RoutedEventArgs e) => Close(ConfirmSaveDiscardResult.Save);
}
