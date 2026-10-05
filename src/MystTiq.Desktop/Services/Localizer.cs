// MystTiq v1.0.0.1: file reviewed for this release (2026-10-04).
using System.ComponentModel;
using System.Text.Json;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace MystTiq.Desktop.Services;

public sealed record UiLanguage(string Code, string NativeName)
{
    public override string ToString() => NativeName;
}

// v0.8.5.0: the Desktop's display language. Strings live in Assets/i18n/<code>.json (flat "key": "text"),
// English is the complete reference and the fallback for any key a translation lacks, so a partial translation can
// never show a blank or a raw key where English exists. Adding a language is adding one JSON file plus one line in
// Languages below; the release gate fails if any language is missing a key English has.
//
// XAML uses {services:Tr key}, a binding to Strings[key]. Strings is replaced by a new object on every language change
// and announced as an ordinary property change, so every bound text updates at once without restarting (a bare indexer
// notification was tried first and Avalonia's bindings did not re-read it). View-model strings (page titles) read
// Localizer.Instance[key] and re-raise on LanguageChanged.
public sealed class Localizer : INotifyPropertyChanged
{
    // v0.9.0.0: the most used languages on PCs and among Palworld's players (Steam's language share; the game's own
    // language list), each shown in its own name. Everything but English is a draft awaiting native review.
    public static readonly IReadOnlyList<UiLanguage> Languages =
    [
        new("en", "English"),
        new("zh-Hans", "简体中文"),
        new("es", "Español"),
        new("pt-BR", "Português (Brasil)"),
        new("ru", "Русский"),
        new("de", "Deutsch"),
        new("fr", "Français"),
        new("ja", "日本語"),
        new("ko", "한국어"),
        new("it", "Italiano"),
        new("pl", "Polski"),
        new("tr", "Türkçe")
    ];

    // v0.9.0.0: the fonts each language is drawn with. Inter (and Bahnschrift for display text) cover the Latin and
    // Cyrillic languages; Chinese, Japanese and Korean fall back to the system's own font for that language, first the
    // Windows one, then the Linux one. Naming the language's own font (not just "any font with the character") keeps
    // Japanese from being drawn with Chinese glyph shapes, which share code points.
    public static string CjkFonts(string code) => code switch
    {
        "zh-Hans" => "Microsoft YaHei UI, Microsoft YaHei, Noto Sans CJK SC, Noto Sans SC, WenQuanYi Micro Hei",
        "ja" => "Yu Gothic UI, Meiryo UI, Meiryo, Noto Sans CJK JP, Noto Sans JP",
        "ko" => "Malgun Gothic, Noto Sans CJK KR, Noto Sans KR",
        _ => string.Empty
    };

    public static string UiFontFamily(string code) => Join("Inter", CjkFonts(code), "Segoe UI");
    public static string DisplayFontFamily(string code) => Join("Bahnschrift, Segoe UI Variable Display", CjkFonts(code), "Inter");
    public static string DisplayTextFontFamily(string code) => Join("Bahnschrift, Segoe UI Variable Text", CjkFonts(code), "Inter");
    private static string Join(params string[] parts) => string.Join(", ", parts.Where(p => p.Length > 0));

    // The styles read these resources (DesignSystem.axaml); set on every language change.
    private static void ApplyFonts(string code)
    {
        if (Avalonia.Application.Current is not { } app) return;
        app.Resources["UiFontFamily"] = new Avalonia.Media.FontFamily(UiFontFamily(code));
        app.Resources["UiDisplayFontFamily"] = new Avalonia.Media.FontFamily(DisplayFontFamily(code));
        app.Resources["UiDisplayTextFontFamily"] = new Avalonia.Media.FontFamily(DisplayTextFontFamily(code));
    }

    public static Localizer Instance { get; } = new();

    private Dictionary<string, string> english = new(StringComparer.Ordinal);
    private Dictionary<string, string> current = new(StringComparer.Ordinal);

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public string LanguageCode { get; private set; } = "en";

    // What XAML binds to; a new instance per language so the binding engine sees a changed value.
    public LocalizedStrings Strings { get; private set; } = new(k => k);

    private Localizer()
    {
        english = LoadAsset("en");
        current = english;
        Messages = MessageCatalog.Build(english, current);
        Strings = new LocalizedStrings(k => this[k]);
    }

    // Missing in the chosen language: English. Missing in English too: the key itself, which the gate prevents.
    public string this[string key] => Lookup(current, english, key);

    // v0.9.1.0: status and error messages built in code (view-model text) in the chosen language; see MessageCatalog.
    public MessageCatalog Messages { get; private set; }

    public string Translate(string? text) => Messages.Translate(text);

    /// <summary>Shorthand for code that sets text directly (dialogs, file-picker titles): the English text translated.</summary>
    public static string T(string? text) => Instance.Translate(text);

    public static string Lookup(IReadOnlyDictionary<string, string> chosen, IReadOnlyDictionary<string, string> fallback, string key) =>
        chosen.TryGetValue(key, out var text) && !string.IsNullOrEmpty(text) ? text
        : fallback.TryGetValue(key, out var english) && !string.IsNullOrEmpty(english) ? english
        : key;

    public void SetLanguage(string? code)
    {
        var language = Languages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) ?? Languages[0];
        ApplyFonts(language.Code);
        if (language.Code == LanguageCode && current.Count > 0) return;
        current = language.Code == "en" ? english : LoadAsset(language.Code);
        LanguageCode = language.Code;
        // v0.9.9.0: numbers, dates and times follow the chosen language (formats only; see DisplayCulture).
        DisplayCulture.Apply(language.Code);
        Messages = MessageCatalog.Build(english, current);
        Strings = new LocalizedStrings(k => this[k]);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Strings)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageCode)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public static Dictionary<string, string> Parse(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json) is { } map
            ? new Dictionary<string, string>(map, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

    private static Dictionary<string, string> LoadAsset(string code)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://MystTiq.Desktop/Assets/i18n/{code}.json"));
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException or FileNotFoundException)
        {
            // A missing or broken translation file must not stop the app: everything falls back to English.
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}

public sealed class LocalizedStrings(Func<string, string> lookup)
{
    public string this[string key] => lookup(key);
}

/// <summary>{services:Tr key}: a live binding to the translated text, updated when the language changes.</summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension(string key) => Key = key;
    public string Key { get; }
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"{nameof(Localizer.Strings)}[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay };
}

/// <summary>
/// v0.8.7.0: {services:TrFormat key, Path=Property}: a translated template such as "Stored: {0}" filled with a bound
/// value, live in both (the language and the value can each change). Replaces StringFormat='...' in XAML, whose
/// template cannot be bound. A template that does not parse shows the value alone rather than failing.
/// </summary>
public sealed class TrFormatExtension : MarkupExtension
{
    public TrFormatExtension(string key) => Key = key;
    public string Key { get; }
    public string Path { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => new MultiBinding
    {
        Bindings =
        {
            new Binding($"{nameof(Localizer.Strings)}[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay },
            new Binding(Path) { Mode = BindingMode.OneWay }
        },
        Converter = TemplateConverter.Instance
    };

    public static string Fill(string? template, object? value)
    {
        // v0.9.4.0: a text value is shown in the language too, like {services:TrText} ("Health: Healthy" → "状態：正常").
        var text = value is string s ? Localizer.T(s) : value?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(template)) return text;
        try { return string.Format(System.Globalization.CultureInfo.CurrentCulture, template, text); }
        catch (FormatException) { return text; }
    }

    private sealed class TemplateConverter : Avalonia.Data.Converters.IMultiValueConverter
    {
        public static readonly TemplateConverter Instance = new();
        public object? Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            Fill(values.Count > 0 ? values[0] as string : null,
                values.Count > 1 && values[1] is not Avalonia.Data.BindingNotification && !ReferenceEquals(values[1], Avalonia.AvaloniaProperty.UnsetValue) ? values[1] : null);
    }
}

/// <summary>
/// v0.9.1.0: {services:TrText Path}: a view-model text shown in the chosen language (Localizer.Translate), live in both
/// the value and the language. For display only (TextBlock, Run, button content, tips, headers); never an editable
/// field, whose text must reach the view model unchanged. A value that is not text passes through untouched.
/// </summary>
public sealed class TrTextExtension : MarkupExtension
{
    public TrTextExtension() { }
    public TrTextExtension(string path) => Path = path;
    public string Path { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => new MultiBinding
    {
        Bindings =
        {
            new Binding(Path) { Mode = BindingMode.OneWay },
            new Binding(nameof(Localizer.Strings)) { Source = Localizer.Instance, Mode = BindingMode.OneWay }
        },
        Converter = TextConverter.Instance
    };

    internal sealed class TextConverter : Avalonia.Data.Converters.IMultiValueConverter
    {
        public static readonly TextConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            var value = values.Count > 0 ? values[0] : null;
            if (value is Avalonia.Data.BindingNotification || ReferenceEquals(value, Avalonia.AvaloniaProperty.UnsetValue)) return Avalonia.AvaloniaProperty.UnsetValue;
            return value switch
            {
                null => null,
                string text => Localizer.Instance.Translate(text),
                _ when targetType == typeof(string) => System.Convert.ToString(value, culture),
                _ => value
            };
        }
    }
}

// The chosen language, a Desktop-local preference beside the other local preference files.
public sealed class LocalLanguagePreferenceStore
{
    public LocalLanguagePreferenceStore(string? storagePath = null) =>
        StoragePath = storagePath ?? Path.Combine(LocalMapPreferencesStore.GetLocalConfigRoot(), "MystTiq", "language.json");

    public string StoragePath { get; }

    public string? Load()
    {
        try
        {
            if (!File.Exists(StoragePath)) return null;
            return JsonSerializer.Deserialize<LanguagePreference>(File.ReadAllText(StoragePath))?.Language;
        }
        catch { return null; }
    }

    public void Save(string code)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
            var temp = StoragePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new LanguagePreference(code)));
            File.Move(temp, StoragePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* the choice still applies for this run */ }
    }

    private sealed record LanguagePreference(string Language);
}
