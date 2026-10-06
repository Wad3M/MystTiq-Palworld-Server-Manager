// MystTiq v1.0.5.0: file reviewed for this release (2026-10-06).
using Avalonia;
using Avalonia.Controls;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v1.0.0.6 (asked 2026-10-05: consistent tags, governed centrally). A status shown as a tag (READY, PASS, Up to date, Healthy,
/// Verified …) takes its colour from what it means, never from the page:
///   ok    green   ready, passing, current, verified, healthy, online
///   warn  amber   needs attention, an update is available, unverified, degraded
///   fail  red     missing, failed, broken, unreadable, misconfigured, offline when it should not be
///   off   grey    disabled, optional, self-updating, not applicable
///   info  blue    anything else (a plain state such as "Checking")
/// Each tag is one Border with Classes="tag" and services:StatusTag.Status="{Binding …}" (the English status), or, for a tag
/// that only ever says one thing (Lucky, Update available), Classes="tag" plus its kind. Before this the
/// Setup, Install and Doctor pages each drew three Borders per tag with their own brushes and showed one by visibility.
/// </summary>
public static class StatusTags
{
    public const string Ok = "ok";
    public const string Warn = "warn";
    public const string Fail = "fail";
    public const string Off = "off";
    public const string Info = "info";

    public static readonly IReadOnlyList<string> All = [Ok, Warn, Fail, Off, Info];

    // States that are neither good nor bad (checked first: "Active / Unverified" must not read as "active").
    private static readonly string[] InfoWords = ["active / unverified", "starting", "transitioning", "checking", "connecting…", "connecting"];
    private static readonly string[] OkWords = ["ready", "pass", "passed", "ok", "up to date", "verified", "healthy", "online", "installed", "present", "running", "confirmed loaded", "confirmed active", "enabled", "done", "success", "succeeded", "current", "active", "connected", "delivered"];
    private static readonly string[] WarnWords = ["attention", "warn", "warning", "update available", "unverified", "degraded", "needs review", "stale", "partial", "pending", "outdated", "behind", "check manually", "needs bearer token", "not proven"];
    private static readonly string[] FailWords = ["fail", "failed", "missing", "error", "critical", "broken", "unreadable", "misconfigured", "not found", "crashed", "crash detected", "blocked", "denied", "not loaded", "locked", "incompatible api version", "invalid profile", "failing"];
    private static readonly string[] OffWords = ["disabled", "optional", "self-updating", "n/a", "not applicable", "unknown", "unavailable", "skipped", "off", "stopped", "not installed", "not connected", "signed out", "notconfigured", "not configured"];

    /// <summary>The tag kind for an English status (case and surrounding spaces ignored; exact phrases before first words).</summary>
    public static string KindFor(string? status)
    {
        var s = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (s.Length == 0 || InfoWords.Contains(s)) return Info;
        if (OkWords.Contains(s)) return Ok;
        if (WarnWords.Contains(s)) return Warn;
        if (FailWords.Contains(s)) return Fail;
        if (OffWords.Contains(s)) return Off;
        // "Connection failed", "Operation failed", "Local bootstrap failed" ...
        if (s.EndsWith(" failed", StringComparison.Ordinal)) return Fail;
        // Longer statuses: the first matching phrase they start with.
        foreach (var (words, kind) in new[] { (FailWords, Fail), (WarnWords, Warn), (OffWords, Off), (OkWords, Ok) })
            if (words.Any(w => s.StartsWith(w + " ", StringComparison.Ordinal) || s.StartsWith(w + ":", StringComparison.Ordinal) || s.StartsWith(w + " —", StringComparison.Ordinal)))
                return kind;
        return Info;
    }

    public static void Apply(StyledElement element, string kind)
    {
        foreach (var other in All) if (other != kind) element.Classes.Remove(other);
        if (!element.Classes.Contains(kind)) element.Classes.Add(kind);
    }
}

/// <summary>services:StatusTag.Status="{Binding Status}" on a Border.tag gives it the ok/warn/fail/off/info class for that status.</summary>
public sealed class StatusTag : AvaloniaObject
{
    public static readonly AttachedProperty<string?> StatusProperty =
        AvaloniaProperty.RegisterAttached<StatusTag, StyledElement, string?>("Status");

    static StatusTag()
    {
        StatusProperty.Changed.AddClassHandler<StyledElement>((element, e) => StatusTags.Apply(element, StatusTags.KindFor(e.NewValue as string)));
    }

    public static string? GetStatus(StyledElement element) => element.GetValue(StatusProperty);
    public static void SetStatus(StyledElement element, string? value) => element.SetValue(StatusProperty, value);
}
