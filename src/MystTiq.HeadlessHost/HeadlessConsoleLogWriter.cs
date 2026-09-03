using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// Structured lines for the shared Live Console log, in the same
// "[timestamp] [LEVEL   ] [CATEGORY] [SUBCATEGORY] message" shape the console page already
// colors and filters by. Any service can write into this stream; it is the same physical file
// PalServer's redirected stdout/stderr and the lifecycle service's own status lines land in, so
// the console shows one coherent narrative instead of raw process output alone.
public sealed class HeadlessConsoleLogWriter
{
    private readonly IServerPathProfile paths;
    private readonly object gate = new();

    public HeadlessConsoleLogWriter(IServerPathProfile paths) => this.paths = paths;

    public void Write(string level, string category, string subcategory, string message)
    {
        try
        {
            var logDirectory = paths.LogsRoot;
            try { Directory.CreateDirectory(logDirectory); }
            catch
            {
                logDirectory = Path.Combine(paths.ManagerRuntimeRoot, "logs");
                Directory.CreateDirectory(logDirectory);
            }
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level.PadRight(8)}] [{category}] [{subcategory}] {message}";
            lock (gate)
                File.AppendAllText(Path.Combine(logDirectory, "MystTiq-PalServer-Console.log"), line + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Info(string category, string subcategory, string message) => Write("INFO", category, subcategory, message);
    public void Status(string category, string subcategory, string message) => Write("STATUS", category, subcategory, message);
    public void Error(string category, string subcategory, string message) => Write("ERROR", category, subcategory, message);
}
