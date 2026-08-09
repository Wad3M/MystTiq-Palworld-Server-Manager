using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Non-destructive static capability analysis for installed UE4SS mods.
/// MystTiq reads scripts only; it never rewrites/injects third-party MOD code.
/// </summary>
public sealed class ModCapabilityAnalysisService
{
    private readonly AppSettings settings;

    public ModCapabilityAnalysisService(AppSettings settings) =>
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

    public ModCapabilityProfile Analyze(ModRow mod)
    {
        if (!IsUe4ss(mod))
            return new ModCapabilityProfile("Not applicable", [], [], "Runtime capability analysis is only used for UE4SS mods.");

        var root = new Ue4ssRuntimeResolver(settings).GetActiveModsRoot();
        var folder = FindModFolder(root, mod);
        if (folder is null)
            return new ModCapabilityProfile("Unknown UE4SS", [], [], "No matching active-root source folder was available for static analysis.");

        var files = SafeFiles(folder).ToList();
        var luaFiles = files.Where(path => path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)).Take(50).ToList();
        var dllFiles = files.Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
        var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var lua in luaFiles)
        {
            string source;
            try { source = File.ReadAllText(lua); }
            catch { continue; }

            if (Contains(source, "RegisterHook(")) { capabilities.Add("RegisterHook"); expected.Add("Hook callback/activity"); }
            if (Contains(source, "RegisterConsoleCommandHandler(")) { capabilities.Add("Console command handler"); expected.Add("Console command response"); }
            if (Contains(source, "RegisterKeyBind(")) { capabilities.Add("Key binding"); expected.Add("Keybind callback"); }
            if (Contains(source, "ExecuteInGameThread(")) { capabilities.Add("Game-thread execution"); expected.Add("Game-thread activity"); }
            if (Contains(source, "LoopAsync(") || Contains(source, "ExecuteAsync(")) { capabilities.Add("Async callback"); expected.Add("Async runtime activity"); }
            if (source.Contains("BeginPlay", StringComparison.OrdinalIgnoreCase)) { capabilities.Add("BeginPlay/event hook"); expected.Add("Lifecycle callback"); }
            if (Regex.IsMatch(source, @"\bprint\s*\(", RegexOptions.IgnoreCase)) { capabilities.Add("Runtime logging"); expected.Add("Mod-generated log output"); }
        }

        var nativeSignals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dll in dllFiles.Take(20))
        {
            try
            {
                using var stream = File.OpenRead(dll);
                using var reader = new BinaryReader(stream);
                if (stream.Length >= 64 && reader.ReadUInt16() == 0x5A4D)
                {
                    capabilities.Add("Windows PE DLL");
                    nativeSignals.Add("Valid PE image");
                }

                // Static printable-string inspection only. This does not load or execute
                // the third-party DLL. Tokens are evidence of capability, not proof of execution.
                stream.Position = 0;
                var bytes = reader.ReadBytes((int)Math.Min(stream.Length, 16 * 1024 * 1024));
                var printable = ExtractPrintableAscii(bytes);
                foreach (var token in new[] { "UE4SS", "RegisterHook", "Unreal", "Palworld", "PalServer", "hook", "callback", "blocked", "filter", "duplicate" })
                    if (printable.Contains(token, StringComparison.OrdinalIgnoreCase))
                        nativeSignals.Add(token);
            }
            catch { }
        }
        if (nativeSignals.Count > 0)
        {
            capabilities.Add("Native static signatures");
            expected.Add("UE4SS native loader acknowledgement");
            expected.Add("Mod-specific runtime log/activity");
        }

        var kind = dllFiles.Count > 0 && luaFiles.Count == 0 ? "UE4SS Native/C++"
            : luaFiles.Count > 0 && dllFiles.Count > 0 ? "UE4SS Hybrid"
            : luaFiles.Count > 0 ? "UE4SS Lua"
            : "UE4SS";

        var detail = capabilities.Count == 0
            ? $"Analyzed {luaFiles.Count} Lua file(s) and {dllFiles.Count} DLL file(s); no known observable capability signature was found."
            : $"Analyzed {luaFiles.Count} Lua file(s) and {dllFiles.Count} DLL file(s). Detected: {string.Join(", ", capabilities)}." +
              (nativeSignals.Count > 0 ? $" Native static signals: {string.Join(", ", nativeSignals.Take(10))}." : "");

        return new ModCapabilityProfile(kind, capabilities.ToList(), expected.ToList(), detail);
    }

    private static string ExtractPrintableAscii(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length / 8);
        var current = new StringBuilder();
        foreach (var value in bytes)
        {
            if (value is >= 32 and <= 126)
            {
                current.Append((char)value);
                continue;
            }
            if (current.Length >= 4) builder.AppendLine(current.ToString());
            current.Clear();
        }
        if (current.Length >= 4) builder.AppendLine(current.ToString());
        return builder.ToString();
    }

    private static bool Contains(string source, string token) =>
        source.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static bool IsUe4ss(ModRow mod) =>
        mod.Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) ||
        mod.Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase);

    private static string? FindModFolder(string root, ModRow mod)
    {
        if (!Directory.Exists(root)) return null;
        var aliases = ModRuntimeEvidenceEngine.BuildAliases(mod);
        try
        {
            return Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(dir =>
                {
                    var name = Path.GetFileName(dir);
                    return aliases.Any(alias => Normalize(alias).Equals(Normalize(name), StringComparison.OrdinalIgnoreCase));
                });
        }
        catch { return null; }
    }

    private static IEnumerable<string> SafeFiles(string folder)
    {
        try { return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories); }
        catch { return []; }
    }

    private static string Normalize(string value) => Regex.Replace(value ?? "", "[^a-zA-Z0-9]", "");
}

public sealed record ModCapabilityProfile(
    string ModKind,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> ExpectedEvidence,
    string Detail)
{
    public bool IsEventDriven => Capabilities.Any(value =>
        value.Contains("Hook", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("callback", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("handler", StringComparison.OrdinalIgnoreCase));
}
