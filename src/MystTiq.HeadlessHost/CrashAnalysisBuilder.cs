using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.7.97.0: turns log lines into findings (via the signature catalog), works out which mods the
// evidence names, and separates findings that are new since a previous analysis from ones already
// reported. Pure and deterministic, so the logic harness covers it without a server or log files.
public static class CrashAnalysisBuilder
{
    private const int EvidenceLinesPerFinding = 5;

    public static IReadOnlyList<HeadlessCrashFinding> Build(
        IEnumerable<string> lines,
        IReadOnlyCollection<string>? installedModNames,
        IReadOnlySet<string>? previouslyReportedKeys)
    {
        var mods = (installedModNames ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => (Name: name, Normalized: Normalize(name)))
            .Where(m => m.Normalized.Length >= 4)
            .ToArray();

        var findings = new List<HeadlessCrashFinding>();
        foreach (var match in CrashSignatureCatalog.Match(lines))
        {
            var texts = match.Lines.Select(l => l.Text).ToArray();
            var stamps = match.Lines.Where(l => l.At.HasValue).Select(l => l.At!.Value).ToArray();
            var mentioned = mods
                .Where(m => texts.Any(t => Normalize(t).Contains(m.Normalized, StringComparison.OrdinalIgnoreCase)))
                .Select(m => m.Name)
                .ToArray();
            var key = KeyFor(match.Signature.Id, texts.Length, texts[^1]);
            findings.Add(new HeadlessCrashFinding(
                match.Signature.Title, match.Signature.Severity, texts.Length,
                texts.TakeLast(EvidenceLinesPerFinding).ToArray(),
                match.Signature.Id, match.Signature.Cause, match.Signature.Fixes,
                stamps.Length > 0 ? stamps.Min() : null, stamps.Length > 0 ? stamps.Max() : null,
                mentioned, previouslyReportedKeys is null || !previouslyReportedKeys.Contains(key), key));
        }

        return findings
            .OrderByDescending(f => f.IsNew)
            .ThenBy(f => SeverityRank(f.Severity))
            .ThenByDescending(f => f.MatchCount)
            .ToArray();
    }

    // A finding is "the same" as an earlier one when it has the same signature, the same number of
    // matching lines and the same last line. A new crash adds lines, so it changes the key.
    public static string KeyFor(string signatureId, int matchCount, string lastLine)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{signatureId}|{matchCount}|{lastLine}"));
        return Convert.ToHexString(hash)[..16];
    }

    public static string BuildSummary(IReadOnlyList<HeadlessCrashFinding> findings)
    {
        if (findings.Count == 0)
            return "No known crash signature was found in the bounded recent-log window. This is not proof that no crash occurred.";

        var fresh = findings.Count(f => f.IsNew);
        var repeated = findings.Count - fresh;
        var top = findings[0];
        var text = $"Found {findings.Count} distinct problem(s): {fresh} new since the last analysis and {repeated} already reported earlier. " +
                   $"Most important: {top.Title}.";
        var mods = NamedMods(findings);
        if (mods.Count > 0)
            text += $" Mod(s) named in the evidence: {string.Join(", ", mods)}. That is a lead, not proof of cause.";
        return text;
    }

    public static IReadOnlyList<string> BuildIsolationPlan(IReadOnlyList<HeadlessCrashFinding> findings)
    {
        var steps = new List<string> { "Preserve the current logs and create a verified backup before changing server files." };
        foreach (var mod in NamedMods(findings).Take(3))
            steps.Add($"Disable \"{mod}\" first (MOD Library): the evidence names it. Then reproduce under observation.");
        if (findings.Any(f => f.SignatureId is "ue4ss-error" or "access-violation" or "stack-overflow") && NamedMods(findings).Count == 0)
            steps.Add("Use MOD Library to disable one recently changed UE4SS mod at a time, then reproduce under observation.");
        if (findings.Any(f => f.SignatureId == "out-of-memory"))
            steps.Add("Compare memory history and player load before attributing the failure to a mod.");
        steps.Add("Validate Palworld server files after evidence is captured; do not treat successful load messages as causal proof.");
        return steps;
    }

    private static IReadOnlyList<string> NamedMods(IReadOnlyList<HeadlessCrashFinding> findings) =>
        findings.Where(f => f.Severity == "Critical" || f.SignatureId == "ue4ss-error")
            .SelectMany(f => f.MentionedMods ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static int SeverityRank(string severity) => severity switch { "Critical" => 0, "Warning" => 1, _ => 2 };

    private static string Normalize(string value) => Regex.Replace(value ?? string.Empty, "[^a-zA-Z0-9]", string.Empty);
}
