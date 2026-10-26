// MystTiq v1.1.5.1: file reviewed for this release (2026-10-26).
namespace MystTiq.Desktop.Services;
using MystTiq.Core.Services;

/// <summary>
/// v1.1.2.0 (roadmap F-20): starts MystTiq when the owner signs in, in the tray (<see cref="TrayArgument"/>). On Windows
/// it is the current user's Run key (no administrator rights, nothing machine-wide); on Linux an XDG autostart entry in
/// the user's own config folder. The operating system's entry is the setting itself, so there is nothing else to keep
/// in step: it is off until the owner turns it on in Settings. Once MystTiq is running, its helper starts again and
/// resumes a server that was running when the computer went down (crash recovery; a server stopped on purpose stays
/// stopped).
/// </summary>
public sealed class StartWithSystem
{
    public const string TrayArgument = "--tray";
    public const string WindowsRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string WindowsValueName = "MystTiq";

    private readonly string windowsRunKey;
    private readonly string linuxAutostartFile;
    private readonly string executablePath;

    /// <param name="windowsRunKey">The HKCU key to write; tests pass their own so the real Run key is never touched.</param>
    /// <param name="linuxAutostartFile">The autostart entry to write; tests pass one in a temporary folder.</param>
    public StartWithSystem(string? windowsRunKey = null, string? linuxAutostartFile = null, string? executablePath = null)
    {
        this.windowsRunKey = windowsRunKey ?? WindowsRunKey;
        this.linuxAutostartFile = linuxAutostartFile ?? DefaultLinuxAutostartFile();
        this.executablePath = executablePath ?? Environment.ProcessPath ?? string.Empty;
    }

    // v1.1.5.0 (roadmap W-2): a portable copy writes nothing to Windows (Settings says why the option is missing).
    public static bool IsSupported => (OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) && !PortableMode.IsActive;

    public static bool StartedInTray(IEnumerable<string>? args) =>
        args?.Any(a => string.Equals(a, TrayArgument, StringComparison.OrdinalIgnoreCase)) == true;

    /// <summary>The Run key value: the quoted executable and <see cref="TrayArgument"/>.</summary>
    public static string WindowsCommand(string executable) => $"\"{executable}\" {TrayArgument}";

    /// <summary>The XDG autostart entry (Exec quoted per the Desktop Entry specification).</summary>
    public static string LinuxDesktopEntry(string executable) =>
        "[Desktop Entry]\n" +
        "Type=Application\n" +
        "Name=MystTiq\n" +
        "Comment=MystTiq Palworld Server Manager (starts in the tray)\n" +
        $"Exec=\"{executable.Replace("\\", "\\\\").Replace("\"", "\\\"")}\" {TrayArgument}\n" +
        "Terminal=false\n" +
        "X-GNOME-Autostart-enabled=true\n";

    public static string DefaultLinuxAutostartFile()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(configHome))
            configHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(configHome, "autostart", "mysttiq.desktop");
    }

    /// <summary>On when the entry exists and starts this copy of MystTiq.</summary>
    public bool IsEnabled()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(windowsRunKey);
                return key?.GetValue(WindowsValueName) is string value && value.Length > 0;
            }
            if (OperatingSystem.IsLinux()) return File.Exists(linuxAutostartFile);
        }
        catch { /* unreadable: shown as off */ }
        return false;
    }

    /// <summary>The entry points at a different copy (an older install): turning it on again repairs it.</summary>
    public bool PointsElsewhere()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(windowsRunKey);
                return key?.GetValue(WindowsValueName) is string value && value.Length > 0
                    && !string.Equals(value, WindowsCommand(executablePath), StringComparison.OrdinalIgnoreCase);
            }
            if (OperatingSystem.IsLinux() && File.Exists(linuxAutostartFile))
                return File.ReadAllText(linuxAutostartFile) != LinuxDesktopEntry(executablePath);
        }
        catch { /* unreadable */ }
        return false;
    }

    public void SetEnabled(bool enabled)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) throw new InvalidOperationException("MystTiq could not tell where it is installed.");
        if (OperatingSystem.IsWindows())
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(windowsRunKey, writable: true);
            if (enabled) key.SetValue(WindowsValueName, WindowsCommand(executablePath));
            else key.DeleteValue(WindowsValueName, throwOnMissingValue: false);
            return;
        }
        if (OperatingSystem.IsLinux())
        {
            if (enabled)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(linuxAutostartFile)!);
                var temp = linuxAutostartFile + ".tmp";
                File.WriteAllText(temp, LinuxDesktopEntry(executablePath));
                File.Move(temp, linuxAutostartFile, overwrite: true);
            }
            else if (File.Exists(linuxAutostartFile)) File.Delete(linuxAutostartFile);
            return;
        }
        throw new PlatformNotSupportedException("Starting with the computer is available on Windows and Linux.");
    }
}
