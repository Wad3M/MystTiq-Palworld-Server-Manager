using Microsoft.Win32;
using System.Text.RegularExpressions;
using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class EnvironmentService
{
    private readonly AppSettings settings;
    private readonly IServerPathProfile paths;
    public EnvironmentService(AppSettings settings, IServerPathProfile? paths = null)
    {
        this.settings = settings;
        this.paths = paths ?? ServerPathProfile.ForCurrentPlatform(settings);
    }

    public string? FindSteamRoot()
    {
        foreach (var candidate in SteamCandidates())
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(Path.Combine(candidate, "steam.exe")))
                return candidate;
        return null;
    }

    public IReadOnlyList<string> FindSteamLibraries()
    {
        var result = new List<string>();
        var steam = FindSteamRoot();
        if (steam is null) return result;
        result.Add(steam);
        var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            const string pattern = "\\\"path\\\"\\s+\\\"(?<p>[^\\\"]+)\\\"";
            foreach (Match match in Regex.Matches(File.ReadAllText(vdf), pattern))
            {
                var path = match.Groups["p"].Value.Replace("\\\\", "\\");
                if (Directory.Exists(path) && !result.Contains(path, StringComparer.OrdinalIgnoreCase))
                    result.Add(path);
            }
        }
        return result;
    }

    public string? FindPalworldClient()
    {
        foreach (var library in FindSteamLibraries())
        {
            var path = Path.Combine(library, "steamapps", "common", "Palworld");
            if (Directory.Exists(path)) return path;
        }
        return null;
    }

    public string? FindWorkshopRoot()
    {
        foreach (var library in FindSteamLibraries())
        {
            var path = Path.Combine(library, "steamapps", "workshop", "content", "1623730");
            if (Directory.Exists(path)) return path;
        }
        return null;
    }

    private static readonly string[] Ue4ssLoaderNames =
    [
        "dwmapi.dll",
        "xinput1_3.dll",
        "xinput1_4.dll",
        "winhttp.dll"
    ];

    private static readonly HashSet<string> Ue4ssBuiltInModFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "ActorDumperMod", "BPModLoaderMod", "BPML_GenericFunctions", "CheatManagerEnablerMod",
        "ConsoleCommandsMod", "ConsoleEnablerMod", "jsbLuaProfilerMod", "Keybinds",
        "LineTraceMod", "SplitScreenMod", "shared"
    };

    private string Ue4ssWin64Folder => paths.RuntimeBinaryRoot;

    public (bool Installed, bool Enabled, string Detail) GetUe4ssRuntimeState()
    {
        var folder = Ue4ssWin64Folder;
        if (!Directory.Exists(folder))
            return (false, false, "UE4SS runtime folder was not found.");

        var runtimePresent = File.Exists(Path.Combine(folder, "UE4SS.dll")) ||
                             File.Exists(Path.Combine(folder, "UE4SS.dll.myst-disabled")) ||
                             Directory.Exists(Path.Combine(folder, "ue4ss")) ||
                             Ue4ssLoaderNames.Any(name => File.Exists(Path.Combine(folder, name)) || File.Exists(Path.Combine(folder, name + ".myst-disabled")));
        if (!runtimePresent)
            return (false, false, "UE4SS runtime files were not found.");

        var enabledLoader = Ue4ssLoaderNames.FirstOrDefault(name => File.Exists(Path.Combine(folder, name)));
        if (!string.IsNullOrWhiteSpace(enabledLoader))
            return (true, true, $"UE4SS runtime enabled through {enabledLoader}.");

        var disabledLoader = Ue4ssLoaderNames.FirstOrDefault(name => File.Exists(Path.Combine(folder, name + ".myst-disabled")));
        if (!string.IsNullOrWhiteSpace(disabledLoader))
            return (true, false, $"UE4SS runtime installed but disabled ({disabledLoader}.myst-disabled).");

        // Some runtime packages load UE4SS.dll directly. If no proxy loader is present,
        // treat UE4SS.dll as the activation file and allow MystTiq to disable it reversibly.
        if (File.Exists(Path.Combine(folder, "UE4SS.dll")))
            return (true, true, "UE4SS runtime enabled (direct UE4SS.dll activation)." );
        if (File.Exists(Path.Combine(folder, "UE4SS.dll.myst-disabled")))
            return (true, false, "UE4SS runtime installed but disabled (UE4SS.dll.myst-disabled)." );

        return (true, false, "UE4SS runtime files are present but no active loader was detected.");
    }

    public string DisableUe4ssRuntime()
    {
        var state = GetUe4ssRuntimeState();
        if (!state.Installed)
            throw new InvalidOperationException("UE4SS is not installed.");

        var folder = Ue4ssWin64Folder;
        var changed = new List<string>();
        foreach (var name in Ue4ssLoaderNames)
        {
            var source = Path.Combine(folder, name);
            var disabled = source + ".myst-disabled";
            if (!File.Exists(source)) continue;
            if (File.Exists(disabled)) File.Delete(disabled);
            File.Move(source, disabled);
            changed.Add(name);
        }

        if (changed.Count == 0)
        {
            var source = Path.Combine(folder, "UE4SS.dll");
            var disabled = source + ".myst-disabled";
            if (File.Exists(source))
            {
                if (File.Exists(disabled)) File.Delete(disabled);
                File.Move(source, disabled);
                changed.Add("UE4SS.dll");
            }
        }

        return changed.Count == 0
            ? "UE4SS was already disabled."
            : "UE4SS runtime disabled. Loader preserved as: " + string.Join(", ", changed.Select(x => x + ".myst-disabled"));
    }

    public string EnableUe4ssRuntime()
    {
        var folder = Ue4ssWin64Folder;
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException("Palworld Win64 folder was not found.");

        var changed = new List<string>();
        foreach (var name in Ue4ssLoaderNames.Append("UE4SS.dll"))
        {
            var target = Path.Combine(folder, name);
            var disabled = target + ".myst-disabled";
            if (!File.Exists(disabled)) continue;
            if (File.Exists(target)) File.Delete(target);
            File.Move(disabled, target);
            changed.Add(name);
        }

        var state = GetUe4ssRuntimeState();
        if (!state.Installed)
            throw new InvalidOperationException("UE4SS runtime files are not installed. Use Install first.");

        return changed.Count == 0
            ? "UE4SS was already enabled."
            : "UE4SS runtime enabled. Restored loader: " + string.Join(", ", changed);
    }


    public string GetUe4ssRuntimeFolder() => Ue4ssWin64Folder;

    public DateTime? GetUe4ssRuntimeLastWriteUtc()
    {
        var candidates = Ue4ssLoaderNames.Append("UE4SS.dll")
            .SelectMany(name => new[] { Path.Combine(Ue4ssWin64Folder, name), Path.Combine(Ue4ssWin64Folder, name + ".myst-disabled") })
            .Where(File.Exists)
            .ToList();
        if (candidates.Count == 0) return null;
        return candidates.Max(File.GetLastWriteTimeUtc);
    }

    public (string Version, string Profile, bool MemberVariableLayoutPresent) GetUe4ssRuntimeIdentity()
    {
        var folder = Ue4ssWin64Folder;
        var dllCandidates = new[]
        {
            Path.Combine(folder, "UE4SS.dll"),
            Path.Combine(folder, "UE4SS.dll.myst-disabled"),
            Path.Combine(folder, "ue4ss", "UE4SS.dll"),
            Path.Combine(folder, "ue4ss", "UE4SS.dll.myst-disabled")
        };

        string version = "Unknown";
        foreach (var candidate in dllCandidates)
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                var info = FileVersionInfo.GetVersionInfo(candidate);
                version = !string.IsNullOrWhiteSpace(info.ProductVersion) ? info.ProductVersion! :
                          !string.IsNullOrWhiteSpace(info.FileVersion) ? info.FileVersion! : "Unknown";
                break;
            }
            catch { }
        }

        var layoutCandidates = new[]
        {
            Path.Combine(folder, "MemberVariableLayout.ini"),
            Path.Combine(folder, "ue4ss", "MemberVariableLayout.ini")
        };
        var hasLayout = layoutCandidates.Any(File.Exists);
        var hasExperimentalMarkers = hasLayout ||
                                     Directory.Exists(Path.Combine(folder, "ue4ss")) ||
                                     File.Exists(Path.Combine(folder, "UE4SS-settings.ini"));
        var profile = hasLayout ? "Experimental / RE-UE4SS compatible" :
                      hasExperimentalMarkers ? "UE4SS runtime (exact flavor not identifiable)" :
                      "Legacy / custom UE4SS layout";
        return (version, profile, hasLayout);
    }

    private string Ue4ssRuntimeMetadataPath => Path.Combine(Ue4ssWin64Folder, ".myst", "ue4ss-runtime.json");

    public void SaveUe4ssRuntimeMetadata(Ue4ssReleaseInfo release)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Ue4ssRuntimeMetadataPath)!);
        var payload = new
        {
            source = release.Source,
            tag = release.Tag,
            name = release.Name,
            assetName = release.AssetName,
            publishedAt = release.PublishedAt,
            installedAt = DateTimeOffset.Now
        };
        File.WriteAllText(Ue4ssRuntimeMetadataPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }

    public (string Source, string Tag, string Name, string AssetName, DateTime PublishedAt)? GetUe4ssRuntimeMetadata()
    {
        if (!File.Exists(Ue4ssRuntimeMetadataPath)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Ue4ssRuntimeMetadataPath));
            var root = doc.RootElement;
            var source = root.TryGetProperty("source", out var sourceNode) ? sourceNode.GetString() ?? "" : "";
            var tag = root.TryGetProperty("tag", out var tagNode) ? tagNode.GetString() ?? "" : "";
            var name = root.TryGetProperty("name", out var nameNode) ? nameNode.GetString() ?? "" : "";
            var asset = root.TryGetProperty("assetName", out var assetNode) ? assetNode.GetString() ?? "" : "";
            var published = DateTime.MinValue;
            if (root.TryGetProperty("publishedAt", out var publishedNode)) DateTime.TryParse(publishedNode.ToString(), out published);
            return (source, tag, name, asset, published);
        }
        catch
        {
            return null;
        }
    }

    public string CreateUe4ssRuntimeSnapshot()
    {
        var state = GetUe4ssRuntimeState();
        if (!state.Installed)
            throw new InvalidOperationException("UE4SS is not installed, so there is no runtime to back up.");

        var snapshotRoot = Path.Combine(settings.BackupRoot, "UE4SS-Runtimes");
        Directory.CreateDirectory(snapshotRoot);
        var output = Path.Combine(snapshotRoot, $"UE4SS_Runtime_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip");

        using var archive = ZipFile.Open(output, ZipArchiveMode.Create);
        foreach (var file in EnumerateRuntimeFilesForSnapshot())
        {
            var relative = Path.GetRelativePath(Ue4ssWin64Folder, file);
            archive.CreateEntryFromFile(file, relative, CompressionLevel.Optimal);
        }
        return output;
    }

    public string ImportUe4ssRuntimeZip(string zipPath)
    {
        if (!File.Exists(zipPath)) throw new FileNotFoundException("Runtime ZIP was not found.", zipPath);
        Directory.CreateDirectory(Ue4ssWin64Folder);

        var snapshot = GetUe4ssRuntimeState().Installed ? CreateUe4ssRuntimeSnapshot() : string.Empty;
        using var archive = ZipFile.OpenRead(zipPath);
        var extracted = 0;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name)) continue;
            var relative = NormalizeRuntimeArchivePath(entry.FullName);
            if (string.IsNullOrWhiteSpace(relative) || !IsAllowedRuntimeImportPath(relative)) continue;

            var destination = Path.GetFullPath(Path.Combine(Ue4ssWin64Folder, relative));
            var root = Path.GetFullPath(Ue4ssWin64Folder) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
            extracted++;
        }

        if (extracted == 0)
            throw new InvalidDataException("The ZIP did not contain a recognizable UE4SS runtime layout.");

        var state = GetUe4ssRuntimeState();
        if (!state.Installed)
            throw new InvalidDataException("Runtime files were extracted, but MystTiq could not detect a valid UE4SS runtime afterward.");

        return string.IsNullOrWhiteSpace(snapshot)
            ? $"Imported UE4SS runtime package ({extracted} files)."
            : $"Imported UE4SS runtime package ({extracted} files). Previous runtime snapshot: {snapshot}";
    }

    public string RestoreUe4ssRuntimeSnapshot(string zipPath)
    {
        if (!File.Exists(zipPath)) throw new FileNotFoundException("Runtime snapshot was not found.", zipPath);
        using var archive = ZipFile.OpenRead(zipPath);
        var extracted = 0;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name)) continue;
            var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (!IsAllowedRuntimeImportPath(relative)) continue;
            var destination = Path.GetFullPath(Path.Combine(Ue4ssWin64Folder, relative));
            var root = Path.GetFullPath(Ue4ssWin64Folder) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
            extracted++;
        }
        if (extracted == 0) throw new InvalidDataException("The snapshot did not contain restorable UE4SS runtime files.");
        return $"Restored UE4SS runtime snapshot ({extracted} files).";
    }

    public string? GetLatestUe4ssRuntimeSnapshot()
    {
        var root = Path.Combine(settings.BackupRoot, "UE4SS-Runtimes");
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, "UE4SS_Runtime_*.zip")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private IEnumerable<string> EnumerateRuntimeFilesForSnapshot()
    {
        var root = Ue4ssWin64Folder;
        if (!Directory.Exists(root)) yield break;

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            if (IsRuntimeCoreFileName(name)) yield return file;
        }

        var legacyMods = Path.Combine(root, "Mods");
        if (Directory.Exists(legacyMods))
        {
            foreach (var folderName in Ue4ssBuiltInModFolders)
            {
                var builtIn = Path.Combine(legacyMods, folderName);
                if (!Directory.Exists(builtIn)) continue;
                foreach (var file in Directory.EnumerateFiles(builtIn, "*", SearchOption.AllDirectories))
                    yield return file;
            }
        }

        var ue4ssFolder = Path.Combine(root, "ue4ss");
        if (Directory.Exists(ue4ssFolder))
        {
            foreach (var file in Directory.EnumerateFiles(ue4ssFolder, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, file);
                if (rel.StartsWith($"ue4ss{Path.DirectorySeparatorChar}Mods{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)) continue;
                yield return file;
            }
        }
    }

    private static string NormalizeRuntimeArchivePath(string fullName)
    {
        var path = fullName.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var marker = $"Pal{Path.DirectorySeparatorChar}Binaries{Path.DirectorySeparatorChar}Win64{Path.DirectorySeparatorChar}";
        var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) path = path[(idx + marker.Length)..];
        if (path.StartsWith($"Win64{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            path = path[("Win64".Length + 1)..];
        return path;
    }

    private static bool IsAllowedRuntimeImportPath(string relative)
    {
        var normalized = relative.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        if (normalized.StartsWith($"ue4ss{Path.DirectorySeparatorChar}Mods{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            return false;
        if (normalized.StartsWith($"Mods{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            var parts = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && Ue4ssBuiltInModFolders.Contains(parts[1]);
        }
        if (normalized.StartsWith($"ue4ss{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            return true;
        return IsRuntimeCoreFileName(Path.GetFileName(normalized));
    }

    private static bool IsRuntimeCoreFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return name.Equals("UE4SS.dll", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("UE4SS.dll.myst-disabled", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("UE4SS-settings.ini", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("MemberVariableLayout.ini", StringComparison.OrdinalIgnoreCase) ||
               Ue4ssLoaderNames.Any(loader => name.Equals(loader, StringComparison.OrdinalIgnoreCase) || name.Equals(loader + ".myst-disabled", StringComparison.OrdinalIgnoreCase));
    }

    public ObservableCollection<EnvironmentComponentRow> Scan()
    {
        var rows = new ObservableCollection<EnvironmentComponentRow>();
        var steam = FindSteamRoot();
        Add(rows, "Steam Client", steam is null ? "MISSING" : "READY", steam ?? "Not detected",
            steam is null ? "Steam is optional, but required to discover local Workshop mods." : "Steam installation detected.",
            steam is null ? "RESCAN" : "VERIFY");

        var client = FindPalworldClient();
        Add(rows, "Palworld Client", client is null ? "MISSING" : "READY", client ?? "Not detected",
            client is null ? "Local game install not found in registered Steam libraries." : "Palworld App ID 1623730 detected.",
            client is null ? "RESCAN" : "VERIFY");

        var python = ResolvePythonExecutable();
        Add(rows, "Python Runtime", string.IsNullOrWhiteSpace(python) ? "MISSING" : "READY", string.IsNullOrWhiteSpace(python) ? "Not detected" : python,
            string.IsNullOrWhiteSpace(python) ? "Required for palworld-save-tools and shared world discovery." : "Python and pip are available for save decoding.",
            string.IsNullOrWhiteSpace(python) ? "INSTALL" : "VERIFY");

        Add(rows, "SteamCMD", File.Exists(settings.SteamCmdPath) ? "READY" : "MISSING", settings.SteamCmdPath,
            File.Exists(settings.SteamCmdPath) ? "Ready for install, update, and repair operations." : "Required for automated dedicated-server installation and updates.",
            File.Exists(settings.SteamCmdPath) ? "VERIFY" : "INSTALL");

        Add(rows, "Palworld Dedicated Server", File.Exists(paths.ServerExecutable) ? "READY" : "MISSING", paths.ServerExecutable,
            File.Exists(paths.ServerExecutable) ? "Server executable detected." : "Dedicated server App ID 2394010 is not installed at the configured location.",
            File.Exists(paths.ServerExecutable) ? "VERIFY" : "INSTALL");

        var ue4ssFolder = Ue4ssWin64Folder;
        var ue4ssState = GetUe4ssRuntimeState();
        Add(rows, "UE4SS Runtime", !ue4ssState.Installed ? "MISSING" : ue4ssState.Enabled ? "READY" : "DISABLED", ue4ssFolder,
            !ue4ssState.Installed ? "Optional runtime for UE4SS-based server mods." : ue4ssState.Detail,
            !ue4ssState.Installed ? "INSTALL" : "MANAGE");

        var saveToolsConverter = ResolveSaveToolsConverter();
        var saveToolsReady = !string.IsNullOrWhiteSpace(saveToolsConverter);
        Add(rows, "Palworld Save Tools", saveToolsReady ? "READY" : "MISSING", saveToolsReady ? saveToolsConverter : Path.Combine(settings.ServerRoot, "Tools", "palworld-save-tools", "convert.py"),
            saveToolsReady ? "Official save converter detected for shared world discovery." : "Required to decode Level.sav for Players, Guilds, Bases, and Inspector data.",
            saveToolsReady ? "VERIFY" : "INSTALL");

        var cppToolsReady = IsCppBuildToolsInstalled();
        Add(rows, "Microsoft C++ Build Tools", cppToolsReady ? "READY" : "MISSING", cppToolsReady ? ResolveCppCompiler() : "Visual Studio 2022 Build Tools",
            cppToolsReady ? "MSVC compiler detected for native Python dependencies such as pyooz." : "Required to compile pyooz for PlM/Oodle save decoding.",
            cppToolsReady ? "VERIFY" : "INSTALL");

        var plmConverter = ResolvePlmConverter();
        var plmReady = !string.IsNullOrWhiteSpace(plmConverter) && File.Exists(Path.Combine(settings.ServerRoot, "Tools", "palworld-plm-tools", ".myst-install.json"));
        Add(rows, "PlM/Oodle Decoder", plmReady ? "READY" : "MISSING", plmReady ? plmConverter : Path.Combine(settings.ServerRoot, "Tools", "palworld-plm-tools"),
            plmReady ? "PlM/Oodle-capable converter detected for current Palworld save containers." : "Required for newer PlM Level.sav files. Installs pyooz and the PlM-capable PalworldSaveTools source.",
            plmReady ? "VERIFY" : "INSTALL");

        Add(rows, "Default Server Settings", File.Exists(settings.ConfigFile) ? "READY" : "MISSING", settings.ConfigFile,
            File.Exists(settings.ConfigFile) ? "Active PalWorldSettings.ini detected." : "A valid default configuration has not been created.",
            File.Exists(settings.ConfigFile) ? "VERIFY" : "CREATE");

        var configText = File.Exists(settings.ConfigFile) ? File.ReadAllText(settings.ConfigFile) : string.Empty;
        var restReady = configText.Contains("RESTAPIEnabled=True", StringComparison.OrdinalIgnoreCase);
        Add(rows, "REST API", restReady ? "READY" : "DISABLED", settings.ApiBaseUrl,
            restReady ? "Enabled in the active configuration." : "Not enabled in the active configuration.",
            restReady ? "VERIFY" : "ENABLE");

        var rconReady = configText.Contains("RCONEnabled=True", StringComparison.OrdinalIgnoreCase);
        Add(rows, "RCON", rconReady ? "READY" : "DISABLED", settings.ConfigFile,
            rconReady ? "Enabled in the active configuration." : "Optional remote administration is disabled.",
            rconReady ? "VERIFY" : "ENABLE");

        Add(rows, "Backup Storage", Directory.Exists(settings.BackupRoot) ? "READY" : "MISSING", settings.BackupRoot,
            Directory.Exists(settings.BackupRoot) ? "Backup folder is available." : "Create backup storage before importing mods.",
            Directory.Exists(settings.BackupRoot) ? "VERIFY" : "CREATE");
        return rows;
    }

    public ObservableCollection<LocalModRow> ScanLocalMods()
    {
        var rows = new ObservableCollection<LocalModRow>();
        var workshop = FindWorkshopRoot();
        if (workshop is null) return rows;

        foreach (var directory in Directory.EnumerateDirectories(workshop))
        {
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToArray();
            var type = files.Any(file => file.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)) ? "PAK" :
                       files.Any(file => file.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) ? "UE4SS / Lua" : "Workshop";
            var compatibility = type == "PAK" ? "Likely server compatible" : type == "UE4SS / Lua" ? "Review required" : "Unknown";
            var id = Path.GetFileName(directory);
            var destination = GetInstalledWorkshopDestination(id);
            var installed = Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any();
            var sourceUpdated = Directory.GetLastWriteTimeUtc(directory);
            var installedUpdated = installed ? Directory.GetLastWriteTimeUtc(destination) : DateTime.MinValue;
            var variantCount = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
                .Count(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                    .Any(path => path.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) ||
                                 path.EndsWith(".ucas", StringComparison.OrdinalIgnoreCase) ||
                                 path.EndsWith(".utoc", StringComparison.OrdinalIgnoreCase) ||
                                 path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)));
            var updateStatus = !installed ? "NOT INSTALLED" : sourceUpdated > installedUpdated.AddSeconds(2) ? "UPDATE AVAILABLE" : "CURRENT";
            rows.Add(new LocalModRow
            {
                Name = ReadWorkshopName(directory) ?? $"Workshop Mod {id}",
                WorkshopId = id,
                SourcePath = directory,
                Type = type,
                Compatibility = compatibility,
                ServerStatus = installed ? "Installed" : "Not installed",
                InstalledVersion = installed ? installedUpdated.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "—",
                AvailableVersion = sourceUpdated.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                UpdateStatus = updateStatus,
                VariantCount = Math.Max(1, variantCount),
                SizeBytes = files.Sum(file => { try { return new FileInfo(file).Length; } catch { return 0L; } }),
                LastUpdated = sourceUpdated.ToLocalTime(),
                Description = ReadWorkshopDescription(directory),
                Author = ReadWorkshopAuthor(directory)
            });
        }
        return rows;
    }

    private static string ReadWorkshopDescription(string directory)
    {
        foreach (var fileName in new[] { "description.txt", "README.md", "README.txt", "readme.md", "readme.txt" })
        {
            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path)) continue;
            try
            {
                var text = File.ReadAllText(path).Trim();
                if (text.Length > 900) text = text[..900] + "…";
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
            catch { }
        }
        return "No local Workshop description was found. Use Open Workshop to view the complete Steam description.";
    }

    private static string ReadWorkshopAuthor(string directory)
    {
        foreach (var jsonName in new[] { "manifest.json", "mod.json", "metadata.json", "Info.json" })
        {
            var path = Directory.EnumerateFiles(directory, jsonName, SearchOption.AllDirectories).FirstOrDefault();
            if (path is null) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                foreach (var key in new[] { "Author", "author", "CreatedBy", "created_by" })
                    if (doc.RootElement.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.ToString()))
                        return value.ToString();
            }
            catch { }
        }
        return "Unknown";
    }

    public string ImportLocalMod(LocalModRow mod)
    {
        if (!Directory.Exists(mod.SourcePath))
            throw new DirectoryNotFoundException("The selected local Workshop mod folder no longer exists: " + mod.SourcePath);

        Directory.CreateDirectory(settings.ServerRoot);
        Directory.CreateDirectory(settings.ModsRoot);
        Directory.CreateDirectory(settings.WorkshopRoot);
        Directory.CreateDirectory(settings.ManagedModsRoot);
        var destination = GetInstalledWorkshopDestination(mod.WorkshopId);
        if (Directory.Exists(destination)) Directory.Delete(destination, true);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        CopyDirectory(mod.SourcePath, destination);
        var manifest = new { mod.Name, mod.WorkshopId, mod.Type, Source = mod.SourcePath, Destination = destination, SourceLastWriteUtc = Directory.GetLastWriteTimeUtc(mod.SourcePath), VariantCount = mod.VariantCount, InstalledUtc = DateTime.UtcNow };
        File.WriteAllText(Path.Combine(destination, "myst-install-manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        return destination;
    }


    private string GetInstalledWorkshopDestination(string workshopId)
    {
        // Palworld's official server Workshop loader reads packages from
        // <ServerRoot>\Mods\Workshop. Keep each Steam item intact beneath its
        // Workshop ID so Info.json, option folders, and referenced payload files
        // remain together exactly as Steam delivered them.
        return Path.Combine(settings.WorkshopRoot, workshopId);
    }

    public void CreateDefaultSettings(string serverName, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settings.ConfigFile)!);
        var defaultPath = Path.Combine(settings.ServerRoot, "DefaultPalWorldSettings.ini");
        var text = File.Exists(defaultPath)
            ? File.ReadAllText(defaultPath)
            : "[/Script/Pal.PalGameWorldSettings]\nOptionSettings=(ServerName=\"Palworld Server\",ServerDescription=\"Dedicated Palworld server\",ServerPlayerMaxNum=32,PublicPort=8211,RESTAPIEnabled=False,RCONEnabled=False)\n";
        text = ReplaceOption(text, "ServerName", $"\"{Escape(serverName)}\"");
        text = ReplaceOption(text, "ServerDescription", $"\"{Escape(description)}\"");
        File.WriteAllText(settings.ConfigFile, text);
    }

    private static string ResolveCppCompiler()
    {
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
        {
            var vsRoot = Path.Combine(root, "Microsoft Visual Studio");
            if (!Directory.Exists(vsRoot)) continue;
            var compiler = Directory.EnumerateFiles(vsRoot, "cl.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(compiler)) return compiler;
        }
        return string.Empty;
    }

    private static bool IsCppBuildToolsInstalled() => !string.IsNullOrWhiteSpace(ResolveCppCompiler());

    public (bool Success, string Message) VerifyComponent(string component)
    {
        return component switch
        {
            "Steam Client" => (FindSteamRoot() is not null, FindSteamRoot() is not null ? "Steam installation verified." : "Steam installation was not found."),
            "Palworld Client" => (FindPalworldClient() is not null, FindPalworldClient() is not null ? "Palworld client App ID 1623730 verified." : "Palworld client was not found in the detected Steam libraries."),
            "Python Runtime" => VerifyPython(),
            "SteamCMD" => (File.Exists(settings.SteamCmdPath), File.Exists(settings.SteamCmdPath)
                ? $"{ServerPlatformProfile.ForCurrentPlatform().SteamCmdExecutableName} verified."
                : $"{ServerPlatformProfile.ForCurrentPlatform().SteamCmdExecutableName} is missing."),
            "Palworld Dedicated Server" => (
                File.Exists(paths.ServerExecutable),
                File.Exists(paths.ServerExecutable)
                    ? $"{Path.GetFileName(paths.ServerExecutable)} verified."
                    : $"{Path.GetFileName(paths.ServerExecutable)} is missing."),
            "UE4SS Runtime" => VerifyUe4ss(),
            "Palworld Save Tools" => VerifySaveTools(),
            "Microsoft C++ Build Tools" => (IsCppBuildToolsInstalled(), IsCppBuildToolsInstalled() ? "MSVC C++ compiler verified." : "Microsoft C++ Build Tools are missing."),
            "PlM/Oodle Decoder" => VerifyPlmDecoder(),
            "Default Server Settings" => (File.Exists(settings.ConfigFile), File.Exists(settings.ConfigFile) ? "PalWorldSettings.ini verified." : "PalWorldSettings.ini is missing."),
            "REST API" => VerifyConfigFlag("RESTAPIEnabled=True", "REST API is enabled.", "REST API is disabled."),
            "RCON" => VerifyConfigFlag("RCONEnabled=True", "RCON is enabled.", "RCON is disabled."),
            "Backup Storage" => VerifyBackupStorage(),
            _ => (false, "No verification rule exists for this component.")
        };
    }



    private string ResolvePythonExecutable()
    {
        foreach (var candidate in new[] { settings.PythonExecutable, "py", "python", "python3" }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo { FileName = candidate, Arguments = "--version", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
                if (process is not null && process.WaitForExit(4000) && process.ExitCode == 0) return candidate;
            }
            catch { }
        }
        return string.Empty;
    }

    private (bool Success, string Message) VerifyPython()
    {
        var python = ResolvePythonExecutable();
        if (string.IsNullOrWhiteSpace(python)) return (false, "Python was not detected.");
        try
        {
            using var process = Process.Start(new ProcessStartInfo { FileName = python, Arguments = "-m pip --version", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
            if (process is null || !process.WaitForExit(5000) || process.ExitCode != 0) return (false, "Python was found, but pip validation failed.");
            return (true, $"Python and pip verified using {python}.");
        }
        catch (Exception ex) { return (false, "Python validation failed: " + ex.Message); }
    }

    private string ResolveSaveToolsConverter()
    {
        var candidates = new[]
        {
            settings.PalworldSaveToolsPath,
            Path.Combine(settings.ServerRoot, "Tools", "palworld-save-tools", "convert.py"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "palworld-save-tools", "convert.py")
        };
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)) ?? string.Empty;
    }


    private string ResolvePlmConverter()
    {
        var root = Path.Combine(settings.ServerRoot, "Tools", "palworld-plm-tools");
        var candidates = new[]
        {
            Path.Combine(root, "convert.py"),
            Path.Combine(root, "tools", "convert.py"),
            Path.Combine(root, "PalworldSaveTools", "convert.py")
        };
        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private (bool Success, string Message) VerifyPlmDecoder()
    {
        var converter = ResolvePlmConverter();
        if (string.IsNullOrWhiteSpace(converter))
            return (false, "PlM/Oodle decoder is missing. Install it from Server Setup.");
        var marker = Path.Combine(settings.ServerRoot, "Tools", "palworld-plm-tools", ".myst-install.json");
        return File.Exists(marker)
            ? (true, "PlM/Oodle decoder verified at " + converter)
            : (false, "PlM converter files exist, but installation metadata is missing. Run Repair.");
    }

    private (bool Success, string Message) VerifySaveTools()
    {
        var converter = ResolveSaveToolsConverter();
        return string.IsNullOrWhiteSpace(converter)
            ? (false, "palworld-save-tools convert.py is missing. Install it from Server Setup.")
            : (true, "palworld-save-tools convert.py verified at " + converter);
    }

    private (bool Success, string Message) VerifyUe4ss()
    {
        var state = GetUe4ssRuntimeState();
        if (!state.Installed) return (false, "UE4SS runtime files were not found.");
        return (true, state.Enabled ? "UE4SS runtime installed and enabled." : "UE4SS runtime installed but intentionally disabled.");
    }

    private (bool Success, string Message) VerifyConfigFlag(string flag, string success, string failure)
    {
        if (!File.Exists(settings.ConfigFile)) return (false, "PalWorldSettings.ini is missing.");
        return File.ReadAllText(settings.ConfigFile).Contains(flag, StringComparison.OrdinalIgnoreCase) ? (true, success) : (false, failure);
    }

    private (bool Success, string Message) VerifyBackupStorage()
    {
        try
        {
            Directory.CreateDirectory(settings.BackupRoot);
            var probe = Path.Combine(settings.BackupRoot, ".myst-write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return (true, "Backup storage exists and is writable.");
        }
        catch (Exception ex) { return (false, "Backup storage verification failed: " + ex.Message); }
    }

    public IReadOnlyList<string> VerifyEnvironment()
    {
        var issues = new List<string>();
        if (!File.Exists(settings.SteamCmdPath)) issues.Add("SteamCMD is missing.");
        if (!File.Exists(paths.ServerExecutable)) issues.Add("Palworld dedicated server is missing.");
        if (!File.Exists(settings.ConfigFile)) issues.Add("PalWorldSettings.ini is missing.");
        try
        {
            Directory.CreateDirectory(settings.BackupRoot);
            var probe = Path.Combine(settings.BackupRoot, ".write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception ex) { issues.Add("Backup storage is not writable: " + ex.Message); }
        return issues;
    }

    private static void Add(ObservableCollection<EnvironmentComponentRow> rows, string component, string status, string location, string details, string action) =>
        rows.Add(new EnvironmentComponentRow { Component = component, Status = status, Location = location, Details = details, Action = action });

    private static IEnumerable<string?> SteamCandidates()
    {
        yield return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null)?.ToString()?.Replace('/', '\\');
        yield return Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null)?.ToString();
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
    }

    private static string? ReadWorkshopName(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
        {
            if (!Path.GetFileName(file).Contains("manifest", StringComparison.OrdinalIgnoreCase) && !file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                foreach (var propertyName in new[] { "title", "name", "Name" })
                    if (document.RootElement.TryGetProperty(propertyName, out var value)) return value.GetString();
            }
            catch { }
        }
        return null;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var directory in Directory.EnumerateDirectories(source)) CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string ReplaceOption(string text, string key, string value)
    {
        var pattern = $@"(?<prefix>(?:^|,){Regex.Escape(key)}=)(?:""(?:\\.|[^""])*""|[^,)]*)";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase)
            ? Regex.Replace(text, pattern, match => match.Groups["prefix"].Value + value, RegexOptions.IgnoreCase)
            : text.Replace(")", "," + key + "=" + value + ")");
    }
}
