// MystTiq v1.1.0.1: file reviewed for this release (2026-10-13).
using System.Globalization;
using System.Text.RegularExpressions;
using MystTiq.Core.Models;

namespace MystTiq.HeadlessHost;

/// <summary>A player's level when MystTiq saw it change, with the playtime MystTiq had recorded for them then.</summary>
public sealed record LevelSample(DateTimeOffset ObservedAt, int Level, double PlaytimeMinutes);

/// <summary>A level gain out of line with the playtime behind it: the numbers a finding shows.</summary>
public sealed record LevelGap(string Window, int FromLevel, int ToLevel, double PlaytimeMinutes, double AllowedLevels);

/// <summary>
/// v1.0.9.3 (roadmap B-2): level-gap scans. A player who gains more levels than their recorded playtime allows is
/// reported, with the numbers. Pure, so the logic harness covers it.
///
/// What a player can gain is worked out from the level they started at, band by band (<see cref="Bands"/>), at the
/// server's experience rate. The bands are deliberately generous: they reach level 50 in about 12 hours of play, several
/// times faster than ordinary progression, so a fast but honest player stays well inside them; an edited or cheated
/// level (dozens of levels in minutes) does not. MystTiq only counts playtime it has seen, so it compares levels gained
/// since it first saw a player, never their total level, and a player already high when first seen is not flagged.
/// Only ever a finding: the rule never kicks or bans.
/// </summary>
public static class LevelGapScan
{
    public const int MaximumSamplesPerPlayer = 50;

    /// <summary>Levels per hour of play from each level on, at experience rate 1.</summary>
    public static readonly IReadOnlyList<(int FromLevel, double PerHour)> Bands =
        [(1, 20), (10, 10), (20, 5), (30, 3), (40, 2), (50, 1), (60, 0.5)];

    /// <summary>The most levels an honest player starting at <paramref name="fromLevel"/> could gain in this much play.</summary>
    public static double AllowedGain(int fromLevel, double playtimeHours, double expRate)
    {
        var rate = Math.Clamp(double.IsFinite(expRate) ? expRate : 1, 0.1, 20);
        double level = Math.Max(1, fromLevel);
        var hours = Math.Max(0, playtimeHours);
        while (hours > 1e-9)
        {
            var band = Bands.Last(b => level >= b.FromLevel);
            var next = Bands.Where(b => b.FromLevel > level).Select(b => (double)b.FromLevel).DefaultIfEmpty(double.PositiveInfinity).First();
            var perHour = band.PerHour * rate;
            var hoursToNext = (next - level) / perHour;
            if (hoursToNext >= hours) { level += hours * perHour; hours = 0; }
            else { level = next; hours -= hoursToNext; }
        }
        return level - Math.Max(1, fromLevel);
    }

    /// <summary>
    /// The gap, if any: from the first sample (since MystTiq first saw the player) and from the one before the latest (a
    /// sudden jump), whichever shows it first. Gains within <see cref="LevelGapRule.SlackLevels"/> never count.
    /// </summary>
    public static LevelGap? Evaluate(IReadOnlyList<LevelSample> samples, LevelGapRule rule, double expRate)
    {
        if (samples.Count < 2) return null;
        var latest = samples[^1];
        foreach (var (window, from) in new[] { ("since MystTiq first saw them", samples[0]), ("since the level before", samples[^2]) })
        {
            var gained = latest.Level - from.Level;
            if (gained <= rule.SlackLevels) continue;
            var minutes = Math.Max(0, latest.PlaytimeMinutes - from.PlaytimeMinutes);
            var allowed = AllowedGain(from.Level, minutes / 60, expRate) + rule.SlackLevels;
            if (gained > allowed) return new LevelGap(window, from.Level, latest.Level, minutes, allowed);
        }
        return null;
    }

    /// <summary>
    /// The history with this sample added when the level changed (the first sample starts it). A lower level than the last
    /// (a reset or a migrated character) starts the history again; only the newest samples are kept, the first always.
    /// </summary>
    public static IReadOnlyList<LevelSample> Add(IReadOnlyList<LevelSample> history, LevelSample sample)
    {
        if (history.Count == 0 || sample.Level < history[^1].Level) return [sample];
        if (sample.Level == history[^1].Level) return history;
        var updated = history.Append(sample).ToList();
        if (updated.Count > MaximumSamplesPerPlayer) updated.RemoveRange(1, updated.Count - MaximumSamplesPerPlayer);
        return updated;
    }

    public static string Describe(string playerName, LevelGap gap) =>
        string.Format(CultureInfo.InvariantCulture,
            "Player '{0}' went from level {1} to {2} with {3:0} minute(s) of play {4}; at most {5:0} level(s) would be expected. Check the player before acting: this rule only reports.",
            playerName, gap.FromLevel, gap.ToLevel, gap.PlaytimeMinutes, gap.Window, Math.Floor(gap.AllowedLevels));

    private static readonly Regex ExpRatePattern = new(@"(?<![A-Za-z])ExpRate=(?<v>[0-9]+(?:\.[0-9]+)?)", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>The server's experience rate from PalWorldSettings.ini (1 when the file or the setting is missing).</summary>
    public static double ReadExpRate(string settingsIni)
    {
        try
        {
            if (!File.Exists(settingsIni)) return 1;
            var match = ExpRatePattern.Match(File.ReadAllText(settingsIni));
            return match.Success && double.TryParse(match.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate) && rate > 0 ? rate : 1;
        }
        catch { return 1; }
    }
}
