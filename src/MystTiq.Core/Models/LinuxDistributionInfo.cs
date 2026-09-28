// MystTiq v0.8.26.0: file reviewed for this release (2026-09-27).
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
