using System.Globalization;
using System.Text.RegularExpressions;

namespace MystTiq.Core.Services;

// v0.7.97.0: a catalog of known server-failure signatures, each with a plain-language cause and
// concrete things to try. Pure (no file or process access), so the logic harness can test it.
//
// The previous analyzer counted keyword hits. That mislabelled ordinary text (the needle "oom"
// also matched "room" and "zoom"), flagged any line that merely mentioned UE4SS, and could not say
// what a finding meant or what to do about it. Here every pattern is anchored on word boundaries or
// a specific message, every line is claimed by exactly one signature (most specific first) so one
// crash is not reported three times, and each signature carries a cause and fixes.
public sealed record CrashSignature(string Id, string Title, string Severity, string Cause, IReadOnlyList<string> Fixes);

public sealed record CrashMatchedLine(string Text, DateTimeOffset? At);

public sealed record CrashSignatureMatch(CrashSignature Signature, IReadOnlyList<CrashMatchedLine> Lines);

public static class CrashSignatureCatalog
{
    private sealed record Entry(CrashSignature Signature, Regex[] Patterns);

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    private static Regex R(string pattern) => new(pattern, Options, MatchTimeout);

    // Order matters: a line is claimed by the FIRST signature that matches it, so specific causes
    // come before the generic "fatal error" catch-all.
    private static readonly Entry[] Entries =
    [
        new(new("missing-runtime-dll", "Missing or broken runtime library", "Critical",
                "Windows could not load a library the server needs (usually the Microsoft Visual C++ runtime), so the server fails at start-up or shortly after.",
                [
                    "Install the latest Microsoft Visual C++ Redistributable (2015-2022, x64) from Microsoft and restart the server.",
                    "Run Palworld's Validate Server Files (Update Center) in case a game file is missing or damaged.",
                    "If a mod or UE4SS was just installed, remove it: an incompatible native library gives the same message."
                ]),
            [R(@"\b0xc0000135\b"), R(@"\b0xc000007b\b"), R(@"\bVCRUNTIME\d+"), R(@"\bMSVCP\d+"), R(@"\bapi-ms-win-crt"), R(@"The code execution cannot proceed"), R(@"\.dll\b.*\bwas not found\b")]),

        new(new("out-of-memory", "Out of memory", "Critical",
                "The server or the machine ran out of memory. Palworld servers are commonly reported to use more memory the longer they stay up and the more bases and Pals exist, so this tends to appear after long uptimes or on small machines. Exit code 137 on Linux is a SIGKILL, most often the kernel's out-of-memory killer, though anything that force-kills the process gives the same code.",
                [
                    "Check the Historical Metrics page: memory climbing steadily until the failure points at a leak or an undersized machine.",
                    "Add memory or swap, or lower the player count and the number of bases and Pals.",
                    "Schedule regular restarts with Automation so memory is released before it runs out.",
                    "If it began after adding a mod, disable the newest one and compare."
                ]),
            [R(@"\bout of memory\b"), R(@"\bOutOfMemory\b"), R(@"\bOOM\b"), R(@"\boom-kill"), R(@"mimalloc: error"), R(@"\bfailed to allocate \d+"), R(@"\bnot enough (virtual |physical )?memory\b"), R(@"Killed process \d+")]),

        new(new("stack-overflow", "Stack overflow", "Critical",
                "Code recursed until the stack ran out. In a modded server this is almost always a script or native mod calling itself, and it kills the process immediately.",
                [
                    "Disable the most recently added or updated mod (MOD Library) and start again.",
                    "If no mods are installed, verify server files and report it with the crash report attached."
                ]),
            [R(@"EXCEPTION_STACK_OVERFLOW"), R(@"\b0xc00000fd\b"), R(@"\bstack overflow\b")]),

        new(new("access-violation", "Access violation (native crash)", "Critical",
                "The process touched memory it should not have. This is a native crash, in the game itself or in a native mod or UE4SS. It is a symptom, not a cause, so the useful question is what changed just before it.",
                [
                    "Disable the most recently changed mod, and UE4SS itself if it was just updated, then reproduce under observation.",
                    "A game update commonly breaks UE4SS and native mods until they are updated: check the Update Center for a matching UE4SS build.",
                    "Validate Palworld server files and check that the save is not corrupt (World Validator)."
                ]),
            [R(@"EXCEPTION_ACCESS_VIOLATION"), R(@"\baccess violation\b"), R(@"\b0xc0000005\b"), R(@"\bsegmentation fault\b"), R(@"\bSIGSEGV\b")]),

        new(new("disk-full", "Disk full", "Critical",
                "The drive holding the server, its saves or its backups has no free space, so saves and backups fail and the server can stop or corrupt a save.",
                [
                    "Free space on that drive now, before the next save.",
                    "Use Backups retention to prune old backups, and move the backup root to a larger drive.",
                    "After freeing space, verify the newest backup and the current save."
                ]),
            [R(@"\bno space left on device\b"), R(@"\bnot enough space on the disk\b"), R(@"\bdisk (is )?full\b"), R(@"\bENOSPC\b"), R(@"\binsufficient disk space\b")]),

        new(new("port-in-use", "Port already in use", "Critical",
                "Another program, or a second copy of the server, is already using the game or query port, so this server cannot start listening.",
                [
                    "Open All Instances and stop any stray PalServer process.",
                    "Run the port check in Diagnostics to see what holds the port.",
                    "If you run several servers on this machine, give each its own game port (and RCON/REST port)."
                ]),
            [R(@"\baddress already in use\b"), R(@"only one usage of each socket address"), R(@"\bWSAEADDRINUSE\b"), R(@"\bfailed to bind\b"), R(@"\bcould not bind\b"), R(@"\bbind\b.{0,20}\bfailed\b")]),

        new(new("save-io", "Save data could not be read or written", "Critical",
                "The server had trouble loading or writing world or player save data. A damaged save or a full or failing disk are the usual causes.",
                [
                    "Do not keep running on it: stop the server and copy the current save folder somewhere safe.",
                    "Use Backups to verify the newest backup, and World Validator to inspect the current save.",
                    "Restore the last good backup if the current save fails validation."
                ]),
            [R(@"\bfailed to (load|read|write|save)\b.{0,60}(\.sav\b|save ?data|save ?game|world)"), R(@"\bcorrupt(ed)?\b.{0,40}\b(save|level\.sav|world)\b"), R(@"\b(save|level\.sav)\b.{0,40}\bcorrupt"), R(@"\bfailed to deserialize\b")]),

        new(new("shutdown-failure", "Shutdown did not complete", "Warning",
                "MystTiq asked the server to stop and it did not exit cleanly or in time, so it may have been forced to end and could have lost unsaved progress.",
                [
                    "Use Force Stop if the process is still running, then check All Instances for leftovers.",
                    "Save before stopping, and give the server more time on busy worlds.",
                    "If it recurs, look for a mod or a very large world slowing shutdown."
                ]),
            [R(@"\bshutdown timed out\b"), R(@"\bfailed to (stop|terminate)\b"), R(@"\bkill failed\b"), R(@"\bcould not stop\b")]),

        new(new("hang", "Server stopped responding", "Warning",
                "The server was alive but not answering, which players notice as freezing or timeouts. Overload, a stuck mod or a very slow disk are the usual causes.",
                [
                    "Check CPU, memory and disk activity in Historical Metrics around that time.",
                    "Look for a mod that was doing work when it happened (MOD Library, Crash Analyzer mod mentions).",
                    "Try a restart, and if it recurs, disable the newest mod."
                ]),
            [R(@"\bwatchdog\b"), R(@"\bhang detected\b"), R(@"\bnot responding\b"), R(@"\bdeadlock\b")]),

        new(new("ue4ss-error", "UE4SS or mod script error", "Warning",
                "UE4SS (the mod loader) or a script mod reported an error. This is only a problem if it keeps happening or is followed by a crash, and it usually means a mod that no longer matches the current game version.",
                [
                    "Note which mod the evidence names below and disable it in MOD Library to see whether the errors stop.",
                    "Update UE4SS and the mod: a game update commonly breaks them until they are updated.",
                    "If a crash follows these errors, treat the named mod as the first suspect."
                ]),
            [R(@"\bLua error\b"), R(@"\[Lua\].*\b(error|exception)\b"), R(@"(?=.*\bUE4SS\b)(?=.*\b(?:error|exception|fail\w*|crash\w*|fatal)\b)"), R(@"\bAOB\b.*\bfail\w*\b")]),

        new(new("steam-init", "Steam did not initialise", "Warning",
                "The server could not start its Steam connection. Depending on the setup this can be harmless, but it can stop players joining through Steam.",
                [
                    "Check that the machine can reach Steam and that no firewall is blocking the server.",
                    "Validate server files, and confirm the query port is open if the server is not appearing in lists."
                ]),
            [R(@"\bSteamAPI_Init\b"), R(@"\bsteamclient(64)?\.dll\b"), R(@"\bSteam\b.{0,30}\b(init\w*|initiali[sz]ation)\b.{0,20}\bfail\w*\b"), R(@"\bfailed to init\w* Steam\b")]),

        // v0.8.9.0: from a real Palworld crash report (UE 5.1.1, LowLevelFatalError in Containers\Array.cpp). Only what the
        // report states is claimed: the check that fired, not which game feature triggered it.
        new(new("engine-array-size", "Engine stopped on an invalid array size", "Critical",
                "The game engine (Unreal Engine) stopped on one of its own safety checks: code tried to give an internal list (a TArray) a size that is not allowed. That is a programming error in the game itself or in a native mod, not a setting that can be changed. The crash report records the check that fired, but not which part of the game hit it.",
                [
                    "Note what was happening on the server at that time (players online, raids, base activity) and whether it happens again; a single occurrence can be a one-off.",
                    "If native mods or UE4SS are installed, disable them and see whether it recurs. If it recurs with none installed, it is the game's own bug.",
                    "Keep the crash report folder under Pal\\Saved\\Crashes (it holds a minidump) and include it when reporting the crash to Pocketpair."
                ]),
            [R(@"Trying to resize TArray to an invalid size")]),

        new(new("ue-fatal", "Fatal engine error", "Critical",
                "The game engine reported a fatal error or a failed assertion and stopped. The lines below say where. This is the catch-all for crashes that match no more specific signature.",
                [
                    "Read the evidence lines for a mod or a file name, and disable that mod first.",
                    "Validate server files, and verify the newest backup before running further.",
                    "If it recurs with no mods installed, report it with the crash report attached."
                ]),
            [R(@"\bfatal error\b"), R(@"\bLowLevelFatalError\b"), R(@"===\s*Critical error"), R(@"\bassertion failed\b"), R(@"\bunhandled exception\b"), R(@"\bcrash reporter\b"), R(@"\bcall stack\b"), R(@"\bstack trace\b")]),
    ];

    public static IReadOnlyList<CrashSignature> All { get; } = Entries.Select(e => e.Signature).ToArray();

    public static CrashSignature? Find(string id) => All.FirstOrDefault(s => s.Id.Equals(id, StringComparison.Ordinal));

    private static readonly Regex ExitCodeLine = new(@"\bexited with code (-?\d+)\b", Options, MatchTimeout);
    private static readonly Regex TimestampLine = new(@"^\s*\[(\d{4})[.\-](\d{2})[.\-](\d{2})[-T ](\d{2})[.:](\d{2})[.:](\d{2})", RegexOptions.CultureInvariant, MatchTimeout);

    // Maps a process exit code to the signature it points at, or null when the code carries no
    // failure meaning (0 is a clean exit and -1 is what Windows reports when the real code is
    // unavailable, so neither is evidence of anything).
    public static string? SignatureForExitCode(long code) => code switch
    {
        3221225477 or -1073741819 => "access-violation",
        3221225725 or -1073741571 => "stack-overflow",
        3221225781 or -1073741515 or 3221225595 or -1073741701 => "missing-runtime-dll",
        3221226505 or -1073740791 => "ue-fatal",
        137 => "out-of-memory",
        139 => "access-violation",
        134 => "ue-fatal",
        _ => null
    };

    // Reads a leading "[2026.09.21-08.12.33" or "[2026-09-21 08:12:33" stamp as local time.
    public static DateTimeOffset? TryParseTimestamp(string line)
    {
        var m = TimestampLine.Match(line);
        if (!m.Success) return null;
        try
        {
            var local = new DateTime(
                int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[6].Value, CultureInfo.InvariantCulture), DateTimeKind.Local);
            return new DateTimeOffset(local);
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    // Each line is claimed by at most one signature. Signatures with no matching line are absent.
    public static IReadOnlyList<CrashSignatureMatch> Match(IEnumerable<string> lines)
    {
        var claimed = new Dictionary<string, List<CrashMatchedLine>>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var line = raw.Length > 600 ? raw[..600] : raw;
            var id = Classify(line);
            if (id is null) continue;
            if (!claimed.TryGetValue(id, out var list)) claimed[id] = list = [];
            list.Add(new CrashMatchedLine(line.Trim(), TryParseTimestamp(line)));
        }
        return Entries
            .Where(e => claimed.ContainsKey(e.Signature.Id))
            .Select(e => new CrashSignatureMatch(e.Signature, claimed[e.Signature.Id]))
            .ToArray();
    }

    private static string? Classify(string line)
    {
        try
        {
            var exit = ExitCodeLine.Match(line);
            // An exit-code line is only ever classified by its code; it must not also match by words.
            if (exit.Success)
                return long.TryParse(exit.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var code)
                    ? SignatureForExitCode(code)
                    : null;

            foreach (var entry in Entries)
                foreach (var pattern in entry.Patterns)
                    if (pattern.IsMatch(line)) return entry.Signature.Id;
        }
        catch (RegexMatchTimeoutException) { }
        return null;
    }
}
