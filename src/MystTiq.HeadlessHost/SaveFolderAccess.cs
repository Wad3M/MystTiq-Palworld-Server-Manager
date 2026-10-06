// MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.0.4: whether this process may replace the save folder. Found live on 2026-10-05: the owner's SaveGames folder (and the
/// decoded Level.sav.json in it) belong to the Administrators group, created by something that ran as administrator, while
/// the Desktop's MystTiq service runs as the signed-in user without administrator rights. Windows then lets it read and add
/// files there but not rename or replace the folder, so every restore failed with "Access to the path …\SaveGames is
/// denied" (2026-10-01), which looked like a file in use. A restore moves the whole folder aside, which needs DELETE on it;
/// this asks Windows for exactly that, without changing anything.
/// </summary>
public static class SaveFolderAccess
{
    private const uint Delete = 0x00010000;
    private const uint ShareAll = 0x00000007;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    /// <summary>True when the folder may be moved or replaced by this process (always true off Windows or when it is missing).</summary>
    public static bool CanReplace(string folder)
    {
        if (!OperatingSystem.IsWindows() || !Directory.Exists(folder)) return true;
        using var handle = CreateFile(folder, Delete, ShareAll, IntPtr.Zero, OpenExisting, BackupSemantics, IntPtr.Zero);
        // ERROR_ACCESS_DENIED (5) is the permission problem; anything else (in use, for example) is not this check's to report.
        return !handle.IsInvalid || Marshal.GetLastWin32Error() != 5;
    }

    /// <summary>True when the file may be overwritten by this process (true when it does not exist).</summary>
    public static bool CanWrite(string file)
    {
        if (!File.Exists(file)) return true;
        try { using var stream = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); return true; }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return true; }
    }

    public const string NoAccessMessage =
        "Windows does not let MystTiq replace the save folder: it belongs to administrators, and MystTiq runs without administrator rights. On the Backups page, click Fix Save Folder Access and confirm the Windows prompt, then restore again.";
}
