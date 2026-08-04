namespace PalworldManager.Models;

public enum ModCompatibilityState
{
    Unknown,
    Compatible,
    Attention,
    Conflict,
    Failed
}

public sealed class ModCompatibilityResult
{
    public string Package { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public IReadOnlyList<string> MissingDependencies { get; init; } = [];
    public IReadOnlyList<string> SatisfiedDependencies { get; init; } = [];
    public IReadOnlyList<string> RedundantDependencies { get; init; } = [];
    public IReadOnlyList<string> Conflicts { get; init; } = [];
    public IReadOnlyList<string> SharedFiles { get; init; } = [];
    public string DependencyStatus { get; init; } = "Unknown";
    public string ConflictStatus { get; init; } = "Unknown";
    public string VersionStatus { get; init; } = "Unknown";
    public string AvailableVersion { get; init; } = "";
    public bool UpdateAvailable { get; init; }
    public ModCompatibilityState OverallState { get; init; } = ModCompatibilityState.Unknown;
    public string OverallStatus { get; init; } = "Unknown";
    public string Details { get; init; } = "";
    public DateTime CheckedAt { get; init; } = DateTime.Now;
}

public sealed class ModCompatibilitySummary
{
    public required IReadOnlyList<ModCompatibilityResult> Results { get; init; }
    public int Updates => Results.Count(result => result.UpdateAvailable);
    public int Conflicts => Results.Count(result => result.OverallState == ModCompatibilityState.Conflict);
    public int MissingDependencies => Results.Count(result => result.MissingDependencies.Count > 0);
    public int RedundantDependencies => Results.Sum(result => result.RedundantDependencies.Count);
    public int Compatible => Results.Count(result => result.OverallState == ModCompatibilityState.Compatible);
    public int Attention => Results.Count(result => result.OverallState == ModCompatibilityState.Attention);
}
