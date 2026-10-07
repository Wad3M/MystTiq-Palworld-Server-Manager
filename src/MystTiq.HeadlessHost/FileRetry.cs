// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.6.0: a file another process holds for a moment (an antivirus scan of a file just written, an indexer) made the
/// final step of a save edit fail ("Unable to remove the file to be replaced"), seen once in the Pal box smoke; the edit
/// rolled back safely, but for nothing. These retry a few times, briefly, on a sharing violation only; a denied access
/// is not retried (it is a permission, not a moment).
/// </summary>
public static class FileRetry
{
    public const int Attempts = 6;
    public static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(500);

    public static void Replace(string replacement, string destination)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { File.Replace(replacement, destination, null, true); return; }
            catch (IOException) when (attempt < Attempts) { Thread.Sleep(Pause); }
        }
    }

    /// <summary>Deletes a folder, retrying a held file briefly. Returns the reason it stayed, or null when it is gone.</summary>
    public static string? TryDeleteDirectory(string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); return null; }
            catch (UnauthorizedAccessException) { return "denied"; }
            catch (IOException) when (attempt < Attempts) { Thread.Sleep(Pause); }
            catch (IOException) { return "in use"; }
        }
    }
}
