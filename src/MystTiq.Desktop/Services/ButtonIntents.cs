// MystTiq v1.0.0.6: file reviewed for this release (2026-10-05).
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v1.0.0.6 (asked 2026-10-05: "i would like to have consistency with all the buttons and tags. they should all be governed
/// by the central look and not hardcoded ... Delete to be red, open to be purple, verify to be green, other colours for
/// other buttons"). Every button has exactly one intent, and the intent alone decides its look (Styles/DesignSystem.axaml):
///   danger  red    destroys, removes or forces            Delete, Remove, Revoke, Kick, Ban, Force Stop, Discard
///   open    purple goes somewhere or shows something      Open, Browse, Show on map, Open guild, Choose ZIP
///   verify  green  checks without changing                Verify, Recheck, Scan, Test, Validate, Analyze, Doctor
///   apply   blue   makes the change asked for             Save, Apply, Create, Add, Install, Send, Connect, Update
///   info    teal   reads again, previews or copies out    Refresh, Preview, Load, Export, Copy, Fetch, Discover
///   caution amber  undoes, resets or pauses               Restore, Reset, Restart, Repair, Pause, Mute, Release
///   plain   neutral moves through a flow or backs out     Cancel, Back, Next, Dismiss, Sign Out
/// A button's XAML carries its intent as a class; the ArtworkHarness checks every one against this table (by the English
/// label), and a button whose label changes at runtime takes its intent from the same table (ButtonIntent.Label).
/// </summary>
public static class ButtonIntents
{
    public const string Danger = "danger";
    public const string Open = "open";
    public const string Verify = "verify";
    public const string Apply = "apply";
    public const string Info = "info";
    public const string Caution = "caution";
    public const string Plain = "plain";

    public static readonly IReadOnlyList<string> All = [Danger, Open, Verify, Apply, Info, Caution, Plain];

    // Classes that shape a button rather than say what it does (the ribbon, window chrome, list rows, map markers, toggles).
    public static readonly IReadOnlyList<string> Structural = ["ribbon", "ghost", "sectionToggle", "palMarker", "marker", "listItem"];

    // First match wins: specific phrases before single verbs.
    private static readonly (Regex Pattern, string Intent)[] Rules =
    [
        (R(@"^(apply with fresh safety backup|apply migration|apply exact preview)"), Danger),
        (R(@"^(delete|remove|revoke|kick|ban|temp ban|force|discard|forget|wipe|clear persistent|stop|■ disconnect|disconnect|cleanup)"), Danger),
        (R(@"^(copy data|choose zip)"), Apply),
        (R(@"^(open|browse|show on|show files|look up|choose|manage|view|go to)"), Open),
        (R(@"^(verify|rescan|recheck|validate|validator|scan|↻ scan|test|check|diagnose|analyze|⚕|doctor|run reachability|run doctor|refresh capture|find the)"), Verify),
        (R(@"^(reset|restore|restart|repair|release|pause|mute|lift|leave running|clear background|enable / disable|toggle|pin / unpin)"), Caution),
        (R(@"^(refresh|↻|preview|load|export|copy id|copy |fetch|discover|auto-detect|latest|trending|🎲|set source)"), Info),
        (R(@"^(save|apply|create|\+ create|add|install|📦|reserve|send|➤|give|import|use it|connect|● connect|sign in|allow|enable|new|download|bootstrap|capture|teleport|fix|clone|prepare|resume|unmute|unban|update|backup|run now)"), Apply),
        (R(@"^(cancel|← back|back|next|continue|dismiss|advanced settings|simple settings|sign out)"), Plain),
    ];

    private static Regex R(string pattern) => new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>The intent of a button from its English label; Plain when no rule matches.</summary>
    public static string For(string? englishLabel)
    {
        var label = (englishLabel ?? string.Empty).Trim();
        foreach (var (pattern, intent) in Rules)
            if (pattern.IsMatch(label)) return intent;
        return Plain;
    }

    /// <summary>Puts exactly one intent class on the control (removing any other intent class).</summary>
    public static void Set(StyledElement element, string intent)
    {
        foreach (var other in All) if (other != intent) element.Classes.Remove(other);
        if (!element.Classes.Contains(intent)) element.Classes.Add(intent);
    }
}

/// <summary>For a button whose label changes at runtime: services:ButtonIntent.Label="{Binding Action}" gives it the intent of
/// that (English) label, from the same table as every other button.</summary>
public sealed class ButtonIntent : AvaloniaObject
{
    public static readonly AttachedProperty<string?> LabelProperty =
        AvaloniaProperty.RegisterAttached<ButtonIntent, StyledElement, string?>("Label");

    static ButtonIntent()
    {
        LabelProperty.Changed.AddClassHandler<StyledElement>((element, e) => ButtonIntents.Set(element, ButtonIntents.For(e.NewValue as string)));
    }

    public static string? GetLabel(StyledElement element) => element.GetValue(LabelProperty);
    public static void SetLabel(StyledElement element, string? value) => element.SetValue(LabelProperty, value);
}
