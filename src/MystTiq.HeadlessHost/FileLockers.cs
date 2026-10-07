// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
using System.Runtime.InteropServices;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.0.4: which programs have files open, through the Windows Restart Manager (the same API Windows uses to say "the file
/// is open in …"). A restore on 2026-10-01 failed twice with "Access to the path …\SaveGames is denied" and nothing said
/// what held it; now the failure names it. Windows only; elsewhere, and for a folder merely shown in an Explorer window
/// (a folder handle, not a file), the list is empty.
/// </summary>
public static class FileLockers
{
    private const int CchRmMaxAppName = 255;
    private const int CchRmMaxSvcName = 63;
    private const int ErrorMoreData = 234;
    private const int MaximumFiles = 4000;

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxAppName + 1)] public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxSvcName + 1)] public string strServiceShortName;
        public int ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)] public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint pSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames, uint nApplications,
        [In] RmUniqueProcess[]? rgApplications, uint nServices, string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo,
        [In, Out] RmProcessInfo[]? rgAffectedApps, ref uint lpdwRebootReasons);

    /// <summary>The programs holding any file under the folder open ("name (pid N)"), empty when none or unknown.</summary>
    public static IReadOnlyList<string> UnderFolder(string folder)
    {
        if (!OperatingSystem.IsWindows() || !Directory.Exists(folder)) return [];
        try
        {
            // The world files first: those are the ones a restore has to move.
            var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .OrderBy(f => f.Contains(Path.DirectorySeparatorChar + "backup" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .Take(MaximumFiles).ToArray();
            return files.Length == 0 ? [] : ForFiles(files);
        }
        catch { return []; }
    }

    public static IReadOnlyList<string> ForFiles(string[] files)
    {
        if (!OperatingSystem.IsWindows() || files.Length == 0) return [];
        if (RmStartSession(out var session, 0, Guid.NewGuid().ToString("N")) != 0) return [];
        try
        {
            if (RmRegisterResources(session, (uint)files.Length, files, 0, null, 0, null) != 0) return [];
            uint needed = 0, count = 0, reasons = 0;
            var status = RmGetList(session, out needed, ref count, null, ref reasons);
            if (status == 0 || needed == 0) return [];
            if (status != ErrorMoreData) return [];
            var infos = new RmProcessInfo[needed];
            count = needed;
            if (RmGetList(session, out needed, ref count, infos, ref reasons) != 0) return [];
            return infos.Take((int)count)
                .Select(i => $"{(string.IsNullOrWhiteSpace(i.strAppName) ? "a program" : i.strAppName)} (pid {i.Process.dwProcessId})")
                .Distinct().ToArray();
        }
        catch { return []; }
        finally { RmEndSession(session); }
    }
}
