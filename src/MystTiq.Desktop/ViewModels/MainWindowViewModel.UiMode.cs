// MystTiq v1.1.6.0: file reviewed for this release (2026-10-26).
using System.Windows.Input;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v1.1.5.0 (roadmap V-2): the Simple / Advanced switch in the title bar and in Settings (see SimpleMode for what each shows).
public sealed partial class MainWindowViewModel
{
    private bool _isSimpleMode;
    private ICommand? _toggleUiModeCommand;

    public bool IsSimpleMode
    {
        get => _isSimpleMode;
        set
        {
            if (_isSimpleMode == value) return;
            _isSimpleMode = value;
            try { _displayPreferences.SaveUiMode(value ? SimpleMode.Simple : SimpleMode.Advanced); }
            catch (Exception ex) { DisplayPreferencesStatusText = Localizer.Instance["uiMode.notRemembered"] + " " + ex.Message; }
            RaiseUiModeDependents();
            RebuildRibbonGroups();
            // Simple has no Advanced settings view, and a page Simple hides is left for the Dashboard.
            if (value)
            {
                Configuration.SetConfigurationView(true);
                if (SimpleMode.Hides(SelectedPage)) Navigate(nameof(NavigationPage.Dashboard));
            }
        }
    }

    public bool IsAdvancedMode => !_isSimpleMode;

    /// <summary>The title-bar switch's label: the mode in use.</summary>
    public string UiModeLabel => Localizer.Instance[_isSimpleMode ? "ui.mode.simple" : "ui.mode.advanced"];

    public ICommand ToggleUiModeCommand => _toggleUiModeCommand ??= new RelayCommand(() => IsSimpleMode = !IsSimpleMode);

    /// <summary>Settings' choice: 0 Simple, 1 Advanced.</summary>
    public IReadOnlyList<string> UiModeLabels => [Localizer.Instance["ui.mode.simple"], Localizer.Instance["ui.mode.advanced"]];
    public int SelectedUiModeIndex
    {
        get => _isSimpleMode ? 0 : 1;
        set { if (value is 0 or 1) IsSimpleMode = value == 0; }
    }

    /// <summary>Automation's New Rule card: Admins, in Advanced.</summary>
    public bool ShowAutomationRuleEditor => CanManageAdmin && !_isSimpleMode;
    public bool ShowAutomationSimpleHint => CanManageAdmin && _isSimpleMode;

    private void LoadUiMode()
    {
        _isSimpleMode = _displayPreferences.LoadUiMode() == SimpleMode.Simple;
        if (_isSimpleMode) Configuration.SetConfigurationView(true);
    }

    private void RaiseUiModeDependents()
    {
        RaisePropertyChanged(nameof(IsSimpleMode));
        RaisePropertyChanged(nameof(IsAdvancedMode));
        RaisePropertyChanged(nameof(UiModeLabel));
        // Not UiModeLabels: Settings' picker reloaded and wrote Advanced back (the switch did nothing, 2026-10-23).
        RaisePropertyChanged(nameof(SelectedUiModeIndex));
        RaisePropertyChanged(nameof(ShowAutomationRuleEditor));
        RaisePropertyChanged(nameof(ShowAutomationSimpleHint));
    }
}
