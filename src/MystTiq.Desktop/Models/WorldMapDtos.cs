namespace MystTiq.Desktop.Models;

// v0.6.16.0: screen-ready map point, computed once in the ViewModel (auto-fit scaling over the
// currently-online players' real location_x/location_y), not a server contract -- matches the
// existing convention MetricHistoryPoint.CpuGraphHeight already establishes for this codebase
// (compute screen-ready values where the data is assembled, not via an XAML value converter).
public sealed record PlayerMapPointDto(string Name, double CanvasX, double CanvasY);
