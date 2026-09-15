using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class PalworldSettingDto : INotifyPropertyChanged
{
    // The INI's own quoted-string convention (ServerName/ServerDescription/
    // AdminPassword/ServerPassword) -- a fixed, known-by-name list, not runtime quote-sniffing,
    // since heuristically detecting "was this already quoted" is fragile once a value's been
    // edited once in the session. Core round-trips Value byte-for-byte, quotes included; this is
    // purely a display-layer concern.
    private static readonly HashSet<string> QuotedStringNames = new(StringComparer.OrdinalIgnoreCase)
    { "ServerName", "ServerDescription", "AdminPassword", "ServerPassword" };

    private string _value = string.Empty;
    private string _originalValue = string.Empty;

    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("displayName")] public string DisplayName { get; init; } = string.Empty;
    [JsonPropertyName("category")] public string Category { get; init; } = string.Empty;
    [JsonPropertyName("value")]
    public string Value
    {
        get => _value;
        set
        {
            if (string.Equals(_value, value, StringComparison.Ordinal)) return;
            _value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayValue)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDirty)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsModifiedDisplay)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDifferentFromDefault)));
        }
    }
    [JsonIgnore]
    public string DisplayValue
    {
        get => QuotedStringNames.Contains(Name) ? Unquote(Value) : Value;
        set => Value = QuotedStringNames.Contains(Name) ? $"\"{value}\"" : value;
    }
    [JsonPropertyName("defaultValue")] public string DefaultValue { get; init; } = string.Empty;
    [JsonPropertyName("isModified")] public bool IsModified { get; init; }
    [JsonIgnore] public string Description => $"Palworld {DisplayName.ToLowerInvariant()} setting.";

    [JsonIgnore] public string OriginalValue => _originalValue;
    [JsonIgnore] public bool IsDirty => !string.Equals(Value, _originalValue, StringComparison.Ordinal);
    [JsonIgnore] public string IsModifiedDisplay => IsDirty ? "Unsaved" : IsModified ? "Non-default" : "Default";
    // Live, not the server-computed-at-load-time IsModified flag -- updates immediately as the
    // user edits Value, so Advanced Settings' highlight reflects the current session's edits too,
    // not just what was already non-default when the page loaded.
    [JsonIgnore] public bool IsDifferentFromDefault => !string.Equals(Value, DefaultValue, StringComparison.Ordinal);

    private static string Unquote(string raw) => raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"' ? raw[1..^1] : raw;

    public void MarkClean()
    {
        _originalValue = Value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDirty)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsModifiedDisplay)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class PalworldSimpleSettingItem : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public PalworldSimpleSettingItem(PalworldSettingDto setting, string title, string description,
        double minimum, double maximum, double step, string unit, string group = "")
    {
        Setting = setting;
        Title = title;
        Description = description;
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
        Unit = unit;
        Group = group;
        Setting.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(PalworldSettingDto.Value) or nameof(PalworldSettingDto.IsDirty))
            {
                Raise(nameof(SliderValue));
                Raise(nameof(ValueText));
                Raise(nameof(DefaultComparisonText));
                Raise(nameof(IsDirty));
            }
        };
    }

    public PalworldSettingDto Setting { get; }
    public string Title { get; }
    public string Description { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Step { get; }
    public string Unit { get; }
    // v0.7.38.0: which World Settings sub-section this rate belongs under (World / Player & Pal /
    // Items & Work), so the flat GAMEPLAY RATES list can render as labeled groups instead.
    public string Group { get; }
    public bool IsDirty => Setting.IsDirty;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            if (!value) Setting.Value = Setting.DefaultValue;
            Raise();
        }
    }
    public double SliderValue
    {
        get => Parse(Setting.Value, Parse(Setting.DefaultValue, Minimum));
        set
        {
            var clamped = Math.Clamp(value, Minimum, Maximum);
            var stepped = Step > 0 ? Math.Round(clamped / Step) * Step : clamped;
            Setting.Value = stepped.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
    public string ValueText => $"{SliderValue:0.##} {Unit}".TrimEnd();
    public string DefaultComparisonText
    {
        get
        {
            var baseline = Parse(Setting.DefaultValue, 0);
            return baseline > 0
                ? $"{(SliderValue / baseline) * 100:0}% of default"
                : $"Default {Setting.DefaultValue}";
        }
    }

    private static double Parse(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public event PropertyChangedEventHandler? PropertyChanged;
}

// Simple Settings' boolean-toggle counterpart to PalworldSimpleSettingItem's slider --
// same wrap-a-PalworldSettingDto shape, just exposing a bool instead of a ranged double.
public sealed class PalworldSimpleToggleItem : INotifyPropertyChanged
{
    public PalworldSimpleToggleItem(PalworldSettingDto setting, string title, string description)
    {
        Setting = setting;
        Title = title;
        Description = description;
        Setting.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(PalworldSettingDto.Value)) Raise(nameof(IsChecked)); };
    }

    public PalworldSettingDto Setting { get; }
    public string Title { get; }
    public string Description { get; }
    public bool IsChecked
    {
        get => bool.TryParse(Setting.Value, out var parsed) && parsed;
        set => Setting.Value = value ? "True" : "False";
    }

    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class PalworldConfigurationSnapshotDto
{
    [JsonPropertyName("configurationPath")] public string ConfigurationPath { get; init; } = string.Empty;
    [JsonPropertyName("defaultConfigurationPath")] public string DefaultConfigurationPath { get; init; } = string.Empty;
    [JsonPropertyName("exists")] public bool Exists { get; init; }
    [JsonPropertyName("settings")] public IReadOnlyList<PalworldSettingDto> Settings { get; init; } = [];
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}

public sealed class PalworldConfigurationSaveResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("backupPath")] public string? BackupPath { get; init; }
    [JsonPropertyName("validationErrors")] public IReadOnlyList<string> ValidationErrors { get; init; } = [];
}

public sealed class PalworldDefaultConfigurationRequestDto
{
    [JsonPropertyName("confirmCreate")] public bool ConfirmCreate { get; init; }
    [JsonPropertyName("serverName")] public string ServerName { get; init; } = string.Empty;
    [JsonPropertyName("serverDescription")] public string ServerDescription { get; init; } = string.Empty;
    [JsonPropertyName("adminPassword")] public string AdminPassword { get; init; } = string.Empty;
    [JsonPropertyName("serverPassword")] public string ServerPassword { get; init; } = string.Empty;
    [JsonPropertyName("maximumPlayers")] public int MaximumPlayers { get; init; }
    [JsonPropertyName("gamePort")] public int GamePort { get; init; }
    [JsonPropertyName("restPort")] public int RestPort { get; init; }
}

public sealed class PalworldConfigurationExportDocument
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; } = 1;
    [JsonPropertyName("exportedAt")] public DateTimeOffset ExportedAt { get; init; } = DateTimeOffset.UtcNow;
    [JsonPropertyName("sourcePath")] public string SourcePath { get; init; } = string.Empty;
    [JsonPropertyName("settings")] public IReadOnlyList<PalworldConfigurationExportSetting> Settings { get; init; } = [];
}

public sealed class PalworldConfigurationExportSetting
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("value")] public string Value { get; init; } = string.Empty;
}
