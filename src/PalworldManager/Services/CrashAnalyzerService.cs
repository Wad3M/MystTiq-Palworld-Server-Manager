using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class CrashAnalyzerService(AppSettings settings)
{
    private readonly Queue<string> recentLines = new();
    private readonly object sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string DiagnosticsDirectory => Path.Combine(settings.LogsRoot, "Diagnostics");

    public void Observe(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        lock (sync)
        {
            recentLines.Enqueue(line);
            while (recentLines.Count > 240) recentLines.Dequeue();
        }
    }

    public CrashDiagnosticReport RecordExit(int exitCode, bool expectedStop, IEnumerable<ModRow> mods)
    {
        List<string> evidence;
        lock (sync) evidence = recentLines.TakeLast(140).ToList();

        var enabledMods = mods
            .Where(m => m.Enabled)
            .GroupBy(m => string.IsNullOrWhiteSpace(m.Name) ? m.Package : m.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var enabledNames = enabledMods
            .Select(DisplayName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var phase = expectedStop ? "Shutdown" : "Runtime";
        var suspiciousLines = evidence.Where(IsSuspiciousLine).TakeLast(18).ToList();
        var strongCrashEvidence = evidence.Any(IsStrongCrashEvidence);
        var shutdownFailureEvidence = evidence.Any(IsShutdownFailureEvidence);
        var orderlyShutdownEvidence = evidence.Any(IsOrderlyShutdownEvidence);

        // Windows/Process can report -1 when the real exit code is unavailable. During a
        // requested stop, -1 by itself is not evidence of a crash or failed shutdown.
        var clean = expectedStop && !strongCrashEvidence && !shutdownFailureEvidence;
        var result = clean
            ? "Requested Shutdown"
            : expectedStop
                ? "Shutdown Failure"
                : exitCode == 0
                    ? "Unexpected Clean Exit"
                    : strongCrashEvidence ? "Crash" : "Unexpected Exit";
        var severity = clean ? "Healthy"
            : result == "Unexpected Clean Exit" || result == "Unexpected Exit" ? "Warning"
            : "Critical";

        var trigger = suspiciousLines.LastOrDefault() ??
                      evidence.LastOrDefault(line => line.Contains("exited", StringComparison.OrdinalIgnoreCase)) ??
                      "No direct error evidence was identified.";

        var analysis = AnalyzeWeightedEvidence(enabledMods, evidence, clean);

        var summary = clean
            ? orderlyShutdownEvidence
                ? "Palworld completed a requested shutdown. Stream-reader cancellation and process cleanup were normal shutdown activity."
                : "Palworld stopped after a requested shutdown. No crash or shutdown-failure evidence was found."
            : expectedStop
                ? $"Palworld encountered explicit failure evidence while a requested shutdown was in progress (reported exit code {exitCode})."
                : exitCode == 0
                    ? "Palworld exited cleanly, but MystTiq did not have an active shutdown request. This is an unexpected exit, not automatically a crash."
                    : strongCrashEvidence
                        ? $"Palworld crashed while the server was expected to be running (reported exit code {exitCode})."
                        : $"Palworld exited unexpectedly while the server was expected to be running (reported exit code {exitCode}), but no direct crash signature was found.";

        if (!clean && enabledNames.Count > 0)
            summary += $" {enabledNames.Count} mod(s) were enabled at the time of exit.";
        if (!clean && analysis.Contributor != "No clear contributor")
            summary += $" Weighted evidence points first to {analysis.Contributor} ({analysis.Confidence.ToLowerInvariant()} confidence). {analysis.ConfidenceReason}";

        Directory.CreateDirectory(DiagnosticsDirectory);
        var stem = $"Crash_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}";
        var textPath = Path.Combine(DiagnosticsDirectory, stem + ".txt");
        var jsonPath = Path.Combine(DiagnosticsDirectory, stem + ".json");

        var report = new CrashDiagnosticReport
        {
            Timestamp = DateTime.Now,
            ExitCode = exitCode,
            Phase = phase,
            Result = result,
            Severity = severity,
            EnabledMods = enabledNames,
            RecentEvidence = evidence,
            Summary = summary,
            LikelyContributor = analysis.Contributor,
            Confidence = analysis.Confidence,
            ConfidenceReason = analysis.ConfidenceReason,
            RuntimeLayer = analysis.RuntimeLayer,
            ActiveContext = analysis.ActiveContext,
            NearbyActivity = analysis.NearbyActivity,
            RankedSuspects = analysis.RankedSuspects,
            TriggerEvidence = trigger,
            ReportPath = textPath
        };

        File.WriteAllText(textPath, BuildTextReport(report));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public IReadOnlyList<CrashDiagnosticReport> LoadRecentReports(int maxResults = 50)
    {
        try
        {
            if (!Directory.Exists(DiagnosticsDirectory)) return [];
            var reports = new List<CrashDiagnosticReport>();
            foreach (var path in Directory.EnumerateFiles(DiagnosticsDirectory, "Crash_*.json")
                         .OrderByDescending(File.GetLastWriteTimeUtc)
                         .Take(Math.Max(1, maxResults)))
            {
                try
                {
                    var report = JsonSerializer.Deserialize<CrashDiagnosticReport>(File.ReadAllText(path));
                    if (report is not null) reports.Add(NormalizeLegacyReport(report));
                }
                catch
                {
                    // A damaged diagnostic record must not break the Analyzer page.
                }
            }
            return reports.OrderByDescending(report => report.Timestamp).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static bool IsSuspiciousLine(string line)
    {
        var value = line.Trim();
        if (value.Length == 0 || IsBenignLifecycleLine(value)) return false;
        return value.Contains("fatal", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("access violation", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("assertion failed", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("lua error", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("failed to load", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("failed loading", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("unhandled", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("[stderr]", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(value, @"\berror\b", RegexOptions.IgnoreCase);
    }


    private static bool IsBenignLifecycleLine(string line) =>
        line.Contains("stream reader cancellation requested", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("stdout reader stopped", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("stderr reader stopped", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("resources released", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("cleanup completed", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("requested shutdown", StringComparison.OrdinalIgnoreCase) ||
        Regex.IsMatch(line, @"server session #\d+ process exited with code -?\d+", RegexOptions.IgnoreCase);

    private static bool IsOrderlyShutdownEvidence(string line) =>
        line.Contains("stream reader cancellation requested", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("stdout reader stopped", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("stderr reader stopped", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("resources released", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("requested shutdown", StringComparison.OrdinalIgnoreCase);

    private static bool IsStrongCrashEvidence(string line)
    {
        var value = line.Trim();
        if (IsBenignLifecycleLine(value)) return false;
        return value.Contains("fatal error", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("unhandled exception", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("access violation", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("0xc0000005", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("assertion failed", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("stack trace", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("call stack", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("crash reporter", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsShutdownFailureEvidence(string line)
    {
        var value = line.Trim();
        if (IsBenignLifecycleLine(value)) return false;
        return value.Contains("shutdown timed out", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("failed to stop", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("failed to terminate", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("kill failed", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("could not stop", StringComparison.OrdinalIgnoreCase);
    }

    private static CrashDiagnosticReport NormalizeLegacyReport(CrashDiagnosticReport report)
    {
        if (!report.Phase.Equals("Shutdown", StringComparison.OrdinalIgnoreCase)) return report;
        if (!report.Result.Equals("Shutdown Problem", StringComparison.OrdinalIgnoreCase) &&
            !report.Result.Equals("Shutdown Failure", StringComparison.OrdinalIgnoreCase)) return report;
        if (report.RecentEvidence.Any(IsStrongCrashEvidence) || report.RecentEvidence.Any(IsShutdownFailureEvidence)) return report;

        var orderly = report.RecentEvidence.Any(IsOrderlyShutdownEvidence);
        return new CrashDiagnosticReport
        {
            Timestamp = report.Timestamp,
            ExitCode = report.ExitCode,
            Phase = report.Phase,
            Result = "Requested Shutdown",
            Severity = "Healthy",
            EnabledMods = report.EnabledMods,
            RecentEvidence = report.RecentEvidence,
            Summary = orderly
                ? "Palworld completed a requested shutdown. Stream-reader cancellation and process cleanup were normal shutdown activity."
                : "Palworld stopped after a requested shutdown. No crash or shutdown-failure evidence was found.",
            LikelyContributor = "No clear contributor",
            Confidence = "Not applicable",
            ConfidenceReason = "This was a requested shutdown without direct failure evidence.",
            RuntimeLayer = "Normal shutdown",
            ActiveContext = "None",
            NearbyActivity = "Normal process and stream cleanup",
            RankedSuspects = [],
            TriggerEvidence = orderly ? "Requested shutdown lifecycle and stream-reader cleanup." : "No direct failure evidence was identified.",
            ReportPath = report.ReportPath
        };
    }

    private sealed record WeightedAnalysis(
        string Contributor,
        string Confidence,
        string ConfidenceReason,
        string RuntimeLayer,
        string ActiveContext,
        string NearbyActivity,
        IReadOnlyList<string> RankedSuspects);

    private static WeightedAnalysis AnalyzeWeightedEvidence(
        IReadOnlyList<ModRow> enabledMods,
        IReadOnlyList<string> evidence,
        bool clean)
    {
        if (clean)
            return new("No clear contributor", "Not applicable", "This was a requested shutdown without direct failure evidence.", "Normal shutdown", "None", "Normal process and stream cleanup", []);

        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var reasons = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in enabledMods)
        {
            var display = DisplayName(mod);
            aliases[display] = new[] { mod.Name, mod.Package, Normalize(mod.Name), Normalize(mod.Package) }
                .Where(v => !string.IsNullOrWhiteSpace(v) && v.Length >= 3)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            scores[display] = 0;
            reasons[display] = [];
        }
        scores["UE4SS runtime"] = 0;
        reasons["UE4SS runtime"] = [];
        aliases["UE4SS runtime"] = ["UE4SS", "RE-UE4SS", "dwmapi.dll", "UE4SS.dll"];

        string runtimeLayer = "Unknown";
        string activeContext = "Unknown";
        string nearbyActivity = "None identified";

        foreach (var line in evidence)
        {
            var lower = line.ToLowerInvariant();
            var success = IsExplicitSuccessLine(line);
            var suspicious = IsSuspiciousLine(line);
            var nativeCrash = lower.Contains("access violation") || lower.Contains("0xc0000005") ||
                              lower.Contains("stack trace") || lower.Contains("call stack") ||
                              lower.Contains("unhandled exception") || lower.Contains("fatal error");
            var executionContext = lower.Contains("reflection") || lower.Contains("handle") ||
                                   lower.Contains("hook") || lower.Contains("callback") || lower.Contains("invoke");

            if ((lower.Contains("ue4ss") || lower.Contains("dwmapi.dll")) && (nativeCrash || suspicious))
                runtimeLayer = "UE4SS";

            foreach (var pair in aliases)
            {
                if (!pair.Value.Any(alias => line.Contains(alias, StringComparison.OrdinalIgnoreCase))) continue;
                var name = pair.Key;
                if (success)
                {
                    // Successful initialization is useful lifecycle context, never fault evidence.
                    nearbyActivity = $"{name}: explicit successful-load message observed.";
                    continue;
                }

                var add = 0;
                if (nativeCrash) add += 10;
                else if (suspicious) add += 6;
                if (name == "UE4SS runtime" && nativeCrash) add += 4;
                if (executionContext) add += 5;
                if (!suspicious && !executionContext) add += 1; // chronological proximity only

                scores[name] += add;
                if (add >= 10) reasons[name].Add("direct native crash/exception evidence names this component");
                else if (executionContext && add >= 5) reasons[name].Add("reflection/hook/callback execution context names this component");
                else if (suspicious) reasons[name].Add("explicit error/failure evidence names this component");
            }

            if (executionContext)
            {
                var contextMod = aliases.FirstOrDefault(pair => pair.Key != "UE4SS runtime" && pair.Value.Any(alias => line.Contains(alias, StringComparison.OrdinalIgnoreCase))).Key;
                if (!string.IsNullOrWhiteSpace(contextMod)) activeContext = contextMod + " (reflection/hook execution)";
                else if (lower.Contains("ue4ss")) activeContext = "UE4SS reflection/hook execution";
            }
        }

        var ranked = scores.OrderByDescending(p => p.Value).ThenBy(p => p.Key).ToList();
        var top = ranked.FirstOrDefault();
        var second = ranked.Skip(1).FirstOrDefault();
        if (top.Value < 3)
            return new("No clear contributor", "Unknown",
                "Only weak chronological/log proximity was found; MystTiq will not promote that to a causal suspect.",
                runtimeLayer, activeContext, nearbyActivity,
                ranked.Where(p => p.Value > 0).Select(p => $"{p.Key}: {p.Value}").ToList());

        var gap = top.Value - second.Value;
        var confidence = top.Value >= 12 && gap >= 3 ? "High" : top.Value >= 7 ? "Medium" : "Low";
        var reasonText = reasons.TryGetValue(top.Key, out var list) && list.Count > 0
            ? string.Join("; ", list.Distinct(StringComparer.OrdinalIgnoreCase).Take(3))
            : "evidence is stronger than simple log proximity";
        var confidenceReason = $"Score {top.Value}; next candidate {second.Key ?? "none"} scored {second.Value}. {reasonText}. Successful-load lines and last-message proximity are not treated as proof.";
        return new(top.Key, confidence, confidenceReason, runtimeLayer, activeContext, nearbyActivity,
            ranked.Where(p => p.Value > 0).Take(5).Select(p => $"{p.Key}: {p.Value}").ToList());
    }

    private static bool IsExplicitSuccessLine(string line) =>
        line.Contains("loaded successfully", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("successfully loaded", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("initialized successfully", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("registered successfully", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("started successfully", StringComparison.OrdinalIgnoreCase);

    private static string DisplayName(ModRow mod) =>
        string.IsNullOrWhiteSpace(mod.Name) ? mod.Package : mod.Name;

    private static string Normalize(string value) =>
        Regex.Replace(value ?? string.Empty, "[^a-zA-Z0-9]", string.Empty);

    private static string BuildTextReport(CrashDiagnosticReport report)
    {
        return new StringBuilder()
            .AppendLine("MystTiq Palworld Server - Crash Analyzer")
            .AppendLine($"Timestamp: {report.Timestamp:O}")
            .AppendLine($"Result: {report.Result}")
            .AppendLine($"Phase: {report.Phase}")
            .AppendLine($"Exit code: {report.ExitCode}")
            .AppendLine($"Enabled mods: {report.EnabledModsDisplay}")
            .AppendLine($"Likely contributor: {report.LikelyContributor}")
            .AppendLine($"Confidence: {report.Confidence}")
            .AppendLine($"Confidence reason: {report.ConfidenceReason}")
            .AppendLine($"Runtime layer: {report.RuntimeLayer}")
            .AppendLine($"Active context: {report.ActiveContext}")
            .AppendLine($"Nearby activity: {report.NearbyActivity}")
            .AppendLine($"Ranked suspects: {(report.RankedSuspects.Count == 0 ? "None" : string.Join(", ", report.RankedSuspects))}")
            .AppendLine()
            .AppendLine(report.Summary)
            .AppendLine()
            .AppendLine("Trigger evidence:")
            .AppendLine(report.TriggerEvidence)
            .AppendLine()
            .AppendLine("Recent evidence:")
            .AppendLine(string.Join(Environment.NewLine, report.RecentEvidence))
            .ToString();
    }
}
