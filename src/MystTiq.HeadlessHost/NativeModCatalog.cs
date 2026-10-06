// MystTiq v1.0.2.0: file reviewed for this release (2026-10-05).
using System.Text.Json;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.0.3 (reported 2026-10-05: "Pal Defender does not show up in the MODs. It has a different installation"). Some MODs
/// are DLLs the game loads through a proxy DLL next to PalServer-Win64-Shipping.exe rather than PAK or UE4SS Lua content:
/// PalDefender (its release ships d3d9.dll, which loads the DLLs d3d9_config.json lists; its wiki also shows version.dll)
/// and UE4SS itself (dwmapi.dll, or xinput1_3.dll for the 2.x layout). MystTiq lists them as NATIVE MODs. Switching one off
/// renames its loader (the game then loads the real system DLL and none of the MOD); switching it on renames it back.
/// Found live: both loaders had been renamed to *.disabled-test by hand during the 2026-10-01 troubleshooting, so neither
/// PalDefender nor any UE4SS MOD had loaded since, with nothing on the MODs page to say so.
/// </summary>
public static class NativeModCatalog
{
    public const string PalDefenderPackage = "PalDefender";
    public const string Ue4ssLoaderPackage = "UE4SS-Loader";

    /// <summary>The suffix MystTiq writes when it switches a loader off.</summary>
    public const string DisableSuffix = ".mysttiq-disabled";

    /// <summary>Suffixes a switched-off loader carries: MystTiq's own, an older MystTiq one, and the usual hand-made ones.</summary>
    public static readonly string[] DisabledSuffixes = [DisableSuffix, ".myst-disabled", ".disabled", ".disabled-test", ".off", ".bak-disabled"];

    private static readonly string[] PalDefenderLoaders = ["d3d9.dll", "version.dll"];
    private static readonly string[] Ue4ssLoaders = ["dwmapi.dll", "xinput1_3.dll"];

    /// <summary>
    /// PalDefender's state from the Win64 folder: installed when PalDefender.dll is there (or switched off as a file);
    /// on when a loader that loads it is there. d3d9.dll loads what d3d9_config.json lists (with no config yet it counts
    /// as loading it: the release ships no config); version.dll loads PalDefender.dll directly.
    /// </summary>
    public static NativeModState? PalDefender(IReadOnlyList<NativeFile> win64, string? d3d9Config)
    {
        var main = Find(win64, "PalDefender.dll");
        var mainOff = main is null ? NewestDisabled(win64, "PalDefender.dll") : null;
        if (main is null && mainOff is null) return null;

        string? problem = null;
        var active = PalDefenderLoaders.FirstOrDefault(l => Find(win64, l) is not null);
        if (active == "d3d9.dll" && d3d9Config is not null && !ConfigLoads(d3d9Config, "PalDefender.dll"))
        {
            problem = "d3d9.dll is there, but d3d9_config.json does not list PalDefender.dll, so it is not loaded. Add \"PalDefender.dll\" to load_dlls.";
            active = null;
        }
        var disabled = active is null ? PalDefenderLoaders.Select(l => NewestDisabled(win64, l)).FirstOrDefault(d => d is not null) : null;
        if (main is null)
            problem = "PalDefender.dll itself is switched off (renamed); put it back as PalDefender.dll.";
        else if (active is null && disabled is null && problem is null)
            problem = "PalDefender.dll is there, but nothing loads it: d3d9.dll from its release belongs next to it.";
        return new NativeModState(PalDefenderPackage, "PalDefender", main is not null && active is not null, active, disabled, problem);
    }

    /// <summary>The UE4SS loader's state, when UE4SS is installed (UE4SS.dll at the root or in ue4ss\).</summary>
    public static NativeModState? Ue4ssLoader(IReadOnlyList<NativeFile> win64, bool ue4ssInstalled)
    {
        if (!ue4ssInstalled) return null;
        var active = Ue4ssLoaders.FirstOrDefault(l => Find(win64, l) is not null);
        var disabled = active is null ? Ue4ssLoaders.Select(l => NewestDisabled(win64, l)).FirstOrDefault(d => d is not null) : null;
        var problem = active is null && disabled is null ? "UE4SS is installed, but its loader (dwmapi.dll) is missing, so no UE4SS MOD loads. Reinstall UE4SS." : null;
        return new NativeModState(Ue4ssLoaderPackage, "UE4SS loader", active is not null, active, disabled, problem);
    }

    /// <summary>
    /// The switched-off copy of a loader to restore: MystTiq's own first (it is what MystTiq switched off), else the newest.
    /// The owner's server had both dwmapi.dll.myst-disabled (July, an older UE4SS) and dwmapi.dll.disabled-test (August).
    /// </summary>
    public static NativeFile? NewestDisabled(IReadOnlyList<NativeFile> files, string loader)
    {
        var candidates = files.Where(f => DisabledSuffixes.Any(s => f.Name.Equals(loader + s, StringComparison.OrdinalIgnoreCase))).ToList();
        return candidates.FirstOrDefault(f => f.Name.EndsWith(DisableSuffix, StringComparison.OrdinalIgnoreCase))
               ?? candidates.OrderByDescending(f => f.LastWriteUtc).FirstOrDefault();
    }

    /// <summary>Whether a d3d9_config.json loads the DLL (its load_dlls array names it; case ignored).</summary>
    public static bool ConfigLoads(string configJson, string dll)
    {
        try
        {
            using var document = JsonDocument.Parse(configJson);
            if (!document.RootElement.TryGetProperty("load_dlls", out var list) || list.ValueKind != JsonValueKind.Array) return false;
            return list.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.String && string.Equals(Path.GetFileName(e.GetString()), dll, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException) { return false; }
    }

    private static NativeFile? Find(IReadOnlyList<NativeFile> files, string name) =>
        files.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

public sealed record NativeFile(string Name, DateTime LastWriteUtc);

/// <summary>A native MOD: on when its loader is in place; the loader in use, or the switched-off copy to restore; what is wrong.</summary>
public sealed record NativeModState(string Package, string Name, bool Enabled, string? ActiveLoader, NativeFile? DisabledLoader, string? Problem);
