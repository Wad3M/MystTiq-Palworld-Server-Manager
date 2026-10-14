// MystTiq v1.1.0.2: file reviewed for this release (2026-10-13).
using System.Text.RegularExpressions;

namespace MystTiq.Core.Services;

/// <summary>
/// v1.0.7.4 (roadmap Q-9): what MystTiq writes to its logs, alerts and exports must not carry a secret. Found: a failed
/// webhook or Discord send logged the whole URL (a Discord webhook URL is its own password), and a server start wrote its
/// arguments verbatim, so a password-style argument would land in the console log and any export of it.
/// </summary>
public static partial class SecretRedaction
{
    public const string Mask = "****";

    /// <summary>A URL with only its scheme, host and port left: "https://discord.com/****".</summary>
    public static string Url(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return Mask;
        var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port;
        return $"{uri.Scheme}://{uri.Host}{port}/{Mask}";
    }

    /// <summary>Launch arguments with the value of anything that looks like a password, token, secret or key hidden.</summary>
    public static IReadOnlyList<string> Arguments(IEnumerable<string> arguments)
    {
        var result = new List<string>();
        var hideNext = false;
        foreach (var argument in arguments)
        {
            if (hideNext) { result.Add(Mask); hideNext = false; continue; }
            var match = SecretArgument().Match(argument);
            if (match.Success)
            {
                if (match.Groups["value"].Success) result.Add(match.Groups["key"].Value + match.Groups["sep"].Value + Mask);
                else { result.Add(argument); hideNext = true; }
                continue;
            }
            result.Add(argument);
        }
        return result;
    }

    /// <summary>Text with every known secret value replaced (values shorter than 4 characters are left alone).</summary>
    public static string Text(string? text, IEnumerable<string?> secrets)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        foreach (var secret in secrets.Where(s => !string.IsNullOrEmpty(s) && s!.Length >= 4).OrderByDescending(s => s!.Length))
            text = text.Replace(secret!, Mask, StringComparison.Ordinal);
        return text;
    }

    // "-AdminPassword=x", "--token x", "RCONPassword=x", "-apikey:x" (the key, then = or :, then the value; or a flag alone,
    // whose value is the next argument). MystTiq's own @mysttiq: options carry no secret and are left alone.
    [GeneratedRegex(@"^(?<key>-{0,2}(?!@mysttiq:)[A-Za-z0-9_.]*(?:pass|password|passwd|pwd|token|secret|apikey|api_key|key)[A-Za-z0-9_.]*)(?:(?<sep>[=:])(?<value>.*))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretArgument();
}
