// MystTiq v1.0.0.0: file reviewed for this release (2026-09-30).
namespace MystTiq.Core.Platform;

public enum MystTiqPlatform
{
    Unknown = 0,
    Windows = 1,
    Linux = 2
}

public static class MystTiqPlatformDetector
{
    public static MystTiqPlatform Current =>
        OperatingSystem.IsWindows() ? MystTiqPlatform.Windows :
        OperatingSystem.IsLinux() ? MystTiqPlatform.Linux :
        MystTiqPlatform.Unknown;
}
