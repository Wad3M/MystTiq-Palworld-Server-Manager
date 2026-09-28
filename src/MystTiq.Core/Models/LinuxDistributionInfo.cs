// MystTiq v0.9.1.0: file reviewed for this release (2026-09-28).
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
