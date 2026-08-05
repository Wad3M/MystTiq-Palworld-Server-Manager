using System.Reflection;

namespace PalworldManager.Services.Infrastructure;

/// <summary>
/// Provides the application version generated from Directory.Build.props.
/// Runtime code should consume this class instead of embedding version strings.
/// </summary>
public static class ApplicationVersion
{
    private static readonly Lazy<string> ResolvedVersion = new(ResolveVersion);

    public static string Version => ResolvedVersion.Value;
    public static string DisplayVersion => $"v{Version}";
    public static string ProductName => "MystTiq Palworld Server Manager";
    public static string WindowTitle => $"{ProductName} {DisplayVersion}";
    public static string UserAgent => $"MystTiqPalworldServer/{Version}";

    private static string ResolveVersion()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var metadataSeparator = informational.IndexOf('+');
            return metadataSeparator >= 0
                ? informational[..metadataSeparator]
                : informational;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
