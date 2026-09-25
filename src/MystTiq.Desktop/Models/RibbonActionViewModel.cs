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

    // v0.8.1.0: the user's vector Ribbon icons (see Services/RibbonIcons). Buttons without one keep their text glyph.
    // v0.8.8.0: the user's colour image icons come first, then the vector icons, then the text glyph.
    public string? ImageIconKey => MystTiq.Desktop.Services.RibbonIcons.ImageKeyFor(Label);
    public Avalonia.Media.Imaging.Bitmap? ImageIcon => MystTiq.Desktop.Services.RibbonIcons.Image(ImageIconKey);
    public bool HasImageIcon => ImageIconKey is not null;
    public string? VectorIconKey => HasImageIcon ? null : MystTiq.Desktop.Services.RibbonIcons.KeyFor(Label, Glyph);
    public Avalonia.Media.Geometry? VectorIcon => MystTiq.Desktop.Services.RibbonIcons.Geometry(VectorIconKey);
    public bool HasVectorIcon => VectorIconKey is not null;
    public bool ShowGlyph => !HasImageIcon && !HasVectorIcon;

    // v0.8.19.0: the role the button's route needs (null = any role), and whether the signed-in role has it. A button the
    // role cannot use is disabled, and its tooltip says which role it needs, instead of letting the server refuse it.
    public string? RequiredRole { get; init; }
    public bool RoleAllowed { get; init; } = true;
    public string ToolTipText => RoleAllowed || RequiredRole is null ? AutomationName : $"{AutomationName} (needs the {RequiredRole} role)";
    // v0.8.6.0: Label stays the English identity (RibbonIcons maps icons by it); this is what is shown.
    public string DisplayLabel { get; init; } = Label;
}

public sealed record RibbonGroupViewModel(string Title, IReadOnlyList<RibbonActionViewModel> Actions)
{
    // v0.8.6.0: the translated group title; Title stays English.
    public string DisplayTitle { get; init; } = Title;
}
