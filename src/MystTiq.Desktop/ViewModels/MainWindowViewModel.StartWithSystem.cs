// MystTiq v1.1.5.0: file reviewed for this release (2026-10-23).
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v1.1.2.0 (roadmap F-20): Settings > Appearance & startup, "Start MystTiq when I sign in". Off until the owner turns it on.
public sealed partial class MainWindowViewModel
{
    private StartWithSystem? _startWithSystem;
    private StartWithSystem StartWithSystemEntry => _startWithSystem ??= new StartWithSystem();

    /// <summary>Tests point it at their own Run key or autostart file.</summary>
    public void UseStartWithSystem(StartWithSystem entry) { _startWithSystem = entry; RaisePropertyChanged(nameof(StartWithSystemEnabled)); RaisePropertyChanged(nameof(StartWithSystemPointsElsewhere)); }

    public bool StartWithSystemSupported => StartWithSystem.IsSupported;

    private string _startWithSystemStatusText = string.Empty;
    public string StartWithSystemStatusText { get => _startWithSystemStatusText; private set => SetField(ref _startWithSystemStatusText, value); }

    public bool StartWithSystemEnabled
    {
        get => StartWithSystemSupported && StartWithSystemEntry.IsEnabled();
        set
        {
            if (!StartWithSystemSupported) return;
            try
            {
                StartWithSystemEntry.SetEnabled(value);
                StartWithSystemStatusText = value
                    ? Localizer.Instance["settings.startWithSystem.on"]
                    : Localizer.Instance["settings.startWithSystem.off"];
            }
            catch (Exception ex) { StartWithSystemStatusText = Localizer.Instance["settings.startWithSystem.failed"] + " " + ex.Message; }
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(StartWithSystemPointsElsewhere));
        }
    }

    // v1.1.5.0 (roadmap W-2): a portable copy says so, and where it keeps everything.
    public bool IsPortableCopy => MystTiq.Core.Services.PortableMode.IsActive;
    public string PortableDataText => MystTiq.Core.Services.PortableMode.DataRoot is { } root
        ? string.Format(Localizer.Instance["settings.portable.where"], root)
        : string.Empty;

    /// <summary>On, but for a different copy of MystTiq (an older install folder).</summary>
    public bool StartWithSystemPointsElsewhere => StartWithSystemSupported && StartWithSystemEntry.PointsElsewhere();
}
