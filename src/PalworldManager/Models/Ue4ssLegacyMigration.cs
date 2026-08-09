namespace PalworldManager.Models;

public sealed record Ue4ssLegacyMigrationPreview(
    string LegacyRoot,
    string ActiveRoot,
    IReadOnlyList<string> LegacyOnlyMods,
    IReadOnlyList<string> AlreadyPresentMods,
    IReadOnlyList<string> SkippedRuntimeComponents,
    bool IsMigrationRequired)
{
    public int CandidateCount => LegacyOnlyMods.Count + AlreadyPresentMods.Count;
}

public sealed record Ue4ssLegacyMigrationResult(
    string LegacyRoot,
    string ActiveRoot,
    int CopiedModCount,
    int CopiedFileCount,
    int ConflictCount,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> Warnings);
