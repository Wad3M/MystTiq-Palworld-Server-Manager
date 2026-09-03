using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

public interface IWindowsServiceManager
{
    Task<WindowsServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<WindowsServiceInstallResult> InstallAsync(string executablePath, string configurationPath, bool startNow, CancellationToken cancellationToken = default);
    Task<bool> UninstallAsync(CancellationToken cancellationToken = default);
}

[SupportedOSPlatform("windows")]
public sealed class WindowsServiceManager : IWindowsServiceManager
{
    public const string ServiceName = "MystTiqPalworld";
    public const string DisplayName = "MystTiq Palworld Server Manager";

    public WindowsServiceManager()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows Service management requires Windows.");
    }

    public async Task<WindowsServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var query = await RunScAsync(["queryex", ServiceName], cancellationToken, allowFailure: true);
        if (query.ExitCode == 1060 || query.Output.Contains("FAILED 1060", StringComparison.OrdinalIgnoreCase))
            return new(ServiceName, false, WindowsServiceState.NotInstalled, null, "MystTiq Windows Service is not installed.");

        var stateMatch = Regex.Match(query.Output, @"STATE\s*:\s*(\d+)\s+([A-Z_]+)");
        var pidMatch = Regex.Match(query.Output, @"PID\s*:\s*(\d+)");
        var state = stateMatch.Success && int.TryParse(stateMatch.Groups[1].Value, out var raw)
            ? Enum.IsDefined(typeof(WindowsServiceState), raw) ? (WindowsServiceState)raw : WindowsServiceState.Unknown
            : WindowsServiceState.Unknown;
        int? pid = pidMatch.Success && int.TryParse(pidMatch.Groups[1].Value, out var parsedPid) && parsedPid > 0 ? parsedPid : null;
        return new(ServiceName, true, state, pid, stateMatch.Success ? stateMatch.Groups[2].Value : "UNKNOWN");
    }

    public async Task<WindowsServiceInstallResult> InstallAsync(string executablePath, string configurationPath, bool startNow, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);
        executablePath = Path.GetFullPath(executablePath);
        configurationPath = Path.GetFullPath(configurationPath);
        if (!File.Exists(executablePath)) throw new FileNotFoundException("MystTiq host executable was not found.", executablePath);

        var existing = await GetStatusAsync(cancellationToken);
        if (existing.Installed)
            await RunScAsync(["stop", ServiceName], cancellationToken, allowFailure: true);

        var binPath = $"\"{executablePath}\" service-run --config \"{configurationPath}\"";
        if (!existing.Installed)
            await RunScAsync(["create", ServiceName, "binPath=", binPath, "start=", "auto", "DisplayName=", DisplayName], cancellationToken);
        else
            await RunScAsync(["config", ServiceName, "binPath=", binPath, "start=", "auto", "DisplayName=", DisplayName], cancellationToken);

        await RunScAsync(["failure", ServiceName, "reset=", "300", "actions=", "restart/10000/restart/10000/restart/10000"], cancellationToken);
        await RunScAsync(["description", ServiceName, "Persistent MystTiq management service for Palworld."], cancellationToken);

        if (startNow) await RunScAsync(["start", ServiceName], cancellationToken);
        return new(true, ServiceName, executablePath, startNow, startNow ? "MystTiq Windows Service installed and started." : "MystTiq Windows Service installed.");
    }

    public async Task<bool> UninstallAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);
        if (!status.Installed) return true;
        await RunScAsync(["stop", ServiceName], cancellationToken, allowFailure: true);
        var result = await RunScAsync(["delete", ServiceName], cancellationToken, allowFailure: true);
        return result.ExitCode == 0;
    }

    private static async Task<(int ExitCode, string Output)> RunScAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool allowFailure = false)
    {
        var psi = new ProcessStartInfo { FileName = "sc.exe", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to launch sc.exe.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = (await stdout) + (await stderr);
        if (!allowFailure && process.ExitCode != 0) throw new InvalidOperationException($"sc.exe failed ({process.ExitCode}): {output.Trim()}");
        return (process.ExitCode, output);
    }
}
