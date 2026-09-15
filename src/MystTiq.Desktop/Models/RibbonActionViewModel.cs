using System.Windows.Input;

namespace MystTiq.Desktop.Models;

public enum RibbonIconColor { Amber, Green, Blue, Red, Cyan }

// Data-bound replacement for what used to be hand-authored per-button XAML (see this release's
// architecture doc for the full rationale). The icon color is expressed as an enum rather than a
// resolved brush so each color still routes through the theme system via Classes.amber/.green/etc.
// + DynamicResource in Styles/DesignSystem.axaml, exactly like every other themed color in the app,
// instead of freezing a brush at construction time.
// NativeDialogAction: some actions (import/export a file) need a real file picker, which this
// codebase's own established convention keeps in code-behind rather than the ViewModel (it needs a
// TopLevel/Window to open against -- see MainWindow.axaml.cs's existing ImportPalworldConfiguration_Click/
// ExportPalworldConfiguration_Click/ExportConsole_Click). Command is left null for these; the shared
// ribbon button template's Click handler (RibbonAction_OnClick) dispatches by this string instead.
public sealed record RibbonActionViewModel(
    string Glyph,
    string Label,
    string AutomationName,
    ICommand? Command,
    object? CommandParameter,
    RibbonIconColor IconColor,
    bool IsSuccessButton = false,
    bool IsDangerButton = false,
    string? NativeDialogAction = null)
{
    public bool IsAmberIcon => IconColor == RibbonIconColor.Amber;
    public bool IsGreenIcon => IconColor == RibbonIconColor.Green;
    public bool IsBlueIcon => IconColor == RibbonIconColor.Blue;
    public bool IsRedIcon => IconColor == RibbonIconColor.Red;
    public bool IsCyanIcon => IconColor == RibbonIconColor.Cyan;
}

public sealed record RibbonGroupViewModel(string Title, IReadOnlyList<RibbonActionViewModel> Actions);
