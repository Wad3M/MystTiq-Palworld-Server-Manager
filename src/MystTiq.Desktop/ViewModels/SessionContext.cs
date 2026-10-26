// MystTiq v1.1.5.1: file reviewed for this release (2026-10-26).
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

/// <summary>
/// v1.0.8.0 (roadmap C-0): what every page's ViewModel shares with the window: the API client, the active tab's connection
/// and sign-in, the busy state, the page shown, the error-recording command runner and the text lookup. The window owns
/// the state; a page reads it through here instead of reaching into MainWindowViewModel, so pages can move out of that
/// file one at a time (C-1 to C-9) and be tested on their own.
/// </summary>
public sealed class SessionContext
{
    private readonly Func<ConnectionProfile?> _profile;
    private readonly Func<ConnectionProfile?> _selectedProfile;
    private readonly Func<object?> _activeTab;
    private readonly Func<ConnectionProfile> _buildProfile;
    private readonly Func<bool> _isConnected;
    private readonly Func<bool> _serverIsRunning;
    private readonly Func<string?> _busyReason;
    private readonly Action<string?> _setBusyReason;
    private readonly Func<string?> _bearerToken;
    private readonly Func<bool> _isBusy;
    private readonly Action<bool> _setBusy;
    private readonly Func<NavigationPage> _page;
    private readonly Action<Func<Task>, string> _runUi;

    public SessionContext(IMystTiqApiClient api, Func<ConnectionProfile?> profile, Func<string?> bearerToken,
        Func<bool> isBusy, Action<bool> setBusy, Func<NavigationPage> page, Action<Func<Task>, string> runUi, Func<ConnectionProfile?> selectedProfile, Func<object?> activeTab, Func<ConnectionProfile> buildProfile, Func<bool> isConnected,
        Func<string?> busyReason, Action<string?> setBusyReason, Func<bool> serverIsRunning)
    {
        _serverIsRunning = serverIsRunning;
        _busyReason = busyReason;
        _setBusyReason = setBusyReason;
        _buildProfile = buildProfile;
        _isConnected = isConnected;
        _selectedProfile = selectedProfile;
        _activeTab = activeTab;
        Api = api;
        _profile = profile;
        _bearerToken = bearerToken;
        _isBusy = isBusy;
        _setBusy = setBusy;
        _page = page;
        _runUi = runUi;
    }

    public IMystTiqApiClient Api { get; }

    /// <summary>The active tab's connection, or null when it has none (a wizard tab, an incomplete profile).</summary>
    public ConnectionProfile? Profile => _profile();
    /// <summary>The active tab's saved profile (what most commands send with), or null.</summary>
    public ConnectionProfile? SelectedProfile => _selectedProfile();
    /// <summary>Whether the active tab is this computer's own MystTiq (the local default profile).</summary>
    public bool IsLocalProfile => SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id;
    /// <summary>The tab now shown: a reply that arrives after the user switched tabs is dropped by comparing with it.</summary>
    public object? ActiveTab => _activeTab();
    /// <summary>The active tab's connection built from its fields; throws with the reason when they are incomplete.</summary>
    public ConnectionProfile BuildProfile() => _buildProfile();
    /// <summary>Whether the active tab is connected to its MystTiq service.</summary>
    public bool IsConnected => _isConnected();
    /// <summary>Whether the active tab's Palworld server is running (v1.0.8.8).</summary>
    public bool ServerIsRunning => _serverIsRunning();
    /// <summary>What the busy state is for, shown in the status bar while it lasts.</summary>
    public string? BusyReason { get => _busyReason(); set => _setBusyReason(value); }
    public string? BearerToken => _bearerToken();
    public bool IsBusy { get => _isBusy(); set => _setBusy(value); }
    public NavigationPage Page => _page();

    /// <summary>Runs a command's work on the UI thread; an error is recorded and shown, never lost (as the window's RunUi).</summary>
    public void RunUi(Func<Task> work, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(work))] string name = "") => _runUi(work, name);

    /// <summary>A view-model text in the chosen language.</summary>
    public static string Tr(string? text) => Localizer.Instance.Translate(text);
}

/// <summary>v1.0.8.0 (roadmap C-0): a page's own ViewModel. The window creates it with the shared session and tells it when its page is shown.</summary>
public abstract class PageViewModel : ViewModelBase
{
    protected PageViewModel(SessionContext session, NavigationPage page)
    {
        Session = session;
        Page = page;
    }

    protected SessionContext Session { get; }
    public NavigationPage Page { get; }
    public bool IsShown => Session.Page == Page;

    /// <summary>Called by the window each time this page is navigated to.</summary>
    public virtual void OnShown() { }

    /// <summary>
    /// v1.0.8.9: tells this page's bindings that one of its properties changed. The window raising
    /// <c>nameof(Page.Property)</c> on itself reached no binding (the property is the page's), which left the Automation
    /// page's "no rules" card, the Workspace discovery status and the browser view's address stale.
    /// </summary>
    internal void Raise(string propertyName) => RaisePropertyChanged(propertyName);
}
