using System.Diagnostics;
using System.Text.Json.Serialization;

namespace MystTiq.Core.Services;

// v0.8.17.0: process priority and eco mode for one server's own processes (on Windows the game itself is the
// grandchild PalServer-Win64-Shipping-Cmd.exe, so the policy is applied to every managed process, not only the one
// MystTiq launched). "Default" priority means MystTiq leaves the priority alone.
[JsonConverter(typeof(JsonStringEnumConverter<ServerPriorityLevel>))]
public enum ServerPriorityLevel { Default, BelowNormal, Normal, AboveNormal, High }

// Eco mode: the operating system's efficiency mode (Windows EcoQoS) plus below-normal priority. On means always;
// WhenEmpty means only after nobody has been online for EcoAfterEmptyMinutes, back to full speed when someone joins.
[JsonConverter(typeof(JsonStringEnumConverter<ServerEcoMode>))]
public enum ServerEcoMode { Off, On, WhenEmpty }

// v0.8.24.0: Cores pins the server to some processor cores ("0-3, 6"); empty means every core (MystTiq leaves the
// affinity alone, or gives back every core where it had pinned them).
public sealed record ServerResourcePolicy(
    ServerPriorityLevel Priority = ServerPriorityLevel.Default,
    ServerEcoMode EcoMode = ServerEcoMode.Off,
    int EcoAfterEmptyMinutes = 10,
    string? Cores = null)
{
    public const int MinimumEmptyMinutes = 1;
    public const int MaximumEmptyMinutes = 240;

    public IReadOnlyList<string> Validate(int? processorCount = null)
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(Priority)) errors.Add("Unknown priority.");
        if (!Enum.IsDefined(EcoMode)) errors.Add("Unknown eco mode.");
        if (EcoAfterEmptyMinutes is < MinimumEmptyMinutes or > MaximumEmptyMinutes)
            errors.Add($"Minutes before eco mode must be between {MinimumEmptyMinutes} and {MaximumEmptyMinutes}.");
        if (CoreSelection.Parse(Cores, processorCount ?? Environment.ProcessorCount).Error is { } coreError) errors.Add(coreError);
        return errors;
    }
}

// v0.8.24.0: the core list, as a person writes it ("0-3, 6") and as the operating system takes it (a bit mask).
// Cores are numbered from 0 as Task Manager and /proc/cpuinfo number them. At most 64: Windows sets a process's affinity
// within one processor group of 64, and MystTiq does not spread a server over groups.
public static class CoreSelection
{
    public const int MaximumCores = 64;

    public static int UsableCores(int processorCount) => Math.Clamp(processorCount, 1, MaximumCores);

    public static ulong AllCoresMask(int processorCount) =>
        UsableCores(processorCount) == 64 ? ulong.MaxValue : (1UL << UsableCores(processorCount)) - 1;

    // Null cores (and no error) means every core.
    public static (IReadOnlyList<int>? Cores, string? Error) Parse(string? text, int processorCount)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, null);
        var usable = UsableCores(processorCount);
        var cores = new SortedSet<int>();
        foreach (var raw in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var ends = raw.Split('-', StringSplitOptions.TrimEntries);
            if (ends.Length is < 1 or > 2 || !int.TryParse(ends[0], out var first) || (ends.Length == 2 && !int.TryParse(ends[1], out _)))
                return (null, $"\"{raw}\" is not a core or a range (write cores like 0-3, 6).");
            var last = ends.Length == 2 ? int.Parse(ends[1]) : first;
            if (first > last) return (null, $"\"{raw}\" runs backwards (write the lower core first).");
            if (first < 0 || last >= usable)
                return (null, $"Core {(first < 0 ? first : last)} does not exist: this machine has cores 0-{usable - 1}.");
            for (var core = first; core <= last; core++) cores.Add(core);
        }
        if (cores.Count == 0) return (null, "Choose at least one core, or leave the list empty for every core.");
        return (cores.ToArray(), null);
    }

    public static ulong Mask(IEnumerable<int> cores) => cores.Aggregate(0UL, (mask, core) => mask | (1UL << core));

    public static IReadOnlyList<int> CoresOf(ulong mask) => Enumerable.Range(0, 64).Where(core => (mask & (1UL << core)) != 0).ToArray();

    // "0-3, 6", or "all 12" when the mask is every core of this machine.
    public static string Describe(ulong mask, int processorCount)
    {
        if ((mask & AllCoresMask(processorCount)) == AllCoresMask(processorCount)) return $"all {UsableCores(processorCount)}";
        var cores = CoresOf(mask);
        var parts = new List<string>();
        for (var i = 0; i < cores.Count;)
        {
            var j = i;
            while (j + 1 < cores.Count && cores[j + 1] == cores[j] + 1) j++;
            parts.Add(j == i ? $"{cores[i]}" : j == i + 1 ? $"{cores[i]}, {cores[j]}" : $"{cores[i]}-{cores[j]}");
            i = j + 1;
        }
        return string.Join(", ", parts);
    }
}

public sealed record EcoDecision(bool Active, string Reason);

// The pure rules, so the logic harness can check every branch without a real process.
public static class ResourcePolicyDecision
{
    // When nobody is online, how long has that been? Unknown player counts reset the clock: eco mode never starts on a
    // guess.
    public static DateTimeOffset? NextEmptySince(DateTimeOffset? previous, int? playersOnline, DateTimeOffset now) =>
        playersOnline == 0 ? previous ?? now : null;

    public static EcoDecision Eco(ServerResourcePolicy policy, int? playersOnline, DateTimeOffset? emptySinceUtc, DateTimeOffset now)
    {
        switch (policy.EcoMode)
        {
            case ServerEcoMode.Off:
                return new(false, "Eco mode is off.");
            case ServerEcoMode.On:
                return new(true, "Eco mode is on.");
        }

        var minutes = policy.EcoAfterEmptyMinutes;
        if (playersOnline is null)
            return new(false, "Eco mode is waiting: who is online cannot be read (it needs the Palworld REST API or RCON).");
        if (playersOnline > 0)
            return new(false, playersOnline == 1 ? "1 player online: full speed." : $"{playersOnline} players online: full speed.");
        var since = emptySinceUtc ?? now;
        var left = since.AddMinutes(minutes) - now;
        if (left <= TimeSpan.Zero)
            return new(true, $"No one has been online for {minutes} min: eco mode.");
        return new(false, $"No one online: eco mode in {Math.Max(1, (int)Math.Ceiling(left.TotalMinutes))} min.");
    }

    // Eco mode runs below normal whatever the chosen priority; otherwise the chosen priority, or none for Default.
    public static ProcessPriorityClass? TargetPriority(ServerResourcePolicy policy, bool ecoActive) => ecoActive
        ? ProcessPriorityClass.BelowNormal
        : policy.Priority switch
        {
            ServerPriorityLevel.BelowNormal => ProcessPriorityClass.BelowNormal,
            ServerPriorityLevel.Normal => ProcessPriorityClass.Normal,
            ServerPriorityLevel.AboveNormal => ProcessPriorityClass.AboveNormal,
            ServerPriorityLevel.High => ProcessPriorityClass.High,
            _ => null,
        };

    // Efficiency mode: on while eco is active; switched back to the system default only on processes MystTiq itself
    // put into efficiency mode, so a setting made elsewhere is left alone.
    public static bool? TargetEfficiency(bool ecoActive, bool appliedByMystTiq) =>
        ecoActive ? true : appliedByMystTiq ? false : null;

    // v0.8.24.0: the cores the process should run on: the chosen ones; every core again where MystTiq pinned it and the
    // list is now empty; otherwise none (a process pinned by something else is left alone). Eco mode does not change it.
    public static ulong? TargetAffinity(ServerResourcePolicy policy, int processorCount, bool pinnedByMystTiq)
    {
        var (cores, error) = CoreSelection.Parse(policy.Cores, processorCount);
        if (error is not null) return null;
        if (cores is not null) return CoreSelection.Mask(cores);
        return pinnedByMystTiq ? CoreSelection.AllCoresMask(processorCount) : null;
    }
}
