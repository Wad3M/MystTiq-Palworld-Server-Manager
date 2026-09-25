namespace MystTiq.Core.Services;

public enum DiskLevel { Ok, Warning, Critical }

// v0.7.102.0: the one definition of "low disk space", used by both the Doctor and the Alert Center.
// They used to disagree: the Alert Center raised a pinned Critical below a percentage of the backup
// volume (5.4%, 51.9 GB free) while the Doctor, which only looked at absolute figures, called the same
// disk a Pass. Now:
//   Critical  free space under 2 GiB, or at or under the configured low-disk percentage (Alert Center rule)
//   Warning   free space under 5 GiB
public static class DiskSpaceRules
{
    public const double CriticalFreeGiB = 2;
    public const double WarningFreeGiB = 5;
    private const double GiB = 1024d * 1024d * 1024d;

    public static double FreePercent(long freeBytes, long totalBytes) => totalBytes <= 0 ? 100 : 100d * freeBytes / totalBytes;

    // criticalPercent is the Alert Center's low-disk percentage when that rule is enabled, else null.
    public static DiskLevel Evaluate(long freeBytes, long totalBytes, double? criticalPercent)
    {
        var freeGiB = freeBytes / GiB;
        if (freeGiB < CriticalFreeGiB) return DiskLevel.Critical;
        if (criticalPercent is > 0 && totalBytes > 0 && FreePercent(freeBytes, totalBytes) <= criticalPercent.Value) return DiskLevel.Critical;
        return freeGiB < WarningFreeGiB ? DiskLevel.Warning : DiskLevel.Ok;
    }
}
