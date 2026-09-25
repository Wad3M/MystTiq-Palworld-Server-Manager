using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessCrashAndSaveToolsService
{
    private const int MaximumLogFiles = 12;
    private const int MaximumLogLines = 4000;
    private const int MaximumSaveFiles = 5000;
    public const int MaximumCrashReports = 20;
    private readonly IServerPathProfile paths;
    private readonly HeadlessActivityLogService activity;
    private readonly string historyRoot;
    // v0.8.9.0: one analysis at a time. The crash-recovery observer and the crash-report watcher can both ask for one, and
    // each marks its findings as reported; running them together could report the same crash twice.
    private readonly object analyzeGate = new();

    public HeadlessCrashAndSaveToolsService(IServerPathProfile paths, HeadlessActivityLogService activity)
    {
        this.paths = paths;
        this.activity = activity;
        historyRoot = Path.Combine(paths.ManagerRuntimeRoot, "crash-analyzer");
        Directory.CreateDirectory(historyRoot);
    }

    // v0.7.97.0: findings now come from the known-signature catalog (cause + fixes per signature,
    // one signature per line, mods named in the evidence) and are marked new or already reported
    // relative to earlier analyses, so re-running Analyze on the same logs does not re-alarm.
    // installedModNames is optional: without it the analysis simply names no mods.
    public HeadlessCrashAnalysisSnapshot Analyze(IReadOnlyCollection<string>? installedModNames = null)
    {
        lock (analyzeGate) return AnalyzeLocked(installedModNames);
    }

    // v0.8.9.0: where Unreal writes its crash report folders for this server.
    public string CrashReportsRoot => Path.Combine(paths.ServerRoot, "Pal", "Saved", "Crashes");

    // The newest crash report folders that hold a readable crash context, newest first.
    public IReadOnlyList<UnrealCrashReport> ReadCrashReports()
    {
        var root = CrashReportsRoot;
        if (!Directory.Exists(root)) return [];
        var reports = new List<UnrealCrashReport>();
        try
        {
            foreach (var folder in Directory.EnumerateDirectories(root).Select(d => new DirectoryInfo(d)).OrderByDescending(d => d.LastWriteTimeUtc).Take(MaximumCrashReports))
            {
                var context = Path.Combine(folder.FullName, UnrealCrashReportParser.ContextFileName);
                if (!File.Exists(context)) continue;
                try
                {
                    var parsed = UnrealCrashReportParser.Parse(folder.Name, File.GetLastWriteTimeUtc(context), File.ReadAllText(context));
                    if (parsed is not null) reports.Add(parsed);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return reports;
    }

    private HeadlessCrashAnalysisSnapshot AnalyzeLocked(IReadOnlyCollection<string>? installedModNames)
    {
        var logEvidence = ReadRecentLogEvidence();
        var reports = ReadCrashReports();
        // Report lines go after the (already capped) log lines: a finding keeps its newest evidence lines, so a busy log can
        // never push a crash report out of what the finding shows.
        var evidence = (Lines: logEvidence.Lines.Concat(reports.OrderBy(r => r.WrittenAt).Select(UnrealCrashReportParser.ToEvidenceLine)).ToArray(),
            FilesScanned: logEvidence.FilesScanned + reports.Count, LinesScanned: logEvidence.LinesScanned + reports.Count);
        var previousKeys = History(30)
            .SelectMany(r => r.Findings)
            .Select(f => f.Key)
            .Where(k => !string.IsNullOrEmpty(k))
            .ToHashSet(StringComparer.Ordinal);
        var findings = CrashAnalysisBuilder.Build(evidence.Lines, installedModNames, previousKeys);

        var report = new HeadlessCrashAnalysisSnapshot(
            Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, evidence.FilesScanned, evidence.LinesScanned,
            findings, CrashAnalysisBuilder.BuildIsolationPlan(findings),
            CrashAnalysisBuilder.BuildSummary(findings) + (reports.Count > 0 ? $" Read {reports.Count} Unreal crash report(s) from Pal\\Saved\\Crashes (newest {reports.Max(r => r.WrittenAt).ToLocalTime():yyyy-MM-dd HH:mm})." : string.Empty),
            findings.Count(f => f.IsNew), findings.Count(f => !f.IsNew), reports.Count);
        var path = Path.Combine(historyRoot, $"crash-{report.ObservedAt:yyyyMMdd-HHmmss}-{report.Id[..8]}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        activity.Record(findings.Any(x => x.IsNew && x.Severity == "Critical") ? "Warning" : "Information", "Crash Analyzer",
            "Completed crash analysis", $"report={report.Id}; files={report.FilesScanned}; findings={report.Findings.Count}; new={report.NewFindings}");
        return report;
    }

    public IReadOnlyList<HeadlessCrashAnalysisSnapshot> History(int maximum = 50)
    {
        maximum = Math.Clamp(maximum, 1, 100);
        if (!Directory.Exists(historyRoot)) return [];
        var rows = new List<HeadlessCrashAnalysisSnapshot>();
        foreach (var file in Directory.EnumerateFiles(historyRoot, "crash-*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(maximum))
        {
            try
            {
                var row = JsonSerializer.Deserialize<HeadlessCrashAnalysisSnapshot>(File.ReadAllText(file));
                if (row is not null) rows.Add(row);
            }
            catch (JsonException) { }
            catch (IOException) { }
        }
        return rows;
    }

    public async Task<HeadlessSaveToolsDiagnostics> DiagnoseSaveToolsAsync(bool runSelfTests, CancellationToken token)
    {
        var python = ResolveExecutable(OperatingSystem.IsWindows() ? ["py.exe", "python.exe", "python3.exe"] : ["python3", "python"]);
        var legacy = FirstExisting(
            Path.Combine(paths.ServerRoot, "Tools", "palworld-save-tools", "convert.py"),
            Path.Combine(paths.ServerRoot, "Tools", "PalworldSaveTools", "convert.py"));
        var plm = FirstExisting(
            Path.Combine(paths.ServerRoot, "Tools", "palworld-plm-tools", "convert.py"),
            Path.Combine(paths.ServerRoot, "Tools", "palworld-plm-tools", "tools", "convert.py"),
            Path.Combine(paths.ServerRoot, "Tools", "palworld-plm-tools", "PalworldSaveTools", "convert.py"));
        var oodle = FindFirst(paths.ServerRoot, ["oo2core*.dll", "liboo2core*.so"]);
        var level = FindActiveLevelSave();
        var tests = new List<HeadlessSaveToolsTest>();
        if (runSelfTests)
        {
            tests.Add(await RunTestAsync("Python", python, python is null ? [] : ["--version"], token));
            tests.Add(await RunTestAsync("Legacy converter", python, python is null || legacy is null ? [] : [legacy, "--help"], token));
            tests.Add(await RunTestAsync("PlM/Oodle converter", python, python is null || plm is null ? [] : [plm, "--help"], token));
        }
        var signature = level is null ? "Unavailable" : ReadSignature(level);
        var ready = python is not null && (legacy is not null || plm is not null);
        var result = new HeadlessSaveToolsDiagnostics(DateTimeOffset.UtcNow, ready, python, legacy, plm, oodle,
            level, level is null ? 0 : new FileInfo(level).Length, signature, tests,
            ready ? "Read-only save inspection is available; conversion requires the matching detected converter." : "Python and at least one supported converter are required for conversion.");
        if (runSelfTests)
            activity.Record(ready ? "Information" : "Warning", "Save Tools", "Completed Save Tools self-test",
                $"python={(python is null ? "missing" : "found")}; legacy={(legacy is null ? "missing" : "found")}; plm={(plm is null ? "missing" : "found")}");
        return result;
    }

    public HeadlessSaveFileInventory BrowseSaves()
    {
        if (!Directory.Exists(paths.SaveRoot))
            return new(paths.SaveRoot, [], DateTimeOffset.UtcNow, "SaveRoot does not exist.");
        var root = Path.GetFullPath(paths.SaveRoot);
        var items = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Take(MaximumSaveFiles)
            .Select(path => new FileInfo(path))
            .Select(file => new HeadlessSaveFile(Path.GetRelativePath(root, file.FullName).Replace('\\', '/'), file.Length,
                file.LastWriteTimeUtc, Classify(file.Name), file.Extension.Equals(".sav", StringComparison.OrdinalIgnoreCase) ? ReadSignature(file.FullName) : "—"))
            .OrderByDescending(x => x.LastWriteUtc).ToArray();
        return new(root, items, DateTimeOffset.UtcNow, $"{items.Length} save-related file(s) returned (bounded to {MaximumSaveFiles}).");
    }

    private (IReadOnlyList<string> Lines, int FilesScanned, int LinesScanned) ReadRecentLogEvidence()
    {
        var roots = new[] { paths.LogsRoot, Path.Combine(paths.ManagerRuntimeRoot, "logs") }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        var files = roots.SelectMany(root => Directory.EnumerateFiles(root, "*.log", SearchOption.TopDirectoryOnly))
            .OrderByDescending(File.GetLastWriteTimeUtc).Take(MaximumLogFiles).ToArray();
        var lines = new List<string>();
        foreach (var file in files)
        {
            try { lines.AddRange(File.ReadLines(file).TakeLast(MaximumLogLines - lines.Count)); }
            catch (IOException) { }
            if (lines.Count >= MaximumLogLines) break;
        }
        return (lines.TakeLast(MaximumLogLines).ToArray(), files.Length, Math.Min(lines.Count, MaximumLogLines));
    }

    private string? FindActiveLevelSave() => Directory.Exists(paths.SaveRoot)
        ? Directory.EnumerateFiles(paths.SaveRoot, "Level.sav", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
        : null;
    private static string Classify(string name) => name.Equals("Level.sav", StringComparison.OrdinalIgnoreCase) ? "World" : name.EndsWith(".sav", StringComparison.OrdinalIgnoreCase) ? "Player/State" : "Supporting";
    private static string ReadSignature(string path)
    {
        try { using var stream = File.OpenRead(path); var bytes = new byte[Math.Min(16, (int)Math.Min(stream.Length, 16))]; stream.ReadExactly(bytes); return Convert.ToHexString(bytes); }
        catch (IOException) { return "Unreadable"; }
    }
    private static string? FirstExisting(params string[] paths) => paths.FirstOrDefault(File.Exists);
    private static string? FindFirst(string root, string[] patterns)
    {
        if (!Directory.Exists(root)) return null;
        foreach (var pattern in patterns) { var match = Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).Take(1).FirstOrDefault(); if (match is not null) return match; }
        return null;
    }
    private static string? ResolveExecutable(string[] names)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var name in names) { var candidate = Path.Combine(directory.Trim(), name); if (File.Exists(candidate)) return candidate; }
        return null;
    }
    private static async Task<HeadlessSaveToolsTest> RunTestAsync(string name, string? executable, IReadOnlyList<string> arguments, CancellationToken token)
    {
        if (executable is null || arguments.Count == 0) return new(name, false, -1, "Required executable or converter was not found.");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Process did not start.");
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token); var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = ((await outputTask) + Environment.NewLine + (await errorTask)).Trim();
            return new(name, process.ExitCode == 0, process.ExitCode, output.Length > 1000 ? output[..1000] : output);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested) { return new(name, false, -1, ex.Message); }
    }
}

// The fields after Evidence are optional so a report saved before v0.7.97.0 still loads.
public sealed record HeadlessCrashFinding(string Title, string Severity, int MatchCount, IReadOnlyList<string> Evidence,
    string SignatureId = "", string Cause = "", IReadOnlyList<string>? Fixes = null,
    DateTimeOffset? FirstSeen = null, DateTimeOffset? LastSeen = null,
    IReadOnlyList<string>? MentionedMods = null, bool IsNew = true, string Key = "");
public sealed record HeadlessCrashAnalysisSnapshot(string Id, DateTimeOffset ObservedAt, int FilesScanned, int LinesScanned,
    IReadOnlyList<HeadlessCrashFinding> Findings, IReadOnlyList<string> IsolationPlan, string Summary,
    int NewFindings = 0, int RepeatedFindings = 0, int CrashReportsRead = 0);
public sealed record HeadlessSaveToolsTest(string Name, bool Success, int ExitCode, string Detail);
public sealed record HeadlessSaveToolsDiagnostics(DateTimeOffset ObservedAt, bool Ready, string? PythonPath, string? LegacyConverterPath,
    string? PlmConverterPath, string? OodlePath, string? ActiveLevelSavePath, long ActiveLevelSaveBytes, string ActiveLevelSignature,
    IReadOnlyList<HeadlessSaveToolsTest> Tests, string Detail);
public sealed record HeadlessSaveFile(string RelativePath, long SizeBytes, DateTime LastWriteUtc, string Category, string Signature);
public sealed record HeadlessSaveFileInventory(string SaveRoot, IReadOnlyList<HeadlessSaveFile> Items, DateTimeOffset ObservedAt, string Detail);
