// MystTiq v1.0.1.0: file reviewed for this release (2026-10-05).
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using MystTiq.Desktop.ViewModels;

namespace MystTiq.Desktop.Services;

// v0.9.4.0: a button or menu item whose command the signed-in role may not use says which role it needs, in its tooltip
// (shown even while it is disabled) and in its accessible help text, instead of only being greyed out. It applies to
// every Button and MenuItem bound to a role-gated command (MainWindowViewModel.CommandRoles.cs); the Ribbon keeps its
// own "(needs the … role)" tooltip. The control's own tooltip, if any, follows the hint and comes back when the role allows
// the command again. Local, token-less use is never blocked, so nothing changes there.
public static class RoleHint
{
    // The hint set on a control, undone when the role allows its command again (Animation priority sits above the
    // control's own local value or binding, so disposing it restores that untouched).
    private static readonly AttachedProperty<Applied?> AppliedProperty =
        AvaloniaProperty.RegisterAttached<Control, Control, Applied?>("RoleHintApplied");

    private static readonly List<WeakReference<Control>> Gated = [];
    private static bool installed;

    public static void Install()
    {
        if (installed) return;
        installed = true;
        Button.CommandProperty.Changed.AddClassHandler<Button>((b, _) => Update(b));
        MenuItem.CommandProperty.Changed.AddClassHandler<MenuItem>((m, _) => Update(m));
        InputElement.IsEffectivelyEnabledProperty.Changed.AddClassHandler<Button>((b, _) => Update(b));
        InputElement.IsEffectivelyEnabledProperty.Changed.AddClassHandler<MenuItem>((m, _) => Update(m));
        Localizer.Instance.LanguageChanged += (_, _) => RefreshAll();
    }

    public static string Text(string requiredRole, string? signedInRole) => Localizer.T(signedInRole is null
        ? $"Needs the {requiredRole} role."
        : $"Needs the {requiredRole} role. You are signed in as {signedInRole}.");

    // The signed-in role or the language changed: every control bound to a gated command is looked at again.
    public static void RefreshAll()
    {
        lock (Gated)
        {
            Gated.RemoveAll(r => !r.TryGetTarget(out _));
            foreach (var reference in Gated.ToArray())
                if (reference.TryGetTarget(out var control)) Update(control);
        }
    }

    private static void Update(Control control)
    {
        if (control.Classes.Contains("ribbon")) return;
        var command = (control switch { Button b => b.Command, MenuItem m => m.Command, _ => null }) as IRoleGatedCommand;
        control.GetValue(AppliedProperty)?.Dispose();
        control.SetValue(AppliedProperty, null);
        if (command?.RequiredRole is not { } required || command.RoleGate is not { } allowed) return;
        Remember(control);
        if (allowed()) return;

        var hint = Text(required, command.SignedInRole?.Invoke());
        var own = ToolTip.GetTip(control) as string;
        var tip = string.IsNullOrWhiteSpace(own) ? hint : hint + "\n" + own;
        ToolTip.SetShowOnDisabled(control, true);
        control.SetValue(AppliedProperty, new Applied(
            control.SetValue(ToolTip.TipProperty, tip, BindingPriority.Animation),
            control.SetValue(AutomationProperties.HelpTextProperty, hint, BindingPriority.Animation)));
    }

    private static void Remember(Control control)
    {
        lock (Gated)
        {
            foreach (var reference in Gated)
                if (reference.TryGetTarget(out var known) && ReferenceEquals(known, control)) return;
            Gated.Add(new WeakReference<Control>(control));
        }
    }

    private sealed class Applied(IDisposable? tip, IDisposable? help) : IDisposable
    {
        public void Dispose()
        {
            tip?.Dispose();
            help?.Dispose();
        }
    }
}
