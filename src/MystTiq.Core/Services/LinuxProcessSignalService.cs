using System.Runtime.InteropServices;

namespace MystTiq.Core.Services;

public interface IProcessSignalService
{
    bool IsAlive(int processId);
    bool TryTerminate(int processId);
    bool TryKill(int processId);
}

public sealed class LinuxProcessSignalService : IProcessSignalService
{
    private const int SigTerm = 15;
    private const int SigKill = 9;

    public bool IsAlive(int processId)
    {
        EnsureLinux();
        return processId > 0 && Directory.Exists($"/proc/{processId}");
    }

    public bool TryTerminate(int processId)
    {
        EnsureLinux();
        return TrySignal(processId, SigTerm);
    }

    public bool TryKill(int processId)
    {
        EnsureLinux();
        return TrySignal(processId, SigKill);
    }

    private static bool TrySignal(int processId, int signal)
    {
        if (processId <= 0)
            return false;

        if (kill(processId, signal) == 0)
            return true;

        // ESRCH means the process already disappeared, which is effectively success.
        return Marshal.GetLastPInvokeError() == 3;
    }

    private static void EnsureLinux()
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("POSIX process signalling requires Linux.");
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int sig);
}
