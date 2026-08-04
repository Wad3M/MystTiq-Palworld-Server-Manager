using PalworldManager.Models;
using SharpCompress.Archives;

namespace PalworldManager.Services;

public sealed class ModService(AppSettings settings)
{
    private static readonly string[] PakExtensions = [".pak", ".ucas", ".utoc"];
    private static readonly HashSet<string> KnownUe4ssRuntimeComponents = new(StringComparer.OrdinalIgnoreCase)
    {
        "ActorDumperMod", "BPML_GenericFunctions", "BPModLoaderMod", "CheatManagerEnablerMod",
        "ConsoleCommandsMod", "ConsoleEnablerMod", "jsbLuaProfilerMod", "Keybinds",
        "LineTraceMod", "shared", "SplitScreenMod"
    };

    private readonly ModScannerService scanner = new(settings);

    public List<ModRow> Scan() => scanner.Scan();

    public ModInstallPreview InspectZip(string zipPath)
    {
        using var prepared = PrepareInstallPlan(zipPath);
        var plan = prepared.Plan;
        var manifestExists = File.Exists(Path.Combine(settings.ManagedModsRoot, plan.Package, "InstallManifest.json"));
        var existingFiles = plan.Files
            .Select(file => file.Destination)
            .Where(path => File.Exists(path) || File.Exists(path + ".disabled"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ModInstallPreview(
            plan.Name,
            plan.Package,
            plan.PackageType,
            plan.InstallLocation,
            plan.Dependencies,
            manifestExists,
            existingFiles);
    }

    public ModInstallResult InstallZip(string zipPath, bool overwriteExisting = false)
    {
        using var prepared = PrepareInstallPlan(zipPath);
        var plan = prepared.Plan;
        var installArchivePath = prepared.ArchivePath;
        Directory.CreateDirectory(settings.ManagedModsRoot);

        var manifestFolder = Path.Combine(settings.ManagedModsRoot, plan.Package);
        var manifestPath = Path.Combine(manifestFolder, "InstallManifest.json");
        InstallManifest? previousManifest = null;
        if (File.Exists(manifestPath))
        {
            try { previousManifest = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath)); }
            catch { previousManifest = null; }
        }

        var conflicts = plan.Files
            .SelectMany(file => new[] { file.Destination, file.Destination + ".disabled" })
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if ((File.Exists(manifestPath) || conflicts.Count > 0) && !overwriteExisting)
            throw new ModAlreadyInstalledException(plan.Name, plan.Package, conflicts.Count);

        var transactionRoot = Path.Combine(Path.GetTempPath(), "PalworldServerManager", "ModUpgrade", Guid.NewGuid().ToString("N"));
        var stagingRoot = Path.Combine(transactionRoot, "Staging");
        var backupRoot = Path.Combine(transactionRoot, "Backup");
        Directory.CreateDirectory(stagingRoot);
        Directory.CreateDirectory(backupRoot);

        var stagedFiles = new List<(string Staged, string Destination)>();
        var installedFiles = new List<string>();
        var backups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var createdDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? oldManifestBackup = null;

        try
        {
            // Stage and validate every archive entry before touching the live server.
            using (var archive = ZipFile.OpenRead(installArchivePath))
            {
                var index = 0;
                foreach (var file in plan.Files)
                {
                    var entry = archive.GetEntry(file.ArchiveEntryName)
                        ?? throw new InvalidDataException($"The archive entry '{file.ArchiveEntryName}' could not be reopened.");
                    EnsureInsideServer(file.Destination);
                    var stagedPath = Path.Combine(stagingRoot, $"{index++:D6}_{Path.GetFileName(file.Destination)}");
                    entry.ExtractToFile(stagedPath, overwrite: true);
                    if (!File.Exists(stagedPath))
                        throw new IOException($"Staging validation failed for '{file.ArchiveEntryName}'.");
                    stagedFiles.Add((stagedPath, file.Destination));
                }
            }

            if (stagedFiles.Count != plan.Files.Count)
                throw new InvalidDataException("The staged MOD package did not contain every planned file.");

            Directory.CreateDirectory(manifestFolder);

            if (overwriteExisting)
            {
                var affected = conflicts
                    .Concat(previousManifest?.Files ?? [])
                    .Concat(stagedFiles.Select(file => file.Destination))
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var path in affected)
                {
                    EnsureInsideServer(path);
                    foreach (var actualPath in new[] { path, path + ".disabled" }.Where(File.Exists))
                    {
                        if (backups.ContainsKey(actualPath)) continue;
                        var backupPath = Path.Combine(backupRoot, $"{backups.Count:D6}_{Path.GetFileName(actualPath)}");
                        File.Copy(actualPath, backupPath, overwrite: true);
                        backups[actualPath] = backupPath;
                    }
                }

                if (File.Exists(manifestPath))
                {
                    oldManifestBackup = Path.Combine(backupRoot, "InstallManifest.json");
                    File.Copy(manifestPath, oldManifestBackup, overwrite: true);
                }
            }

            // Commit the staged package. Each failure reports the exact locked or
            // inaccessible destination and triggers a complete rollback below.
            foreach (var staged in stagedFiles)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(staged.Destination)!);
                    var existed = File.Exists(staged.Destination);
                    File.Copy(staged.Staged, staged.Destination, overwrite: overwriteExisting);
                    if (!existed) createdDestinations.Add(staged.Destination);
                    installedFiles.Add(staged.Destination);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Could not replace MOD file '{staged.Destination}'. Stop PalServer and any editor or antivirus process using this file, then try again. {ex.Message}", ex);
                }
            }

            // A true upgrade removes files owned by the previous version that are no
            // longer shipped by the new package. They remain recoverable until commit.
            if (overwriteExisting && previousManifest is not null)
            {
                var newFiles = installedFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var obsolete in previousManifest.Files
                             .Where(path => !newFiles.Contains(path))
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    EnsureInsideServer(obsolete);
                    try
                    {
                        if (File.Exists(obsolete)) File.Delete(obsolete);
                        if (File.Exists(obsolete + ".disabled")) File.Delete(obsolete + ".disabled");
                    }
                    catch (Exception ex)
                    {
                        throw new IOException($"Could not remove obsolete MOD file '{obsolete}'. The upgrade has been rolled back. {ex.Message}", ex);
                    }
                }
            }

            if (!plan.IsWorkshopPackage)
            {
                var manifest = new InstallManifest
                {
                    Package = plan.Package,
                    Name = plan.Name,
                    Version = plan.Version,
                    SourceZip = Path.GetFullPath(zipPath),
                    InstalledUtc = DateTime.UtcNow,
                    Files = installedFiles,
                    Dependencies = plan.Dependencies.ToList(),
                    Type = DetectManagedType(installedFiles),
                    EnableMethod = DetectEnableMethod(installedFiles),
                    LastKnownEnabled = previousManifest?.LastKnownEnabled ?? true
                };

                File.WriteAllText(
                    manifestPath,
                    JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            }

            try { Directory.Delete(transactionRoot, recursive: true); } catch { }
            return new ModInstallResult(plan.Name, plan.Package, plan.PackageType, installedFiles.Count, plan.SkippedFiles);
        }
        catch
        {
            // Remove newly committed files first, then restore every backed-up file
            // and the previous manifest. This leaves the old version usable when an
            // upgrade encounters a lock, permission problem, or damaged archive.
            foreach (var destination in installedFiles.AsEnumerable().Reverse())
            {
                try
                {
                    if (backups.TryGetValue(destination, out var backupPath) && File.Exists(backupPath))
                        File.Copy(backupPath, destination, overwrite: true);
                    else if (createdDestinations.Contains(destination) && File.Exists(destination))
                        File.Delete(destination);
                }
                catch { }
            }

            foreach (var pair in backups)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(pair.Key)!);
                    File.Copy(pair.Value, pair.Key, overwrite: true);
                }
                catch { }
            }

            try
            {
                if (oldManifestBackup is not null && File.Exists(oldManifestBackup))
                    File.Copy(oldManifestBackup, manifestPath, overwrite: true);
                else if (!File.Exists(oldManifestBackup ?? string.Empty) && File.Exists(manifestPath) && previousManifest is null)
                    File.Delete(manifestPath);
            }
            catch { }

            try
            {
                if (Directory.Exists(manifestFolder) && !Directory.EnumerateFileSystemEntries(manifestFolder).Any())
                    Directory.Delete(manifestFolder);
            }
            catch { }

            try { if (Directory.Exists(transactionRoot)) Directory.Delete(transactionRoot, recursive: true); } catch { }
            throw;
        }
    }


    private PreparedInstallPlan PrepareInstallPlan(string zipPath)
    {
        var extension = Path.GetExtension(zipPath);
        if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".rar", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".7z", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Supported mod packages are ZIP, RAR, and 7Z archives.");
        }

        string installArchivePath = zipPath;
        string? normalizedTempRoot = null;
        if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            normalizedTempRoot = Path.Combine(Path.GetTempPath(), "PalworldServerManager", "ArchiveImport", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(normalizedTempRoot);
            var extractedRoot = Path.Combine(normalizedTempRoot, "Extracted");
            Directory.CreateDirectory(extractedRoot);
            ExtractArchiveSafely(zipPath, extractedRoot);
            ExpandNestedArchives(extractedRoot, 0);
            installArchivePath = Path.Combine(normalizedTempRoot, "Normalized.zip");
            ZipFile.CreateFromDirectory(extractedRoot, installArchivePath, CompressionLevel.Fastest, includeBaseDirectory: false);
        }

        try
        {
            return new PreparedInstallPlan(BuildInstallPlan(installArchivePath), installArchivePath, normalizedTempRoot);
        }
        catch (InvalidOperationException ex) when
            (ex.Message.StartsWith("No supported Palworld mod files were found", StringComparison.OrdinalIgnoreCase))
        {
            // Nexus and other mod sites sometimes wrap the actual mod ZIP inside a
            // download ZIP. The old smart installer only inspected the outer archive,
            // so a perfectly valid nested .pak package was reported as unsupported.
            var tempRoot = normalizedTempRoot is null
                ? Path.Combine(Path.GetTempPath(), "PalworldServerManager", "NestedMod", Guid.NewGuid().ToString("N"))
                : Path.Combine(normalizedTempRoot, "NestedMod");
            Directory.CreateDirectory(tempRoot);

            try
            {
                var candidates = new List<(InstallPlan Plan, string ArchivePath, string DisplayName)>();
                DiscoverNestedInstallPlans(installArchivePath, tempRoot, 0, candidates);

                if (candidates.Count == 1)
                {
                    var candidate = candidates[0];
                    return new PreparedInstallPlan(candidate.Plan, candidate.ArchivePath, normalizedTempRoot ?? tempRoot);
                }

                if (candidates.Count > 1)
                {
                    var names = string.Join(Environment.NewLine, candidates
                        .Select(candidate => "  • " + candidate.DisplayName)
                        .Distinct(StringComparer.OrdinalIgnoreCase));
                    throw new InvalidOperationException(
                        "The ZIP contains multiple nested Palworld mod packages. MystTiq will not guess which variant to install." +
                        Environment.NewLine + Environment.NewLine + names +
                        Environment.NewLine + Environment.NewLine +
                        "Extract the ZIP and install the specific variant ZIP you want.");
                }

                var nestedArchiveNames = ReadNestedArchiveNames(installArchivePath);
                var nestedHint = nestedArchiveNames.Count == 0
                    ? string.Empty
                    : Environment.NewLine + Environment.NewLine +
                      "Nested archives were found but none contained a supported Palworld package:" +
                      Environment.NewLine + string.Join(Environment.NewLine, nestedArchiveNames.Select(name => "  • " + name));

                throw new InvalidOperationException(
                    "No supported Palworld mod files were found. MystTiq recursively checked the package for Pal folders, Content/Paks, Mods folders, loose .pak/.ucas/.utoc files, and nested archives." +
                    nestedHint);
            }
            catch
            {
                try { if (Directory.Exists(normalizedTempRoot ?? tempRoot)) Directory.Delete(normalizedTempRoot ?? tempRoot, recursive: true); } catch { }
                throw;
            }
        }
    }

    private static void ExtractArchiveSafely(string archivePath, string destinationRoot)
    {
        var destinationFull = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            var key = (entry.Key ?? string.Empty).Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(key) || key.Contains("../", StringComparison.Ordinal) || key.Equals("..", StringComparison.Ordinal))
                throw new InvalidDataException($"The archive contains an unsafe path: {entry.Key}");

            var destination = Path.GetFullPath(Path.Combine(destinationRoot, key.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(destinationFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"The archive contains an unsafe path: {entry.Key}");

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = entry.OpenEntryStream();
            using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
    }

    private static void ExpandNestedArchives(string root, int depth)
    {
        if (depth >= 3) return;
        var archives = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var ext = Path.GetExtension(path);
                return ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                       ext.Equals(".rar", StringComparison.OrdinalIgnoreCase) ||
                       ext.Equals(".7z", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        foreach (var nested in archives)
        {
            var nestedRoot = nested + ".expanded";
            if (Directory.Exists(nestedRoot)) continue;
            Directory.CreateDirectory(nestedRoot);
            try
            {
                ExtractArchiveSafely(nested, nestedRoot);
                ExpandNestedArchives(nestedRoot, depth + 1);
            }
            catch
            {
                try { Directory.Delete(nestedRoot, recursive: true); } catch { }
            }
        }
    }

    private void DiscoverNestedInstallPlans(
        string archivePath,
        string tempRoot,
        int depth,
        List<(InstallPlan Plan, string ArchivePath, string DisplayName)> candidates)
    {
        if (depth >= 4) return;

        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)))
        {
            if (!Path.GetExtension(entry.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                continue;

            var safeName = CleanName(Path.GetFileNameWithoutExtension(entry.Name));
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "NestedPackage";
            var extractedPath = Path.Combine(tempRoot, $"{depth:D2}_{Guid.NewGuid():N}_{safeName}.zip");
            entry.ExtractToFile(extractedPath, overwrite: true);

            try
            {
                var plan = BuildInstallPlan(extractedPath);
                candidates.Add((plan, extractedPath, entry.FullName));
            }
            catch (InvalidOperationException nestedEx) when
                (nestedEx.Message.StartsWith("No supported Palworld mod files were found", StringComparison.OrdinalIgnoreCase))
            {
                DiscoverNestedInstallPlans(extractedPath, tempRoot, depth + 1, candidates);
            }
        }
    }

    private static List<string> ReadNestedArchiveNames(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        return archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name) &&
                            Path.GetExtension(entry.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName)
            .ToList();
    }

    private InstallPlan BuildInstallPlan(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            throw new FileNotFoundException("The selected mod package could not be found.", zipPath);

        if (!Path.GetExtension(zipPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only ZIP mod packages are currently supported.");

        var archiveName = CleanName(Path.GetFileNameWithoutExtension(zipPath));
        if (string.IsNullOrWhiteSpace(archiveName)) archiveName = "ImportedMod";

        var files = new List<PlannedInstallFile>();
        var skippedFiles = new List<string>();
        string? discoveredName = null;
        string discoveredVersion = "";

        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count == 0)
            throw new InvalidOperationException("The ZIP file is empty.");

        var fileEntries = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToList();
        var commonRoot = FindCommonWrapperFolder(fileEntries.Select(entry => Normalize(entry.FullName)));

        string RelativeArchivePath(ZipArchiveEntry entry)
        {
            var path = Normalize(entry.FullName);
            if (!string.IsNullOrEmpty(commonRoot) && path.StartsWith(commonRoot + "/", StringComparison.OrdinalIgnoreCase))
                path = path[(commonRoot.Length + 1)..];
            return path;
        }

        // Canonical UE4SS package layout: Mods/<ModName>/Scripts/*.lua. The ZIP
        // filename can include versions or compatibility suffixes, but the runtime
        // folder is the stable package identity used by mods.txt and the MOD Library.
        // Detect it before the generic Mods/ deployment path so one install creates
        // one managed record instead of a temporary archive-name row plus a loose
        // UE4SS-folder row.
        var relativePaths = fileEntries.Select(entry => RelativeArchivePath(entry)).ToList();
        var ue4ssFolderCandidates = relativePaths
            .Where(path => path.StartsWith("Mods/", StringComparison.OrdinalIgnoreCase))
            .Select(path => path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 3)
            .Select(parts => parts[1])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ue4ssFolderCandidates.Count == 1)
        {
            var candidate = ue4ssFolderCandidates[0];
            var prefix = $"Mods/{candidate}/";
            var candidatePaths = relativePaths
                .Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(path => path[prefix.Length..])
                .ToList();
            var hasCandidateLua = candidatePaths.Any(path =>
                path.StartsWith("Scripts/", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase));

            if (hasCandidateLua)
            {
                var modFolderName = CleanName(candidate);
                var modFolder = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", "Mods", modFolderName);

                foreach (var entry in fileEntries)
                {
                    var archivePath = RelativeArchivePath(entry);
                    if (!archivePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        skippedFiles.Add(archivePath);
                        continue;
                    }

                    var pathInsideMod = archivePath[prefix.Length..];
                    if (string.IsNullOrWhiteSpace(pathInsideMod) || pathInsideMod.Contains("../", StringComparison.Ordinal))
                    {
                        skippedFiles.Add(archivePath);
                        continue;
                    }

                    var destination = Path.Combine(modFolder, ToWindows(pathInsideMod));
                    EnsureInsideServer(destination);
                    files.Add(new PlannedInstallFile(entry.FullName, destination));
                }

                return new InstallPlan(
                    modFolderName,
                    modFolderName,
                    ExtractVersionFromArchiveName(Path.GetFileNameWithoutExtension(zipPath)),
                    files,
                    skippedFiles,
                    false,
                    "UE4SS Lua Mod",
                    modFolder,
                    ["UE4SS"]);
            }
        }

        // Smart package detection: a very common UE4SS Lua package is wrapped as
        // <ModName>/enabled.txt + <ModName>/Scripts/main.lua. After removing the
        // wrapper folder, retain the logical mod name and install the whole package
        // beneath Pal\Binaries\Win64\Mods\<ModName>.
        var hasEnabledMarker = relativePaths.Any(path =>
            path.Equals("enabled.txt", StringComparison.OrdinalIgnoreCase));
        var hasLuaScripts = relativePaths.Any(path =>
            path.StartsWith("Scripts/", StringComparison.OrdinalIgnoreCase) &&
            path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase));

        if (hasEnabledMarker && hasLuaScripts)
        {
            var modFolderName = CleanName(string.IsNullOrWhiteSpace(commonRoot) ? archiveName : commonRoot);
            if (string.IsNullOrWhiteSpace(modFolderName)) modFolderName = archiveName;
            var modFolder = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", "Mods", modFolderName);

            foreach (var entry in fileEntries)
            {
                var archivePath = RelativeArchivePath(entry);
                if (string.IsNullOrWhiteSpace(archivePath) || archivePath.Contains("../", StringComparison.Ordinal))
                {
                    skippedFiles.Add(archivePath);
                    continue;
                }

                var destination = Path.Combine(modFolder, ToWindows(archivePath));
                EnsureInsideServer(destination);
                files.Add(new PlannedInstallFile(entry.FullName, destination));
            }

            return new InstallPlan(
                modFolderName,
                modFolderName,
                discoveredVersion,
                files,
                skippedFiles,
                false,
                "UE4SS Lua Mod",
                modFolder,
                ["UE4SS"]);
        }

        // UE4SS C++/DLL mods commonly use <ModName>/dlls/main.dll together
        // with enabled.txt and one or more INI files. These packages do not carry
        // Info.json or a MystTiq manifest, but they are valid UE4SS user mods and must
        // remain together beneath Mods\<ModName>.
        var hasUe4ssDll = relativePaths.Any(path =>
            path.Equals("dlls/main.dll", StringComparison.OrdinalIgnoreCase));

        if (hasEnabledMarker && hasUe4ssDll)
        {
            var modFolderName = CleanName(string.IsNullOrWhiteSpace(commonRoot) ? archiveName : commonRoot);
            if (string.IsNullOrWhiteSpace(modFolderName)) modFolderName = archiveName;
            var modFolder = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", "Mods", modFolderName);

            foreach (var entry in fileEntries)
            {
                var archivePath = RelativeArchivePath(entry);
                if (string.IsNullOrWhiteSpace(archivePath) || archivePath.Contains("../", StringComparison.Ordinal))
                {
                    skippedFiles.Add(archivePath);
                    continue;
                }

                var destination = Path.Combine(modFolder, ToWindows(archivePath));
                EnsureInsideServer(destination);
                files.Add(new PlannedInstallFile(entry.FullName, destination));
            }

            return new InstallPlan(
                modFolderName,
                modFolderName,
                ExtractVersionFromArchiveName(Path.GetFileNameWithoutExtension(zipPath)),
                files,
                skippedFiles,
                false,
                "UE4SS DLL Mod",
                modFolder,
                ["UE4SS"]);
        }

        // Native Win64 loader/anti-cheat packages such as PalDefender place their
        // module and proxy loader directly beside PalServer-Win64-Test-Cmd.exe.
        // Treat the pair as one managed package so upgrades are staged, existing
        // proxy DLLs are backed up, and rollback remains available.
        var rootFileNames = relativePaths
            .Where(path => path.IndexOf('/') < 0)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasPalDefenderModule = rootFileNames.Contains("PalDefender.dll");
        var hasSupportedProxyLoader = rootFileNames.Contains("d3d9.dll") ||
                                      rootFileNames.Contains("dwmapi.dll") ||
                                      rootFileNames.Contains("xinput1_3.dll");

        if (hasPalDefenderModule && hasSupportedProxyLoader)
        {
            const string packageName = "PalDefender";
            var win64Folder = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64");
            var allowedRootExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".dll", ".ini", ".json", ".toml", ".cfg", ".txt"
            };

            foreach (var entry in fileEntries)
            {
                var archivePath = RelativeArchivePath(entry);
                if (string.IsNullOrWhiteSpace(archivePath) || archivePath.Contains("../", StringComparison.Ordinal) ||
                    archivePath.IndexOf('/') >= 0 || !allowedRootExtensions.Contains(Path.GetExtension(archivePath)))
                {
                    skippedFiles.Add(archivePath);
                    continue;
                }

                var destination = Path.Combine(win64Folder, Path.GetFileName(archivePath));
                EnsureInsideServer(destination);
                files.Add(new PlannedInstallFile(entry.FullName, destination));
            }

            if (files.Count == 0)
                throw new InvalidOperationException("The PalDefender package did not contain deployable Win64 files.");

            return new InstallPlan(
                packageName,
                packageName,
                ExtractVersionFromArchiveName(Path.GetFileNameWithoutExtension(zipPath)),
                files,
                skippedFiles,
                false,
                "Win64 Loader / Anti-Cheat",
                win64Folder,
                ["Stop PalServer before installation", "May replace an existing proxy loader DLL"]);
        }

        // Palworld 1.0 Workshop packages must remain intact beneath
        // Mods\Workshop, including Info.json and all files referenced by its
        // InstallRules. Palworld deploys these files itself on server restart.
        var workshopInfoEntry = fileEntries.FirstOrDefault(entry =>
            RelativeArchivePath(entry).Equals("Info.json", StringComparison.OrdinalIgnoreCase));

        if (workshopInfoEntry is not null &&
            TryReadWorkshopInfo(workshopInfoEntry, out var workshopPackage, out var workshopName,
                out var workshopVersion, out var supportsServer))
        {
            if (string.IsNullOrWhiteSpace(workshopPackage))
                throw new InvalidDataException("Info.json does not contain a PackageName.");

            if (!supportsServer)
                throw new InvalidOperationException(
                    $"The mod '{workshopName}' does not declare a server-compatible InstallRule (IsServer=true).");

            var workshopFolder = Path.Combine(settings.WorkshopRoot, CleanName(workshopPackage));
            foreach (var entry in fileEntries)
            {
                var archivePath = RelativeArchivePath(entry);
                if (string.IsNullOrWhiteSpace(archivePath) || archivePath.Contains("../", StringComparison.Ordinal))
                {
                    skippedFiles.Add(archivePath);
                    continue;
                }

                var destination = Path.Combine(workshopFolder, ToWindows(archivePath));
                EnsureInsideServer(destination);
                files.Add(new PlannedInstallFile(entry.FullName, destination));
            }

            return new InstallPlan(
                workshopPackage,
                string.IsNullOrWhiteSpace(workshopName) ? workshopPackage : workshopName,
                workshopVersion,
                files,
                skippedFiles,
                true,
                "Steam Workshop Package",
                workshopFolder,
                []);
        }

        foreach (var entry in fileEntries)
        {
            var archivePath = RelativeArchivePath(entry);

            if (archivePath.Equals("Info.json", StringComparison.OrdinalIgnoreCase))
            {
                TryReadInfo(entry, ref discoveredName, ref discoveredVersion);
                continue;
            }

            var destination = ResolveDestination(archivePath);
            if (destination is null)
            {
                skippedFiles.Add(archivePath);
                continue;
            }

            EnsureInsideServer(destination);
            files.Add(new PlannedInstallFile(entry.FullName, destination));
        }

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                "No supported Palworld mod files were found. The ZIP should contain a Pal folder, " +
                "a Content/Paks folder, a Mods folder, or .pak/.ucas/.utoc files.");
        }

        return new InstallPlan(
            archiveName,
            string.IsNullOrWhiteSpace(discoveredName) ? archiveName : discoveredName,
            discoveredVersion,
            files,
            skippedFiles,
            false,
            "Palworld Mod Package",
            files.Select(file => Path.GetDirectoryName(file.Destination) ?? settings.ServerRoot).FirstOrDefault() ?? settings.ServerRoot,
            []);
    }

    public ModDeleteResult Delete(string package)
    {
        if (string.IsNullOrWhiteSpace(package))
            throw new ArgumentException("A mod package is required.", nameof(package));

        Directory.CreateDirectory(settings.ManagedModsRoot);
        var manifestFolder = Path.Combine(settings.ManagedModsRoot, CleanName(package));
        var manifestPath = Path.Combine(manifestFolder, "InstallManifest.json");
        var deletedFiles = 0;
        var missingFiles = 0;

        // Official Palworld Workshop packages are removed from their source
        // folder. Palworld removes the deployed copy on the next restart.
        var workshopFolder = FindLegacyModFolder(package);
        if (workshopFolder is not null)
        {
            deletedFiles = Directory.EnumerateFiles(workshopFolder, "*", SearchOption.AllDirectories).Count();
            Directory.Delete(workshopFolder, recursive: true);
            RemoveEnabledPackage(package);
            return new ModDeleteResult(package, deletedFiles, missingFiles);
        }

        if (File.Exists(manifestPath))
        {
            var manifest = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath))
                ?? throw new InvalidDataException("The mod installation manifest could not be read.");

            foreach (var file in manifest.Files
                         .Where(path => !string.IsNullOrWhiteSpace(path))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                EnsureInsideServer(file);
                var candidate = File.Exists(file) ? file : file + ".disabled";
                EnsureInsideServer(candidate);
                if (!File.Exists(candidate))
                {
                    missingFiles++;
                    continue;
                }

                File.Delete(candidate);
                deletedFiles++;
                DeleteEmptyParents(Path.GetDirectoryName(candidate));
            }

            File.Delete(manifestPath);
            if (Directory.Exists(manifestFolder) && !Directory.EnumerateFileSystemEntries(manifestFolder).Any())
                Directory.Delete(manifestFolder);
        }
        else
        {
            // Legacy workshop entries are represented by their Info.json folder.
            var legacyFolder = FindLegacyModFolder(package);
            if (legacyFolder is not null)
            {
                Directory.Delete(legacyFolder, recursive: true);
                deletedFiles = 1;
            }
            else
            {
                // Mods discovered directly from the live server folders may not
                // have a manager manifest. Remove only the matching pak bundle or
                // UE4SS folder selected in the list.
                deletedFiles = DeleteUnmanagedModFiles(package);
                if (deletedFiles == 0)
                    throw new InvalidOperationException("No installation manifest or matching mod files were found for the selected mod.");
            }
        }

        RemoveEnabledPackage(package);
        return new ModDeleteResult(package, deletedFiles, missingFiles);
    }

    public ModApplyResult Apply(IEnumerable<ModRow> mods)
    {
        var rows = mods.ToList();
        var enabledCount = 0;
        var disabledCount = 0;
        var changedFiles = 0;
        var warnings = new List<string>();

        foreach (var mod in rows)
        {
            try
            {
                var changed = SetModEnabled(mod.Package, mod.Enabled);
                changedFiles += changed;
                if (mod.Enabled) enabledCount++; else disabledCount++;
            }
            catch (Exception ex)
            {
                warnings.Add($"{mod.Name}: {ex.Message}");
            }
        }

        Directory.CreateDirectory(settings.ModsRoot);
        var lines = new List<string> { "[PalModSettings]", "bGlobalEnableMod=true" };
        lines.AddRange(rows.Where(mod => mod.Enabled).Select(mod => $"ActiveModList={mod.Package}"));
        AtomicFile.Write(settings.ModSettingsFile, string.Join(Environment.NewLine, lines));
        UpdateUe4ssModsTxt(rows);

        return new ModApplyResult(enabledCount, disabledCount, changedFiles, warnings);
    }

    private int SetModEnabled(string package, bool enabled)
    {
        var changed = 0;

        // Official Workshop packages are loaded from the directory passed through
        // -workshopdir. Merely changing PalModSettings.ini is not sufficient, so
        // disabling a Workshop package moves its intact folder to a sibling holding
        // directory that Palworld does not scan. Enabling moves it back.
        var workshopFolder = FindWorkshopFolder(package);
        if (!string.IsNullOrWhiteSpace(workshopFolder))
        {
            var folderName = Path.GetFileName(workshopFolder);
            var activeTarget = Path.Combine(settings.WorkshopRoot, folderName);
            var disabledTarget = Path.Combine(settings.DisabledWorkshopRoot, folderName);
            Directory.CreateDirectory(settings.WorkshopRoot);
            Directory.CreateDirectory(settings.DisabledWorkshopRoot);

            var currentlyActive = Path.GetFullPath(workshopFolder).StartsWith(
                Path.GetFullPath(settings.WorkshopRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);

            if (enabled && !currentlyActive)
            {
                if (Directory.Exists(activeTarget))
                    throw new InvalidOperationException($"Cannot enable '{package}' because an active Workshop folder already exists at {activeTarget}.");
                Directory.Move(workshopFolder, activeTarget);
                changed++;
            }
            else if (!enabled && currentlyActive)
            {
                if (Directory.Exists(disabledTarget))
                    throw new InvalidOperationException($"Cannot disable '{package}' because a disabled Workshop folder already exists at {disabledTarget}.");
                Directory.Move(workshopFolder, disabledTarget);
                changed++;
            }
        }
        var manifestPath = Path.Combine(settings.ManagedModsRoot, CleanName(package), "InstallManifest.json");
        InstallManifest? managedManifest = null;
        if (File.Exists(manifestPath))
        {
            managedManifest = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath));
            if (managedManifest is not null)
            {
                var managedType = string.IsNullOrWhiteSpace(managedManifest.Type)
                    ? DetectManagedType(managedManifest.Files)
                    : managedManifest.Type;
                var isManagedUe4ss = managedType.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) ||
                                     managedManifest.Files.Any(IsUe4ssPath);

                foreach (var original in managedManifest.Files.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    EnsureInsideServer(original);
                    if (isManagedUe4ss)
                    {
                        // UE4SS user mods stay deployed. mods.txt is the canonical
                        // enable source. A package-provided enabled.txt bypasses
                        // mods.txt, so keep that marker neutralized for BOTH states.
                        if (IsUe4ssEnabledMarker(original))
                        {
                            changed += NeutralizeEnabledMarker(original);
                            continue;
                        }

                        // Older MystTiq builds may have renamed individual Lua/script
                        // files to *.disabled. Restore those files and let mods.txt
                        // control whether the mod loads.
                        var disabledOriginal = original + ".disabled";
                        if (!File.Exists(original) && File.Exists(disabledOriginal))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(original)!);
                            File.Move(disabledOriginal, original);
                            changed++;
                        }
                    }
                    else
                    {
                        changed += ToggleFile(original, enabled);
                    }
                }
            }
        }

        var pakRoot = Path.Combine(settings.ServerRoot, "Pal", "Content", "Paks", "~mods");
        foreach (var extension in PakExtensions)
            changed += ToggleFile(Path.Combine(pakRoot, package + extension), enabled);

        var ue4ssRoot = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", "Mods");
        var activeFolder = Path.Combine(ue4ssRoot, package);
        var disabledFolder = activeFolder + ".disabled";
        EnsureInsideServer(activeFolder);
        // UE4SS enable state is controlled by Mods\mods.txt. Restore folders
        // disabled by older manager builds, then leave them in place.
        if (Directory.Exists(disabledFolder) && !Directory.Exists(activeFolder))
        {
            Directory.Move(disabledFolder, activeFolder);
            changed++;
        }

        // UE4SS enabled.txt bypasses mods.txt. Always neutralize the marker for
        // user-installed mods so the manager and UE4SS share one authoritative
        // activation source. Built-in UE4SS runtime components are never managed
        // through this path.
        if (Directory.Exists(activeFolder) && !KnownUe4ssRuntimeComponents.Contains(package))
            changed += NeutralizeEnabledMarker(Path.Combine(activeFolder, "enabled.txt"));

        // Preserve MystTiq's last requested state as a fallback for ZIP packages whose
        // runtime format does not expose a separate enable marker. The next Scan()
        // still prefers actual PAK deployment and UE4SS mods.txt evidence.
        if (File.Exists(manifestPath))
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath));
                if (manifest is not null)
                {
                    manifest.LastKnownEnabled = enabled;
                    File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch
            {
                // State persistence is supplementary; the runtime file state remains authoritative.
            }
        }

        return changed;
    }

    private void UpdateUe4ssModsTxt(IReadOnlyList<ModRow> rows)
    {
        var ue4ssRoot = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", "Mods");
        if (!Directory.Exists(ue4ssRoot))
            return;

        var modsTxt = Path.Combine(ue4ssRoot, "mods.txt");
        var lines = File.Exists(modsTxt) ? File.ReadAllLines(modsTxt).ToList() : [];

        foreach (var row in rows)
        {
            var folder = Path.Combine(ue4ssRoot, row.Package);
            var disabledFolder = folder + ".disabled";
            if (!Directory.Exists(folder) && !Directory.Exists(disabledFolder))
                continue;

            var prefix = row.Package + " :";
            var replacement = $"{row.Package} : {(row.Enabled ? 1 : 0)}";
            var index = lines.FindIndex(line => line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                lines[index] = replacement;
            }
            else
            {
                var keybindIndex = lines.FindIndex(line => line.TrimStart().StartsWith("Keybinds :", StringComparison.OrdinalIgnoreCase));
                if (keybindIndex >= 0) lines.Insert(keybindIndex, replacement);
                else lines.Add(replacement);
            }
        }

        AtomicFile.Write(modsTxt, string.Join(Environment.NewLine, lines));
    }

    private bool IsUe4ssEnabledMarker(string path)
    {
        if (!Path.GetFileName(path).Equals("enabled.txt", StringComparison.OrdinalIgnoreCase))
            return false;
        return IsUe4ssPath(path);
    }

    private int NeutralizeEnabledMarker(string markerPath)
    {
        EnsureInsideServer(markerPath);
        if (!File.Exists(markerPath))
            return 0;

        var disabledPath = markerPath + ".disabled";
        if (!File.Exists(disabledPath))
        {
            File.Move(markerPath, disabledPath);
            return 1;
        }

        // If a prior MystTiq build already preserved the original marker, move the
        // active override to a uniquely named backup. The important invariant is
        // that no file named exactly enabled.txt remains active.
        var backupPath = markerPath + $".myst-backup_{DateTime.Now:yyyyMMdd_HHmmss_fff}";
        File.Move(markerPath, backupPath);
        return 1;
    }

    private int ToggleFile(string activePath, bool enabled)
    {
        var disabledPath = activePath + ".disabled";
        EnsureInsideServer(activePath);
        if (enabled && File.Exists(disabledPath) && !File.Exists(activePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(activePath)!);
            File.Move(disabledPath, activePath);
            return 1;
        }
        if (!enabled && File.Exists(activePath) && !File.Exists(disabledPath))
        {
            File.Move(activePath, disabledPath);
            return 1;
        }
        return 0;
    }

    public ModStateRepairResult RepairUe4ssStates()
    {
        var ue4ssRoot = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", "Mods");
        if (!Directory.Exists(ue4ssRoot))
            return new ModStateRepairResult(0, 0, []);

        var modsTxt = Path.Combine(ue4ssRoot, "mods.txt");
        var lines = File.Exists(modsTxt) ? File.ReadAllLines(modsTxt).ToList() : [];
        var warnings = new List<string>();
        var repaired = 0;
        var entriesAdded = 0;

        foreach (var folder in Directory.EnumerateDirectories(ue4ssRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(folder);
            if (string.IsNullOrWhiteSpace(name) ||
                name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) ||
                KnownUe4ssRuntimeComponents.Contains(name))
                continue;

            try
            {
                var marker = Path.Combine(folder, "enabled.txt");
                if (!File.Exists(marker))
                    continue;

                var prefix = name + " :";
                var index = lines.FindIndex(line => line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    // No mods.txt state existed, so preserve the marker's current
                    // effective state by adding an enabled entry before removing the
                    // bypass marker.
                    var replacement = $"{name} : 1";
                    var keybindIndex = lines.FindIndex(line => line.TrimStart().StartsWith("Keybinds :", StringComparison.OrdinalIgnoreCase));
                    if (keybindIndex >= 0) lines.Insert(keybindIndex, replacement);
                    else lines.Add(replacement);
                    entriesAdded++;
                }

                repaired += NeutralizeEnabledMarker(marker);
            }
            catch (Exception ex)
            {
                warnings.Add($"{name}: {ex.Message}");
            }
        }

        if (entriesAdded > 0)
            AtomicFile.Write(modsTxt, string.Join(Environment.NewLine, lines));

        return new ModStateRepairResult(repaired, entriesAdded, warnings);
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
        var ue4ssModsTxt = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", "Mods", "mods.txt");
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

                var enabledMarker = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", "Mods", folder, "enabled.txt");
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

    private string DetectEnableMethod(IEnumerable<string> files)
    {
        var list = files.ToList();
        if (list.Any(IsUe4ssPath)) return "UE4SS mods.txt / enabled marker";
        if (list.Any(IsPakPath)) return "Active/disabled file deployment";
        return "Managed manifest";
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
        var modsRoot = Path.GetFullPath(Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", "Mods"))
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
        var modsRoot = Path.GetFullPath(Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", "Mods"))
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

    private string? FindWorkshopFolder(string package)
    {
        foreach (var root in new[] { settings.WorkshopRoot, settings.DisabledWorkshopRoot })
        {
            if (!Directory.Exists(root))
                continue;

            if (package.StartsWith("Workshop_", StringComparison.OrdinalIgnoreCase))
            {
                var id = package["Workshop_".Length..];
                var direct = Path.Combine(root, id);
                if (Directory.Exists(direct))
                    return direct;
            }

            foreach (var folder in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var info = Directory.EnumerateFiles(folder, "Info.json", SearchOption.AllDirectories).FirstOrDefault();
                    if (info is not null)
                    {
                        using var document = JsonDocument.Parse(File.ReadAllText(info));
                        var candidate = Get(document.RootElement, "PackageName");
                        if (candidate.Equals(package, StringComparison.OrdinalIgnoreCase))
                            return folder;
                    }

                    var manifest = Directory.EnumerateFiles(folder, "myst-install-manifest.json", SearchOption.AllDirectories).FirstOrDefault();
                    if (manifest is not null)
                    {
                        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
                        var id = Get(document.RootElement, "WorkshopId");
                        if (("Workshop_" + id).Equals(package, StringComparison.OrdinalIgnoreCase))
                            return folder;
                    }
                }
                catch
                {
                    // Third-party metadata can be malformed; continue scanning.
                }
            }
        }

        return null;
    }

    private static string? ReadWorkshopPackageName(string folder)
    {
        var info = Directory.EnumerateFiles(folder, "Info.json", SearchOption.AllDirectories).FirstOrDefault();
        if (info is null) return null;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(info));
            var package = Get(document.RootElement, "PackageName");
            return string.IsNullOrWhiteSpace(package) ? null : package;
        }
        catch
        {
            return null;
        }
    }

    private string? FindLegacyModFolder(string package)
    {
        var managedWorkshopFolder = FindWorkshopFolder(package);
        if (!string.IsNullOrWhiteSpace(managedWorkshopFolder))
            return managedWorkshopFolder;

        if (package.StartsWith("Workshop_", StringComparison.OrdinalIgnoreCase))
        {
            var id = package["Workshop_".Length..];
            var direct = Path.Combine(settings.WorkshopRoot, id);
            if (Directory.Exists(direct)) return direct;
        }

        foreach (var manifest in Directory.EnumerateFiles(settings.WorkshopRoot, "myst-install-manifest.json", SearchOption.AllDirectories))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(manifest));
                var id = Get(document.RootElement, "WorkshopId");
                var folder = Path.GetDirectoryName(manifest)!;
                var manifestPackage = ReadWorkshopPackageName(folder) ?? (string.IsNullOrWhiteSpace(id) ? Path.GetFileName(folder) : "Workshop_" + id);
                if (manifestPackage.Equals(package, StringComparison.OrdinalIgnoreCase)) return folder;
            }
            catch { }
        }

        foreach (var info in Directory.EnumerateFiles(settings.WorkshopRoot, "Info.json", SearchOption.AllDirectories))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(info));
                var candidate = Get(document.RootElement, "PackageName");
                if (candidate.Equals(package, StringComparison.OrdinalIgnoreCase))
                    return Path.GetDirectoryName(info);
            }
            catch { }
        }

        return null;
    }


    private int DeleteUnmanagedModFiles(string package)
    {
        var deleted = 0;
        var pakRoot = Path.Combine(settings.ServerRoot, "Pal", "Content", "Paks", "~mods");
        if (Directory.Exists(pakRoot))
        {
            foreach (var extension in PakExtensions)
            {
                var path = Path.Combine(pakRoot, package + extension);
                foreach (var candidate in new[] { path, path + ".disabled" })
                {
                    EnsureInsideServer(candidate);
                    if (!File.Exists(candidate))
                        continue;

                    File.Delete(candidate);
                    deleted++;
                }
            }
        }

        var ue4ssFolder = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", "Mods", package);
        EnsureInsideServer(ue4ssFolder);
        foreach (var candidate in new[] { ue4ssFolder, ue4ssFolder + ".disabled" })
        {
            EnsureInsideServer(candidate);
            if (!Directory.Exists(candidate))
                continue;

            Directory.Delete(candidate, recursive: true);
            deleted++;
        }

        return deleted;
    }

    private void RemoveEnabledPackage(string package)
    {
        if (!File.Exists(settings.ModSettingsFile))
            return;

        var retained = File.ReadAllLines(settings.ModSettingsFile)
            .Where(line => !line.Trim().Equals($"ActiveModList={package}", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        AtomicFile.Write(settings.ModSettingsFile, string.Join(Environment.NewLine, retained));
    }

    private void DeleteEmptyParents(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        var serverRoot = Path.GetFullPath(settings.ServerRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = Path.GetFullPath(directory);

        while (current.StartsWith(serverRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
               !current.Equals(serverRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(current) || Directory.EnumerateFileSystemEntries(current).Any())
                break;

            Directory.Delete(current);
            current = Path.GetDirectoryName(current) ?? serverRoot;
        }
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

    private string? ResolveDestination(string relativePath)
    {
        relativePath = Normalize(relativePath).TrimStart('/');
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains("../", StringComparison.Ordinal))
            return null;

        if (relativePath.StartsWith("Pal/", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith("Engine/", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(settings.ServerRoot, ToWindows(relativePath));

        if (relativePath.StartsWith("Content/Paks/", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(settings.ServerRoot, "Pal", ToWindows(relativePath));

        if (relativePath.StartsWith("Paks/", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(settings.ServerRoot, "Pal", "Content", ToWindows(relativePath));

        if (relativePath.StartsWith("Mods/", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", ToWindows(relativePath));

        if (relativePath.StartsWith("Binaries/Win64/", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(settings.ServerRoot, "Pal", ToWindows(relativePath));

        var extension = Path.GetExtension(relativePath);
        if (PakExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return Path.Combine(settings.ServerRoot, "Pal", "Content", "Paks", "~mods", Path.GetFileName(relativePath));

        // Common UE4SS bootstrap files supplied at the root of some mod-loader ZIPs.
        if (relativePath.IndexOf('/') < 0 &&
            (relativePath.Equals("UE4SS.dll", StringComparison.OrdinalIgnoreCase) ||
             relativePath.Equals("xinput1_3.dll", StringComparison.OrdinalIgnoreCase) ||
             relativePath.Equals("dwmapi.dll", StringComparison.OrdinalIgnoreCase)))
            return Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64", Path.GetFileName(relativePath));

        return null;
    }

    private void EnsureInsideServer(string path)
    {
        var serverRoot = Path.GetFullPath(settings.ServerRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(serverRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The ZIP contains an unsafe path outside the Palworld server folder.");
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

    private static string? TryGetUe4ssRuntimeFolderName(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        var normalized = Normalize(filePath);
        const string marker = "/Pal/Binaries/Win64/Mods/";
        var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            const string shorterMarker = "Pal/Binaries/Win64/Mods/";
            index = normalized.IndexOf(shorterMarker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;
            index += shorterMarker.Length;
        }
        else
        {
            index += marker.Length;
        }

        var remainder = normalized[index..];
        var slash = remainder.IndexOf('/');
        return slash <= 0 ? null : remainder[..slash];
    }

    private static string ExtractVersionFromArchiveName(string archiveName)
    {
        var match = Regex.Match(archiveName ?? string.Empty, @"(?<!\d)v?(\d+\.\d+(?:\.\d+){0,2})(?!\d)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string? FindCommonWrapperFolder(IEnumerable<string> paths)
    {
        var firstParts = paths
            .Where(path => path.Contains('/'))
            .Select(path => path.Split('/', 2)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (firstParts.Count != 1) return null;
        var root = firstParts[0];
        return root.Equals("Pal", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("Engine", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("Content", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("Paks", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("Mods", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("Binaries", StringComparison.OrdinalIgnoreCase)
            ? null
            : root;
    }

    private static bool TryReadWorkshopInfo(
        ZipArchiveEntry entry,
        out string package,
        out string name,
        out string version,
        out bool supportsServer)
    {
        package = "";
        name = "";
        version = "";
        supportsServer = false;

        try
        {
            using var stream = entry.Open();
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            package = Get(root, "PackageName");
            name = Get(root, "Name");
            if (string.IsNullOrWhiteSpace(name)) name = Get(root, "DisplayName");
            version = Get(root, "Version");

            if (root.TryGetProperty("InstallRules", out var rules) && rules.ValueKind == JsonValueKind.Array)
            {
                foreach (var rule in rules.EnumerateArray())
                {
                    if (rule.TryGetProperty("IsServer", out var isServer) &&
                        isServer.ValueKind is JsonValueKind.True)
                    {
                        supportsServer = true;
                        break;
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryReadInfo(ZipArchiveEntry entry, ref string? name, ref string version)
    {
        try
        {
            using var stream = entry.Open();
            using var document = JsonDocument.Parse(stream);
            name = Get(document.RootElement, "Name");
            if (string.IsNullOrWhiteSpace(name)) name = Get(document.RootElement, "DisplayName");
            version = Get(document.RootElement, "Version");
        }
        catch { }
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

    private sealed record PlannedInstallFile(string ArchiveEntryName, string Destination);

    private sealed class PreparedInstallPlan(InstallPlan plan, string archivePath, string? tempRoot) : IDisposable
    {
        public InstallPlan Plan { get; } = plan;
        public string ArchivePath { get; } = archivePath;

        public void Dispose()
        {
            if (string.IsNullOrWhiteSpace(tempRoot)) return;
            try
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            }
            catch { }
        }
    }

    private sealed record InstallPlan(
        string Package,
        string Name,
        string Version,
        IReadOnlyList<PlannedInstallFile> Files,
        IReadOnlyList<string> SkippedFiles,
        bool IsWorkshopPackage,
        string PackageType,
        string InstallLocation,
        IReadOnlyList<string> Dependencies);

}

public sealed record ModInstallPreview(
    string Name,
    string Package,
    string PackageType,
    string InstallLocation,
    IReadOnlyList<string> Dependencies,
    bool ManifestExists,
    IReadOnlyList<string> ExistingFiles)
{
    public bool AlreadyExists => ManifestExists || ExistingFiles.Count > 0;
}

public sealed class ModAlreadyInstalledException(string name, string package, int conflictingFileCount)
    : InvalidOperationException($"The mod '{name}' already exists ({conflictingFileCount} conflicting files).")
{
    public string ModName { get; } = name;
    public string Package { get; } = package;
    public int ConflictingFileCount { get; } = conflictingFileCount;
}

public sealed record ModInstallResult(
    string Name,
    string Package,
    string PackageType,
    int InstalledFileCount,
    IReadOnlyList<string> SkippedFiles);

public sealed record ModDeleteResult(string Package, int DeletedFileCount, int MissingFileCount);

public sealed record ModApplyResult(int EnabledCount, int DisabledCount, int ChangedItemCount, IReadOnlyList<string> Warnings);

public sealed record ModStateRepairResult(int RepairedMarkers, int EntriesAdded, IReadOnlyList<string> Warnings);
