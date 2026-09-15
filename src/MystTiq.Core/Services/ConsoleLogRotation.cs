namespace MystTiq.Core.Services;

// v0.7.64.0: MystTiq-PalServer-Console.log had no size cap or rotation on either writer that
// appends to it (HeadlessConsoleLogWriter's structured lines, and WindowsServerLifecycleService's
// raw PalServer stdout/stderr capture) -- File.AppendAllText forever, on a file this app's own
// design explicitly expects to live on a long-running production service (the reference Linux VM
// this project tests against had 5+ days of continuous uptime; low disk space on the reference
// Windows machine has separately been flagged as worth attention). Both writers now call
// RotateIfNeeded immediately before appending. Shared here (MystTiq.Core) rather than duplicated,
// since one writer lives in MystTiq.Core and the other in MystTiq.HeadlessHost, which references
// Core but not the reverse.
public static class ConsoleLogRotation
{
    // 25 MB keeps the file well within "open comfortably in Notepad/the app's own Console page"
    // territory while still holding many hours of a busy server's combined MystTiq+PalServer output.
    public const long MaxBytes = 25 * 1024 * 1024;

    // Renames the current file to "<name>.1<ext>" (overwriting any prior ".1", so exactly one
    // rotated generation is kept -- "the last time it filled up," not a deep history) once it
    // crosses MaxBytes, so the very next append starts a fresh file. Checking size-then-renaming
    // right before an append is inherently racy between the two independent writers that call this
    // (no shared lock spans both classes), but the failure mode of that race is at worst one extra
    // or slightly-late rotation of a log file -- never corruption -- so it is accepted rather than
    // adding cross-class coordination for a non-critical stream.
    public static void RotateIfNeeded(string logPath)
    {
        try
        {
            var info = new FileInfo(logPath);
            if (!info.Exists || info.Length < MaxBytes) return;

            var rotatedPath = Path.Combine(
                Path.GetDirectoryName(logPath) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(logPath)}.1{Path.GetExtension(logPath)}");

            if (File.Exists(rotatedPath)) File.Delete(rotatedPath);
            File.Move(logPath, rotatedPath);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
