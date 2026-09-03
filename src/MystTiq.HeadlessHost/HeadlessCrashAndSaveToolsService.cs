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
    private readonly IServerPathProfile paths;
    private readonly HeadlessActivityLogService activity;
    private readonly string historyRoot;

    public HeadlessCrashAndSaveToolsService(IServerPathProfile paths, HeadlessActivityLogService activity)
    {
        this.paths = paths;
        this.activity = activity;
        historyRoot = Path.Combine(paths.ManagerRuntimeRoot, "crash-analyzer");
        Directory.CreateDirectory(historyRoot);
    }

    public HeadlessCrashAnalysisSnapshot Analyze()
    {
        var evidence = ReadRecentLogEvidence();
        var findings = new List<HeadlessCrashFinding>();
        AddFinding(findings, evidence, "Fatal error", "Critical", "fatal error");
        AddFinding(findings, evidence, "Unhandled exception", "Critical", "unhandled exception");
        AddFinding(findings, evidence, "Access violation", "Critical", "access violation", "0xc0000005");
        AddFinding(findings, evidence, "Out of memory", "Critical", "out of memory", "oom");
        AddFinding(findings, evidence, "Watchdog or hang", "Warning", "watchdog", "hang detected", "not responding");
        AddFinding(findings, evidence, "UE4SS context", "Warning", "ue4ss", "dwmapi.dll");

        var report = new HeadlessCrashAnalysisSnapshot(
            Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, evidence.FilesScanned, evidence.LinesScanned,
            findings, BuildIsolationPlan(findings),
            findings.Count == 0
                ? "No explicit crash signature was found in the bounded recent-log window. This is not proof that no crash occurred."
                : $"Found {findings.Count} evidence-backed crash signature group(s). Correlation is shown without claiming an unproven cause.");
        var path = Path.Combine(historyRoot, $"crash-{report.ObservedAt:yyyyMMdd-HHmmss}-{report.Id[..8]}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        activity.Record(findings.Any(x => x.Severity == "Critical") ? "Warning" : "Information", "Crash Analyzer",
            "Completed crash analysis", $"report={report.Id}; files={report.FilesScanned}; findings={report.Findings.Count}");
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

    private static void AddFinding(List<HeadlessCrashFinding> findings, (IReadOnlyList<string> Lines, int FilesScanned, int LinesScanned) evidence,
        string title, string severity, params string[] needles)
    {
        var matches = evidence.Lines.Where(line => needles.Any(n => line.Contains(n, StringComparison.OrdinalIgnoreCase))).TakeLast(5).ToArray();
        if (matches.Length > 0) findings.Add(new(title, severity, matches.Length, matches));
    }

    private static IReadOnlyList<string> BuildIsolationPlan(IReadOnlyList<HeadlessCrashFinding> findings)
    {
        var steps = new List<string> { "Preserve the current logs and create a verified backup before changing server files." };
        if (findings.Any(x => x.Title.Contains("UE4SS", StringComparison.OrdinalIgnoreCase)))
            steps.Add("Use MOD Library to disable one recently changed UE4SS mod at a time, then reproduce under observation.");
        if (findings.Any(x => x.Title.Contains("memory", StringComparison.OrdinalIgnoreCase)))
            steps.Add("Compare memory history and player load before attributing the failure to a mod.");
        steps.Add("Validate Palworld server files after evidence is captured; do not treat successful load messages as causal proof.");
        return steps;
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

public sealed record HeadlessCrashFinding(string Title, string Severity, int MatchCount, IReadOnlyList<string> Evidence);
public sealed record HeadlessCrashAnalysisSnapshot(string Id, DateTimeOffset ObservedAt, int FilesScanned, int LinesScanned,
    IReadOnlyList<HeadlessCrashFinding> Findings, IReadOnlyList<string> IsolationPlan, string Summary);
public sealed record HeadlessSaveToolsTest(string Name, bool Success, int ExitCode, string Detail);
public sealed record HeadlessSaveToolsDiagnostics(DateTimeOffset ObservedAt, bool Ready, string? PythonPath, string? LegacyConverterPath,
    string? PlmConverterPath, string? OodlePath, string? ActiveLevelSavePath, long ActiveLevelSaveBytes, string ActiveLevelSignature,
    IReadOnlyList<HeadlessSaveToolsTest> Tests, string Detail);
public sealed record HeadlessSaveFile(string RelativePath, long SizeBytes, DateTime LastWriteUtc, string Category, string Signature);
public sealed record HeadlessSaveFileInventory(string SaveRoot, IReadOnlyList<HeadlessSaveFile> Items, DateTimeOffset ObservedAt, string Detail);
