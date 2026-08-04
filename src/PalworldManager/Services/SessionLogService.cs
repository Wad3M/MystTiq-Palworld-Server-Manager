using System.Globalization;

namespace PalworldManager.Services;

public sealed class SessionLogService : IDisposable
{
    private readonly object sync = new();
    private readonly StreamWriter writer;
    private bool disposed;

    public SessionLogService(string logsRoot)
    {
        var managerRoot = Path.Combine(logsRoot, "MystTiqPalworldServer");
        Directory.CreateDirectory(managerRoot);
        CurrentLogPath = Path.Combine(managerRoot, $"Server_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
        writer = new StreamWriter(new FileStream(CurrentLogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false))
        {
            AutoFlush = true
        };
        Write("STATUS", "MANAGER", "MystTiq Palworld Server logging session started.");
    }

    public string CurrentLogPath { get; }

    public string Write(string severity, string category, string message)
    {
        var safeMessage = (message ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        var line = $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}] [{severity.ToUpperInvariant(),-8}] [{category.ToUpperInvariant()}] {safeMessage}";
        lock (sync)
        {
            if (!disposed)
                writer.WriteLine(line);
        }
        return line;
    }

    public static (string Severity, string Category) Classify(string message)
    {
        var text = message ?? string.Empty;
        var lower = text.ToLowerInvariant();
        var category = lower.Contains("rcon") ? "RCON"
            : lower.Contains("rest") || lower.Contains("/v1/api/") ? "REST API"
            : lower.Contains("ue4ss") ? "UE4SS"
            : lower.Contains("mod") || lower.Contains("loaded successfully") || lower.Contains("admincommands") ? "MODS"
            : lower.Contains("backup") ? "BACKUP"
            : lower.Contains("steamcmd") ? "STEAMCMD"
            : lower.Contains("player") || lower.Contains("connected") || lower.Contains("disconnected") ? "PLAYERS"
            : lower.Contains("manager") ? "MANAGER"
            : "SERVER";

        var explicitSuccess = lower.Contains("loaded successfully") ||
                              lower.Contains("initialized successfully") ||
                              lower.Contains("registered successfully") ||
                              lower.Contains("started successfully");

        var severity = lower.Contains("fatal") || lower.Contains("critical") ? "CRITICAL"
            : explicitSuccess ? "STATUS"
            : lower.Contains("error") || lower.Contains("exception") || lower.Contains("failed") ? "ERROR"
            : lower.Contains("warning") || lower.Contains("deprecated") || lower.Contains("timeout") || lower.Contains("retry") ? "WARNING"
            : lower.Contains("started") || lower.Contains("stopped") || lower.Contains("ready") || lower.Contains("connected") || lower.Contains("saved") || lower.Contains("exited") ? "STATUS"
            : "INFO";

        return (severity, category);
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            writer.Dispose();
        }
    }
}
