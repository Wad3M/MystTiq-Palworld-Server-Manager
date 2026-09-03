using System.Text.Json;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessActivityLogService
{
    private readonly object gate = new();
    private readonly string activityPath;
    private readonly string auditPath;

    public HeadlessActivityLogService(IServerPathProfile paths)
    {
        var root = Path.Combine(paths.ManagerRuntimeRoot, "logs");
        Directory.CreateDirectory(root);
        activityPath = Path.Combine(root, "MystTiq-Activity.log");
        auditPath = Path.Combine(root, "MystTiq-Audit.jsonl");
    }

    public string ActivityPath => activityPath;
    public string AuditPath => auditPath;

    public void Record(string severity, string category, string action, string detail, string? actor = null)
    {
        var now = DateTimeOffset.UtcNow;
        var safeDetail = (detail ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        var line = $"[{now:O}] [{severity.ToUpperInvariant()}] [{category}] {action} — {safeDetail}";
        var audit = JsonSerializer.Serialize(new
        {
            timestampUtc = now,
            severity,
            category,
            action,
            detail = safeDetail,
            actor = actor ?? "local-management-api"
        });

        try
        {
            lock (gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(activityPath)!);
                File.AppendAllText(activityPath, line + Environment.NewLine);
                File.AppendAllText(auditPath, audit + Environment.NewLine);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public HeadlessActivityLogSnapshot GetTail(int requestedLines)
    {
        var count = Math.Clamp(requestedLines, 10, 500);
        try
        {
            var lines = File.Exists(activityPath)
                ? File.ReadLines(activityPath).TakeLast(count).ToArray()
                : Array.Empty<string>();
            return new HeadlessActivityLogSnapshot(true, Path.GetFileName(activityPath), lines, DateTimeOffset.UtcNow,
                lines.Length == 0 ? "MystTiq activity log is ready; no recorded activity yet." : $"Showing the newest {lines.Length} MystTiq activity event(s).");
        }
        catch (Exception ex)
        {
            return new HeadlessActivityLogSnapshot(false, Path.GetFileName(activityPath), [], DateTimeOffset.UtcNow,
                "Unable to read MystTiq activity log: " + ex.Message);
        }
    }
}

public sealed record HeadlessActivityLogSnapshot(
    bool Available,
    string FileName,
    IReadOnlyList<string> Lines,
    DateTimeOffset ObservedAt,
    string Detail);
