// MystTiq v1.1.0.1: file reviewed for this release (2026-10-13).
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;
using MystTiq.Core.Services;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Windows.Input;

namespace MystTiq.Desktop.ViewModels;

/// <summary>
/// v1.0.8.4 (roadmap C-4): the Configuration page -- the Palworld settings editor (simple and advanced views, categories,
/// search, validation, presets, import and export) and the MystTiq service settings, with the Launcher's start options --
/// moved out of MainWindowViewModel unchanged. The window shows it as vm.Configuration and keeps the unsaved-changes guard
/// on navigation.
/// </summary>
public sealed class ConfigurationViewModel : PageViewModel
{
    /// <summary>Raised when the service settings were read (the Workspace page checks its paths again).</summary>
    public event Action? ConfigurationLoaded;

    private Action<string> SetWorkspaceState { get; }

    public ConfigurationViewModel(SessionContext session, Action<string> setWorkspaceState) : base(session, NavigationPage.Configuration)
    {
        SetWorkspaceState = setWorkspaceState;
        LoadConfigurationCommand = new AsyncCommand(LoadConfigurationAsync, () => !Session.IsBusy);
        SaveConfigurationCommand = new AsyncCommand(SaveConfigurationAsync, () => !Session.IsBusy && ConfigLoaded);
        SavePalworldConfigurationCommand = new AsyncCommand(SavePalworldConfigurationAsync, () => !Session.IsBusy && PalworldConfigLoaded && PalworldConfigIsDirty && !PalworldConfigHasValidationErrors);
        ApplyStarterPresetCommand = new AsyncCommand(ApplyStarterPresetAsync, () => !Session.IsBusy);
        ShowSimpleConfigCommand = new RelayCommand(() => SetConfigurationView(true));
        ShowAdvancedConfigCommand = new RelayCommand(() => SetConfigurationView(false));
        ResetConfigChangesCommand = new RelayCommand(ResetPalworldConfigurationChanges, () => PalworldConfigLoaded && PalworldConfigIsDirty && !Session.IsBusy);
        SaveCurrentAsPresetCommand = new RelayCommand(SaveCurrentAsPreset, () => PalworldConfigLoaded && !Session.IsBusy && !string.IsNullOrWhiteSpace(NewConfigPresetName));
    }

    private int _startupTimeoutSeconds = 90;

    private int _stopTimeoutSeconds = 30;

    private int _servicePollSeconds = 5;

    private int _recoveryBackoffSeconds = 10;

    private int _maximumRecoveryAttempts = 5;

    private int _recoveryWindowSeconds = 300;

    private string _selectedConfigCategory = "All Categories";

    // v0.7.96.0: on by default. The old image mapping assumed the pre-expansion frame; the
    // conversion is now calibrated to the bundled expanded map (see PalworldMapCoordinates). It
    // only ever applies while the Palpagos map is the background (Map.IsPalpagosMapActive).
    private readonly LocalConfigPresetStore _configPresetStore = new();

    private bool _configLoaded;

    private bool _configApiEnabled;

    private string _configBindAddress = "127.0.0.1";

    private int _configPort = 8213;

    private string _configServerRoot = string.Empty;

    private string _configSteamCmdPath = string.Empty;

    private string _configBackupRoot = string.Empty;

    private string _configRuntimeRoot = string.Empty;

    private string _configLaunchArguments = string.Empty;

    private string _configSecurityText = "Not loaded";

    private string _configState = "Not loaded";

    private string _palworldConfigState = "Not loaded";

    private string _palworldConfigPath = "Not loaded";

    private bool _palworldConfigLoaded;

    private bool _isConfigSimpleView = true;

    private string _configSearchText = string.Empty;

    private string _selectedConfigPreset = "Balanced QoL";

    private IReadOnlyList<string> _palworldConfigPresets = ["Official / Vanilla", "Balanced QoL", "Relaxed QoL", "Custom"];

    private string _newConfigPresetName = string.Empty;

    private string _palworldConfigDirtyText = "No unsaved changes";

    private string _palworldConfigValidationText = "Load the active configuration to validate settings.";

    private bool _palworldConfigHasValidationErrors;

    public ObservableCollection<PalworldSettingDto> PalworldSettings { get; } = [];

    public ObservableCollection<PalworldSettingDto> FilteredPalworldSettings { get; } = [];

    // v0.7.38.0: was one flat SimplePalworldSettings collection; split into the three World
    // Settings sub-sections (World / Player & Pal / Items & Work) so the page can render labeled
    // groups instead of one long list.
    public ObservableCollection<PalworldSimpleSettingItem> SimpleWorldRateSettings { get; } = [];

    public ObservableCollection<PalworldSimpleSettingItem> SimplePlayerPalRateSettings { get; } = [];

    public ObservableCollection<PalworldSimpleSettingItem> SimpleItemsWorkRateSettings { get; } = [];

    public ObservableCollection<PalworldSimpleToggleItem> SimpleToggleSettings { get; } = [];

    public ObservableCollection<PalworldSettingDto> SimpleNetworkSettings { get; } = [];

    public ObservableCollection<string> PalworldConfigCategories { get; } = ["All Categories"];

    public IReadOnlyList<string> PalworldConfigPresets { get => _palworldConfigPresets; private set => SetField(ref _palworldConfigPresets, value); }

    public bool ConfigLoaded { get => _configLoaded; private set => SetField(ref _configLoaded, value); }

    public bool ConfigApiEnabled { get => _configApiEnabled; set => SetField(ref _configApiEnabled, value); }

    public string ConfigBindAddress { get => _configBindAddress; set => SetField(ref _configBindAddress, value); }

    public int ConfigPort { get => _configPort; set => SetField(ref _configPort, value); }

    public string ConfigServerRoot { get => _configServerRoot; set { if (SetField(ref _configServerRoot, value)) LauncherConfigurationChanged(); } }

    public string ConfigSteamCmdPath { get => _configSteamCmdPath; set => SetField(ref _configSteamCmdPath, value); }

    public string ConfigBackupRoot { get => _configBackupRoot; set => SetField(ref _configBackupRoot, value); }

    public string ConfigRuntimeRoot { get => _configRuntimeRoot; set => SetField(ref _configRuntimeRoot, value); }

    public string ConfigLaunchArguments { get => _configLaunchArguments; set => SetField(ref _configLaunchArguments, value); }

    public string ConfigSecurityText { get => _configSecurityText; private set => SetField(ref _configSecurityText, value); }

    public string ConfigState { get => _configState; private set => SetField(ref _configState, value); }

    public ICommand LoadConfigurationCommand { get; }

    public ICommand SaveConfigurationCommand { get; }

    public ICommand SavePalworldConfigurationCommand { get; }

    public ICommand ApplyStarterPresetCommand { get; }

    public ICommand ShowSimpleConfigCommand { get; }

    public ICommand ShowAdvancedConfigCommand { get; }

    public string PalworldConfigState { get => _palworldConfigState; internal set => SetField(ref _palworldConfigState, value); }

    public string PalworldConfigPath { get => _palworldConfigPath; private set => SetField(ref _palworldConfigPath, value); }

    public bool PalworldConfigLoaded
    {
        get => _palworldConfigLoaded;
        private set
        {
            if (!SetField(ref _palworldConfigLoaded, value)) return;
            (SavePalworldConfigurationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ResetConfigChangesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveCurrentAsPresetCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool IsConfigSimpleView => _isConfigSimpleView;

    public string NewConfigPresetName
    {
        get => _newConfigPresetName;
        set { if (SetField(ref _newConfigPresetName, value ?? string.Empty)) (SaveCurrentAsPresetCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public string ConfigSearchText
    {
        get => _configSearchText;
        set { if (SetField(ref _configSearchText, value)) ApplyPalworldConfigurationFilter(); }
    }

    public string SelectedConfigPreset
    {
        get => _selectedConfigPreset;
        set
        {
            if (!SetField(ref _selectedConfigPreset, value ?? "Custom")) return;
            // "Custom" means "current values don't match any known preset" -- there's nothing to
            // apply for it. DetectAndSyncConfigPreset() bypasses this setter entirely (direct field
            // assignment) specifically so that reverse-detected selection never re-triggers this.
            if (PalworldConfigLoaded && !Session.IsBusy && !string.Equals(_selectedConfigPreset, "Custom", StringComparison.Ordinal))
                ApplySelectedConfigurationPreset();
        }
    }

    public string PalworldConfigDirtyText { get => _palworldConfigDirtyText; private set => SetField(ref _palworldConfigDirtyText, value); }

    public string PalworldConfigValidationText { get => _palworldConfigValidationText; private set => SetField(ref _palworldConfigValidationText, value); }

    public bool PalworldConfigHasValidationErrors { get => _palworldConfigHasValidationErrors; private set => SetField(ref _palworldConfigHasValidationErrors, value); }

    public bool PalworldConfigIsDirty => PalworldSettings.Any(x => x.IsDirty);

    private static readonly HashSet<string> SimpleConfigurationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ServerName", "ServerDescription", "ServerPassword", "AdminPassword", "PublicPort", "ServerPlayerMaxNum",
        "RESTAPIEnabled", "RESTAPIPort", "RCONEnabled", "RCONPort",
        "DayTimeSpeedRate", "NightTimeSpeedRate", "ExpRate", "PalCaptureRate", "PalSpawnNumRate",
        "PlayerDamageRateAttack", "PlayerDamageRateDefense", "PlayerStomachDecreaceRate", "PlayerStaminaDecreaceRate",
        "PlayerAutoHPRegeneRate", "PalStomachDecreaceRate", "PalStaminaDecreaceRate", "PalAutoHPRegeneRate",
        "CollectionDropRate", "CollectionObjectRespawnSpeedRate", "WorkSpeedRate", "MonsterFarmActionSpeedRate",
        "ItemWeightRate", "ItemCorruptionMultiplier", "EquipmentDurabilityDamageRate", "BaseCampWorkerMaxNum",
        "BaseCampMaxNumInGuild", "BuildObjectDeteriorationDamageRate", "SupplyDropSpan"
    };

    // v0.7.29.0 bug fix: the gameplay-rate-only subset of SimpleConfigurationNames above -- deliberately
    // excludes identity (ServerName/ServerDescription/ServerPassword/AdminPassword) and
    // network/operational fields (PublicPort/ServerPlayerMaxNum/RESTAPIEnabled/RESTAPIPort/
    // RCONEnabled/RCONPort). SimpleConfigurationNames itself stays untouched -- it's correctly used
    // elsewhere to decide what the Simple Settings *view* shows, a different concern from what a QoL
    // preset should actually mutate. ApplySelectedConfigurationPreset's "Official" branch used to
    // reset every SimpleConfigurationNames entry, which silently wiped the server's name/description/
    // passwords back to their defaults any time "Official / Vanilla" was selected.
    private static readonly HashSet<string> GameplayRateConfigurationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "DayTimeSpeedRate", "NightTimeSpeedRate", "ExpRate", "PalCaptureRate", "PalSpawnNumRate",
        "PlayerDamageRateAttack", "PlayerDamageRateDefense", "PlayerStomachDecreaceRate", "PlayerStaminaDecreaceRate",
        "PlayerAutoHPRegeneRate", "PalStomachDecreaceRate", "PalStaminaDecreaceRate", "PalAutoHPRegeneRate",
        "CollectionDropRate", "CollectionObjectRespawnSpeedRate", "WorkSpeedRate", "MonsterFarmActionSpeedRate",
        "ItemWeightRate", "ItemCorruptionMultiplier", "EquipmentDurabilityDamageRate", "BaseCampWorkerMaxNum",
        "BaseCampMaxNumInGuild", "BuildObjectDeteriorationDamageRate", "SupplyDropSpan"
    };

    internal void SetConfigurationView(bool simple)
    {
        if (_isConfigSimpleView == simple) return;
        _isConfigSimpleView = simple;
        RaisePropertyChanged(nameof(IsConfigSimpleView));
        RaisePropertyChanged(nameof(IsConfigAdvancedView));
        ApplyPalworldConfigurationFilter();
    }

    private void RebuildPalworldConfigurationCategories()
    {
        var selected = SelectedConfigCategory;
        PalworldConfigCategories.Clear();
        PalworldConfigCategories.Add("All Categories");
        foreach (var category in PalworldSettings.Select(x => x.Category).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            PalworldConfigCategories.Add(category);
        SelectedConfigCategory = PalworldConfigCategories.Contains(selected) ? selected : "All Categories";
    }

    private void ApplyPalworldConfigurationFilter()
    {
        var search = ConfigSearchText.Trim();
        var category = SelectedConfigCategory;
        var items = PalworldSettings.Where(setting =>
            (!_isConfigSimpleView || SimpleConfigurationNames.Contains(setting.Name)) &&
            MatchesConfigFilter(setting, search, category));
        FilteredPalworldSettings.Clear();
        foreach (var item in items) FilteredPalworldSettings.Add(item);
        // Simple view previously ignored search/category entirely (a separate, unfiltered
        // collection) -- rebuilding it here keeps both views' filtering behavior identical.
        RebuildSimplePalworldSettings(search, category);
    }

    // v0.7.1.0: makes visible what RebuildSimplePalworldSettings always silently skipped -- a
    // curated setting whose name has no match in the live PalWorldSettings.ini at all (an older
    // Palworld server build, typically) previously just vanished from Simple view with no
    // indication. Computed independent of search/category filtering: existence in the live INI,
    // not whether it currently matches the filter, is what "missing" means here.
    public string SimpleSettingsGapText { get; private set; } = string.Empty;

    public bool HasSimpleSettingsGap => SimpleSettingsGapText.Length > 0;

    private void RebuildSimplePalworldSettings(string? search = null, string? category = null)
    {
        search ??= ConfigSearchText.Trim();
        category ??= SelectedConfigCategory;

        var curatedNames = SimpleRateDefinitions.Select(d => d.Name)
            .Concat(SimpleToggleDefinitions.Select(d => d.Name))
            .Concat(SimpleNetworkDefinitions.Select(d => d.Name));
        var missing = curatedNames
            .Where(name => !PalworldSettings.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        SimpleSettingsGapText = missing.Count == 0
            ? string.Empty
            : $"{missing.Count} of {SimpleRateDefinitions.Length + SimpleToggleDefinitions.Length + SimpleNetworkDefinitions.Length} curated settings aren't in this server's PalWorldSettings.ini (older server build?): {string.Join(", ", missing)}";
        RaisePropertyChanged(nameof(SimpleSettingsGapText));
        RaisePropertyChanged(nameof(HasSimpleSettingsGap));

        SimpleWorldRateSettings.Clear();
        SimplePlayerPalRateSettings.Clear();
        SimpleItemsWorkRateSettings.Clear();
        foreach (var definition in SimpleRateDefinitions)
        {
            var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase));
            if (setting is null || !MatchesConfigFilter(setting, search, category)) continue;
            var item = new PalworldSimpleSettingItem(setting, definition.Title, definition.Description, definition.Min, definition.Max, definition.Step, definition.Unit, definition.Group);
            var target = definition.Group switch
            {
                "Player & Pal" => SimplePlayerPalRateSettings,
                "Items & Work" => SimpleItemsWorkRateSettings,
                _ => SimpleWorldRateSettings,
            };
            target.Add(item);
        }
        SimpleToggleSettings.Clear();
        foreach (var definition in SimpleToggleDefinitions)
        {
            var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase));
            if (setting is not null && MatchesConfigFilter(setting, search, category))
                SimpleToggleSettings.Add(new PalworldSimpleToggleItem(setting, definition.Title, definition.Description));
        }
        SimpleNetworkSettings.Clear();
        foreach (var definition in SimpleNetworkDefinitions)
        {
            var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase));
            if (setting is not null && MatchesConfigFilter(setting, search, category))
                SimpleNetworkSettings.Add(setting);
        }

        RaisePropertyChanged(nameof(ServerNameSetting));
        RaisePropertyChanged(nameof(ServerDescriptionSetting));
        RaisePropertyChanged(nameof(AdminPasswordSetting));
        RaisePropertyChanged(nameof(ServerPasswordSetting));
    }

    private void PalworldSetting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PalworldSettingDto.Value) && e.PropertyName != nameof(PalworldSettingDto.IsDirty)) return;
        RefreshPalworldConfigurationState();
        if (!string.IsNullOrWhiteSpace(ConfigSearchText)) ApplyPalworldConfigurationFilter();
    }

    private void RefreshPalworldConfigurationState()
    {
        var dirtyCount = PalworldSettings.Count(x => x.IsDirty);
        PalworldConfigDirtyText = dirtyCount == 0 ? "No unsaved changes" : $"{dirtyCount} unsaved setting change(s)";
        RaisePropertyChanged(nameof(PalworldConfigIsDirty));
        var errors = ValidatePalworldConfiguration();
        PalworldConfigHasValidationErrors = errors.Count > 0;
        PalworldConfigValidationText = errors.Count == 0 ? "Configuration values pass client-side validation." : string.Join(" • ", errors.Take(4));
        (SavePalworldConfigurationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (ResetConfigChangesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        DetectAndSyncConfigPreset();
    }

    private void DetectAndSyncConfigPreset()
    {
        if (!PalworldConfigLoaded || PalworldSettings.Count == 0) return;
        string matched = "Custom";
        foreach (var candidate in new[] { "Official / Vanilla", "Balanced QoL", "Relaxed QoL" })
        {
            if (MatchesConfigPreset(candidate)) { matched = candidate; break; }
        }
        if (!string.Equals(_selectedConfigPreset, matched, StringComparison.Ordinal))
        {
            _selectedConfigPreset = matched;
            RaisePropertyChanged(nameof(SelectedConfigPreset));
        }
    }

    private bool MatchesConfigPreset(string preset)
    {
        if (preset.StartsWith("Official", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var key in QolPresetComparisonKeys)
            {
                var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (setting is not null && !ConfigValuesEqual(setting.Value, setting.DefaultValue)) return false;
            }
            return true;
        }
        foreach (var pair in GetQolPreset(preset))
        {
            var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
            if (setting is not null && !ConfigValuesEqual(setting.Value, pair.Value)) return false;
        }
        return true;
    }

    private static bool ConfigValuesEqual(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        if (double.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var da) &&
            double.TryParse(b, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var db))
            return Math.Abs(da - db) < 0.0001;
        return false;
    }

    private List<string> ValidatePalworldConfiguration()
    {
        var errors = new List<string>();
        foreach (var name in new[] { "PublicPort", "RESTAPIPort", "RCONPort" })
        {
            var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (setting is not null && (!int.TryParse(setting.Value, out var port) || port is < 1 or > 65535))
                errors.Add($"{setting.DisplayName}: port must be 1-65535");
        }
        var maxPlayers = PalworldSettings.FirstOrDefault(x => x.Name.Equals("ServerPlayerMaxNum", StringComparison.OrdinalIgnoreCase));
        if (maxPlayers is not null && (!int.TryParse(maxPlayers.Value, out var max) || max < 1))
            errors.Add("Maximum Players must be at least 1");
        return errors;
    }

    internal void ResetPalworldConfigurationChanges()
    {
        foreach (var setting in PalworldSettings.Where(x => x.IsDirty).ToArray()) setting.Value = setting.OriginalValue;
        PalworldConfigState = "Unsaved configuration changes were discarded.";
        RefreshPalworldConfigurationState();
    }

    // v0.7.81.0: the new-server wizard's Settings step gained a starter preset picker (direct
    // request: "choose one of the preset settings like vanilla or quality of life"), reusing the
    // exact Load -> Apply -> Save sequence the Configuration page's own preset picker already does
    // manually across three separate buttons, chained here into one action since the wizard only
    // needs "apply this preset to the server I just created," not the Configuration page's fuller
    // review-then-save workflow. No new backend -- LoadPalworldConfigurationAsync/
    // SavePalworldConfigurationAsync already use Session.BuildProfile(), so
    // both already work mid-wizard where Session.SelectedProfile is still null.
    internal async Task ApplyStarterPresetAsync()
    {
        await LoadPalworldConfigurationAsync();
        if (!PalworldConfigLoaded) return;
        ApplySelectedConfigurationPreset();
        await SavePalworldConfigurationAsync();
    }

    private void ApplySelectedConfigurationPreset()
    {
        if (!PalworldConfigLoaded) return;
        var presetKey = SelectedConfigPreset;
        if (presetKey.StartsWith("Official", StringComparison.OrdinalIgnoreCase))
        {
            // v0.7.29.0 bug fix: was SimpleConfigurationNames, which also covers identity
            // (ServerName/ServerDescription/ServerPassword/AdminPassword) -- resetting those
            // whenever "Official / Vanilla" was selected silently wiped the server's name and
            // passwords. Only gameplay rates should reset here, matching the Balanced/Relaxed
            // branch below, which never touched identity or network settings either.
            foreach (var setting in PalworldSettings.Where(x => GameplayRateConfigurationNames.Contains(x.Name))) setting.Value = setting.DefaultValue;
        }
        else
        {
            // Built-in presets (Balanced/Relaxed) come from GetQolPreset's hardcoded maps; anything
            // else is a user-saved custom preset (the "MystTiq" preset included -- it's not a
            // special case, just the first name someone saved) read from local disk.
            var builtIn = GetQolPreset(presetKey);
            var targets = builtIn.Count > 0 ? builtIn : _configPresetStore.LoadPreset(presetKey);
            foreach (var pair in targets)
            {
                var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
                if (setting is not null) setting.Value = pair.Value;
            }
        }
        PalworldConfigState = $"{presetKey} preset loaded locally. Review highlighted changes, then Save Changes.";
        RefreshPalworldConfigurationState();
    }

    internal void RebuildConfigPresetList()
    {
        var custom = _configPresetStore.LoadPresetNames();
        PalworldConfigPresets = new List<string> { "Official / Vanilla", "Balanced QoL", "Relaxed QoL" }.Concat(custom).Append("Custom").ToArray();
    }

    public string ExportPalworldConfigurationJson()
    {
        var document = new PalworldConfigurationExportDocument
        {
            SourcePath = PalworldConfigPath,
            Settings = PalworldSettings.Select(x => new PalworldConfigurationExportSetting { Name = x.Name, Value = x.Value }).ToArray()
        };
        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
    }

    public void ReportPalworldConfigurationFileSuccess(string message) => PalworldConfigState = message;

    public void ReportPalworldConfigurationFileError(string message) => PalworldConfigState = message;

    public void ImportPalworldConfigurationJson(string json)
    {
        var document = JsonSerializer.Deserialize<PalworldConfigurationExportDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The selected file does not contain a MystTiq Palworld configuration export.");
        if (document.SchemaVersion != 1) throw new InvalidOperationException($"Unsupported configuration export schema {document.SchemaVersion}.");
        var applied = 0;
        foreach (var imported in document.Settings)
        {
            if (string.IsNullOrWhiteSpace(imported.Name)) continue;
            var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(imported.Name, StringComparison.OrdinalIgnoreCase));
            if (setting is null) continue;
            setting.Value = imported.Value ?? string.Empty;
            applied++;
        }
        PalworldConfigState = $"Imported {applied} setting value(s) into the local editor. Save Changes to write the server configuration.";
        RefreshPalworldConfigurationState();
        ApplyPalworldConfigurationFilter();
    }

    internal async Task LoadPalworldConfigurationAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { PalworldConfigState = ex.Message; return; }
        Session.IsBusy = true;
        PalworldConfigState = "Loading PalWorldSettings.ini…";
        try
        {
            var snapshot = await Session.Api.GetPalworldConfigurationAsync(profile, Session.BearerToken);
            foreach (var existing in PalworldSettings) existing.PropertyChanged -= PalworldSetting_PropertyChanged;
            PalworldSettings.Clear();
            foreach (var setting in snapshot.Settings)
            {
                setting.MarkClean();
                setting.PropertyChanged += PalworldSetting_PropertyChanged;
                PalworldSettings.Add(setting);
            }
            PalworldConfigPath = snapshot.ConfigurationPath;
            PalworldConfigLoaded = snapshot.Exists;
            PalworldConfigState = snapshot.Detail;
            RebuildPalworldConfigurationCategories();
            RebuildSimplePalworldSettings();
            ApplyPalworldConfigurationFilter();
            RefreshPalworldConfigurationState();
        }
        catch (Exception ex) { PalworldConfigLoaded = false; PalworldConfigState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    internal async Task SavePalworldConfigurationAsync()
    {
        if (!PalworldConfigLoaded) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { PalworldConfigState = ex.Message; return; }
        Session.IsBusy = true;
        PalworldConfigState = "Saving PalWorldSettings.ini…";
        try
        {
            var result = await Session.Api.SavePalworldConfigurationAsync(profile, PalworldSettings.ToArray(), Session.BearerToken);
            PalworldConfigState = result.Success ? result.Message : string.Join("; ", result.ValidationErrors.DefaultIfEmpty(result.Message));
            if (result.Success) await LoadPalworldConfigurationAsync();
        }
        catch (Exception ex) { PalworldConfigState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    internal async Task LoadConfigurationAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { ConfigState = ex.Message; return; }

        Session.IsBusy = true;
        ConfigState = "Loading…";
        try
        {
            var config = await Session.Api.GetEditableConfigurationAsync(profile, Session.BearerToken);

            ConfigApiEnabled = config.Api.Enabled;
            ConfigBindAddress = config.Api.BindAddress;
            ConfigPort = config.Api.Port;

            StartupTimeoutSeconds = config.Lifecycle.StartupTimeoutSeconds;
            StopTimeoutSeconds = config.Lifecycle.StopTimeoutSeconds;
            ServicePollSeconds = config.Lifecycle.ServicePollSeconds;
            RecoveryBackoffSeconds = config.Lifecycle.RecoveryBackoffSeconds;
            MaximumRecoveryAttempts = config.Lifecycle.MaximumRecoveryAttempts;
            RecoveryWindowSeconds = config.Lifecycle.RecoveryWindowSeconds;

            ConfigServerRoot = config.Server.ServerRoot;
            ConfigSteamCmdPath = config.Server.SteamCmdPath;
            ConfigBackupRoot = config.Server.BackupRoot;
            ConfigRuntimeRoot = config.Server.RuntimeRoot;
            LoadLauncherConfiguration(config.Server.LaunchArguments);

            ConfigSecurityText =
                $"Authentication: {(config.AuthenticationEnabled ? "Enabled" : "Disabled")} · TLS: {(config.TlsEnabled ? "Enabled" : "Disabled")}";

            ConfigLoaded = true;
            ConfigState = config.RestartRequired
                ? "Loaded · service restart already required"
                : "Loaded";
            ConfigurationLoaded?.Invoke();
        }
        catch (Exception ex)
        {
            ConfigLoaded = false;
            ConfigState = ex.Message;
            SetWorkspaceState($"Workspace unavailable: {ex.Message}");
        }
        finally { Session.IsBusy = false; }
    }

    private async Task SaveConfigurationAsync()
    {
        if (!ConfigLoaded) return;

        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { ConfigState = ex.Message; return; }

        var editable = new EditableConfigurationDto
        {
            Api = new EditableApiConfigurationDto
            {
                Enabled = ConfigApiEnabled,
                BindAddress = ConfigBindAddress,
                Port = ConfigPort
            },
            Lifecycle = new EditableLifecycleConfigurationDto
            {
                StartupTimeoutSeconds = StartupTimeoutSeconds,
                StopTimeoutSeconds = StopTimeoutSeconds,
                ServicePollSeconds = ServicePollSeconds,
                RecoveryBackoffSeconds = RecoveryBackoffSeconds,
                MaximumRecoveryAttempts = MaximumRecoveryAttempts,
                RecoveryWindowSeconds = RecoveryWindowSeconds
            },
            Server = new EditableServerConfigurationDto
            {
                ServerRoot = ConfigServerRoot,
                SteamCmdPath = ConfigSteamCmdPath,
                BackupRoot = ConfigBackupRoot,
                RuntimeRoot = ConfigRuntimeRoot,
                LaunchArguments = BuildLauncherConfigurationArguments()
            }
        };

        Session.IsBusy = true;
        ConfigState = "Saving…";
        try
        {
            var result = await Session.Api.SaveEditableConfigurationAsync(profile, editable, Session.BearerToken);

            if (!result.Success)
            {
                ConfigState = result.ValidationErrors.Count > 0
                    ? "Validation failed: " + string.Join("; ", result.ValidationErrors)
                    : result.Message;
                return;
            }

            ConfigState = result.RestartRequired
                ? "Saved · restart MystTiq service to apply"
                : "Saved";
            SetWorkspaceState(result.RestartRequired
                ? "Paths saved with rollback backup · restart the MystTiq service to apply."
                : "Paths saved through the managed headless configuration service.");
        }
        catch (Exception ex) { ConfigState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    public string SelectedConfigCategory
    {
        get => _selectedConfigCategory;
        set { if (SetField(ref _selectedConfigCategory, value ?? "All Categories")) ApplyPalworldConfigurationFilter(); }
    }

    private static bool MatchesConfigFilter(PalworldSettingDto setting, string search, string category) =>
        (string.Equals(category, "All Categories", StringComparison.OrdinalIgnoreCase) || string.Equals(setting.Category, category, StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(search) || setting.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || setting.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) || setting.Category.Contains(search, StringComparison.OrdinalIgnoreCase));

    private static readonly (string Name, string Title, string Description)[] SimpleToggleDefinitions =
    [
        ("RESTAPIEnabled", "REST API Enabled", "Enables Palworld's own REST API, which MystTiq's player/world polling relies on."),
        ("RCONEnabled", "RCON Enabled", "Enables Source RCON, which server broadcast/commands rely on.")
    ];

    // Data-driven off the same SimpleConfigurationNames curated set that used to only
    // gate a filter with no real UI effect -- one entry per name, control shape chosen by the
    // setting's actual type, replacing the previous 8-item hardcoded slider array. Identity/
    // credential fields (ServerName/ServerDescription/AdminPassword/ServerPassword) are excluded
    // here -- they're bound directly in the Server Identity section of the page, not part of any
    // of these three collections.
    // v0.7.38.0: Group added so the formerly-flat GAMEPLAY RATES list can render as labeled
    // sub-sections under the renamed WORLD SETTINGS heading, matching the v0.2.16.4 reference's
    // grouping (its own listed examples -- "World": Death Penalty/Longer Days/Shorter
    // Nights/More Item Drops/Faster Resource Respawn/Supply Drop Interval; "Player & Pal":
    // Player/Pal Health Regeneration -- named only a few by example, not the full list; every
    // curated rate below has been assigned to whichever of the three groups it actually belongs
    // to semantically: World (environment/spawn/decay pacing), Player & Pal (survival/combat body
    // stats), Items & Work (economy/base productivity/equipment).
    private static readonly (string Name, string Title, string Description, double Min, double Max, double Step, string Unit, string Group)[] SimpleRateDefinitions =
    [
        ("DayTimeSpeedRate", "Daytime Speed", "Controls how quickly daylight passes.", 0.1, 5, 0.05, "x", "World"),
        ("NightTimeSpeedRate", "Nighttime Speed", "Controls how quickly nighttime passes.", 0.1, 5, 0.05, "x", "World"),
        ("ExpRate", "Experience Rate", "Controls experience earned by players and Pals.", 0.1, 20, 0.1, "x", "World"),
        ("PalCaptureRate", "Pal Capture Rate", "Controls how easily Pals are captured.", 0.1, 5, 0.05, "x", "World"),
        ("PalSpawnNumRate", "Pal Spawn Rate", "Controls how many Pals spawn in the world.", 0.1, 5, 0.05, "x", "World"),
        ("SupplyDropSpan", "Supply Drop Interval", "Sets the time between supply drops.", 1, 360, 1, "min", "World"),
        ("BuildObjectDeteriorationDamageRate", "Base Decay Rate", "Controls how quickly base structures deteriorate.", 0, 5, 0.05, "x", "World"),
        ("PlayerDamageRateAttack", "Player Attack Damage", "Controls damage dealt by players.", 0.1, 10, 0.1, "x", "Player & Pal"),
        ("PlayerDamageRateDefense", "Player Damage Taken", "Controls damage received by players.", 0.1, 10, 0.1, "x", "Player & Pal"),
        ("PlayerStomachDecreaceRate", "Player Hunger Drain", "Controls how quickly player hunger depletes.", 0.1, 5, 0.05, "x", "Player & Pal"),
        ("PlayerStaminaDecreaceRate", "Player Stamina Drain", "Controls how quickly player stamina depletes.", 0.1, 5, 0.05, "x", "Player & Pal"),
        ("PlayerAutoHPRegeneRate", "Player Health Regeneration", "Increases automatic player health recovery.", 0.1, 10, 0.1, "x", "Player & Pal"),
        ("PalStomachDecreaceRate", "Pal Hunger Drain", "Controls how quickly Pal hunger depletes.", 0.1, 5, 0.05, "x", "Player & Pal"),
        ("PalStaminaDecreaceRate", "Pal Stamina Drain", "Controls how quickly Pal stamina depletes.", 0.1, 5, 0.05, "x", "Player & Pal"),
        ("PalAutoHPRegeneRate", "Pal Health Regeneration", "Increases automatic Pal health recovery.", 0.1, 10, 0.1, "x", "Player & Pal"),
        ("CollectionDropRate", "More Item Drops", "Increases resources dropped from collection objects.", 0.1, 10, 0.1, "x", "Items & Work"),
        ("CollectionObjectRespawnSpeedRate", "Resource Respawn", "Controls how quickly collection objects return.", 0.1, 10, 0.1, "x", "Items & Work"),
        ("WorkSpeedRate", "Work Speed", "Controls how quickly base tasks are completed.", 0.1, 10, 0.1, "x", "Items & Work"),
        ("MonsterFarmActionSpeedRate", "Ranch Action Speed", "Controls how quickly ranch-type Pals produce.", 0.1, 20, 0.5, "x", "Items & Work"),
        ("ItemWeightRate", "Item Weight", "Controls carried-item weight.", 0.1, 10, 0.1, "x", "Items & Work"),
        // v0.7.102.0: minimum lowered from 0.1 to 0 (step 0.05). Real servers run below 0.1 (the Relaxed preset
        // uses 0.25, and a hand-edited 0.05 was the value that exposed the phantom-change bug).
        ("ItemCorruptionMultiplier", "Item Corruption Rate", "Controls how quickly items degrade.", 0, 10, 0.05, "x", "Items & Work"),
        ("EquipmentDurabilityDamageRate", "Equipment Durability Loss", "Controls how quickly equipment durability drops.", 0.1, 5, 0.05, "x", "Items & Work")
    ];

    private static readonly (string Name, string Title)[] SimpleNetworkDefinitions =
    [
        ("PublicPort", "Game Port"), ("ServerPlayerMaxNum", "Max Players"),
        ("RESTAPIPort", "REST API Port"), ("RCONPort", "RCON Port"),
        ("BaseCampWorkerMaxNum", "Base Workers (per base)"), ("BaseCampMaxNumInGuild", "Bases per Guild")
    ];

    public int StopTimeoutSeconds { get => _stopTimeoutSeconds; set => SetField(ref _stopTimeoutSeconds, value); }

    public int StartupTimeoutSeconds { get => _startupTimeoutSeconds; set => SetField(ref _startupTimeoutSeconds, value); }

    public int ServicePollSeconds { get => _servicePollSeconds; set => SetField(ref _servicePollSeconds, value); }

    public int RecoveryWindowSeconds { get => _recoveryWindowSeconds; set => SetField(ref _recoveryWindowSeconds, value); }

    public int RecoveryBackoffSeconds { get => _recoveryBackoffSeconds; set => SetField(ref _recoveryBackoffSeconds, value); }

    public int MaximumRecoveryAttempts { get => _maximumRecoveryAttempts; set => SetField(ref _maximumRecoveryAttempts, value); }

    public ICommand SaveCurrentAsPresetCommand { get; }

    public ICommand ResetConfigChangesCommand { get; }

    private static IReadOnlyDictionary<string, string> GetQolPreset(string preset)
    {
        var p = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (preset.StartsWith("Balanced", StringComparison.OrdinalIgnoreCase))
        {
            p["DayTimeSpeedRate"]="0.85"; p["NightTimeSpeedRate"]="1.10";
            p["PlayerAutoHPRegeneRate"]="1.5"; p["PalAutoHPRegeneRate"]="1.5";
            p["PlayerStomachDecreaceRate"]="0.75"; p["PalStomachDecreaceRate"]="0.75";
            p["PlayerStaminaDecreaceRate"]="0.80"; p["PalStaminaDecreaceRate"]="0.80";
            p["ItemWeightRate"]="0.65"; p["ItemCorruptionMultiplier"]="0.50"; p["EquipmentDurabilityDamageRate"]="0.75";
            p["WorkSpeedRate"]="1.25"; p["MonsterFarmActionSpeedRate"]="1.25";
            p["CollectionDropRate"]="1.5"; p["CollectionObjectRespawnSpeedRate"]="1.25";
            p["BaseCampWorkerMaxNum"]="30"; p["BaseCampMaxNumInGuild"]="6"; p["BuildObjectDeteriorationDamageRate"]="0";
        }
        else if (preset.StartsWith("Relaxed", StringComparison.OrdinalIgnoreCase))
        {
            p["DayTimeSpeedRate"]="0.75"; p["NightTimeSpeedRate"]="1.25";
            p["PlayerAutoHPRegeneRate"]="2.0"; p["PalAutoHPRegeneRate"]="2.0";
            p["PlayerStomachDecreaceRate"]="0.60"; p["PalStomachDecreaceRate"]="0.60";
            p["PlayerStaminaDecreaceRate"]="0.65"; p["PalStaminaDecreaceRate"]="0.65";
            p["ItemWeightRate"]="0.50"; p["ItemCorruptionMultiplier"]="0.25"; p["EquipmentDurabilityDamageRate"]="0.60";
            p["WorkSpeedRate"]="1.50"; p["MonsterFarmActionSpeedRate"]="1.50";
            p["CollectionDropRate"]="2.0"; p["CollectionObjectRespawnSpeedRate"]="1.50";
            p["SupplyDropSpan"]="90"; p["BaseCampWorkerMaxNum"]="35"; p["BaseCampMaxNumInGuild"]="8"; p["BuildObjectDeteriorationDamageRate"]="0";
        }
        return p;
    }

    private static readonly string[] QolPresetComparisonKeys =
    [
        "DayTimeSpeedRate", "NightTimeSpeedRate", "PlayerAutoHPRegeneRate", "PalAutoHPRegeneRate",
        "PlayerStomachDecreaceRate", "PalStomachDecreaceRate", "PlayerStaminaDecreaceRate", "PalStaminaDecreaceRate",
        "ItemWeightRate", "ItemCorruptionMultiplier", "EquipmentDurabilityDamageRate", "WorkSpeedRate",
        "MonsterFarmActionSpeedRate", "CollectionDropRate", "CollectionObjectRespawnSpeedRate",
        "BaseCampWorkerMaxNum", "BaseCampMaxNumInGuild", "BuildObjectDeteriorationDamageRate", "SupplyDropSpan"
    ];

    public PalworldSettingDto? ServerPasswordSetting => PalworldSettings.FirstOrDefault(x => x.Name.Equals("ServerPassword", StringComparison.OrdinalIgnoreCase));

    public PalworldSettingDto? ServerNameSetting => PalworldSettings.FirstOrDefault(x => x.Name.Equals("ServerName", StringComparison.OrdinalIgnoreCase));

    public PalworldSettingDto? ServerDescriptionSetting => PalworldSettings.FirstOrDefault(x => x.Name.Equals("ServerDescription", StringComparison.OrdinalIgnoreCase));

    public PalworldSettingDto? AdminPasswordSetting => PalworldSettings.FirstOrDefault(x => x.Name.Equals("AdminPassword", StringComparison.OrdinalIgnoreCase));

    public bool IsConfigAdvancedView => !_isConfigSimpleView;

    private void SaveCurrentAsPreset()
    {
        var name = NewConfigPresetName.Trim();
        if (string.IsNullOrWhiteSpace(name) || !PalworldConfigLoaded) return;
        var snapshot = PalworldSettings.ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
        _configPresetStore.SavePreset(name, snapshot);
        RebuildConfigPresetList();
        // Direct field assignment, bypassing the public setter -- same bypass
        // DetectAndSyncConfigPreset() already uses. The values just saved already match what's
        // loaded; re-applying through the setter would be a harmless but pointless no-op.
        _selectedConfigPreset = name;
        RaisePropertyChanged(nameof(SelectedConfigPreset));
        NewConfigPresetName = string.Empty;
        PalworldConfigState = $"Saved the current settings locally as preset \"{name}\".";
    }

    // MystTiq v1.0.0.1 launcher workspace + troubleshooting presets (2026-10-01).
    private const string LauncherOptionPrefix = "@mysttiq:";
    private bool _launcherSyncing;

    private bool _configLauncherPortEnabled = true;
    private int _configLauncherPort = 8211;
    // v1.0.6.1: off by default: every start that gave players the wrong character had these on.
    private bool _configLauncherLog;
    private bool _configLauncherStdout;
    private bool _configLauncherFullStdOutLogOutput;
    private bool _configLauncherAbsLog;
    private string _configLauncherAbsLogPath = string.Empty;
    private bool _configLauncherUnattended;
    private bool _configLauncherUsePerfThreads;
    private bool _configLauncherNoAsyncLoadingThread;
    private bool _configLauncherUseMultithreadForDs;
    private bool _configLauncherLogFormatText;
    private bool _configLauncherNoMods;
    private string _configLauncherCustomArguments = string.Empty;

    private string _configLauncherExecutable = "PalServer.exe";
    private string _configLauncherCustomExecutablePath = string.Empty;
    private string _configLauncherWorkingDirectory = "Server root";
    private string _configLauncherCustomWorkingDirectory = string.Empty;
    private bool _configLauncherUseShellExecute;
    private bool _configLauncherCreateNoWindow;
    private string _configLauncherWindowStyle = "Hidden";
    private bool _configLauncherRedirectStandardOutput;
    private bool _configLauncherRedirectStandardError;
    private bool _configLauncherCaptureRedirectedOutput = true;
    private bool _configLauncherHideManagedWindows = true;
    private string _configLauncherEffectiveCommand = "Load configuration to preview the launcher command.";
    private string _configLauncherProcessSummary = string.Empty;
    private string _configLauncherWarning = string.Empty;
    private string _configLauncherPresetStatus = "Choose a troubleshooting preset or adjust individual options below.";
    private System.Windows.Input.ICommand? _applyLauncherPresetCommand;

    public IReadOnlyList<string> ConfigLauncherExecutableOptions { get; } =
        ["PalServer.exe", "Shipping-Cmd.exe", "Shipping.exe", "Test-Cmd.exe", "Test.exe", "Custom path"];

    public IReadOnlyList<string> ConfigLauncherWorkingDirectoryOptions { get; } =
        ["Server root", "Executable folder", "Custom path"];

    public IReadOnlyList<string> ConfigLauncherWindowStyleOptions { get; } =
        ["Hidden", "Normal", "Minimized", "Maximized"];

    public bool ConfigLauncherPortEnabled { get => _configLauncherPortEnabled; set => SetLauncherField(ref _configLauncherPortEnabled, value); }
    public int ConfigLauncherPort { get => _configLauncherPort; set => SetLauncherField(ref _configLauncherPort, Math.Clamp(value, 1, 65535)); }
    public bool ConfigLauncherLog { get => _configLauncherLog; set => SetLauncherField(ref _configLauncherLog, value); }
    public bool ConfigLauncherStdout { get => _configLauncherStdout; set => SetLauncherField(ref _configLauncherStdout, value); }
    public bool ConfigLauncherFullStdOutLogOutput { get => _configLauncherFullStdOutLogOutput; set => SetLauncherField(ref _configLauncherFullStdOutLogOutput, value); }
    public bool ConfigLauncherAbsLog { get => _configLauncherAbsLog; set => SetLauncherField(ref _configLauncherAbsLog, value); }
    public string ConfigLauncherAbsLogPath { get => _configLauncherAbsLogPath; set => SetLauncherField(ref _configLauncherAbsLogPath, value ?? string.Empty); }
    public bool ConfigLauncherUnattended { get => _configLauncherUnattended; set => SetLauncherField(ref _configLauncherUnattended, value); }
    public bool ConfigLauncherUsePerfThreads { get => _configLauncherUsePerfThreads; set => SetLauncherField(ref _configLauncherUsePerfThreads, value); }
    public bool ConfigLauncherNoAsyncLoadingThread { get => _configLauncherNoAsyncLoadingThread; set => SetLauncherField(ref _configLauncherNoAsyncLoadingThread, value); }
    public bool ConfigLauncherUseMultithreadForDs { get => _configLauncherUseMultithreadForDs; set => SetLauncherField(ref _configLauncherUseMultithreadForDs, value); }
    public bool ConfigLauncherLogFormatText { get => _configLauncherLogFormatText; set => SetLauncherField(ref _configLauncherLogFormatText, value); }
    public bool ConfigLauncherNoMods { get => _configLauncherNoMods; set => SetLauncherField(ref _configLauncherNoMods, value); }
    public string ConfigLauncherCustomArguments { get => _configLauncherCustomArguments; set => SetLauncherField(ref _configLauncherCustomArguments, value ?? string.Empty); }

    public string ConfigLauncherExecutable { get => _configLauncherExecutable; set => SetLauncherField(ref _configLauncherExecutable, value ?? "PalServer.exe"); }
    public string ConfigLauncherCustomExecutablePath { get => _configLauncherCustomExecutablePath; set => SetLauncherField(ref _configLauncherCustomExecutablePath, value ?? string.Empty); }
    public string ConfigLauncherWorkingDirectory { get => _configLauncherWorkingDirectory; set => SetLauncherField(ref _configLauncherWorkingDirectory, value ?? "Server root"); }
    public string ConfigLauncherCustomWorkingDirectory { get => _configLauncherCustomWorkingDirectory; set => SetLauncherField(ref _configLauncherCustomWorkingDirectory, value ?? string.Empty); }
    public bool ConfigLauncherUseShellExecute { get => _configLauncherUseShellExecute; set => SetLauncherField(ref _configLauncherUseShellExecute, value); }
    public bool ConfigLauncherCreateNoWindow { get => _configLauncherCreateNoWindow; set => SetLauncherField(ref _configLauncherCreateNoWindow, value); }
    public string ConfigLauncherWindowStyle { get => _configLauncherWindowStyle; set => SetLauncherField(ref _configLauncherWindowStyle, value ?? "Hidden"); }
    public bool ConfigLauncherRedirectStandardOutput { get => _configLauncherRedirectStandardOutput; set => SetLauncherField(ref _configLauncherRedirectStandardOutput, value); }
    public bool ConfigLauncherRedirectStandardError { get => _configLauncherRedirectStandardError; set => SetLauncherField(ref _configLauncherRedirectStandardError, value); }
    public bool ConfigLauncherCaptureRedirectedOutput { get => _configLauncherCaptureRedirectedOutput; set => SetLauncherField(ref _configLauncherCaptureRedirectedOutput, value); }
    public bool ConfigLauncherHideManagedWindows { get => _configLauncherHideManagedWindows; set => SetLauncherField(ref _configLauncherHideManagedWindows, value); }

    public string ConfigLauncherEffectiveCommand { get => _configLauncherEffectiveCommand; private set => SetField(ref _configLauncherEffectiveCommand, value); }
    public string ConfigLauncherProcessSummary { get => _configLauncherProcessSummary; private set => SetField(ref _configLauncherProcessSummary, value); }
    public string ConfigLauncherWarning { get => _configLauncherWarning; private set => SetField(ref _configLauncherWarning, value); }
    public string ConfigLauncherPresetStatus { get => _configLauncherPresetStatus; private set => SetField(ref _configLauncherPresetStatus, value); }
    public System.Windows.Input.ICommand ApplyLauncherPresetCommand => _applyLauncherPresetCommand ??= new RelayCommand<string>(ApplyLauncherPreset);

    private void ApplyLauncherPreset(string? preset)
    {
        var mode = (preset ?? string.Empty).Trim();
        _launcherSyncing = true;
        try
        {
            // Presets intentionally preserve the configured game port and absolute-log path, because
            // those identify this server. They reset launch mechanics and custom arguments so each
            // troubleshooting run is reproducible.
            ConfigLauncherExecutable = "PalServer.exe";
            ConfigLauncherCustomExecutablePath = string.Empty;
            ConfigLauncherWorkingDirectory = "Server root";
            ConfigLauncherCustomWorkingDirectory = string.Empty;
            ConfigLauncherUseShellExecute = false;
            ConfigLauncherCreateNoWindow = false;
            ConfigLauncherRedirectStandardOutput = false;
            ConfigLauncherRedirectStandardError = false;
            ConfigLauncherCaptureRedirectedOutput = true;
            ConfigLauncherPortEnabled = true;
            // v1.0.6.1: no preset adds -log, -stdout, -FullStdOutLogOutput or -abslog any more: every start that gave players the
            // wrong character had them (reported 2026-10-04 and again 2026-10-06, after the Show Window preset was saved).
            ConfigLauncherLog = false;
            ConfigLauncherStdout = false;
            ConfigLauncherFullStdOutLogOutput = false;
            ConfigLauncherAbsLog = false;
            ConfigLauncherUnattended = false;
            ConfigLauncherUsePerfThreads = false;
            ConfigLauncherNoAsyncLoadingThread = false;
            ConfigLauncherUseMultithreadForDs = false;
            ConfigLauncherLogFormatText = false;
            ConfigLauncherCustomArguments = string.Empty;

            if (mode.Equals("DoubleClick", StringComparison.OrdinalIgnoreCase))
            {
                // v1.0.0.1 (2026-10-04): exactly what double-clicking PalServer.exe in the server folder does, the start the
                // owner found keeps players' characters: started through the Windows shell, a normal window, no arguments
                // (only -port= for a server not on 8211), nothing hidden afterwards.
                ConfigLauncherUseShellExecute = true;
                ConfigLauncherPortEnabled = ConfigLauncherPort != 8211;
                ConfigLauncherLog = false;
                ConfigLauncherStdout = false;
                ConfigLauncherFullStdOutLogOutput = false;
                ConfigLauncherAbsLog = false;
                ConfigLauncherNoMods = false;
                ConfigLauncherWindowStyle = "Normal";
                ConfigLauncherHideManagedWindows = false;
                ConfigLauncherPresetStatus = "Like double-click preset applied: PalServer.exe started through Windows, as a double-click in its folder does, with a normal window and no arguments. Click Save Launcher Settings before testing.";
            }
            else if (mode.Equals("NoMods", StringComparison.OrdinalIgnoreCase))
            {
                ConfigLauncherNoMods = true;
                ConfigLauncherWindowStyle = "Hidden";
                ConfigLauncherHideManagedWindows = true;
                ConfigLauncherPresetStatus = "No Mods preset applied (-NoMods). Port and log path were preserved. Click Save Launcher Settings before testing.";
            }
            else if (mode.Equals("ShowWindow", StringComparison.OrdinalIgnoreCase))
            {
                ConfigLauncherNoMods = false;
                ConfigLauncherWindowStyle = "Normal";
                ConfigLauncherHideManagedWindows = false;
                ConfigLauncherPresetStatus = "Show Window preset applied. PalServer uses a normal visible window with no post-launch hiding. Click Save Launcher Settings before testing.";
            }
            else
            {
                ConfigLauncherNoMods = false;
                ConfigLauncherWindowStyle = "Hidden";
                ConfigLauncherHideManagedWindows = true;
                ConfigLauncherPresetStatus = "Default preset applied. Mods remain enabled; MystTiq hides the managed PalServer window. Port and log path were preserved. Click Save Launcher Settings before testing.";
            }
        }
        finally
        {
            _launcherSyncing = false;
        }

        SyncConfigLaunchArgumentsFromLauncherEditor();
        UpdateLauncherPreview();
    }

    private void SetLauncherField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!SetField(ref field, value, propertyName)) return;
        LauncherConfigurationChanged();
    }

    private void LauncherConfigurationChanged()
    {
        if (_launcherSyncing) return;
        ConfigLauncherPresetStatus = "Custom launcher settings modified. Click Save Launcher Settings before testing.";
        SyncConfigLaunchArgumentsFromLauncherEditor();
        UpdateLauncherPreview();
    }

    private void LoadLauncherConfiguration(IReadOnlyList<string> storedArguments)
    {
        _launcherSyncing = true;
        try
        {
            var arguments = storedArguments
                .Where(argument => !string.IsNullOrWhiteSpace(argument))
                .Select(argument => argument.Trim())
                .ToArray();
            var options = ParseLauncherOptions(arguments);
            var explicitLauncher = options.ContainsKey("launcherVersion");

            ConfigLauncherExecutable = ReadLauncherOption(options, "executable", "PalServer.exe");
            ConfigLauncherCustomExecutablePath = ReadLauncherOption(options, "customExecutablePath", string.Empty);
            ConfigLauncherWorkingDirectory = ReadLauncherOption(options, "workingDirectory", "Server root");
            ConfigLauncherCustomWorkingDirectory = ReadLauncherOption(options, "customWorkingDirectory", string.Empty);
            ConfigLauncherUseShellExecute = ReadLauncherBool(options, "useShellExecute", false);
            ConfigLauncherCreateNoWindow = ReadLauncherBool(options, "createNoWindow", false);
            ConfigLauncherWindowStyle = ReadLauncherOption(options, "windowStyle", "Hidden");
            ConfigLauncherRedirectStandardOutput = ReadLauncherBool(options, "redirectStandardOutput", false);
            ConfigLauncherRedirectStandardError = ReadLauncherBool(options, "redirectStandardError", false);
            ConfigLauncherCaptureRedirectedOutput = ReadLauncherBool(options, "captureRedirectedOutput", true);
            ConfigLauncherHideManagedWindows = ReadLauncherBool(options, "hideManagedWindows", true);

            var portArgument = arguments.LastOrDefault(a => a.StartsWith("-port=", StringComparison.OrdinalIgnoreCase));
            ConfigLauncherPortEnabled = portArgument is not null || !explicitLauncher;
            ConfigLauncherPort = portArgument is not null && int.TryParse(portArgument["-port=".Length..], out var parsedPort)
                ? Math.Clamp(parsedPort, 1, 65535)
                : 8211;

            if (explicitLauncher)
            {
                ConfigLauncherLog = HasExact(arguments, "-log");
                ConfigLauncherStdout = HasExact(arguments, "-stdout");
                ConfigLauncherFullStdOutLogOutput = HasExact(arguments, "-FullStdOutLogOutput");
                ConfigLauncherUnattended = HasExact(arguments, "-unattended");
                ConfigLauncherUsePerfThreads = HasExact(arguments, "-useperfthreads");
                ConfigLauncherNoAsyncLoadingThread = HasExact(arguments, "-NoAsyncLoadingThread");
                ConfigLauncherUseMultithreadForDs = HasExact(arguments, "-UseMultithreadForDS");
                ConfigLauncherLogFormatText = arguments.Any(a => a.Equals("-logformat=text", StringComparison.OrdinalIgnoreCase));
                ConfigLauncherNoMods = HasExact(arguments, "-NoMods");
                ConfigLauncherAbsLog = arguments.Any(a => a.StartsWith("-abslog=", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                // v1.0.6.1: a profile with no saved launcher settings starts with no arguments (v1.0.0.1), so the editor shows
                // that. It used to show -log, -stdout, -FullStdOutLogOutput and -abslog here, and saving the page wrote them
                // in: the options every wrong-character start had (seen on the owner's main server on 2026-10-06).
                ConfigLauncherLog = false;
                ConfigLauncherStdout = false;
                ConfigLauncherFullStdOutLogOutput = false;
                ConfigLauncherAbsLog = false;
                ConfigLauncherUnattended = false;
                ConfigLauncherUsePerfThreads = false;
                ConfigLauncherNoAsyncLoadingThread = false;
                ConfigLauncherUseMultithreadForDs = false;
                ConfigLauncherLogFormatText = false;
                ConfigLauncherNoMods = false;
            }

            var absLogArgument = arguments.LastOrDefault(a => a.StartsWith("-abslog=", StringComparison.OrdinalIgnoreCase));
            ConfigLauncherAbsLogPath = absLogArgument is null
                ? DefaultIdentityLogPath()
                : absLogArgument["-abslog=".Length..].Trim().Trim('"');

            ConfigLauncherCustomArguments = string.Join(Environment.NewLine,
                arguments.Where(argument =>
                    !argument.StartsWith(LauncherOptionPrefix, StringComparison.OrdinalIgnoreCase) &&
                    !IsKnownLauncherArgument(argument) &&
                    (explicitLauncher || !IsHistoricalSuppressedArgument(argument))));
        }
        finally
        {
            _launcherSyncing = false;
        }

        ConfigLauncherPresetStatus = "Saved launcher settings loaded. Choose a preset or adjust individual options, then save before testing.";
        SyncConfigLaunchArgumentsFromLauncherEditor();
        UpdateLauncherPreview();
    }

    private IReadOnlyList<string> BuildLauncherConfigurationArguments()
    {
        var result = new List<string>
        {
            "@mysttiq:launcherVersion=1",
            $"@mysttiq:executable={ConfigLauncherExecutable}",
            $"@mysttiq:customExecutablePath={ConfigLauncherCustomExecutablePath.Trim()}",
            $"@mysttiq:workingDirectory={ConfigLauncherWorkingDirectory}",
            $"@mysttiq:customWorkingDirectory={ConfigLauncherCustomWorkingDirectory.Trim()}",
            $"@mysttiq:useShellExecute={ConfigLauncherUseShellExecute.ToString().ToLowerInvariant()}",
            $"@mysttiq:createNoWindow={ConfigLauncherCreateNoWindow.ToString().ToLowerInvariant()}",
            $"@mysttiq:windowStyle={ConfigLauncherWindowStyle}",
            $"@mysttiq:redirectStandardOutput={ConfigLauncherRedirectStandardOutput.ToString().ToLowerInvariant()}",
            $"@mysttiq:redirectStandardError={ConfigLauncherRedirectStandardError.ToString().ToLowerInvariant()}",
            $"@mysttiq:captureRedirectedOutput={ConfigLauncherCaptureRedirectedOutput.ToString().ToLowerInvariant()}",
            $"@mysttiq:hideManagedWindows={ConfigLauncherHideManagedWindows.ToString().ToLowerInvariant()}"
        };

        if (ConfigLauncherPortEnabled) result.Add($"-port={ConfigLauncherPort}");
        if (ConfigLauncherLog) result.Add("-log");
        if (ConfigLauncherLogFormatText) result.Add("-logformat=text");
        if (ConfigLauncherStdout) result.Add("-stdout");
        if (ConfigLauncherFullStdOutLogOutput) result.Add("-FullStdOutLogOutput");
        if (ConfigLauncherAbsLog)
        {
            var path = string.IsNullOrWhiteSpace(ConfigLauncherAbsLogPath) ? DefaultIdentityLogPath() : ConfigLauncherAbsLogPath.Trim();
            result.Add($"-abslog={path}");
        }
        if (ConfigLauncherUnattended) result.Add("-unattended");
        if (ConfigLauncherUsePerfThreads) result.Add("-useperfthreads");
        if (ConfigLauncherNoAsyncLoadingThread) result.Add("-NoAsyncLoadingThread");
        if (ConfigLauncherUseMultithreadForDs) result.Add("-UseMultithreadForDS");
        if (ConfigLauncherNoMods) result.Add("-NoMods");

        foreach (var argument in SplitArgumentLines(ConfigLauncherCustomArguments))
        {
            if (argument.StartsWith(LauncherOptionPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (IsKnownLauncherArgument(argument)) continue;
            result.Add(argument);
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private void SyncConfigLaunchArgumentsFromLauncherEditor()
    {
        if (_launcherSyncing) return;
        var generated = string.Join(Environment.NewLine, BuildLauncherConfigurationArguments());
        if (_configLaunchArguments == generated) return;
        _configLaunchArguments = generated;
        RaisePropertyChanged(nameof(ConfigLaunchArguments));
    }

    private void UpdateLauncherPreview()
    {
        var executable = ResolvePreviewExecutable();
        var workingDirectory = ResolvePreviewWorkingDirectory(executable);
        var actualArguments = BuildLauncherConfigurationArguments()
            .Where(a => !a.StartsWith(LauncherOptionPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        ConfigLauncherEffectiveCommand = string.Join(" ", new[] { QuotePreview(executable) }.Concat(actualArguments.Select(QuotePreview)));
        ConfigLauncherProcessSummary =
            $"Working directory: {workingDirectory} · UseShellExecute={ConfigLauncherUseShellExecute} · CreateNoWindow={ConfigLauncherCreateNoWindow} · " +
            $"WindowStyle={ConfigLauncherWindowStyle} · Redirect stdout={ConfigLauncherRedirectStandardOutput} · Redirect stderr={ConfigLauncherRedirectStandardError} · " +
            $"Capture redirected output={ConfigLauncherCaptureRedirectedOutput} · Hide managed windows={ConfigLauncherHideManagedWindows}";

        var warnings = new List<string>();
        if (ConfigLauncherUseShellExecute && (ConfigLauncherRedirectStandardOutput || ConfigLauncherRedirectStandardError))
            warnings.Add("UseShellExecute cannot be combined with redirected stdout/stderr.");
        if (ConfigLauncherExecutable == "Custom path" && string.IsNullOrWhiteSpace(ConfigLauncherCustomExecutablePath))
            warnings.Add("Custom executable is selected but no path is set.");
        if (ConfigLauncherWorkingDirectory == "Custom path" && string.IsNullOrWhiteSpace(ConfigLauncherCustomWorkingDirectory))
            warnings.Add("Custom working directory is selected but no path is set.");
        if (ConfigLauncherLog || ConfigLauncherStdout || ConfigLauncherFullStdOutLogOutput || ConfigLauncherAbsLog)
            warnings.Add("Players were given the wrong character every time PalServer started with -log, -stdout, -FullStdOutLogOutput or -abslog, and kept theirs when it started like a double-click. Use Like double-click unless you are diagnosing.");
        if (ConfigLauncherExecutable != "PalServer.exe")
            warnings.Add("Direct Shipping executable modes bypass PalServer.exe and are intended for diagnosis/testing.");

        ConfigLauncherWarning = string.Join(" ", warnings);
    }

    private string ResolvePreviewExecutable()
    {
        if (ConfigLauncherExecutable.Equals("Shipping-Cmd.exe", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(ConfigServerRoot, "Pal", "Binaries", "Win64", "PalServer-Win64-Shipping-Cmd.exe");
        if (ConfigLauncherExecutable.Equals("Shipping.exe", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(ConfigServerRoot, "Pal", "Binaries", "Win64", "PalServer-Win64-Shipping.exe");
        if (ConfigLauncherExecutable.Equals("Test-Cmd.exe", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(ConfigServerRoot, "Pal", "Binaries", "Win64", "PalServer-Win64-Test-Cmd.exe");
        if (ConfigLauncherExecutable.Equals("Test.exe", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(ConfigServerRoot, "Pal", "Binaries", "Win64", "PalServer-Win64-Test.exe");
        if (ConfigLauncherExecutable.Equals("Custom path", StringComparison.OrdinalIgnoreCase))
            return ExpandPreviewPath(ConfigLauncherCustomExecutablePath);
        return Path.Combine(ConfigServerRoot, "PalServer.exe");
    }

    private string ResolvePreviewWorkingDirectory(string executable)
    {
        if (ConfigLauncherWorkingDirectory.Equals("Executable folder", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(executable) ?? ConfigServerRoot;
        if (ConfigLauncherWorkingDirectory.Equals("Custom path", StringComparison.OrdinalIgnoreCase))
            return ExpandPreviewPath(ConfigLauncherCustomWorkingDirectory);
        return ConfigServerRoot;
    }

    private string ExpandPreviewPath(string value)
    {
        var logsRoot = string.IsNullOrWhiteSpace(ConfigServerRoot)
            ? string.Empty
            : Path.Combine(ConfigServerRoot, "Pal", "Saved", "Logs");
        return Environment.ExpandEnvironmentVariables(value ?? string.Empty)
            .Replace("%SERVERROOT%", ConfigServerRoot ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("%LOGSROOT%", logsRoot, StringComparison.OrdinalIgnoreCase)
            .Trim().Trim('"');
    }

    private string DefaultIdentityLogPath() =>
        string.IsNullOrWhiteSpace(ConfigServerRoot)
            ? @"%LOGSROOT%\Identity-Diagnostic.log"
            : Path.Combine(ConfigServerRoot, "Pal", "Saved", "Logs", "Identity-Diagnostic.log");

    private static Dictionary<string, string> ParseLauncherOptions(IEnumerable<string> arguments)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var argument in arguments)
        {
            if (!argument.StartsWith(LauncherOptionPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            var payload = argument[LauncherOptionPrefix.Length..];
            var separator = payload.IndexOf('=');
            if (separator < 0) values[payload] = "true";
            else values[payload[..separator]] = payload[(separator + 1)..];
        }
        return values;
    }

    private static string ReadLauncherOption(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

    private static bool ReadLauncherBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static bool HasExact(IEnumerable<string> arguments, string value) =>
        arguments.Any(a => a.Equals(value, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> SplitArgumentLines(string text) =>
        (text ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsKnownLauncherArgument(string argument)
    {
        var value = argument.Trim();
        return value.StartsWith("-port=", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-log", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-logformat=text", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-stdout", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-FullStdOutLogOutput", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("-abslog=", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-unattended", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-useperfthreads", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-NoAsyncLoadingThread", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-UseMultithreadForDS", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-NoMods", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHistoricalSuppressedArgument(string argument) =>
        argument.Equals("-unattended", StringComparison.OrdinalIgnoreCase) ||
        argument.Equals("-useperfthreads", StringComparison.OrdinalIgnoreCase) ||
        argument.Equals("-NoAsyncLoadingThread", StringComparison.OrdinalIgnoreCase) ||
        argument.Equals("-UseMultithreadForDS", StringComparison.OrdinalIgnoreCase) ||
        argument.StartsWith("-logformat=", StringComparison.OrdinalIgnoreCase);

    private static string QuotePreview(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "\"\"";
        return value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
    }
}
