// MystTiq v1.1.5.0: file reviewed for this release (2026-10-23).
namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.6.0: a file another process holds for a moment (an antivirus scan of a file just written, an indexer) made the
/// final step of a save edit fail ("Unable to remove the file to be replaced"), seen once in the Pal box smoke; the edit
/// rolled back safely, but for nothing. These retry a few times, briefly, while the file is held; a denied access is not
/// retried (it is a permission, not a moment).
/// v1.0.7.0 (roadmap Q-5): only a held or locked file is retried. Before, every IOException was (a missing folder, a full
/// disk), which only delayed the same failure by 2.5 s.
/// </summary>
public static class FileRetry
{
    public const int Attempts = 6;
    public static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(500);

    // Windows error codes that clear once the other process lets go: ERROR_SHARING_VIOLATION, ERROR_LOCK_VIOLATION,
    // ERROR_UNABLE_TO_REMOVE_REPLACED, ERROR_UNABLE_TO_MOVE_REPLACEMENT and ERROR_UNABLE_TO_MOVE_REPLACEMENT_2.
    private static readonly int[] HeldCodes = [32, 33, 1175, 1176, 1177];

    /// <summary>True for an error that means another process holds the file for now.</summary>
    public static bool IsHeld(IOException ex) => HeldCodes.Contains(ex.HResult & 0xFFFF);

    public static void Replace(string replacement, string destination) => Replace(replacement, destination, null);

    /// <summary>Replaces <paramref name="destination"/>, keeping the file it replaced as <paramref name="backup"/> when given.</summary>
    public static void Replace(string replacement, string destination, string? backup)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { File.Replace(replacement, destination, backup, true); return; }
            catch (IOException ex) when (IsHeld(ex) && attempt < Attempts) { Thread.Sleep(Pause); }
        }
    }

    public static void Move(string source, string destination)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { File.Move(source, destination, true); return; }
            catch (IOException ex) when (IsHeld(ex) && attempt < Attempts) { Thread.Sleep(Pause); }
        }
    }

    /// <summary>Deletes a folder, retrying a held file briefly. Returns the reason it stayed, or null when it is gone.</summary>
    public static string? TryDeleteDirectory(string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); return null; }
            catch (UnauthorizedAccessException) { return "denied"; }
            catch (IOException ex) when (IsHeld(ex) && attempt < Attempts) { Thread.Sleep(Pause); }
            catch (IOException ex) when (IsHeld(ex)) { return "in use"; }
            catch (IOException ex) { return ex.Message; }
        }
    }
}
