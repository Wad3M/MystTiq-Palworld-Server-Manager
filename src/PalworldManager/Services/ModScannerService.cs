using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Builds the logical installed-mod inventory from Workshop, managed ZIP, PAK, and UE4SS sources.
/// The service is read-only apart from normalizing legacy MystTiq install-manifest identities.
/// </summary>
public sealed class ModScannerService
{
    private readonly AppSettings settings;
    private readonly Ue4ssRuntimeResolver ue4ssResolver;
    private readonly RuntimeStateService runtimeState;
    private readonly WorkshopIdentityService workshopIdentity = new();
    private static readonly string[] PakExtensions = [".pak", ".ucas", ".utoc"];
    private static readonly HashSet<string> KnownUe4ssRuntimeComponents = new(StringComparer.OrdinalIgnoreCase)
    {
        "ActorDumperMod", "BPML_GenericFunctions", "BPModLoaderMod", "CheatManagerEnablerMod",
        "ConsoleCommandsMod", "ConsoleEnablerMod", "jsbLuaProfilerMod", "Keybinds",
        "LineTraceMod", "shared", "SplitScreenMod"
    };

    public ModScannerService(AppSettings settings)
        : this(settings, new Ue4ssRuntimeResolver(settings), new RuntimeStateService()) { }

    public ModScannerService(AppSettings settings, Ue4ssRuntimeResolver ue4ssRuntimeResolver, RuntimeStateService runtimeState)
    {
        this.settings = settings;
        this.ue4ssResolver = ue4ssRuntimeResolver ?? throw new ArgumentNullException(nameof(ue4ssRuntimeResolver));
        this.runtimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
    }

    private string ActiveUe4ssModsRoot => ue4ssResolver.GetActiveModsRoot();

    public List<ModRow> Scan()
    {
        Directory.CreateDirectory(settings.WorkshopRoot);
        Directory.CreateDirectory(settings.DisabledWorkshopRoot);
        Directory.CreateDirectory(settings.ManagedModsRoot);
        NormalizeManagedUe4ssManifestIdentities();

        var enabled = ReadEnabledPackages();
        var rows = new Dictionary<string, ModRow>(StringComparer.OrdinalIgnoreCase);
        var workshopAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Build one logical inventory record for each top-level Workshop item. Older
        // versions scanned Info.json, the MystTiq manifest, and the folder itself in
        // separate passes, which could display the same Workshop item multiple times.
        var workshopInventoryFolders = Directory.EnumerateDirectories(settings.WorkshopRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(folder => (Folder: folder, Active: true))
            .Concat(Directory.EnumerateDirectories(settings.DisabledWorkshopRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(folder => (Folder: folder, Active: false)));

        foreach (var workshopEntry in workshopInventoryFolders)
        {
            var folder = workshopEntry.Folder;
            var workshopActive = workshopEntry.Active;
            try
            {
                var folderId = Path.GetFileName(folder);
                var manifestPath = Directory.EnumerateFiles(folder, "myst-install-manifest.json", SearchOption.AllDirectories).FirstOrDefault();
                var infoPath = Directory.EnumerateFiles(folder, "Info.json", SearchOption.AllDirectories).FirstOrDefault();

                string workshopId = folderId;
                string name = "";
                string type = "";
                string package = "";

                if (manifestPath is not null)
                {
                    using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    workshopId = Get(manifestDocument.RootElement, "WorkshopId");
                    if (string.IsNullOrWhiteSpace(workshopId)) workshopId = folderId;
                    name = Get(manifestDocument.RootElement, "Name");
                    type = Get(manifestDocument.RootElement, "Type");
                }

                if (infoPath is not null)
                {
                    using var infoDocument = JsonDocument.Parse(File.ReadAllText(infoPath));
                    package = Get(infoDocument.RootElement, "PackageName");
                    if (string.IsNullOrWhiteSpace(name)) name = Get(infoDocument.RootElement, "Name");
                    if (string.IsNullOrWhiteSpace(name)) name = Get(infoDocument.RootElement, "DisplayName");
                }

                if (string.IsNullOrWhiteSpace(package))
                    package = "Workshop_" + workshopId;
                if (string.IsNullOrWhiteSpace(name))
                    name = ReadWorkshopDisplayName(folder) ?? $"Workshop Mod {workshopId}";
                name = workshopIdentity.ResolveDisplayName(workshopId, name);
                if (string.IsNullOrWhiteSpace(type))
                    type = DetectModType(folder);

                var deployed = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                    .Any(path => !path.EndsWith("myst-install-manifest.json", StringComparison.OrdinalIgnoreCase));

                var runtimeAliases = FindUe4ssRuntimeAliases(folder);

                rows[package] = new ModRow
                {
                    Package = package,
                    Name = name,
                    Version = ReadVersionFromFolder(folder),
                    Enabled = workshopActive && deployed,
                    Deployed = deployed,
                    Source = $"Steam Workshop {workshopId}",
                    Type = type,
                    Description = ReadLocalDescription(folder),
                    RuntimeAliases = runtimeAliases,
                    EnableReason = !deployed
                        ? "Workshop package has no deployable files."
                        : workshopActive
                            ? "Workshop package is present in the active Workshop folder."
                            : "Workshop package is installed but stored in MystTiq's disabled Workshop folder."
                };

                // Remember every deployable component owned by this Workshop item.
                // Loose PAK and UE4SS scans use these aliases to avoid creating a
                // second row for the deployed copy of the same logical mod.
                workshopAliases.Add(package);
                workshopAliases.Add("Workshop_" + workshopId);
                foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    if (IsPakOrDisabledPak(file))
                        workshopAliases.Add(GetPackageFromPakPath(file));
                }
                foreach (var directory in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories))
                {
                    var directoryName = Path.GetFileName(directory);
                    if (!string.IsNullOrWhiteSpace(directoryName) &&
                        !KnownUe4ssRuntimeComponents.Contains(directoryName) &&
                        Directory.EnumerateFiles(directory, "*.lua", SearchOption.AllDirectories).Any())
                        workshopAliases.Add(directoryName);
                }
            }
            catch
            {
                // A damaged third-party package must not prevent the remaining
                // server inventory from being displayed.
            }
        }

        // Mods installed by this manager from ordinary ZIP packages.
        foreach (var manifestPath in Directory.EnumerateFiles(settings.ManagedModsRoot, "InstallManifest.json", SearchOption.AllDirectories))
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath));
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.Package))
                    continue;

                // A legacy manager build may have created a second manifest for a
                // Workshop item. Prefer the real Workshop inventory record.
                if (workshopAliases.Contains(manifest.Package))
                    continue;

                var managedType = string.IsNullOrWhiteSpace(manifest.Type) ||
                                  manifest.Type.Equals("Palworld Mod Package", StringComparison.OrdinalIgnoreCase)
                    ? DetectManagedType(manifest.Files)
                    : manifest.Type;
                var managedState = DetermineManagedState(manifest, managedType, enabled);

                rows[manifest.Package] = new ModRow
                {
                    Package = manifest.Package,
                    Name = string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Package : manifest.Name,
                    Version = manifest.Version,
                    Enabled = managedState.Enabled,
                    Deployed = managedState.Deployed,
                    Source = "ZIP",
                    Type = managedType,
                    RuntimeAliases = FindManagedUe4ssRuntimeAliases(manifest.Files),
                    EnableReason = managedState.Reason
                };
            }
            catch
            {
                // A damaged manifest should not prevent the Mods page from opening.
            }
        }

        // Detect genuine unmanaged mods while suppressing files and UE4SS folders
        // that are already owned by a Workshop inventory record.
        ScanLoosePakMods(rows, enabled, workshopAliases);
        ScanUe4ssMods(rows, enabled, workshopAliases);
        AnnotateUe4ssRuntimeState(rows.Values);

        return rows.Values
            .GroupBy(row => BuildLogicalInventoryKey(row), StringComparer.OrdinalIgnoreCase)
            .Select(group => MergeLogicalRows(group))
            .OrderBy(row => row.Name)
            .ToList();
    }

    private void AnnotateUe4ssRuntimeState(IEnumerable<ModRow> rows)
    {
        // Runtime load evidence is dynamic while PalServer is running. A scanner
        // pass must not reuse a pre-start/session-cached snapshot or every row can
        // remain stuck at "Not loaded" after UE4SS has actually started the mods.
        var info = ue4ssResolver.Refresh();
        runtimeState.Observe(info);
        var materialized = rows.ToList();
        foreach (var row in materialized)
        {
            var isUe4ss = row.Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) || row.Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase);
            if (!isUe4ss) continue;

            var candidates = new[] { row.Package, row.Name }
                .Concat(row.RuntimeAliases ?? [])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            row.PresentInActiveRuntime = candidates.Any(value => Directory.Exists(Path.Combine(info.ActiveModsRoot, value)));
        }
        runtimeState.ApplyTo(materialized);
    }

    private static string BuildLogicalInventoryKey(ModRow row)
    {
        var workshopId = ExtractWorkshopId(row.Source);
        if (!string.IsNullOrWhiteSpace(workshopId)) return "workshop:" + workshopId;
        return "package:" + row.Package;
    }

    private static string ExtractWorkshopId(string source)
    {
        const string prefix = "Steam Workshop ";
        return source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? source[prefix.Length..].Trim()
            : "";
    }

    private static ModRow MergeLogicalRows(IEnumerable<ModRow> candidates)
    {
        var list = candidates.ToList();
        var preferred = list
            .OrderByDescending(row => row.Source.StartsWith("Steam Workshop ", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(row => !row.Name.StartsWith("Workshop Mod ", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(row => !string.IsNullOrWhiteSpace(row.Version))
            .First();

        preferred.Enabled = list.Any(row => row.Enabled);
        preferred.Deployed = list.Any(row => row.Deployed);
        preferred.PresentInActiveRuntime = list.Any(row => row.PresentInActiveRuntime);
        preferred.LoadedByUe4ss = list.Any(row => row.LoadedByUe4ss);
        preferred.RuntimeAliases = list.SelectMany(row => row.RuntimeAliases ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (preferred.Type.Equals("Workshop", StringComparison.OrdinalIgnoreCase))
        {
            var richerType = list.Select(row => row.Type)
                .FirstOrDefault(value => !value.Equals("Workshop", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(richerType)) preferred.Type = richerType;
        }
        return preferred;
    }

    private void ScanLoosePakMods(Dictionary<string, ModRow> rows, HashSet<string> enabled, HashSet<string> workshopAliases)
    {
        var pakRoot = Path.Combine(settings.ServerRoot, "Pal", "Content", "Paks", "~mods");
        if (!Directory.Exists(pakRoot))
            return;

        foreach (var file in Directory.EnumerateFiles(pakRoot, "*", SearchOption.TopDirectoryOnly)
                     .Where(path => IsPakOrDisabledPak(path)))
        {
            var package = GetPackageFromPakPath(file);
            if (string.IsNullOrWhiteSpace(package) || rows.ContainsKey(package) || workshopAliases.Contains(package))
                continue;

            rows[package] = new ModRow
            {
                Package = package,
                Name = package,
                Version = "",
                Enabled = enabled.Contains(package),
                Deployed = true,
                Source = "Server Files",
                Type = "PAK",
                EnableReason = enabled.Contains(package)
                    ? "PAK is active and listed as enabled."
                    : "PAK files were found but no enabled state was recorded."
            };
        }
    }

    private void ScanUe4ssMods(Dictionary<string, ModRow> rows, HashSet<string> enabled, HashSet<string> workshopAliases)
    {
        var ue4ssRoot = ActiveUe4ssModsRoot;
        if (!Directory.Exists(ue4ssRoot))
            return;

        foreach (var folder in Directory.EnumerateDirectories(ue4ssRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var folderName = Path.GetFileName(folder);
            var package = folderName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                ? folderName[..^".disabled".Length]
                : folderName;
            if (string.IsNullOrWhiteSpace(package) || rows.ContainsKey(package) || workshopAliases.Contains(package))
                continue;

            // UE4SS ships several framework/runtime folders in Mods. They are dependencies,
            // not user-installed Palworld mods, so keep them out of the MOD Manager inventory.
            if (KnownUe4ssRuntimeComponents.Contains(package))
                continue;

            var configuredEnabled = enabled.Contains(package);
            var enabledMarker = Path.Combine(folder, "enabled.txt");
            var markerOverride = File.Exists(enabledMarker);
            rows[package] = new ModRow
            {
                Package = package,
                Name = package,
                Version = "",
                Enabled = configuredEnabled || markerOverride,
                Deployed = Directory.EnumerateFileSystemEntries(folder).Any(),
                Source = "UE4SS",
                Type = "UE4SS / Lua",
                Description = ReadLocalDescription(folder),
                EnableReason = markerOverride && !configuredEnabled
                    ? "STATE MISMATCH: enabled.txt is overriding mods.txt (configured disabled, runtime effectively enabled). Use Repair States."
                    : markerOverride
                        ? "UE4SS mods.txt marks this mod enabled, but an enabled.txt bypass marker is also present. Repair States is recommended."
                        : configuredEnabled
                            ? "UE4SS mods.txt marks this mod enabled."
                            : "UE4SS mods.txt marks this mod disabled."
            };
        }
    }



    private IReadOnlyList<string> FindManagedUe4ssRuntimeAliases(IEnumerable<string> files)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var activeRoot = Path.GetFullPath(ActiveUe4ssModsRoot);
        foreach (var file in files.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            try
            {
                var fullPath = Path.GetFullPath(file);
                var relative = Path.GetRelativePath(activeRoot, fullPath);
                if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                    continue;

                var first = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first) && !KnownUe4ssRuntimeComponents.Contains(first))
                    aliases.Add(first);
            }
            catch { }
        }
        return aliases.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> FindUe4ssRuntimeAliases(string packageRoot)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var luaPath in Directory.EnumerateFiles(packageRoot, "*.lua", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(packageRoot, luaPath);
                var parts = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
                for (var index = parts.Length - 2; index >= 0; index--)
                {
                    if (!parts[index].Equals("Mods", StringComparison.OrdinalIgnoreCase) || index + 1 >= parts.Length)
                        continue;

                    var candidate = parts[index + 1];
                    if (!string.IsNullOrWhiteSpace(candidate) && !KnownUe4ssRuntimeComponents.Contains(candidate))
                        aliases.Add(candidate);
                    break;
                }
            }
        }
        catch
        {
            // Alias discovery is supplemental evidence only. A damaged package must
            // not prevent the rest of the MOD inventory from being displayed.
        }
        return aliases.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsPakOrDisabledPak(string path)
    {
        var name = Path.GetFileName(path);
        if (name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
            name = name[..^".disabled".Length];
        return PakExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);
    }

    private static string GetPackageFromPakPath(string path)
    {
        var name = Path.GetFileName(path);
        if (name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
            name = name[..^".disabled".Length];
        return Path.GetFileNameWithoutExtension(name);
    }

    private static string DetectModType(string folder)
    {
        var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToArray();
        if (files.Any(path => PakExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))) return "PAK / Workshop";
        if (files.Any(path => path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))) return "UE4SS / Lua";
        return "Workshop";
    }

    private static string ReadVersionFromFolder(string folder)
    {
        foreach (var jsonName in new[] { "Info.json", "manifest.json", "mod.json", "metadata.json" })
        {
            var path = Directory.EnumerateFiles(folder, jsonName, SearchOption.AllDirectories).FirstOrDefault();
            if (path is null) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                foreach (var key in new[] { "Version", "version", "ModVersion", "mod_version" })
                    if (doc.RootElement.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.ToString())) return value.ToString();
            }
            catch { }
        }
        return Directory.GetLastWriteTime(folder).ToString("yyyy-MM-dd HH:mm");
    }

    private static string? ReadWorkshopPackageName(string folder)
    {
        var info = Directory.EnumerateFiles(folder, "Info.json", SearchOption.AllDirectories).FirstOrDefault();
        if (info is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(info));
            var package = Get(doc.RootElement, "PackageName");
            return string.IsNullOrWhiteSpace(package) ? null : package;
        }
        catch { return null; }
    }

    private static string? ReadWorkshopDisplayName(string folder)
    {
        foreach (var jsonName in new[] { "Info.json", "manifest.json", "mod.json", "metadata.json" })
        {
            var path = Directory.EnumerateFiles(folder, jsonName, SearchOption.AllDirectories).FirstOrDefault();
            if (path is null) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                foreach (var key in new[] { "Name", "name", "DisplayName", "display_name", "Title", "title" })
                    if (doc.RootElement.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.ToString())) return value.ToString();
            }
            catch { }
        }
        return null;
    }

    private static string ReadLocalDescription(string folder)
    {
        foreach (var name in new[] { "README.md", "README.txt", "readme.md", "readme.txt", "description.txt" })
        {
            var path = Path.Combine(folder, name);
            if (!File.Exists(path)) continue;
            try
            {
                var text = File.ReadAllText(path).Trim();
                if (text.Length > 700) text = text[..700] + "…";
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
            catch { }
        }
        return "Manually installed UE4SS mod. No README or description metadata was found.";
    }



    private HashSet<string> ReadEnabledPackages()
    {
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Palworld Workshop / PalModSettings state.
        if (File.Exists(settings.ModSettingsFile))
        {
            foreach (var line in File.ReadLines(settings.ModSettingsFile))
            {
                if (line.TrimStart().StartsWith("ActiveModList=", StringComparison.OrdinalIgnoreCase))
                    enabled.Add(line.Trim()["ActiveModList=".Length..].Trim());
            }
        }

        // UE4SS has its own authoritative enable list. ZIP-installed Lua mods must
        // be reconstructed from this file instead of relying on PalModSettings.ini.
        var ue4ssModsTxt = Path.Combine(ActiveUe4ssModsRoot, "mods.txt");
        if (File.Exists(ue4ssModsTxt))
        {
            foreach (var rawLine in File.ReadLines(ue4ssModsTxt))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var separator = line.LastIndexOf(':');
                if (separator <= 0) continue;
                var name = line[..separator].Trim();
                var state = line[(separator + 1)..].Trim();
                if (state == "1" && !string.IsNullOrWhiteSpace(name))
                    enabled.Add(name);
            }
        }

        return enabled;
    }

    private (bool Enabled, bool Deployed, string Reason) DetermineManagedState(InstallManifest manifest, string type, HashSet<string> enabledPackages)
    {
        var files = manifest.Files ?? [];
        var deployed = files.Any(path => File.Exists(path) || File.Exists(path + ".disabled"));
        if (!deployed)
            return (false, false, "No files recorded by the ZIP install manifest are present.");

        if (type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) || files.Any(IsUe4ssPath))
        {
            var folders = GetManagedUe4ssFolders(files).ToList();
            foreach (var folder in folders)
            {
                if (enabledPackages.Contains(folder))
                    return (true, true, $"UE4SS mods.txt marks '{folder}' enabled.");

                var enabledMarker = Path.Combine(ActiveUe4ssModsRoot, folder, "enabled.txt");
                if (File.Exists(enabledMarker))
                    return (true, true, $"STATE MISMATCH: '{folder}/enabled.txt' is overriding mods.txt. Use Repair States to make mods.txt authoritative.");
            }

            if (enabledPackages.Contains(manifest.Package))
                return (true, true, "The ZIP package is marked enabled by the active mod configuration.");

            if (manifest.LastKnownEnabled && folders.Count == 0)
                return (true, true, "The install manifest records this ZIP mod as enabled; no UE4SS folder name could be resolved.");

            return (false, true, folders.Count > 0
                ? $"UE4SS mods.txt does not mark {string.Join(", ", folders)} enabled."
                : "UE4SS files are present, but no active enable record was found.");
        }

        if (type.Contains("PAK", StringComparison.OrdinalIgnoreCase) || files.Any(IsPakPath))
        {
            var pakFiles = files.Where(IsPakPath).ToList();
            var activeCount = pakFiles.Count(File.Exists);
            var disabledCount = pakFiles.Count(path => File.Exists(path + ".disabled"));
            if (activeCount > 0 && disabledCount == 0)
                return (true, true, $"{activeCount} active PAK/UCAS/UTOC file(s) are deployed.");
            if (disabledCount > 0 && activeCount == 0)
                return (false, true, $"{disabledCount} PAK/UCAS/UTOC file(s) are in the disabled state.");
            if (activeCount > 0)
                return (true, true, "PAK deployment is partially active; review the installed file set.");
        }

        var fallbackEnabled = enabledPackages.Contains(manifest.Package) || manifest.LastKnownEnabled;
        return (fallbackEnabled, true, fallbackEnabled
            ? "The ZIP install manifest / active mod configuration records this mod as enabled."
            : "Files are present, but no authoritative enabled state was found.");
    }

    private string DetectManagedType(IEnumerable<string> files)
    {
        var list = files.ToList();
        if (list.Any(path => Path.GetFileName(path).Equals("PalDefender.dll", StringComparison.OrdinalIgnoreCase)))
            return "Win64 Loader / Anti-Cheat";
        if (list.Any(IsUe4ssPath))
            return list.Any(path => Normalize(path).Contains("/dlls/main.dll", StringComparison.OrdinalIgnoreCase))
                ? "UE4SS DLL Mod"
                : "UE4SS / Lua";
        if (list.Any(IsPakPath)) return "PAK";
        return "ZIP Mod";
    }

    private static bool IsPakPath(string path)
    {
        var candidate = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            ? path[..^".disabled".Length]
            : path;
        return PakExtensions.Contains(Path.GetExtension(candidate), StringComparer.OrdinalIgnoreCase);
    }

    private bool IsUe4ssPath(string path)
    {
        var modsRoot = Path.GetFullPath(ActiveUe4ssModsRoot)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        try
        {
            return Path.GetFullPath(path).StartsWith(modsRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private IEnumerable<string> GetManagedUe4ssFolders(IEnumerable<string> files)
    {
        var modsRoot = Path.GetFullPath(ActiveUe4ssModsRoot)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var file in files)
        {
            string full;
            try { full = Path.GetFullPath(file); }
            catch { continue; }
            if (!full.StartsWith(modsRoot, StringComparison.OrdinalIgnoreCase)) continue;
            var relative = full[modsRoot.Length..];
            var separator = relative.IndexOf(Path.DirectorySeparatorChar);
            var folder = separator >= 0 ? relative[..separator] : Path.GetDirectoryName(relative);
            if (!string.IsNullOrWhiteSpace(folder) && !folder.Equals("mods.txt", StringComparison.OrdinalIgnoreCase))
                yield return folder;
        }
    }

    private void NormalizeManagedUe4ssManifestIdentities()
    {
        foreach (var manifestPath in Directory.EnumerateFiles(settings.ManagedModsRoot, "InstallManifest.json", SearchOption.AllDirectories).ToList())
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath));
                if (manifest is null || manifest.Files.Count == 0) continue;

                var canonicalNames = manifest.Files
                    .Select(TryGetUe4ssRuntimeFolderName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (canonicalNames.Count != 1) continue;

                var canonical = CleanName(canonicalNames[0]!);
                if (string.IsNullOrWhiteSpace(canonical) || KnownUe4ssRuntimeComponents.Contains(canonical)) continue;
                if (manifest.Package.Equals(canonical, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Path.GetFileName(Path.GetDirectoryName(manifestPath)), canonical, StringComparison.OrdinalIgnoreCase))
                    continue;

                manifest.Package = canonical;
                manifest.Name = canonical;
                var targetFolder = Path.Combine(settings.ManagedModsRoot, canonical);
                var targetPath = Path.Combine(targetFolder, "InstallManifest.json");
                Directory.CreateDirectory(targetFolder);

                if (File.Exists(targetPath) && !targetPath.Equals(manifestPath, StringComparison.OrdinalIgnoreCase))
                {
                    var existing = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(targetPath));
                    if (existing is not null)
                    {
                        existing.Package = canonical;
                        existing.Name = canonical;
                        existing.Files = existing.Files.Concat(manifest.Files).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        existing.Dependencies = existing.Dependencies.Concat(manifest.Dependencies).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        if (string.IsNullOrWhiteSpace(existing.Version)) existing.Version = manifest.Version;
                        if (string.IsNullOrWhiteSpace(existing.SourceZip)) existing.SourceZip = manifest.SourceZip;
                        existing.LastKnownEnabled |= manifest.LastKnownEnabled;
                        manifest = existing;
                    }
                }

                File.WriteAllText(targetPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
                if (!targetPath.Equals(manifestPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(manifestPath);
                    var oldFolder = Path.GetDirectoryName(manifestPath);
                    if (!string.IsNullOrWhiteSpace(oldFolder) && Directory.Exists(oldFolder) && !Directory.EnumerateFileSystemEntries(oldFolder).Any())
                        Directory.Delete(oldFolder);
                }
            }
            catch
            {
                // Keep inventory usable even when a legacy third-party manifest is malformed.
            }
        }
    }

    private string? TryGetUe4ssRuntimeFolderName(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        var info = ue4ssResolver.Resolve();
        foreach (var root in new[] { info.ActiveModsRoot, info.ModernModsRoot, info.LegacyModsRoot }
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var fileFull = Path.GetFullPath(filePath);
                if (!fileFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) continue;
                var relative = fileFull[rootFull.Length..];
                var separator = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
                return separator <= 0 ? null : relative[..separator];
            }
            catch { }
        }

        return null;
    }

    private static string CleanName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
    }

    private static string Normalize(string value) => value.Replace('\\', '/').TrimStart('/');
    private static string ToWindows(string value) => value.Replace('/', Path.DirectorySeparatorChar);

    private static string Get(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return property.Value.ToString();
        return "";
    }

}
