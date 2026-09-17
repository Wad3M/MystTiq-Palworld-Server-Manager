using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MystTiq.Desktop.Views;

public sealed record SelectPlayerOption(string PlayerId, string DisplayName)
{
    public override string ToString() => DisplayName;
}

// v0.7.75.0: a populated dropdown of real, known players to pick from -- built for "Copy Player
// Data From...", but written generically enough (prompt text + option list in, selected id or
// null out) to reuse anywhere else a "pick one other player" step is needed, rather than
// hand-typing a 32-hex player ID the way the existing Base/Guild ownership cards still do today.
public sealed partial class SelectPlayerDialog : Window
{
    private string? selectedPlayerId;

    public SelectPlayerDialog()
    {
        InitializeComponent();
    }

    public SelectPlayerDialog(string prompt, IReadOnlyList<SelectPlayerOption> options) : this()
    {
        PromptText.Text = prompt;
        PlayerCombo.ItemsSource = options;
        if (options.Count > 0) PlayerCombo.SelectedIndex = 0;
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void Next_OnClick(object? sender, RoutedEventArgs e)
    {
        selectedPlayerId = (PlayerCombo.SelectedItem as SelectPlayerOption)?.PlayerId;
        Close(selectedPlayerId);
    }
}
