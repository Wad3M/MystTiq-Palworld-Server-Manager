using System.Diagnostics;

namespace PalworldManager.Services;

/// <summary>
/// Platform-specific server lifecycle operations that are not part of the
/// platform-neutral session/lifecycle policy.
/// </summary>
public interface IServerPlatformOperations
{
    string ResolveServerExecutable();
    ProcessStartInfo CreateServerStartInfo(string executable, string arguments);
    Task ApplyPostLaunchWindowPolicyAsync(CancellationToken token);
    bool HasDetectedServerProcess(int ownedProcessId);
    void KillProcessTree(Process process);
    void KillDetectedServerProcesses(int ownedProcessId);
}
