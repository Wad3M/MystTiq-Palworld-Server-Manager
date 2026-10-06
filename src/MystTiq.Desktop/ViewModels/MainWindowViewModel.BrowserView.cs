// MystTiq v1.0.5.0: file reviewed for this release (2026-10-06).
namespace MystTiq.Desktop.ViewModels;

// v1.0.3.0 (roadmap W-1): the read-only browser view lives at the same address as the API, under /web.
public sealed partial class MainWindowViewModel
{
    public string BrowserViewUrlVerbatim => SelectedProfile is { } profile ? new Uri(profile.BaseAddress, "/web").ToString() : "—";
}
