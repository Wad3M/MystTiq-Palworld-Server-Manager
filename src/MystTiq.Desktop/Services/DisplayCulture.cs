// MystTiq v1.0.0.1: file reviewed for this release (2026-10-04).
using System.Globalization;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v0.9.9.0: numbers, dates and times follow the language chosen in MystTiq. Before, they followed the operating
/// system's language whatever was chosen (German decimals in an English window, or English ones in a German window).
///
/// Only the formats change. The culture used is the system's own with the chosen language's number and date formats put
/// in: how text is compared and upper- or lower-cased stays as it was, because switching those (Turkish has its own rules
/// for "i") can break comparisons of names and ids. English keeps the system's own formats, as before.
/// </summary>
public static class DisplayCulture
{
    // The system's culture when the app started, before any language was applied.
    private static readonly CultureInfo Original = CultureInfo.CurrentCulture;

    private static readonly Dictionary<string, string> Cultures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zh-Hans"] = "zh-CN", ["es"] = "es-ES", ["pt-BR"] = "pt-BR", ["ru"] = "ru-RU", ["de"] = "de-DE", ["fr"] = "fr-FR",
        ["ja"] = "ja-JP", ["ko"] = "ko-KR", ["it"] = "it-IT", ["pl"] = "pl-PL", ["tr"] = "tr-TR",
    };

    /// <summary>The culture for a language: <paramref name="original"/> itself for English or an unknown code.</summary>
    public static CultureInfo Build(string languageCode, CultureInfo original)
    {
        if (!Cultures.TryGetValue(languageCode, out var name)) return original;
        try
        {
            var formats = CultureInfo.GetCultureInfo(name);
            var culture = (CultureInfo)original.Clone();
            culture.NumberFormat = (NumberFormatInfo)formats.NumberFormat.Clone();
            culture.DateTimeFormat = (DateTimeFormatInfo)formats.DateTimeFormat.Clone();
            return culture;
        }
        catch (CultureNotFoundException) { return original; }
    }

    public static void Apply(string languageCode)
    {
        var culture = Build(languageCode, Original);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
    }
}