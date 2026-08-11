namespace MystTiq.Core.Models;

public sealed record LinuxDistributionInfo(
    string Id,
    string VersionId,
    string PrettyName,
    string Kernel,
    string Architecture)
{
    public static LinuxDistributionInfo Unknown(string kernel, string architecture) =>
        new("unknown", "unknown", "Unknown Linux", kernel, architecture);
}
