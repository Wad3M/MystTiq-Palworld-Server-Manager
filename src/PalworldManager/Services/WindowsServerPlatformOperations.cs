using PalworldManager.Models;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;

namespace PalworldManager.Services;

/// <summary>
/// Windows implementation of PalServer executable resolution, launch policy,
/// console-window hiding, and forced-process cleanup.
/// </summary>
public sealed class WindowsServerPlatformOperations : IServerPlatformOperations
{
    private const int SwHide = 0;
    private readonly AppSettings settings;
    private readonly ServerProcessDiscoveryService processDiscovery;
    private readonly ServerPlatformProfile profile;

    public WindowsServerPlatformOperations(
        AppSettings settings,
        ServerProcessDiscoveryService processDiscovery,
        ServerPlatformProfile profile)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.processDiscovery = processDiscovery ?? throw new ArgumentNullException(nameof(processDiscovery));
        this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    public string ResolveServerExecutable()
    {
        var candidates = profile.ServerExecutableRelativePaths
            .Select(relativePath => Path.GetFullPath(Path.Combine(settings.ServerRoot, relativePath)))
            .ToArray();

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException(
                "No Palworld server executable was found.",
                string.Join(Environment.NewLine, candidates));
    }

    public ProcessStartInfo CreateServerStartInfo(string executable, string arguments) =>
        new()
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? settings.ServerRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

    public async Task ApplyPostLaunchWindowPolicyAsync(CancellationToken token)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            token.ThrowIfCancellationRequested();

            foreach (var name in profile.ProcessNames)
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    using (process)
                    {
                        try
                        {
                            process.Refresh();
                            var handle = process.MainWindowHandle;
                            if (handle != IntPtr.Zero)
                                ShowWindow(handle, SwHide);
                        }
                        catch
                        {
                            // Process may be starting or exiting.
                        }
                    }
                }
            }

            await Task.Delay(500, token).ConfigureAwait(false);
        }
    }

    public bool HasDetectedServerProcess(int ownedProcessId)
    {
        foreach (var name in profile.ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        if (process.Id == ownedProcessId && !process.HasExited)
                            return true;

                        var path = string.Empty;
                        try { path = process.MainModule?.FileName ?? string.Empty; } catch { }

                        if (processDiscovery.IsPathInsideServerRoot(path) && !process.HasExited)
                            return true;
                    }
                    catch { }
                }
            }
        }

        return false;
    }

    public void KillProcessTree(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (!process.HasExited)
            process.Kill(entireProcessTree: true);
    }

    public void KillDetectedServerProcesses(int ownedProcessId)
    {
        foreach (var name in profile.ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        var path = string.Empty;
                        try { path = process.MainModule?.FileName ?? string.Empty; } catch { }

                        var belongsToThisServer =
                            process.Id == ownedProcessId ||
                            processDiscovery.IsPathInsideServerRoot(path);

                        if (belongsToThisServer && !process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // Best effort. ServerService performs verification after kill attempts.
                    }
                }
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);
}
