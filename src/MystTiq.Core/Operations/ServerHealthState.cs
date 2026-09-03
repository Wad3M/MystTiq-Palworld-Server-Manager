namespace MystTiq.Core.Operations;

// Formalizes the ad-hoc "READY" / "ATTENTION" / transitioning strings the
// Dashboard already computes (MainWindowViewModel.DashboardHealthText,
// IsHealthGlowRed/Amber/Green) into a real Core-level contract other
// features and future providers can produce/consume without re-deriving
// the same three-state logic from scratch.
public enum ServerHealthState
{
    Unknown = 0,
    Ready = 1,
    Degraded = 2,
    Attention = 3,
    Transitioning = 4
}

public sealed record ServerHealthSnapshot(ServerHealthState State, string Detail, DateTimeOffset ObservedAt);
