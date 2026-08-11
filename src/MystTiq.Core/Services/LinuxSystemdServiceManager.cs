using System.Diagnostics;
using System.Runtime.Versioning;
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

public interface ILinuxServiceManager
{
    Task<LinuxServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<LinuxServiceInstallResult> InstallAsync(string sourceExecutable, string serviceUser, bool startNow, CancellationToken cancellationToken = default);
    Task<bool> UninstallAsync(CancellationToken cancellationToken = default);
}

[SupportedOSPlatform("linux")]
public sealed class LinuxSystemdServiceManager : ILinuxServiceManager
{
    public const string UnitName = "mysttiq-palworld.service";
    public const string UnitPath = "/etc/systemd/system/" + UnitName;
    public const string InstallDirectory = "/opt/mysttiq/bin";
    public const string InstalledExecutable = InstallDirectory + "/mysttiq-server";

    private readonly IServerPathProfile paths;

    public LinuxSystemdServiceManager(IServerPathProfile paths)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("systemd service management requires Linux.");
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public async Task<LinuxServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(UnitPath))
            return new(UnitName, false, false, LinuxServiceState.NotInstalled, "not-installed", "not-installed", null, "MystTiq systemd unit is not installed.");

        var show = await RunSystemctlAsync(
            ["show", UnitName, "--property=LoadState,ActiveState,SubState,MainPID,UnitFileState", "--no-pager"],
            cancellationToken, allowFailure: true);

        var values = ParseProperties(show.StandardOutput);
        var loadState = Get(values, "LoadState", "unknown");
        var activeState = Get(values, "ActiveState", "unknown");
        var subState = Get(values, "SubState", "unknown");
        var unitFileState = Get(values, "UnitFileState", "unknown");
        var enabled = unitFileState is "enabled" or "enabled-runtime";

        int? pid = null;
        if (int.TryParse(Get(values, "MainPID", "0"), out var parsedPid) && parsedPid > 0)
            pid = parsedPid;

        var state = activeState switch
        {
            "active" => LinuxServiceState.Active,
            "activating" => LinuxServiceState.Activating,
            "deactivating" => LinuxServiceState.Deactivating,
            "failed" => LinuxServiceState.Failed,
            "inactive" => LinuxServiceState.Inactive,
            _ => LinuxServiceState.Unknown
        };

        return new(UnitName, loadState == "loaded", enabled, state, activeState, subState, pid,
            $"LoadState={loadState}; UnitFileState={unitFileState}");
    }

    public async Task<LinuxServiceInstallResult> InstallAsync(
        string sourceExecutable,
        string serviceUser,
        bool startNow,
        CancellationToken cancellationToken = default)
    {
        EnsureRoot();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceUser);

        sourceExecutable = Path.GetFullPath(sourceExecutable);
        if (!File.Exists(sourceExecutable))
            throw new FileNotFoundException("Headless host executable was not found.", sourceExecutable);

        Directory.CreateDirectory(InstallDirectory);
        if (!Path.GetFullPath(sourceExecutable).Equals(
                Path.GetFullPath(InstalledExecutable),
                StringComparison.Ordinal))
        {
            File.Copy(sourceExecutable, InstalledExecutable, overwrite: true);
        }
        File.SetUnixFileMode(
            InstalledExecutable,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        Directory.CreateDirectory(paths.ManagerRuntimeRoot);
        await RunCommandAsync(
            "/usr/bin/chown",
            [$"{serviceUser}:{serviceUser}", paths.ManagerRuntimeRoot],
            cancellationToken);

        File.WriteAllText(UnitPath, BuildUnit(serviceUser));

        await RunSystemctlAsync(["daemon-reload"], cancellationToken);
        await RunSystemctlAsync(["enable", UnitName], cancellationToken);

        var started = false;
        if (startNow)
        {
            await RunSystemctlAsync(["restart", UnitName], cancellationToken);
            started = true;
        }

        var status = await GetStatusAsync(cancellationToken);
        return new(true, UnitName, InstalledExecutable, UnitPath, status.Enabled, started,
            startNow
                ? "MystTiq systemd service installed, enabled, and started."
                : "MystTiq systemd service installed and enabled. It was not started.");
    }

    public async Task<bool> UninstallAsync(CancellationToken cancellationToken = default)
    {
        EnsureRoot();

        if (File.Exists(UnitPath))
        {
            await RunSystemctlAsync(["disable", "--now", UnitName], cancellationToken, allowFailure: true);
            File.Delete(UnitPath);
            await RunSystemctlAsync(["daemon-reload"], cancellationToken);
            await RunSystemctlAsync(["reset-failed"], cancellationToken, allowFailure: true);
        }

        return !File.Exists(UnitPath);
    }

    private static string BuildUnit(string serviceUser) =>
        "[Unit]\n" +
        "Description=MystTiq Palworld Headless Server Manager\n" +
        "Documentation=https://github.com/Wad3M/MystTiq-Palworld-Server-Manager\n" +
        "After=network-online.target\n" +
        "Wants=network-online.target\n" +
        "\n" +
        "[Service]\n" +
        "Type=simple\n" +
        $"User={serviceUser}\n" +
        "WorkingDirectory=/opt/mysttiq\n" +
        $"ExecStart={InstalledExecutable} service-run\n" +
        "ExecStop=/bin/kill -s TERM $MAINPID\n" +
        "Restart=on-failure\n" +
        "RestartSec=10\n" +
        "StartLimitIntervalSec=300\n" +
        "StartLimitBurst=5\n" +
        "TimeoutStopSec=60\n" +
        "KillMode=process\n" +
        "NoNewPrivileges=true\n" +
        "\n" +
        "[Install]\n" +
        "WantedBy=multi-user.target\n";

    private static void EnsureRoot()
    {
        if (Environment.UserName != "root")
            throw new UnauthorizedAccessException("This command modifies systemd and must be run with sudo/root privileges.");
    }


    private static async Task RunCommandAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to launch {executable}.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"{executable} {string.Join(' ', arguments)} failed with exit code {process.ExitCode}: {stderr.Trim()}");
    }

    private static async Task<CommandResult> RunSystemctlAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool allowFailure = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/systemctl",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to launch systemctl.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (!allowFailure && process.ExitCode != 0)
            throw new InvalidOperationException(
                $"systemctl {string.Join(' ', arguments)} failed with exit code {process.ExitCode}: {stderr.Trim()}");

        return new(process.ExitCode, stdout, stderr);
    }

    private static Dictionary<string, string> ParseProperties(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var index = line.IndexOf('=');
            if (index <= 0) continue;
            result[line[..index]] = line[(index + 1)..];
        }
        return result;
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
}
