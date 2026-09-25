namespace MystTiq.Desktop.Models;

// v0.6.4.0 unified diagnostics. State mirrors Core's DiagnosticState int values exactly
// (Pass=0,Warning=1,Fail=2,Starting=3,Skipped=4,Unknown=5) -- see NetworkDiagnosticModels.cs's
// own comment for why Unknown was appended rather than inserted first.
//
// v0.7.105.0: implements INotifyPropertyChanged for one new client-side-only field, FixConfirmed --
// same technique PalworldSimpleSettingItem already uses for its own live-bound state. Never sent to
// or received from the server; every fresh finding (a report refresh, or the one RecheckDiagnosticAsync
// swaps in after a fix) starts a new instance with it false, so confirmation never survives a refresh.
public sealed class DiagnosticFindingDto : System.ComponentModel.INotifyPropertyChanged
{
    private bool _fixConfirmed;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public int State { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string? ActionKind { get; set; }
    public bool ActionSupported { get; set; }
    public string? UnavailableReason { get; set; }
    public DateTimeOffset ObservedAt { get; set; }

    // v0.7.105.0: gates the Fix button, the same "tick a box before the button works" pattern Backup
    // Restore already uses (BackupRestoreConfirmed) -- named as a desired follow-up since v0.7.102.0.
    public bool FixConfirmed
    {
        get => _fixConfirmed;
        set { if (_fixConfirmed == value) return; _fixConfirmed = value; PropertyChanged?.Invoke(this, new(nameof(FixConfirmed))); }
    }

    public string StateText => State switch { 0 => "PASS", 1 => "WARNING", 2 => "FAIL", 3 => "STARTING", 4 => "SKIPPED", _ => "UNKNOWN" };
    public bool CanFix => ActionKind is not null;
    // v0.7.103.0: says what the button will do. "Fix Automatically" on every finding hid that one fix creates a
    // schedule and another downloads and installs a server.
    public string FixButtonText => ActionKind switch
    {
        "create-backup-rule" => "Create Nightly Backup Rule",
        "create-backup-root" => "Create Backup Folder",
        "install-distribution" => "Install / Repair Server Files",
        _ => "Fix Automatically"
    };
    // v0.7.105.0: what the confirmation checkbox says, specific to what the fix actually does -- the same
    // reason FixButtonText stopped being one generic label in v0.7.103.0.
    public string FixConfirmText => ActionKind switch
    {
        "create-backup-rule" => "I understand this creates a nightly backup rule",
        "create-backup-root" => "I understand this creates the backup folder",
        "install-distribution" => "I understand this downloads and installs server files",
        _ => "I understand this runs the fix"
    };

    // v0.7.79.0: drives the state badge's color on the Server Doctor page (direct request: "Pass
    // can be highlighted in green or red"). Starting/Skipped/Unknown fall through to the badge's
    // own neutral default rather than getting their own color -- only Pass/Warning/Fail are
    // meaningfully color-codeable states.
    public bool IsPass => State == 0;
    public bool IsWarning => State == 1;
    public bool IsFail => State == 2;
    public bool IsOtherState => State is not (0 or 1 or 2);
    // Many findings report an identical Evidence and Recommendation string when nothing needs
    // fixing (e.g. both "Steam installation detected.") -- showing both lines is pure redundancy,
    // part of the same "make use of the spacing" complaint.
    public bool ShowRecommendation => !string.IsNullOrWhiteSpace(Recommendation) &&
        !string.Equals(Recommendation, Evidence, StringComparison.Ordinal);
}

public sealed class DiagnosticsReportDto
{
    public int OverallHealth { get; set; }
    public string OverallHealthDetail { get; set; } = string.Empty;
    public int Passed { get; set; }
    public int Warnings { get; set; }
    public int Failures { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public List<DiagnosticFindingDto> Findings { get; set; } = [];

    // Mirrors ServerHealthState (Unknown=0, Ready=1, Degraded=2, Attention=3, Transitioning=4).
    public string OverallHealthText => OverallHealth switch { 1 => "READY", 2 => "DEGRADED", 3 => "ATTENTION", 4 => "TRANSITIONING", _ => "UNKNOWN" };
}

public sealed record HeadlessDiagnosticFixResultDto(bool Success, string Message);
