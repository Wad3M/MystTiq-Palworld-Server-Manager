using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed record ManagementServiceStatus(
    string UnitName,
    bool Installed,
    bool Enabled,
    string ActiveState,
    string SubState,
    int? MainProcessId,
    string Detail);

public interface IManagementServiceStatusProvider
{
    Task<ManagementServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

public sealed class LinuxManagementServiceStatusProvider(ILinuxServiceManager serviceManager) : IManagementServiceStatusProvider
{
    public async Task<ManagementServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = await serviceManager.GetStatusAsync(cancellationToken);
        return new(status.UnitName, status.Installed, status.Enabled, status.ActiveState, status.SubState, status.MainProcessId, status.Detail);
    }
}

// Honest answer for api-run --desktop-sidecar: that process genuinely is not running as a
// Windows Service, so it correctly always reports Installed=false rather than querying SCM.
public sealed class WindowsStandaloneManagementServiceStatusProvider : IManagementServiceStatusProvider
{
    public Task<ManagementServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ManagementServiceStatus(
            "MystTiqPalworld",
            false,
            false,
            "active",
            "windows-sidecar",
            Environment.ProcessId,
            "Standalone Windows headless API process."));
}

// v0.6.3.0: used by the Windows `service-run` path (real SCM-backed status), unlike the
// hardcoded-false WindowsStandaloneManagementServiceStatusProvider above.
public sealed class WindowsSystemServiceStatusProvider(IWindowsServiceManager serviceManager) : IManagementServiceStatusProvider
{
    public async Task<ManagementServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = await serviceManager.GetStatusAsync(cancellationToken);
        var enabled = status.State is not MystTiq.Core.Models.WindowsServiceState.NotInstalled;
        return new(status.ServiceName, status.Installed, enabled, status.State.ToString(), status.State.ToString(), status.ProcessId, status.Detail);
    }
}
