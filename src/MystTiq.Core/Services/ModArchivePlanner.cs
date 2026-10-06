// MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
namespace MystTiq.Core.Services;

// v1.0.5.0 (roadmap M-1): what a MOD archive holds and whether MystTiq can install it where the game loads it. The MOD
// browser shows this before anything is installed, and the service decides by the same rules, so a ZIP dropped on the
// page and one fetched from a repository are treated alike. Archives MystTiq would install wrongly are refused with the
// reason rather than installed half: several PAKs (they overwrote each other), unreal_shimloader packages, loader DLLs
// and anything executable.
// v1.0.6.0 (roadmap M-2): MODs laid out as the game folder install too: a LogicMods PAK (a blueprint MOD for UE4SS) goes
// into Paks\LogicMods\<package> with the files beside it, and a PAK with UE4SS scripts installs as two parts under one
// name (the PAK where it belongs, the scripts as a UE4SS MOD folder). Both need UE4SS (the service checks).
public enum ModArchiveKind { Pak, Ue4ss, MixedPakAndScripts, LogicModsPak, SeveralPaks, SeveralUe4ssMods, Shimloader, NativeLoader, Executable, NoMod, UnsafePath }

public sealed record ModArchivePlan(ModArchiveKind Kind, bool Installable, string Summary, string? Ue4ssRoot, IReadOnlyList<string> Files)
{
    // v1.0.6.0: where the PAK goes ("~mods", or "LogicMods" with the files of its folder), and that folder in the archive.
    public string? PakTarget { get; init; }
    public string? PakDirectory { get; init; }

    // "PAK", "UE4SS" or "PAK+UE4SS" for an archive MystTiq installs; null otherwise.
    public string? InstallType => !Installable ? null : Kind switch
    {
        ModArchiveKind.Pak or ModArchiveKind.LogicModsPak => "PAK",
        ModArchiveKind.Ue4ss => "UE4SS",
        ModArchiveKind.MixedPakAndScripts => "PAK+UE4SS",
        _ => null,
    };
    public bool NeedsUe4ss => Installable && (Kind == ModArchiveKind.MixedPakAndScripts || PakTarget == "LogicMods");
}

public static class ModArchivePlanner
{
    public static readonly string[] PakExtensions = [".pak", ".ucas", ".utoc"];

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".com", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".scr", ".msi", ".msp",
        ".hta", ".cpl", ".lnk", ".reg", ".jar", ".sh",
    };

    // Proxy DLLs the game loads from beside its executable: PalDefender, UE4SS's own loader and the usual loader names.
    private static readonly HashSet<string> LoaderDlls = new(StringComparer.OrdinalIgnoreCase)
    {
        "PalDefender.dll", "UE4SS.dll", "d3d9.dll", "dwmapi.dll", "dxgi.dll", "winmm.dll", "version.dll", "xinput1_3.dll", "xinput1_4.dll",
    };

    public static ModArchivePlan Plan(IEnumerable<string> entryNames)
    {
        var files = entryNames.Select(n => n.Replace('\\', '/')).Where(n => n.Length > 0 && !n.EndsWith('/')).ToList();
        ModArchivePlan Refuse(ModArchiveKind kind, string summary) => new(kind, false, summary, null, files);

        if (files.Any(f => f.StartsWith('/') || f.Contains(':') || f.Split('/').Any(s => s == "..")))
            return Refuse(ModArchiveKind.UnsafePath, "The archive contains a path outside its own folder, so MystTiq will not install it.");
        var executable = files.FirstOrDefault(f => ExecutableExtensions.Contains(Path.GetExtension(f)));
        if (executable is not null)
            return Refuse(ModArchiveKind.Executable, $"The archive contains a program or script ({FileName(executable)}). MystTiq does not install archives that contain programs.");
        var loader = files.FirstOrDefault(f => LoaderDlls.Contains(FileName(f)));
        if (loader is not null)
            return Refuse(ModArchiveKind.NativeLoader, $"The archive contains {FileName(loader)}, a loader DLL that goes beside the server's executable, not a MOD folder. PalDefender updates from the Update Center; UE4SS installs from its own page.");
        var top = files.Select(f => f.Split('/')[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (files.Any(f => f.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)) && (top.Contains("mod") || top.Contains("pak") || top.Contains("cfg")))
            return Refuse(ModArchiveKind.Shimloader, "This is an unreal_shimloader package (Thunderstore's layout). It needs unreal_shimloader, which MystTiq does not manage.");

        var paks = files.Where(f => PakExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)).ToList();
        var roots = files.Select(Ue4ssRootOf).Where(r => r is not null).Select(r => r!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (roots.Count > 1)
            return Refuse(ModArchiveKind.SeveralUe4ssMods, $"The archive holds {roots.Count} UE4SS MODs. Install them one at a time.");
        if (paks.Count > 0)
        {
            var names = paks.Select(p => Path.GetFileNameWithoutExtension(FileName(p))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (names.Count > 1)
                return Refuse(ModArchiveKind.SeveralPaks, $"The archive holds {names.Count} different PAKs ({string.Join(", ", names.Take(4))}), often choices of one. Extract the one you want and install that.");
            var logic = paks.Any(p => p.Split('/').Any(s => s.Equals("LogicMods", StringComparison.OrdinalIgnoreCase)));
            var pakDirectory = paks[0].Contains('/') ? paks[0][..(paks[0].LastIndexOf('/') + 1)] : string.Empty;
            var where = logic ? "Pal\\Content\\Paks\\LogicMods" : "Pal\\Content\\Paks\\~mods";
            if (roots.Count == 1)
                return new(ModArchiveKind.MixedPakAndScripts, true, $"A PAK ({names[0]}) with UE4SS scripts, installed as two parts under one name: the PAK into {where}, the scripts as a UE4SS MOD folder.", roots[0], files)
                    { PakTarget = logic ? "LogicMods" : "~mods", PakDirectory = pakDirectory };
            if (logic)
                return new(ModArchiveKind.LogicModsPak, true, $"A blueprint MOD for UE4SS ({names[0]}), installed into Pal\\Content\\Paks\\LogicMods in its own folder.", null, files)
                    { PakTarget = "LogicMods", PakDirectory = pakDirectory };
            return new(ModArchiveKind.Pak, true, $"A PAK MOD ({names[0]}), installed into Pal\\Content\\Paks\\~mods.", null, files) { PakTarget = "~mods", PakDirectory = pakDirectory };
        }
        if (roots.Count == 1)
        {
            var where = roots[0].Length == 0 ? "at the archive's top" : $"in {roots[0].TrimEnd('/')}";
            return new(ModArchiveKind.Ue4ss, true, $"A UE4SS MOD (its scripts {where}), installed as a UE4SS MOD folder.", roots[0], files);
        }
        return Refuse(ModArchiveKind.NoMod, "No MOD found in the archive: no PAK, and no Scripts\\main.lua or dlls\\main.dll.");
    }

    // The folder (with a trailing '/', or "" for the archive's top) that holds a UE4SS MOD's Scripts/main.lua or dlls/main.dll.
    private static string? Ue4ssRootOf(string file)
    {
        foreach (var marker in new[] { "scripts/main.lua", "dlls/main.dll" })
        {
            if (file.Equals(marker, StringComparison.OrdinalIgnoreCase)) return string.Empty;
            if (file.EndsWith("/" + marker, StringComparison.OrdinalIgnoreCase)) return file[..^marker.Length];
        }
        return null;
    }

    private static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];
}
