using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MystTiq.Desktop.Views;

public sealed record SelectGuildOption(string GuildId, string DisplayName)
{
    public override string ToString() => DisplayName;
}

// v0.7.76.0: a populated dropdown of real, known guilds to pick from -- built for "Transfer Base
// to Guild...", mirroring SelectPlayerDialog's own reasoning: a real dropdown instead of hand-typing
// a 32-hex guild ID the way the existing Base Ownership Transfer card still requires today.
public sealed partial class SelectGuildDialog : Window
{
    public SelectGuildDialog()
    {
        InitializeComponent();
    }

    public SelectGuildDialog(string prompt, IReadOnlyList<SelectGuildOption> options) : this()
    {
        PromptText.Text = prompt;
        GuildCombo.ItemsSource = options;
        if (options.Count > 0) GuildCombo.SelectedIndex = 0;
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void Next_OnClick(object? sender, RoutedEventArgs e)
    {
        var selected = (GuildCombo.SelectedItem as SelectGuildOption)?.GuildId;
        Close(selected);
    }
}
