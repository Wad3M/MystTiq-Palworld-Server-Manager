namespace PalworldManager.Models;

public enum MystTiqReleaseComparison
{
    Unknown,
    UpToDate,
    UpdateAvailable,
    NewerThanPublicRelease
}

public sealed record MystTiqReleaseCheckResult(
    string InstalledVersion,
    string LatestVersion,
    string LatestTag,
    string ReleaseUrl,
    DateTimeOffset? PublishedAt,
    MystTiqReleaseComparison Comparison,
    string Detail)
{
    public bool UpdateAvailable => Comparison == MystTiqReleaseComparison.UpdateAvailable;
}
