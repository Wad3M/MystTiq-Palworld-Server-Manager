// MystTiq v1.1.5.0: file reviewed for this release (2026-10-24).
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v1.0.0.1 (requested 2026-09-30): the Dashboard's stuck-start panel and addresses line.
public sealed partial class MainWindowViewModel
{
    private bool _isStartupStuck;
    public bool IsStartupStuck
    {
        get => _isStartupStuck;
        private set { if (SetField(ref _isStartupStuck, value)) RaisePropertyChanged(nameof(ShowStuckStartPanel)); }
    }

    public bool ShowStuckStartPanel => IsStartupStuck || Mods.ModSafeStartStatus is not null;
    public bool IsModSafeStartRunning => Mods.ModSafeStartStatus is { IsRunning: true };
    public string StuckStartStatusText => StuckStartText.Describe(Mods.ModSafeStartStatus);
}
