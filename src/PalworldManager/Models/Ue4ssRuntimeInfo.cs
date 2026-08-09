namespace PalworldManager.Models;

/// <summary>
/// Immutable snapshot of the UE4SS runtime paths resolved for the configured Palworld server.
/// The ActiveModsRoot is the manager's authoritative path for the lifetime of the snapshot;
/// RuntimeModsRoot is independently parsed from UE4SS.log when available and is used to detect drift.
/// </summary>
public sealed record Ue4ssRuntimeInfo(
    string Win64Root,
    string Ue4ssRoot,
    string ModernModsRoot,
    string LegacyModsRoot,
    string ActiveModsRoot,
    string? RuntimeModsRoot,
    string DetectionMethod,
    bool HasUe4ssRoot,
    bool HasModernModsRoot,
    bool HasLegacyModsRoot,
    bool RuntimeVerified,
    bool RuntimeMatchesActiveRoot,
    string HealthState,
    string WarningMessage,
    string? RuntimeLogPath,
    IReadOnlyList<string> LoadedMods,
    int ActiveModDirectoryCount,
    int LegacyModDirectoryCount)
{
    public bool HasPathMismatch => RuntimeVerified && !RuntimeMatchesActiveRoot;
}
