using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class ModVerificationService
{
    private readonly AppSettings settings;
    private readonly List<IModVerifier> verifiers;
    private readonly ModHealthEvaluationService healthEvaluator;
    private readonly RuntimeStateService runtimeState;

    public ModVerificationService(AppSettings settings, RuntimeStateService runtimeState, ModHealthEvaluationService? healthEvaluator = null)
    {
        this.settings = settings;
        this.runtimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
        this.healthEvaluator = healthEvaluator ?? new ModHealthEvaluationService();
        verifiers = [new GenericModVerifier(this.healthEvaluator)];
    }

    public IReadOnlyList<VerificationResult> VerifyAll(IEnumerable<ModRow> mods, bool serverRunning)
    {
        var installed = mods.ToList();
        var logFiles = FindRelevantLogs();
        var logLines = ReadRecentLogLines(logFiles);
        var counts = BuildLogicalInstallCounts(installed);
        var context = new ModVerificationContext
        {
            Settings = settings,
            LogFiles = logFiles,
            LogLines = logLines,
            LogicalInstallCounts = counts,
            ServerRunning = serverRunning,
            RuntimeState = runtimeState.Current
        };

        var results = new List<VerificationResult>();
        foreach (var mod in installed)
        {
            try
            {
                var verifier = verifiers.First(item => item.CanVerify(mod));
                results.Add(verifier.Verify(mod, context));
            }
            catch (Exception ex)
            {
                results.Add(new VerificationResult
                {
                    Package = mod.Package,
                    Name = mod.Name,
                    Type = mod.Type,
                    FilesPresent = mod.Deployed,
                    Enabled = mod.Enabled,
                    RuntimeEvidenceFound = false,
                    RuntimeErrorFound = false,
                    DuplicateDetected = false,
                    FilesStatus = mod.Deployed ? "Present" : "Missing",
                    RuntimeStatus = "Verification error",
                    ErrorSummary = "None",
                    Details = "MystTiq could not complete verification for this mod: " + ex.Message,
                    HealthScore = 0,
                    HealthStatus = mod.Enabled ? ModHealthStatus.Unknown : ModHealthStatus.Disabled,
                    VerifiedAt = DateTime.Now
                });
            }
        }
        return results;
    }

    private IReadOnlyList<string> FindRelevantLogs()
    {
        var roots = new[]
        {
            settings.LogsRoot,
            Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64"),
            Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", "UE4SS")
        };

        var files = new List<string>();
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                files.AddRange(Directory.EnumerateFiles(root, "*.log", SearchOption.AllDirectories));
                files.AddRange(Directory.EnumerateFiles(root, "*.txt", SearchOption.TopDirectoryOnly)
                    .Where(path => Path.GetFileName(path).Contains("ue4ss", StringComparison.OrdinalIgnoreCase)));
            }
            catch
            {
                // A locked or protected log folder must not abort verification.
            }
        }

        return files.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path =>
            {
                try { return File.GetLastWriteTimeUtc(path); }
                catch { return DateTime.MinValue; }
            })
            .Take(8)
            .ToList();
    }

    private static IReadOnlyList<string> ReadRecentLogLines(IEnumerable<string> files)
    {
        var lines = new List<string>();
        foreach (var file in files)
        {
            try
            {
                var all = File.ReadLines(file).TakeLast(5000);
                lines.AddRange(all);
            }
            catch
            {
                // Logs can be locked while the server is running. Continue with
                // every readable source instead of reporting a false failure.
            }
        }
        return lines;
    }

    private static IReadOnlyDictionary<string, int> BuildLogicalInstallCounts(IEnumerable<ModRow> mods)
    {
        return mods.GroupBy(mod => Normalize(mod.Package), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    internal static string Normalize(string value) => Regex.Replace(value ?? "", "[^a-zA-Z0-9]", "").ToLowerInvariant();

    private sealed class GenericModVerifier : IModVerifier
    {
        private readonly ModHealthEvaluationService healthEvaluator;

        public GenericModVerifier(ModHealthEvaluationService healthEvaluator)
        {
            this.healthEvaluator = healthEvaluator;
        }

        private static readonly string[] ErrorTokens =
        [
            "error", "fatal", "exception", "stack trace", "lua error", "failed to load",
            "missing dependency", "attempt to index", "script error"
        ];

        private static readonly string[] SuccessTokens =
        [
            "loaded", "loading", "enabled", "registered", "executing", "initialized", "mounted"
        ];

        public bool CanVerify(ModRow mod) => true;

        public VerificationResult Verify(ModRow mod, ModVerificationContext context)
        {
            var aliases = BuildAliases(mod);
            var relevant = context.LogLines.Where(line => aliases.Any(alias =>
                line.Contains(alias, StringComparison.OrdinalIgnoreCase))).ToList();

            var isUe4ss = mod.Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) ||
                          mod.Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase);
            var sharedRuntimeErrors = context.RuntimeState.RuntimeErrors
                .Where(line => aliases.Any(alias => line.Contains(alias, StringComparison.OrdinalIgnoreCase)))
                .Take(3)
                .ToList();
            var errorLines = isUe4ss
                ? sharedRuntimeErrors
                : relevant.Where(IsRuntimeErrorLine).Take(3).ToList();
            var successFound = relevant.Any(IsRuntimeSuccessLine);
            var runtimeEvidenceFound = isUe4ss ? mod.LoadedByUe4ss : successFound || mod.LoadedByUe4ss;

            var duplicate = context.LogicalInstallCounts.TryGetValue(ModVerificationService.Normalize(mod.Package), out var count) && count > 1;
            var filesPresent = mod.Deployed;
            var enabled = mod.Enabled;
            var runtimeError = errorLines.Count > 0;

            var evaluation = healthEvaluator.Evaluate(
                mod,
                context.ServerRunning,
                runtimeChecked: true,
                runtimeEvidenceFound: runtimeEvidenceFound,
                runtimeErrorFound: runtimeError,
                duplicateDetected: duplicate);

            var runtimeStatus = runtimeError ? "Error detected"
                : runtimeEvidenceFound ? "Loaded"
                : context.ServerRunning ? "No load evidence"
                : "Server offline";

            var detailParts = new List<string>();
            if (isUe4ss)
                detailParts.Add($"Runtime session {(context.RuntimeState.SessionActive ? context.RuntimeState.SessionId : "inactive")}, revision {context.RuntimeState.Revision}; centralized current-session evidence used.");
            else if (context.LogFiles.Count == 0) detailParts.Add("No UE4SS or server logs were found.");
            else detailParts.Add($"Checked {context.LogFiles.Count} recent log file(s).");
            if (duplicate) detailParts.Add("Duplicate logical installation detected.");
            if (mod.EnableReason.Contains("STATE MISMATCH", StringComparison.OrdinalIgnoreCase))
                detailParts.Add("Configured and effective UE4SS activation state do not match. Repair States should be run before the next server start.");
            if (!runtimeEvidenceFound && context.ServerRunning) detailParts.Add("The server is running, but no matching load entry was found.");
            if (!context.ServerRunning) detailParts.Add("Start the server, then verify again for runtime evidence.");
            if (!string.IsNullOrWhiteSpace(mod.EnableReason)) detailParts.Add("Enabled-state evidence: " + mod.EnableReason);

            return new VerificationResult
            {
                Package = mod.Package,
                Name = mod.Name,
                Type = mod.Type,
                FilesPresent = filesPresent,
                Enabled = enabled,
                RuntimeEvidenceFound = runtimeEvidenceFound,
                RuntimeErrorFound = runtimeError,
                DuplicateDetected = duplicate,
                FilesStatus = !filesPresent ? "Missing" : duplicate ? "Duplicate" : "Present",
                RuntimeStatus = runtimeStatus,
                ErrorSummary = errorLines.Count == 0 ? "None" : string.Join(" | ", errorLines.Select(TrimLine)),
                Details = string.Join(" ", detailParts),
                HealthScore = evaluation.Score,
                HealthStatus = evaluation.Status,
                VerifiedAt = DateTime.Now
            };
        }

        private static bool IsRuntimeSuccessLine(string line)
        {
            var lower = line.ToLowerInvariant();
            return lower.Contains("loaded successfully") ||
                   lower.Contains("initialized successfully") ||
                   lower.Contains("registered successfully") ||
                   SuccessTokens.Any(token => lower.Contains(token));
        }

        private static bool IsRuntimeErrorLine(string line)
        {
            var lower = line.ToLowerInvariant();
            if (lower.Contains("loaded successfully") ||
                lower.Contains("initialized successfully") ||
                lower.Contains("registered successfully"))
                return false;

            // Manager log wrappers may say [ERROR] only because a third-party loader
            // wrote a benign message to stderr. Ignore the wrapper and look for
            // explicit failure language in the actual payload.
            var payload = lower;
            var serverMarker = payload.IndexOf("[server]", StringComparison.Ordinal);
            if (serverMarker >= 0)
                payload = payload[(serverMarker + "[server]".Length)..];

            return ErrorTokens.Any(token => payload.Contains(token));
        }

        private static IReadOnlyList<string> BuildAliases(ModRow mod)
        {
            var values = new[] { mod.Package, mod.Name }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .SelectMany(value => new[] { value, ModVerificationService.Normalize(value) })
                .Where(value => value.Length >= 3)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return values;
        }

        private static string TrimLine(string line)
        {
            var value = line.Trim();
            return value.Length <= 180 ? value : value[..180] + "…";
        }
    }
}
