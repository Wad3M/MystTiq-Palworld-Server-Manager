using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class ModRuntimeEvidenceEngine
{
    private static readonly Regex[] NamedPositivePatterns =
    [
        new("Starting\\s+Lua\\s+mod\\s+[\\\"'](?<name>[^\\\"']+)[\\\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("Loading\\s+Lua\\s+mod\\s+[\\\"'](?<name>[^\\\"']+)[\\\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("Mod\\s+[\\\"'](?<name>[^\\\"']+)[\\\"']\\s+has\\s+enabled\\.txt\\s*,?\\s*starting\\s+mod", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("Lua\\s+mod\\s+[\\\"'](?<name>[^\\\"']+)[\\\"']\\s+(?:started|loaded|initialized|enabled|registered)(?:\\s+successfully)?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("Mod\\s+[\\\"'](?<name>[^\\\"']+)[\\\"']\\s+(?:started|loaded|initialized|registered)(?:\\s+successfully)?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("(?:Loaded|Initialized|Registered)\\s+(?:Lua\\s+)?mod\\s*[:=]\\s*[\\\"']?(?<name>[A-Za-z0-9_. -]{3,})[\\\"']?", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    ];

    public RuntimeEvidenceAssessment Assess(ModRow mod, RuntimeStateSnapshot state, bool serverRunning)
    {
        ArgumentNullException.ThrowIfNull(mod);
        ArgumentNullException.ThrowIfNull(state);
        if (!IsUe4ss(mod)) return Result(RuntimeEvidenceState.NotRequired, 100, "Not required", "", "UE4SS runtime proof is not required for this MOD type.");
        if (!mod.Enabled) return Result(RuntimeEvidenceState.Disabled, 0, "Disabled", "", "The MOD is intentionally disabled.");
        if (!mod.Deployed || !mod.PresentInActiveRuntime) return Result(RuntimeEvidenceState.NotLoaded, 10, "Deployment state", "", "Deployment/active-root state prevents runtime loading.");
        if (!serverRunning) return Result(RuntimeEvidenceState.ActiveUnverified, 45, "Server offline", "", "Installed and enabled; runtime execution cannot be observed while PalServer is offline.");

        var aliases = BuildAliases(mod);
        var matchedAlias = state.LoadedAliases.FirstOrDefault(runtimeAlias => aliases.Any(alias => AliasEquals(alias, runtimeAlias)));
        if (!string.IsNullOrWhiteSpace(matchedAlias))
            return Result(RuntimeEvidenceState.ConfirmedLoaded, 100, "Unified runtime session", matchedAlias,
                $"Current runtime session {state.SessionId} revision {state.Revision} contains positive loader/initialization evidence '{matchedAlias}'.");
        if (mod.LoadedByUe4ss)
            return Result(RuntimeEvidenceState.ConfirmedLoaded, 95, "Unified inventory state", FirstUsefulAlias(mod),
                $"The authoritative MOD inventory carries positive current-session UE4SS evidence for session {(state.SessionActive ? state.SessionId : "current")}.");

        return Result(RuntimeEvidenceState.ActiveUnverified, 70, state.SessionActive ? "Current session unverified" : "No active runtime session", "",
            state.SessionActive
                ? $"Installed, enabled, and present in the Active UE4SS Mods Root. Runtime session {state.SessionId} revision {state.Revision} has no mod-specific positive signature for aliases: {string.Join(", ", aliases.Take(6))}. This can be normal for quiet/event-driven hook mods."
                : "Installed, enabled, and present, but no active runtime session is available to observe.");
    }

    public RuntimeEvidenceAssessment PromoteFunctionalActivity(
        RuntimeEvidenceAssessment assessment,
        ModRow mod,
        IEnumerable<string> currentLogLines)
    {
        var aliases = BuildAliases(mod);
        var activityTokens = new[] { "hook", "callback", "execut", "blocked", "filtered", "command", "event", "handled", "intercept" };
        var line = currentLogLines.FirstOrDefault(candidate =>
            aliases.Any(alias => candidate.Contains(alias, StringComparison.OrdinalIgnoreCase)) &&
            activityTokens.Any(token => candidate.Contains(token, StringComparison.OrdinalIgnoreCase)));

        if (line is null) return assessment;

        var matched = aliases.FirstOrDefault(alias => line.Contains(alias, StringComparison.OrdinalIgnoreCase)) ?? assessment.MatchedAlias;
        return new RuntimeEvidenceAssessment(
            RuntimeEvidenceState.ConfirmedRunning,
            100,
            "Observed functional runtime activity",
            matched,
            "A current runtime log line contains both this MOD identity and an execution/activity signature. " +
            "MystTiq observed functional activity rather than loader presence alone.");
    }

    public static IReadOnlyList<string> ExtractPositiveAliases(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return [];
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in NamedPositivePatterns)
        {
            var match = pattern.Match(line);
            if (!match.Success) continue;
            var name = match.Groups["name"].Value.Trim().TrimEnd('.', ',', ';', ':');
            foreach (var alias in ExpandAlias(name)) aliases.Add(alias);
        }
        return aliases.ToList();
    }

    public static IReadOnlyList<string> BuildAliases(ModRow mod) => new[] { mod.Package, mod.Name }.Concat(mod.RuntimeAliases ?? [])
        .Where(value => !string.IsNullOrWhiteSpace(value)).SelectMany(ExpandAlias).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private static RuntimeEvidenceAssessment Result(RuntimeEvidenceState state, int confidence, string source, string alias, string detail) => new(state, confidence, source, alias, detail);
    private static bool IsUe4ss(ModRow mod) => mod.Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) || mod.Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase);
    private static string FirstUsefulAlias(ModRow mod) => (mod.RuntimeAliases ?? []).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? (!string.IsNullOrWhiteSpace(mod.Package) ? mod.Package : mod.Name);
    private static bool AliasEquals(string left, string right) => left.Equals(right, StringComparison.OrdinalIgnoreCase) || Normalize(left).Equals(Normalize(right), StringComparison.OrdinalIgnoreCase);
    private static string Normalize(string value) => Regex.Replace(value ?? "", "[^a-zA-Z0-9]", "");
    private static IEnumerable<string> ExpandAlias(string value) { var trimmed=value.Trim(); if(trimmed.Length>=3) yield return trimmed; var compact=Normalize(trimmed); if(compact.Length>=3 && !compact.Equals(trimmed,StringComparison.OrdinalIgnoreCase)) yield return compact; }
}

public enum RuntimeEvidenceState { NotRequired, ConfirmedLoaded,
    ConfirmedRunning, ActiveUnverified, NotLoaded, Error, Disabled }
public sealed record RuntimeEvidenceAssessment(RuntimeEvidenceState State, int Confidence, string Source, string MatchedAlias, string Detail)
{
    public bool Confirmed => State is RuntimeEvidenceState.ConfirmedLoaded or RuntimeEvidenceState.ConfirmedRunning;
    public string DisplayStatus => State switch { RuntimeEvidenceState.ConfirmedLoaded => "Confirmed Loaded",
        RuntimeEvidenceState.ConfirmedRunning => "Confirmed Running", RuntimeEvidenceState.ActiveUnverified => "Active / Unverified", RuntimeEvidenceState.NotLoaded => "Not Loaded", RuntimeEvidenceState.Error => "Error", RuntimeEvidenceState.Disabled => "Disabled", _ => "N/A" };
}
