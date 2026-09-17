using Avalonia;
using Avalonia.Media;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.ViewModels;

// Per-tab connection state for true multi-tab support. Before this, "tabs" were just
// a bookmark list over a single global connection (SelectedProfile/BearerToken/etc. on
// MainWindowViewModel) -- switching which one was "selected" tore down and replaced the whole
// connection. Now each open tab owns its own copy of that same state, and MainWindowViewModel's
// SelectedProfile/BearerToken/ManagementApiConnected/ConnectionState/IsBusy/ServerIsRunning
// properties become pass-through accessors over whichever TabSession is ActiveTab, so every
// existing call site that reads/writes those properties keeps working unchanged, now scoped to
// the active tab instead of the whole app.
public sealed class TabSession : ViewModelBase
{
    private ConnectionProfile? _profile;
    private string _profileName = string.Empty;
    private string _serverUrl = string.Empty;
    private string _certificateSha256 = string.Empty;
    private string _bearerToken = string.Empty;
    private string _targetServerId = string.Empty;
    private bool _managementApiConnected;
    private string _connectionState = "Not connected";
    private bool _isBusy;
    private bool _serverIsRunning;
    private int _wizardStep = 1;
    private string _connectionKind = string.Empty;
    private bool _isNewServerSetupFlow;
    private string _worldSource = string.Empty;
    private string _serverRoot = string.Empty;
    private string _duplicateInstallWarning = string.Empty;

    public Guid Id { get; } = Guid.NewGuid();

    public ConnectionProfile? Profile
    {
        get => _profile;
        set
        {
            if (!SetField(ref _profile, value)) return;
            RaisePropertyChanged(nameof(ConnectionKindText));
            RefreshAccentVisual();
        }
    }
    public string ProfileName { get => _profileName; set => SetField(ref _profileName, value); }
    public string ServerUrl { get => _serverUrl; set => SetField(ref _serverUrl, value); }
    public string CertificateSha256 { get => _certificateSha256; set => SetField(ref _certificateSha256, value); }
    public string BearerToken { get => _bearerToken; set => SetField(ref _bearerToken, value); }
    // v0.7.63.0: editor-side mirror of Profile.ServerId (see ConnectionProfile.cs) -- which server
    // on a multi-server fleet host this tab targets. Empty means "this host's default server."
    public string TargetServerId { get => _targetServerId; set => SetField(ref _targetServerId, value); }
    public bool ManagementApiConnected { get => _managementApiConnected; set => SetField(ref _managementApiConnected, value); }
    // Bound directly by the tab bar's status dot, so it needs to update live even while this
    // tab is in the background (not the ActiveTab) and nothing else is re-raising it.
    public string ConnectionState
    {
        get => _connectionState;
        set { if (SetField(ref _connectionState, value)) RaisePropertyChanged(nameof(StatusDotColorKey)); }
    }
    // v0.7.76.0: both now also re-raise StatusDotColorKey -- see that property's own updated
    // comment for why it needs to react to these two, not just ConnectionState.
    public bool IsBusy { get => _isBusy; set { if (SetField(ref _isBusy, value)) RaisePropertyChanged(nameof(StatusDotColorKey)); } }
    public bool ServerIsRunning { get => _serverIsRunning; set { if (SetField(ref _serverIsRunning, value)) RaisePropertyChanged(nameof(StatusDotColorKey)); } }

    // v0.7.74.0: reported live -- switching tabs kept whatever page was currently on screen instead
    // of returning to whatever THIS tab was last viewing (e.g. leaving the Backups page up after
    // switching from a tab that was on it, even for a tab you'd last left on the Dashboard). Plain
    // auto-property, not SetField-backed -- nothing binds to this directly; MainWindowViewModel's
    // SelectedPage setter writes it on every navigation and its ActiveTab setter reads it back.
    public NavigationPage LastPage { get; set; } = NavigationPage.Dashboard;

    // v0.7.6.0: the "Set Up New Server" wizard's step, moved here from a shared
    // MainWindowViewModel field -- it was previously a single app-wide value, so two tabs both
    // mid-setup at once (or one abandoned mid-wizard) would silently share and corrupt each
    // other's step whenever either was advanced or the user switched between them.
    public int WizardStep { get => _wizardStep; set => SetField(ref _wizardStep, value); }

    // v0.7.13.0: the "Set Up New Server" wizard's Step 0 choice ("Local" or "Remote", empty = not
    // yet chosen). Per-tab for the same reason WizardStep is -- two tabs could each be mid-setup
    // with a different choice at once.
    public string ConnectionKind { get => _connectionKind; set => SetField(ref _connectionKind, value); }

    // v0.7.81.0: distinguishes a genuine "Set Up New Server" install wizard from "Connect to Local/
    // Remote Server" -- both used to share the exact same wizard steps with no real difference in
    // behavior, just different framing text (direct live feedback: "it should not be trying to
    // connect to a previously installed version"). Per-tab for the same reason as WizardStep/
    // ConnectionKind above.
    public bool IsNewServerSetupFlow { get => _isNewServerSetupFlow; set => SetField(ref _isNewServerSetupFlow, value); }

    // v0.7.81.0: the new-server wizard's "Clone an Existing Server" / "Set Up a New World" /
    // "Import a World" choice ("Clone"/"NewWorld"/"Import", empty = not yet chosen). Only ever set
    // when IsNewServerSetupFlow is true.
    public string WorldSource { get => _worldSource; set => SetField(ref _worldSource, value); }

    // v0.7.31.0: cached last-known install directory for this tab's server, reported by the server
    // itself (GetServerDistributionStatusAsync) during routine refresh -- a ConnectionProfile alone
    // has no concept of install location, only a host:port address, so this can only ever be known
    // *after* successfully connecting, not before. Used by MainWindowViewModel to detect two tabs
    // pointed at what's actually the same physical server install, regardless of host/port.
    public string ServerRoot { get => _serverRoot; set => SetField(ref _serverRoot, value); }

    // Non-empty when this tab's ServerRoot matches another open tab's -- surfaced as a warning
    // banner rather than blocking the connection outright, since the match can only be discovered
    // after the connection already succeeded.
    public string DuplicateInstallWarning
    {
        get => _duplicateInstallWarning;
        set { if (SetField(ref _duplicateInstallWarning, value)) RaisePropertyChanged(nameof(HasDuplicateInstallWarning)); }
    }
    public bool HasDuplicateInstallWarning => !string.IsNullOrWhiteSpace(DuplicateInstallWarning);

    // Tab bar status dot color. A semantic KEY, not a hex literal -- resolved to the live theme
    // brush by SemanticStatusColorConverter so it follows accent-theme/Light-Dark switches instead
    // of staying locked to its original color (v0.7.63.0 theme-system bugfix).
    //
    // v0.7.76.0 bugfix: reported live -- this previously equated "Connected" (the MANAGEMENT API
    // reachable and authenticated) with green, full stop, so a tab whose PalServer was genuinely
    // stopped still showed the same green dot as one that was running, as long as MystTiq itself
    // could still reach and talk to that machine's API (which it can regardless of whether the game
    // server process is up). Green now specifically means the game server is running, matching what
    // the dot sits next to (the server's own name, not the API connection's own health) --
    // ConnectionState's failure/connecting states still take priority since those mean nothing about
    // ServerIsRunning is even known yet.
    public string StatusDotColorKey => ConnectionState switch
    {
        "Connecting…" => "Amber",
        // v0.7.23.0: "Incompatible API version"/"Needs bearer token" are the two new specific
        // failure reasons RefreshTabLightweightAsync can now report (previously both collapsed
        // into "Connection failed") -- still real connection failures, still the red dot.
        "Connection failed" or "Invalid profile" or "Incompatible API version" or "Needs bearer token" => "Red",
        "Connected" => IsBusy ? "Amber" : (ServerIsRunning ? "Green" : "Red"),
        _ => "Muted"
    };

    // Shown under the server name in the tab bar. Empty while the tab is mid-setup (Profile is
    // null, e.g. right after "Set Up New Server") rather than defaulting to "Remote".
    //
    // v0.7.89.0 bug fix: reported live -- a brand-new, genuinely local second server (registered
    // via the New Server wizard) was labeled "Remote" here, purely because this used to compare
    // against the literal default profile Id instead of actually checking the address. Any
    // loopback-addressed profile is local, whether or not it happens to be "default".
    public string ConnectionKindText => Profile is null
        ? string.Empty
        : System.Net.IPAddress.TryParse(Profile.BaseAddress.Host, out var address) && System.Net.IPAddress.IsLoopback(address)
            ? "Local"
            : "Remote";

    // Each open tab gets its own timer instance instead of the app sharing one -- wired up by
    // MainWindowViewModel (which owns the tick handler and knows how to tell an active tab's full
    // refresh apart from a background tab's lightweight poll), started/stopped as tabs open/close.
    public Avalonia.Threading.DispatcherTimer? Timer { get; set; }

    // v0.7.52.0: Per-Tab Color Coding (item 2) -- resolves Profile.AccentColorKey to the actual
    // brush ThemeApplier already writes into Application.Current.Resources (e.g.
    // "BlueAccentBorderBrush", from the same derivation engine v0.7.48.0 built). A plain
    // {Binding}-driven value like this does NOT auto-refresh when the resource's underlying VALUE
    // changes on a theme switch the way {DynamicResource} does in XAML -- only when this property
    // itself re-raises change notification, which RefreshAccentVisual does, called from Profile's
    // own setter and by MainWindowViewModel over every open tab right after each ThemeApplier.Apply.
    private IBrush _accentBrush = Brushes.Transparent;
    public IBrush AccentBrush { get => _accentBrush; private set => SetField(ref _accentBrush, value); }

    // v0.7.77.0: companion to AccentBrush, same resource family (ThemeApplier's own
    // "{name}AccentGlowShadowLow", already computed per DerivedColorName -- see ThemeApplier.cs).
    // The border-color fix alone (v0.7.63.0/v0.7.74.0) read as too subtle live: every one of those
    // bordered elements also carries a glow BoxShadow that stayed fixed blue, since XAML's
    // BoxShadow="{DynamicResource ...}" shorthand cannot data-bind (a disclosed limitation from the
    // v0.7.48.0 theme pass). Resolving the BoxShadows value here in C# and exposing it as a plain
    // property sidesteps that entirely -- it's a normal {Binding}, not a DynamicResource, so the
    // shorthand-string restriction never applies.
    private BoxShadows _accentGlowShadow;
    public BoxShadows AccentGlowShadow { get => _accentGlowShadow; private set => SetField(ref _accentGlowShadow, value); }

    public void RefreshAccentVisual()
    {
        var key = Profile?.AccentColorKey;
        var name = string.IsNullOrWhiteSpace(key) ? "Blue" : key;

        AccentBrush = Application.Current?.TryGetResource($"{name}AccentBorderBrush", null, out var borderResource) == true && borderResource is IBrush brush
            ? brush
            : Brushes.Transparent;

        AccentGlowShadow = Application.Current?.TryGetResource($"{name}AccentGlowShadowLow", null, out var glowResource) == true && glowResource is BoxShadows glow
            ? glow
            : default;

        // StatusDotColorKey's own VALUE doesn't change on a theme switch, but the brush it resolves
        // to (via SemanticStatusColorConverter) does -- the converter only re-runs when the bound
        // property re-raises change notification, so this nudge is required the same way it is for
        // AccentBrush/AccentGlowShadow above.
        RaisePropertyChanged(nameof(StatusDotColorKey));
    }
}
