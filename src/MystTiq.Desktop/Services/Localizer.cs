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
    public static readonly IReadOnlyList<UiLanguage> Languages =
    [
        new("en", "English"),
        new("de", "Deutsch"),
        new("es", "Español")
    ];

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
        Strings = new LocalizedStrings(k => this[k]);
    }

    // Missing in the chosen language: English. Missing in English too: the key itself, which the gate prevents.
    public string this[string key] => Lookup(current, english, key);

    public static string Lookup(IReadOnlyDictionary<string, string> chosen, IReadOnlyDictionary<string, string> fallback, string key) =>
        chosen.TryGetValue(key, out var text) && !string.IsNullOrEmpty(text) ? text
        : fallback.TryGetValue(key, out var english) && !string.IsNullOrEmpty(english) ? english
        : key;

    public void SetLanguage(string? code)
    {
        var language = Languages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) ?? Languages[0];
        if (language.Code == LanguageCode && current.Count > 0) return;
        current = language.Code == "en" ? english : LoadAsset(language.Code);
        LanguageCode = language.Code;
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
        var text = value?.ToString() ?? string.Empty;
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
