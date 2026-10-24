// MystTiq v1.1.5.0: file reviewed for this release (2026-10-24).
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

/// <summary>
/// Pages that, opened while the app is busy (start-up, another refresh), wait for it to finish and then load, instead of
/// skipping their load. v0.7.100.0: Map and Players. v1.1.5.0 (owner 2026-10-24: "the world selected should populate
/// these settings without having to run the server"): Configuration, which MystTiq can open on at start (the page it was
/// left on) and then showed "Not loaded" until the owner clicked Load; the service reads PalWorldSettings.ini from disk
/// whether the server runs or not.
/// </summary>
public static class PageLoad
{
    public static bool WaitsWhileBusy(NavigationPage page) => page is NavigationPage.Map or NavigationPage.Players or NavigationPage.Configuration;
}
