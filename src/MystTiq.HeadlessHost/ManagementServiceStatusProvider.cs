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
