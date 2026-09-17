using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessModManagementService
{
    private static readonly string[] PakExtensions = [".pak", ".ucas", ".utoc"];
    private static readonly Regex ModsRootLogPattern = new(
        "Loading\\s+mods\\s+from:\\s*[\"']?(?<path>.+?)[\"']?\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StartedModPattern = new(
        "Starting\\s+(?<kind>Lua|C\\+\\+)\\s+mod\\s+[\"'](?<name>[^\"']+)[\"']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> KnownRuntimeComponents =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Keybinds", "shared", "LogicMods", "BPModLoaderMod", "CheatManagerEnablerMod"
        };

    private readonly IServerPathProfile paths;
    private readonly IServerLifecycleService lifecycle;
    private readonly HeadlessActivityLogService activity;
    private readonly HeadlessConsoleLogWriter? consoleLog;
    private readonly SemaphoreSlim mutationGate = new(1, 1);
    private readonly HeadlessUe4ssReleaseCatalogService ue4ssReleaseCatalog = new();
    private readonly Dictionary<string, Ue4ssInstallPreview> ue4ssInstallPreviews = new();
    private static readonly HttpClient Ue4ssDownloadHttp = BuildUe4ssDownloadHttpClient();
    private static readonly string[] Ue4ssKnownLayoutMarkers = ["UE4SS.dll", "UE4SS-settings.ini"];
    private static readonly HttpClient DescriptionHttp = BuildDescriptionHttpClient();
    private static readonly JsonSerializerOptions DescriptionJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public HeadlessModManagementService(IServerPathProfile paths, IServerLifecycleService lifecycle, HeadlessActivityLogService activity, HeadlessConsoleLogWriter? consoleLog = null)
    {
        this.paths = paths;
        this.lifecycle = lifecycle;
        this.activity = activity;
        this.consoleLog = consoleLog;
    }

    public async Task<HeadlessModMutationResult> InstallZipAsync(string type, string package, Stream content, long? contentLength, CancellationToken cancellationToken)
    {
        if (contentLength is > 536_870_912) return HeadlessModMutationResult.Failure("MOD archive exceeds the 512 MB limit.");
        if (!await mutationGate.WaitAsync(0, cancellationToken)) return HeadlessModMutationResult.Failure("A MOD mutation is already in progress.");
        var staging = Path.Combine(paths.ManagerRuntimeRoot, "mod-staging", Guid.NewGuid().ToString("N"));
        try
        {
            var blocked = await RejectWhenRunningAsync(cancellationToken); if (blocked is not null) return blocked;
            package = NormalizePackage(package);
            CaptureSnapshot(type, package);
            Directory.CreateDirectory(staging);
            var archivePath = Path.Combine(staging, "upload.zip");
            await using (var output = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await CopyBoundedAsync(content, output, 536_870_912, cancellationToken);
            var extracted = Path.Combine(staging, "extracted"); Directory.CreateDirectory(extracted);
            using (var zip = ZipFile.OpenRead(archivePath))
            {
                foreach (var entry in zip.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relative = entry.FullName.Replace('\\', '/');
                    if (relative.StartsWith('/') || relative.Split('/').Any(x => x == "..")) throw new InvalidDataException("Archive contains an unsafe path.");
                    var destination = Path.GetFullPath(Path.Combine(extracted, relative));
                    if (!destination.StartsWith(Path.GetFullPath(extracted) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive path escaped staging.");
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!); entry.ExtractToFile(destination, true);
                }
            }
            // v0.7.39.0: the archive's own contents, not the caller-supplied type, decide how it
            // installs. Presence of any .pak/.ucas/.utoc file anywhere in the archive is an
            // unambiguous PAK signal -- no UE4SS/Lua mod ever ships those. Everything else installs
            // as UE4SS (InstallUe4ssFiles' own "archive is empty" check still catches a genuinely
            // empty upload). This replaces trusting a manual dropdown default of PAK, which
            // silently installed a UE4SS mod as PAK (and vice versa) whenever a user forgot to
            // flip it.
            var detectedType = DetectModType(extracted);
            var changed = detectedType.Equals("PAK", StringComparison.OrdinalIgnoreCase)
                ? InstallPakFiles(extracted, package)
                : InstallUe4ssFiles(extracted, package);
            var typeNote = string.Equals(detectedType, type, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $"; requestedType={type.ToUpperInvariant()} (auto-corrected from archive contents)";
            activity.Record("Information", "MODs", "Installed MOD archive", $"type={detectedType}; package={package}; files={changed}{typeNote}");
            return new(true, detectedType, package, true, changed, $"Installed {package} from validated ZIP archive ({detectedType} detected from contents).");
        }
        catch (Exception ex) { return HeadlessModMutationResult.Failure(ex.Message); }
        finally { TryDeleteDirectory(staging); mutationGate.Release(); }
    }

    public async Task<HeadlessModMutationResult> DeleteAsync(string type, string package, CancellationToken cancellationToken)
    {
        if (!await mutationGate.WaitAsync(0, cancellationToken)) return HeadlessModMutationResult.Failure("A MOD mutation is already in progress.");
        try
        {
            var blocked = await RejectWhenRunningAsync(cancellationToken); if (blocked is not null) return blocked;
            package = NormalizePackage(package);
            CaptureSnapshot(type, package);
            var changed = 0;
            if (type.Equals("PAK", StringComparison.OrdinalIgnoreCase))
            {
                var root = Path.Combine(paths.ServerRoot, "Pal", "Content", "Paks", "~mods");
                foreach (var ext in PakExtensions) foreach (var suffix in new[] { ext, ext + ".disabled" }) { var file = Path.Combine(root, package + suffix); if (File.Exists(file)) { File.Delete(file); changed++; } }
                if (changed == 0)
                {
                    var modFolder = FindNestedPakModFolder(package);
                    if (modFolder is not null) { Directory.Delete(modFolder, true); changed++; }
                }
            }
            else if (type.Equals("UE4SS", StringComparison.OrdinalIgnoreCase))
            {
                var root = ResolveUe4ss().ActiveModsRoot; var folder = Path.Combine(root, package);
                if (Directory.Exists(folder)) { Directory.Delete(folder, true); changed++; }
                RemoveModLine(Path.Combine(root, "mods.txt"), package);
            }
            else return HeadlessModMutationResult.Failure("Only PAK and UE4SS MOD types are managed.");
            if (changed == 0) return HeadlessModMutationResult.Failure("Managed MOD was not found.");
            activity.Record("Information", "MODs", "Deleted MOD", $"type={type.ToUpperInvariant()}; package={package}; items={changed}");
            return new(true, type.ToUpperInvariant(), package, false, changed, $"Deleted {package}.");
        }
        catch (Exception ex) { return HeadlessModMutationResult.Failure(ex.Message); }
        finally { mutationGate.Release(); }
    }

    public async Task<HeadlessModMutationResult> SetAllEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        if (!await mutationGate.WaitAsync(0, cancellationToken)) return HeadlessModMutationResult.Failure("A MOD mutation is already in progress.");
        try
        {
            var blocked = await RejectWhenRunningAsync(cancellationToken); if (blocked is not null) return blocked;
            var inventory = await GetInventoryAsync(cancellationToken); var changed = 0;
            foreach (var item in inventory.Mods) changed += item.Type == "PAK" ? Math.Max(0, TogglePak(item.Package, enabled)) : Math.Max(0, ToggleUe4ss(item.Package, enabled));
            activity.Record("Information", "MODs", enabled ? "Enabled all MODs" : "Disabled all MODs", $"packages={inventory.Mods.Count}; items={changed}");
            return new(true, "ALL", "all", enabled, changed, $"{(enabled ? "Enabled" : "Disabled")} all managed MODs.");
        }
        catch (Exception ex) { return HeadlessModMutationResult.Failure(ex.Message); }
        finally { mutationGate.Release(); }
    }

    public async Task<HeadlessModMutationResult> RepairAsync(CancellationToken cancellationToken)
    {
        if (!await mutationGate.WaitAsync(0, cancellationToken)) return HeadlessModMutationResult.Failure("A MOD mutation is already in progress.");
        try
        {
            var blocked = await RejectWhenRunningAsync(cancellationToken); if (blocked is not null) return blocked;
            var ue4ss = ResolveUe4ss(); Directory.CreateDirectory(ue4ss.ActiveModsRoot); var changed = 0;
            foreach (var marker in Directory.EnumerateFiles(ue4ss.ActiveModsRoot, "enabled.txt", SearchOption.AllDirectories)) { var disabled = marker + ".mysttiq-disabled"; if (!File.Exists(disabled)) File.Move(marker, disabled); else File.Delete(marker); changed++; }
            var modsTxt = Path.Combine(ue4ss.ActiveModsRoot, "mods.txt"); if (!File.Exists(modsTxt)) { AtomicWrite(modsTxt, string.Empty); changed++; }
            activity.Record("Information", "MODs", "Repaired MOD state", $"changes={changed}; root={ue4ss.ActiveModsRoot}");
            return new(true, "UE4SS", "state", true, changed, "Repaired authoritative mods.txt state and neutralized enabled.txt overrides.");
        }
        catch (Exception ex) { return HeadlessModMutationResult.Failure(ex.Message); }
        finally { mutationGate.Release(); }
    }

    // Mirrors the legacy MystTiq startup narrative: resolve UE4SS layout, reconcile mods.txt
    // against what is actually on disk, then list every enabled mod's initial load state, all
    // written into the same console log PalServer's own redirected output lands in.
    public async Task LogPreStartDiagnosticsAsync(CancellationToken cancellationToken)
    {
        if (consoleLog is null) return;
        var ue4ss = ResolveUe4ss();
        consoleLog.Info("UE4SS", "UE4SS", $"Win64 Root: {ue4ss.RuntimeBinaryRoot}");
        consoleLog.Info("UE4SS", "UE4SS", $"UE4SS Root: {ue4ss.Ue4ssRoot}");
        consoleLog.Info("UE4SS", "UE4SS", $"Modern Mods Root: {ue4ss.ModernModsRoot} (exists: {ue4ss.HasModernModsRoot})");
        consoleLog.Info("UE4SS", "UE4SS", $"Legacy Mods Root: {ue4ss.LegacyModsRoot} (exists: {ue4ss.HasLegacyModsRoot})");
        consoleLog.Info("UE4SS", "UE4SS", $"Active Mods Root: {ue4ss.ActiveModsRoot}");
        consoleLog.Info("UE4SS", "UE4SS", $"Detection Method: {ue4ss.DetectionMethod}");
        consoleLog.Info("UE4SS", "UE4SS", $"Runtime Mods Root: {ue4ss.RuntimeModsRoot ?? "Not reported"}");
        consoleLog.Info("UE4SS", "UE4SS", $"Runtime Verified: {ue4ss.RuntimeVerified}");
        consoleLog.Info("UE4SS", "UE4SS", $"Active Mod Directories: {ue4ss.ActiveModDirectoryCount}");
        consoleLog.Info("UE4SS", "UE4SS", $"Legacy Mod Directories: {ue4ss.LegacyModDirectoryCount}");
        var startupEvidence = ReadModRuntimeEvidence(ue4ss.RuntimeLogPath);
        var startupLuaCount = startupEvidence.Started.Count(kv => kv.Value.Equals("Lua", StringComparison.OrdinalIgnoreCase));
        consoleLog.Info("UE4SS", "UE4SS", $"Lua Mods Loaded: {startupLuaCount}");
        var healthDetail = !string.IsNullOrWhiteSpace(ue4ss.WarningMessage)
            ? ue4ss.WarningMessage
            : ue4ss.HasModernModsRoot && ue4ss.HasLegacyModsRoot
                ? "Both modern and legacy Mods roots exist. Modern root is active; legacy content must be treated as migration information."
                : "UE4SS layout resolved without conflicts.";
        consoleLog.Info("UE4SS", "UE4SS", $"Runtime Health: {ue4ss.HealthState} — {healthDetail}");

        var (neutralized, added, scanned) = await ReconcileModsPreStartAsync(ue4ss, cancellationToken);
        consoleLog.Info("MODS", "MOD LIFECYCLE", $"Pre-start reconciliation complete: {neutralized} enabled.txt override(s) neutralized; {added} mods.txt entries added; {scanned} MOD(s) scanned.");
        consoleLog.Status("MODS", "MOD LIFECYCLE", "Startup health gate: Ready. Normal modded startup may continue.");

        consoleLog.Info("MODS", "MOD LOAD", "===== STARTUP MOD LOAD TRACKING =====");
        consoleLog.Info("UE4SS", "MOD LOAD", "UE4SS Runtime: ENABLED");
        var modsTxt = Path.Combine(ue4ss.ActiveModsRoot, "mods.txt");
        if (File.Exists(modsTxt))
        {
            foreach (var line in File.ReadAllLines(modsTxt))
            {
                var parts = line.Split(':', 2);
                if (parts.Length != 2) continue;
                var name = parts[0].Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;
                var enabled = parts[1].Trim() != "0";
                consoleLog.Info("MODS", "MOD LOAD", enabled ? $"{name}: ENABLED - LOAD NOT CONFIRMED" : $"{name}: DISABLED");
            }
        }
    }

    private async Task<(int Neutralized, int Added, int Scanned)> ReconcileModsPreStartAsync(HeadlessUe4ssStatus ue4ss, CancellationToken cancellationToken)
    {
        if (!await mutationGate.WaitAsync(0, cancellationToken)) return (0, 0, 0);
        try
        {
            Directory.CreateDirectory(ue4ss.ActiveModsRoot);
            var neutralized = 0;
            foreach (var marker in Directory.EnumerateFiles(ue4ss.ActiveModsRoot, "enabled.txt", SearchOption.AllDirectories))
            {
                var disabled = marker + ".mysttiq-disabled";
                if (!File.Exists(disabled)) File.Move(marker, disabled); else File.Delete(marker);
                neutralized++;
            }

            var modsTxt = Path.Combine(ue4ss.ActiveModsRoot, "mods.txt");
            if (!File.Exists(modsTxt)) AtomicWrite(modsTxt, string.Empty);
            var lines = File.ReadAllLines(modsTxt).ToList();
            var known = new HashSet<string>(lines.Select(l => l.Split(':')[0].Trim()).Where(n => n.Length > 0), StringComparer.OrdinalIgnoreCase);
            var added = 0;
            foreach (var directory in Directory.EnumerateDirectories(ue4ss.ActiveModsRoot))
            {
                var name = Path.GetFileName(directory);
                if (KnownRuntimeComponents.Contains(name) || name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) continue;
                if (known.Contains(name)) continue;
                lines.Add($"{name} : 1");
                known.Add(name);
                added++;
            }
            if (added > 0) AtomicWrite(modsTxt, string.Join(Environment.NewLine, lines));

            var scanned = lines.Count(l => l.Contains(':') && !string.IsNullOrWhiteSpace(l.Split(':')[0]));
            if (neutralized > 0 || added > 0)
                activity.Record("Information", "MODs", "Pre-start MOD reconciliation", $"neutralized={neutralized}; added={added}; scanned={scanned}");
            return (neutralized, added, scanned);
        }
        finally { mutationGate.Release(); }
    }

    private async Task<HeadlessModMutationResult?> RejectWhenRunningAsync(CancellationToken token)
    {
        var status = await lifecycle.GetStatusAsync(token);
        return status.NativeProcessId.HasValue || status.Ready ? HeadlessModMutationResult.Failure("Stop PalServer before changing MOD files.") : null;
    }

    // v0.6.8.0 "Backup, staged install, runtime verification and rollback": the roadmap's backup/
    // rollback half was genuinely missing -- InstallZipAsync/DeleteAsync had a staging area for the
    // NEW content but took no snapshot of what a package looked like BEFORE the mutation, so a bad
    // install or an accidental delete had no built-in undo. One snapshot per (type, package) is kept
    // (overwritten on the next mutation of that same package) -- "undo my last change to this MOD",
    // not a full history, matching the actual failure mode this protects against.
    private string SnapshotPath(string type, string package) =>
        Path.Combine(paths.ManagerRuntimeRoot, "mod-snapshots", $"{type.ToUpperInvariant()}_{package}.zip");

    private void CaptureSnapshot(string type, string package)
    {
        try
        {
            var snapshotPath = SnapshotPath(type, package);
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            var metaPath = snapshotPath + ".meta";
            var absentMarker = snapshotPath + ".absent";
            foreach (var stale in new[] { snapshotPath, metaPath, absentMarker }) if (File.Exists(stale)) File.Delete(stale);

            if (type.Equals("PAK", StringComparison.OrdinalIgnoreCase))
            {
                var flatRoot = Path.Combine(paths.ServerRoot, "Pal", "Content", "Paks", "~mods");
                var flatFiles = Directory.Exists(flatRoot)
                    ? PakExtensions.SelectMany(ext => new[] { ext, ext + ".disabled" })
                        .Select(suffix => Path.Combine(flatRoot, package + suffix))
                        .Where(File.Exists).ToList()
                    : [];
                if (flatFiles.Count > 0)
                {
                    using (var zip = ZipFile.Open(snapshotPath, ZipArchiveMode.Create))
                        foreach (var file in flatFiles) zip.CreateEntryFromFile(file, Path.GetFileName(file));
                    File.WriteAllText(metaPath, $"flat|{flatRoot}");
                    return;
                }

                var nested = FindNestedPakModFolder(package);
                if (nested is not null && Directory.Exists(nested))
                {
                    ZipFile.CreateFromDirectory(nested, snapshotPath, CompressionLevel.Optimal, false);
                    File.WriteAllText(metaPath, $"folder|{nested}");
                    return;
                }
            }
            else if (type.Equals("UE4SS", StringComparison.OrdinalIgnoreCase))
            {
                var folder = Path.Combine(ResolveUe4ss().ActiveModsRoot, package);
                if (Directory.Exists(folder))
                {
                    ZipFile.CreateFromDirectory(folder, snapshotPath, CompressionLevel.Optimal, false);
                    File.WriteAllText(metaPath, $"folder|{folder}");
                    return;
                }
            }

            // Nothing existed for this package before the mutation -- record that explicitly so
            // RollbackAsync removes whatever the mutation just created rather than silently no-op'ing.
            File.WriteAllText(absentMarker, DateTimeOffset.UtcNow.ToString("O"));
        }
        catch
        {
            // A snapshot failure must not block the underlying mutation, which the operator already
            // explicitly confirmed -- losing the safety-net snapshot is a degraded, not blocking, state.
        }
    }

    public async Task<HeadlessModMutationResult> RollbackAsync(string type, string package, CancellationToken cancellationToken)
    {
        if (!await mutationGate.WaitAsync(0, cancellationToken))
            return HeadlessModMutationResult.Failure("A MOD mutation is already in progress.");
        try
        {
            var blocked = await RejectWhenRunningAsync(cancellationToken); if (blocked is not null) return blocked;
            package = NormalizePackage(package);
            var snapshotPath = SnapshotPath(type, package);
            var metaPath = snapshotPath + ".meta";
            var absentMarker = snapshotPath + ".absent";

            if (!File.Exists(snapshotPath) && !File.Exists(absentMarker))
                return HeadlessModMutationResult.Failure(
                    $"No rollback snapshot is available for {type.ToUpperInvariant()} {package}. A snapshot is captured automatically the next time this MOD is installed or deleted.");

            if (type.Equals("PAK", StringComparison.OrdinalIgnoreCase))
            {
                var flatRoot = Path.Combine(paths.ServerRoot, "Pal", "Content", "Paks", "~mods");
                foreach (var suffix in PakExtensions.SelectMany(ext => new[] { ext, ext + ".disabled" }))
                {
                    var file = Path.Combine(flatRoot, package + suffix);
                    if (File.Exists(file)) File.Delete(file);
                }
                var nested = FindNestedPakModFolder(package);
                if (nested is not null && Directory.Exists(nested)) Directory.Delete(nested, true);
            }
            else if (type.Equals("UE4SS", StringComparison.OrdinalIgnoreCase))
            {
                var folder = Path.Combine(ResolveUe4ss().ActiveModsRoot, package);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            else return HeadlessModMutationResult.Failure("Only PAK and UE4SS MOD types are managed.");

            var restored = false;
            if (File.Exists(snapshotPath) && File.Exists(metaPath))
            {
                var meta = File.ReadAllText(metaPath).Split('|', 2);
                if (meta[0] == "flat")
                {
                    var flatRoot = Path.Combine(paths.ServerRoot, "Pal", "Content", "Paks", "~mods");
                    Directory.CreateDirectory(flatRoot);
                    using var zip = ZipFile.OpenRead(snapshotPath);
                    // v0.7.7.0: this was the one archive-extraction call site in this file that
                    // skipped the path-traversal guard InstallZipAsync (above) established for
                    // user-uploaded archives. Snapshot ZIPs are normally self-generated by
                    // CaptureSnapshot, but a rollback should not trust a tampered/corrupted
                    // snapshot file on disk any more than a fresh upload is trusted.
                    foreach (var entry in zip.Entries)
                    {
                        var relative = entry.FullName.Replace('\\', '/');
                        if (relative.StartsWith('/') || relative.Split('/').Any(x => x == "..")) throw new InvalidDataException("Snapshot archive contains an unsafe path.");
                        var destination = Path.GetFullPath(Path.Combine(flatRoot, relative));
                        if (!destination.StartsWith(Path.GetFullPath(flatRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Snapshot archive path escaped destination.");
                        if (string.IsNullOrEmpty(entry.Name)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        entry.ExtractToFile(destination, true);
                    }
                }
                else if (meta[0] == "folder" && meta.Length > 1)
                {
                    Directory.CreateDirectory(meta[1]);
                    ZipFile.ExtractToDirectory(snapshotPath, meta[1], true);
                }
                restored = true;
            }

            activity.Record("Information", "MODs", "Rolled back MOD", $"type={type.ToUpperInvariant()}; package={package}; restored={restored}");
            return new HeadlessModMutationResult(true, type.ToUpperInvariant(), package, restored, 1,
                restored ? $"{package} was restored to its state before the last install/delete."
                         : $"{package} was removed -- it did not exist before the last install/delete.");
        }
        catch (Exception ex) { return HeadlessModMutationResult.Failure(ex.Message); }
        finally { mutationGate.Release(); }
    }

    // v0.7.49.0 "UE4SS Install/Rollback": the v0.7.47.0 release catalog was deliberately
    // listing-only. This is the real install path. Verified live against real release zips from
    // both catalog sources before writing this (not guessed): Okaetsu/RE-UE4SS packages the modern
    // ue4ss/ subfolder layout (dwmapi.dll at the zip root, everything else -- UE4SS.dll,
    // UE4SS-settings.ini, UE4SS_SDK_Backends/, Mods/ -- under ue4ss/); UE4SS-RE/RE-UE4SS packages
    // the legacy flat layout (dwmapi.dll, UE4SS.dll, UE4SS-settings.ini and Mods/ all directly at
    // the zip root). The two sources are not interchangeable file layouts, they're genuinely
    // different packaging conventions -- rather than guess a flatten/nest transform between them
    // (unverifiable without live testing against a real server), install faithfully mirrors
    // whatever the selected zip's own root structure is onto RuntimeBinaryRoot, exactly what each
    // project's own install instructions already tell a user to do by hand. If the zip's layout
    // doesn't match an existing install's layout, install is refused rather than silently leaving
    // both in place (risks UE4SS loading twice, per the fork's own release notes). Any zip entry
    // under a "Mods" folder at any nesting level is skipped outright, so mods.txt/mods.json and
    // every installed MOD folder are never touched by an engine install. UE4SS-settings.ini is
    // preserved if it already exists on disk (skip, don't overwrite) since it's commonly
    // hand-edited -- matching UE4SS's own v3.0.1 release notes' framing of "replacing files while
    // preserving custom configuration settings".
    public async Task<Ue4ssInstallPreview?> PreviewUe4ssInstallAsync(string source, string tagName, CancellationToken cancellationToken)
    {
        var catalog = await ue4ssReleaseCatalog.GetCatalogAsync(cancellationToken);
        var releases = source.Equals("PalworldFork", StringComparison.OrdinalIgnoreCase)
            ? catalog.PalworldForkReleases : catalog.OfficialUpstreamReleases;
        var release = releases.FirstOrDefault(r => r.TagName == tagName);
        if (release?.RecommendedAssetDownloadUrl is null) return null;

        var modernInstalled = Directory.Exists(paths.Ue4ssRoot);
        var legacyInstalled = !modernInstalled && Ue4ssKnownLayoutMarkers
            .Select(name => Path.Combine(paths.RuntimeBinaryRoot, name)).Any(File.Exists);
        var currentLayout = modernInstalled ? "Modern (ue4ss/ subfolder)" : legacyInstalled ? "Legacy (flat root)" : "None currently installed";

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var preview = new Ue4ssInstallPreview(
            token, release.Source, release.TagName, release.RecommendedAssetName, release.RecommendedAssetSizeBytes,
            release.RecommendedAssetDownloadUrl, currentLayout, DetectUe4ssVersion(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15),
            $"Will install {release.RecommendedAssetName} from {release.TagName}. MOD folders and mods.txt are never touched. " +
            "A safety snapshot of the current engine files is taken automatically before anything is overwritten and can be undone with Rollback.");
        lock (ue4ssInstallPreviews) ue4ssInstallPreviews[token] = preview;
        activity.Record("Information", "UE4SS", "Previewed UE4SS install", $"token={token}; source={release.Source}; tag={release.TagName}");
        return preview;
    }

    public async Task<Ue4ssInstallResult> ApplyUe4ssInstallAsync(string token, CancellationToken cancellationToken)
    {
        if (!await mutationGate.WaitAsync(0, cancellationToken))
            return Ue4ssInstallResult.Failure("A MOD/engine mutation is already in progress.");
        var staging = Path.Combine(paths.ManagerRuntimeRoot, "ue4ss-install-staging", Guid.NewGuid().ToString("N"));
        try
        {
            Ue4ssInstallPreview? preview;
            lock (ue4ssInstallPreviews) ue4ssInstallPreviews.Remove(token ?? string.Empty, out preview);
            if (preview is null || preview.ExpiresAt < DateTimeOffset.UtcNow)
                return Ue4ssInstallResult.Failure("Install preview is missing or expired. Preview again.");

            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.NativeProcessId.HasValue || status.Ready)
                return Ue4ssInstallResult.Failure("Stop PalServer before installing UE4SS -- its engine files are loaded into the running process.");

            Directory.CreateDirectory(staging);
            var archivePath = Path.Combine(staging, "ue4ss-release.zip");
            using (var downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                downloadCts.CancelAfter(TimeSpan.FromMinutes(3));
                await using (var output = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using var response = await Ue4ssDownloadHttp.GetAsync(preview.AssetDownloadUrl, HttpCompletionOption.ResponseHeadersRead, downloadCts.Token);
                    if (!response.IsSuccessStatusCode)
                        return Ue4ssInstallResult.Failure($"Download failed: HTTP {(int)response.StatusCode}.");
                    await using var input = await response.Content.ReadAsStreamAsync(downloadCts.Token);
                    await CopyBoundedDownloadAsync(input, output, 209_715_200, downloadCts.Token);
                }
            }

            using var zip = ZipFile.OpenRead(archivePath);
            var zipIsModern = zip.Entries.Any(e => IsUnderTopLevel(e.FullName, "ue4ss"));
            var modernInstalled = Directory.Exists(paths.Ue4ssRoot);
            var legacyInstalled = !modernInstalled && Ue4ssKnownLayoutMarkers
                .Select(name => Path.Combine(paths.RuntimeBinaryRoot, name)).Any(File.Exists);
            if (zipIsModern && legacyInstalled)
                return Ue4ssInstallResult.Failure(
                    "This release uses the modern ue4ss/ subfolder layout, but a legacy (flat-root) UE4SS install already exists at this server. " +
                    "Installing it would leave both in place and risk UE4SS loading twice. Remove the existing UE4SS.dll/UE4SS-settings.ini/dwmapi.dll " +
                    "from the server's binaries folder first, or pick a release from the same layout family.");
            if (!zipIsModern && modernInstalled)
                return Ue4ssInstallResult.Failure(
                    "This release uses the legacy flat-root layout, but a modern ue4ss/-subfolder UE4SS install already exists at this server. " +
                    "Installing it would leave both in place and risk UE4SS loading twice. Remove the existing ue4ss/ folder first, or pick a release " +
                    "from the same layout family.");

            var plannedWrites = new List<(string Relative, ZipArchiveEntry Entry)>();
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry
                var relative = entry.FullName.Replace('\\', '/');
                if (relative.StartsWith('/') || relative.Split('/').Any(x => x == "..")) throw new InvalidDataException("Release archive contains an unsafe path.");
                if (relative.Split('/').Any(segment => segment.Equals("Mods", StringComparison.OrdinalIgnoreCase))) continue;
                plannedWrites.Add((relative, entry));
            }
            if (plannedWrites.Count == 0) return Ue4ssInstallResult.Failure("Release archive contained no installable engine files.");

            CaptureUe4ssEngineSnapshot(plannedWrites.Select(w => w.Relative).ToArray());

            var installed = 0;
            foreach (var (relative, entry) in plannedWrites)
            {
                var destination = Path.GetFullPath(Path.Combine(paths.RuntimeBinaryRoot, relative));
                if (!destination.StartsWith(Path.GetFullPath(paths.RuntimeBinaryRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Release archive path escaped the server binaries folder.");
                if (Path.GetFileName(relative).Equals("UE4SS-settings.ini", StringComparison.OrdinalIgnoreCase) && File.Exists(destination))
                    continue; // preserve an existing, possibly user-edited settings file
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, true);
                installed++;
            }

            // v0.7.60.0 bug fix: DetectUe4ssVersion() below has no reliable source of the REAL
            // installed version -- this fork ships no version marker file, and the DLL's own
            // FileVersionInfo comes back as an unstamped "0.0.0.0" (already filtered out by
            // v0.7.30.0's own fix). Without this manifest, every install looked identical to every
            // other to the app's own version-checking logic, which is exactly how Update Center
            // could report a component as current when it was not -- there was nothing to compare
            // against. Recording exactly which catalog release was actually applied here is the one
            // place MystTiq genuinely knows the true version, since it just downloaded and installed
            // it itself.
            WriteUe4ssInstallManifest(preview.Source, preview.TagName);
            var installedVersion = DetectUe4ssVersion();
            activity.Record("Information", "UE4SS", "Installed UE4SS engine", $"source={preview.Source}; tag={preview.TagName}; files={installed}");
            return new Ue4ssInstallResult(true, installedVersion, paths.RuntimeBinaryRoot,
                $"Installed {preview.TagName} ({installed} engine file(s)). MOD folders and mods.txt were not touched. A pre-install snapshot was saved -- use Rollback to undo this.");
        }
        catch (Exception ex)
        {
            RestoreUe4ssEngineSnapshot();
            return Ue4ssInstallResult.Failure($"Install failed and any partially-written files were rolled back: {ex.Message}");
        }
        finally
        {
            mutationGate.Release();
            TryDeleteDirectory(staging);
        }
    }

    public async Task<Ue4ssInstallResult> RollbackUe4ssInstallAsync(CancellationToken cancellationToken)
    {
        if (!await mutationGate.WaitAsync(0, cancellationToken))
            return Ue4ssInstallResult.Failure("A MOD/engine mutation is already in progress.");
        try
        {
            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.NativeProcessId.HasValue || status.Ready)
                return Ue4ssInstallResult.Failure("Stop PalServer before rolling back UE4SS -- its engine files are loaded into the running process.");

            if (!File.Exists(Ue4ssEngineSnapshotMetaPath))
                return Ue4ssInstallResult.Failure("No UE4SS install snapshot is available to roll back to. A snapshot is captured automatically the next time an install runs.");

            RestoreUe4ssEngineSnapshot();
            // The manifest recorded exactly one install's own release tag -- after a rollback the
            // engine files no longer match what it describes (the snapshot could be from any
            // earlier state, not necessarily a tracked release), so it must not keep being reported
            // as the truth. Falls back to DetectUe4ssVersion()'s existing honest "unavailable" state.
            DeleteUe4ssInstallManifest();
            var version = DetectUe4ssVersion();
            activity.Record("Information", "UE4SS", "Rolled back UE4SS engine install", string.Empty);
            return new Ue4ssInstallResult(true, version, paths.RuntimeBinaryRoot, "UE4SS engine files were restored to their state before the last install.");
        }
        catch (Exception ex) { return Ue4ssInstallResult.Failure(ex.Message); }
        finally { mutationGate.Release(); }
    }

    public bool Ue4ssRollbackAvailable => File.Exists(Ue4ssEngineSnapshotMetaPath);

    private string Ue4ssEngineSnapshotPath => Path.Combine(paths.ManagerRuntimeRoot, "ue4ss-snapshots", "UE4SS-ENGINE_current.zip");
    private string Ue4ssEngineSnapshotMetaPath => Ue4ssEngineSnapshotPath + ".meta.json";

    private void CaptureUe4ssEngineSnapshot(IReadOnlyList<string> plannedRelativePaths)
    {
        try
        {
            var snapshotPath = Ue4ssEngineSnapshotPath;
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            if (File.Exists(snapshotPath)) File.Delete(snapshotPath);

            var existed = new List<string>();
            using (var zip = ZipFile.Open(snapshotPath, ZipArchiveMode.Create))
            {
                foreach (var relative in plannedRelativePaths)
                {
                    var current = Path.Combine(paths.RuntimeBinaryRoot, relative);
                    if (!File.Exists(current)) continue;
                    zip.CreateEntryFromFile(current, relative);
                    existed.Add(relative);
                }
            }
            var meta = new Ue4ssSnapshotMeta(plannedRelativePaths.ToArray(), existed.ToArray(), DateTimeOffset.UtcNow);
            File.WriteAllText(Ue4ssEngineSnapshotMetaPath, JsonSerializer.Serialize(meta));
        }
        catch
        {
            // A snapshot failure must not block an install the operator already confirmed via
            // Preview -- losing the safety net is a degraded, not blocking, state (same policy as
            // the per-MOD CaptureSnapshot above).
        }
    }

    private void RestoreUe4ssEngineSnapshot()
    {
        if (!File.Exists(Ue4ssEngineSnapshotMetaPath)) return;
        var meta = JsonSerializer.Deserialize<Ue4ssSnapshotMeta>(File.ReadAllText(Ue4ssEngineSnapshotMetaPath));
        if (meta is null) return;

        foreach (var relative in meta.PlannedPaths)
        {
            var destination = Path.GetFullPath(Path.Combine(paths.RuntimeBinaryRoot, relative));
            if (!destination.StartsWith(Path.GetFullPath(paths.RuntimeBinaryRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            if (!meta.ExistedPaths.Contains(relative, StringComparer.OrdinalIgnoreCase) && File.Exists(destination))
                File.Delete(destination);
        }

        if (File.Exists(Ue4ssEngineSnapshotPath))
        {
            using var zip = ZipFile.OpenRead(Ue4ssEngineSnapshotPath);
            foreach (var entry in zip.Entries)
            {
                var relative = entry.FullName.Replace('\\', '/');
                if (relative.StartsWith('/') || relative.Split('/').Any(x => x == "..")) throw new InvalidDataException("Snapshot archive contains an unsafe path.");
                var destination = Path.GetFullPath(Path.Combine(paths.RuntimeBinaryRoot, relative));
                if (!destination.StartsWith(Path.GetFullPath(paths.RuntimeBinaryRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Snapshot archive path escaped destination.");
                if (string.IsNullOrEmpty(entry.Name)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, true);
            }
        }
    }

    private static bool IsUnderTopLevel(string entryFullName, string topFolder)
    {
        var relative = entryFullName.Replace('\\', '/').TrimStart('/');
        return relative.StartsWith(topFolder + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CopyBoundedDownloadAsync(Stream input, Stream output, long maximum, CancellationToken token)
    {
        var buffer = new byte[81920]; long total = 0; int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            total += read;
            if (total > maximum) throw new InvalidDataException("UE4SS release archive exceeds the 200 MB download limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
        }
    }

    private static HttpClient BuildUe4ssDownloadHttpClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MystTiq-Palworld-Server-Manager");
        return client;
    }

    // v0.7.39.0: real-content detection backing InstallZipAsync's auto-detect -- any .pak/.ucas/
    // .utoc file anywhere in the extracted archive is PAK; otherwise it's treated as UE4SS.
    private static string DetectModType(string extracted) =>
        Directory.EnumerateFiles(extracted, "*", SearchOption.AllDirectories)
            .Any(x => PakExtensions.Contains(Path.GetExtension(x), StringComparer.OrdinalIgnoreCase))
            ? "PAK" : "UE4SS";

    private int InstallPakFiles(string extracted, string package)
    {
        var files = Directory.EnumerateFiles(extracted, "*", SearchOption.AllDirectories).Where(x => PakExtensions.Contains(Path.GetExtension(x), StringComparer.OrdinalIgnoreCase)).ToArray();
        if (files.Length == 0) throw new InvalidDataException("PAK archive contains no .pak/.ucas/.utoc files.");
        var root = Path.Combine(paths.ServerRoot, "Pal", "Content", "Paks", "~mods"); Directory.CreateDirectory(root);
        foreach (var file in files) File.Copy(file, Path.Combine(root, package + Path.GetExtension(file).ToLowerInvariant()), true);
        return files.Length;
    }

    private int InstallUe4ssFiles(string extracted, string package)
    {
        var root = ResolveUe4ss().ActiveModsRoot; var destination = Path.Combine(root, package);
        if (Directory.Exists(destination)) throw new IOException("A UE4SS MOD with this package name already exists.");
        Directory.CreateDirectory(destination); var files = Directory.EnumerateFiles(extracted, "*", SearchOption.AllDirectories).ToArray();
        if (files.Length == 0) throw new InvalidDataException("UE4SS archive is empty.");
        foreach (var file in files) { var rel=Path.GetRelativePath(extracted,file); var target=Path.Combine(destination,rel); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file,target); }
        ToggleUe4ss(package, true); return files.Length;
    }

    private static string NormalizePackage(string package)
    {
        package = Path.GetFileName(package ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(package) || package.Contains("..") || package.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new InvalidDataException("Invalid MOD package name.");
        return package;
    }

    private static async Task CopyBoundedAsync(Stream input, Stream output, long maximum, CancellationToken token)
    {
        var buffer = new byte[81920]; long total = 0; int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0) { total += read; if (total > maximum) throw new InvalidDataException("MOD archive exceeds the 512 MB limit."); await output.WriteAsync(buffer.AsMemory(0, read), token); }
    }

    private static void RemoveModLine(string modsTxt, string package)
    {
        if (!File.Exists(modsTxt)) return; var prefix = package + " :";
        AtomicWrite(modsTxt, string.Join(Environment.NewLine, File.ReadAllLines(modsTxt).Where(x => !x.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))));
    }

    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }

    public async Task<HeadlessModInventory> GetInventoryAsync(CancellationToken cancellationToken)
    {
        var status = await lifecycle.GetStatusAsync(cancellationToken);
        var serverRunning = status.NativeProcessId.HasValue || status.Ready;
        var ue4ss = ResolveUe4ss();
        var enabledUe4ss = ReadEnabledPackages(ue4ss.ActiveModsRoot);
        var runtimeEvidence = ReadModRuntimeEvidence(ue4ss.RuntimeLogPath);

        var mods = new List<HeadlessModItem>();
        mods.AddRange(ScanPakMods(serverRunning));
        mods.AddRange(ScanUe4ssMods(ue4ss, enabledUe4ss, runtimeEvidence, serverRunning));

        // v0.7.78.0: per-mod update availability and installed version, surfaced directly on the
        // Installed MODs list instead of requiring a manual "Check for Update" click per selection.
        // Update-availability reuses CheckModUpdateAsync's own existing, cheap, local, no-network
        // comparison unchanged. Version reads the installed mod's own Info.json directly out of its
        // install folder (ReadWorkshopManifestVersion, already used elsewhere for the UE4SS-runtime
        // hash-match case) -- this is the actually-installed copy's own declared version, not the
        // local Workshop cache's version, so it stays honest even if the installed copy predates an
        // available update. Non-Workshop MODs (no Info.json ever copied in) simply report no
        // version, rather than a misleading guess.
        for (var i = 0; i < mods.Count; i++)
        {
            var check = await CheckModUpdateAsync(mods[i].Type, mods[i].Package, cancellationToken);
            var installedVersion = ReadWorkshopManifestVersion(mods[i].InstallPath);
            if ((check.HasKnownSource && check.UpdateAvailable) || installedVersion is not null)
                mods[i] = mods[i] with
                {
                    UpdateAvailable = check.HasKnownSource && check.UpdateAvailable,
                    UpdateHint = check.HasKnownSource && check.UpdateAvailable ? "Update available locally" : "",
                    InstalledVersion = installedVersion,
                };
        }

        var issues = mods.Count(m => m.Health is "Failed" or "Missing" or "Misconfigured" or "Attention");
        var disabled = mods.Count(m => m.Health == "Disabled");
        var unverified = mods.Count(m => m.Health == "Active / Unverified");
        var confirmed = mods.Count(m => m.RuntimeState is "Confirmed Loaded" or "Confirmed Active");

        // Operational-health parity: Disabled and Active / Unverified are neutral.
        var overall = issues > 0 ? "Degraded" : "Healthy";
        var summary = mods.Count == 0
            ? "Vanilla server profile. No managed MODs detected."
            : issues > 0
                ? $"{issues} enabled MOD issue(s) require attention."
                : $"{confirmed} confirmed loaded · {unverified} active/unverified · {disabled} disabled.";

        return new HeadlessModInventory(
            paths.PlatformId, serverRunning, mods.Count, confirmed, unverified, disabled, issues,
            overall, summary, ue4ss,
            mods.OrderBy(m => m.Type).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            DateTimeOffset.UtcNow);
    }

    public async Task<HeadlessModVerificationResult> VerifyAsync(CancellationToken cancellationToken)
    {
        var inventory = await GetInventoryAsync(cancellationToken);
        return new HeadlessModVerificationResult(
            inventory.Installed,
            inventory.Mods.Count(m => m.Health is "Healthy" or "Disabled" or "Active / Unverified"),
            inventory.RuntimeConfirmed,
            inventory.ActiveUnverified,
            inventory.Disabled,
            inventory.ConfirmedIssues,
            inventory.OverallHealth,
            inventory.Summary,
            DateTimeOffset.UtcNow,
            inventory.Mods);
    }

    public async Task<HeadlessModMutationResult> SetEnabledAsync(
        string type, string package, bool enabled, CancellationToken cancellationToken)
    {
        if (!await mutationGate.WaitAsync(0, cancellationToken))
            return HeadlessModMutationResult.Failure("A MOD state change is already in progress.");

        try
        {
            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.NativeProcessId.HasValue || status.Ready)
                return HeadlessModMutationResult.Failure("Stop PalServer before enabling or disabling MOD files.");

            package = Path.GetFileName(package ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(package) || package.Contains("..", StringComparison.Ordinal))
                return HeadlessModMutationResult.Failure("The MOD package name is invalid.");

            var changed = type.Equals("PAK", StringComparison.OrdinalIgnoreCase)
                ? TogglePak(package, enabled)
                : type.Equals("UE4SS", StringComparison.OrdinalIgnoreCase)
                    ? ToggleUe4ss(package, enabled)
                    : -1;

            if (changed < 0)
                return HeadlessModMutationResult.Failure("Only PAK and UE4SS MOD types are managed.");
            if (changed == 0)
                return HeadlessModMutationResult.Failure($"No managed {type} MOD named '{package}' was found.");

            activity.Record("Information", "MODs", enabled ? "Enabled MOD" : "Disabled MOD",
                $"type={type.ToUpperInvariant()}; package={package}; items={changed}");
            return new HeadlessModMutationResult(
                true, type.ToUpperInvariant(), package, enabled, changed,
                $"{package} is now {(enabled ? "enabled" : "disabled")}.");
        }
        catch (Exception ex)
        {
            return HeadlessModMutationResult.Failure(ex.Message);
        }
        finally
        {
            mutationGate.Release();
        }
    }

    private static readonly string[] WorkshopPakRootNames = ["~WorkshopMods", "LogicMods"];

    private IEnumerable<HeadlessModItem> ScanPakMods(bool serverRunning)
    {
        var flatRoot = Path.Combine(paths.ServerRoot, "Pal", "Content", "Paks", "~mods");
        if (Directory.Exists(flatRoot))
            foreach (var item in ScanFlatPakFiles(flatRoot, serverRunning)) yield return item;

        // Steam Workshop PAK/LogicMods items install one subfolder per mod (e.g. ~WorkshopMods\QualityOfLife\*.pak)
        // rather than loose files directly in ~mods, and were previously invisible to inventory scanning.
        foreach (var rootName in WorkshopPakRootNames)
        {
            var nestedRoot = Path.Combine(paths.ServerRoot, "Pal", "Content", "Paks", rootName);
            if (!Directory.Exists(nestedRoot)) continue;
            foreach (var modFolder in Directory.EnumerateDirectories(nestedRoot))
                foreach (var item in ScanNestedPakFolder(modFolder, rootName, serverRunning)) yield return item;
        }
    }

    private IEnumerable<HeadlessModItem> ScanFlatPakFiles(string root, bool serverRunning)
    {
        var entries = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                var activeName = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                    ? path[..^".disabled".Length] : path;
                return PakExtensions.Any(ext => activeName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
            })
            .Select(path =>
            {
                var enabled = !path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                var activeName = enabled ? path : path[..^".disabled".Length];
                return new { Package = Path.GetFileNameWithoutExtension(activeName), Path = path, Enabled = enabled };
            })
            .GroupBy(x => x.Package, StringComparer.OrdinalIgnoreCase);

        foreach (var group in entries)
            yield return BuildPakModItem(group.Key, group.Select(i => i.Path).ToArray(), root, serverRunning, "PAK deployment");
    }

    private IEnumerable<HeadlessModItem> ScanNestedPakFolder(string modFolder, string sourceRootName, bool serverRunning)
    {
        var package = Path.GetFileName(modFolder);
        var files = Directory.EnumerateFiles(modFolder, "*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var activeName = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                    ? path[..^".disabled".Length] : path;
                return PakExtensions.Any(ext => activeName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
            })
            .ToArray();
        if (files.Length == 0) yield break;

        yield return BuildPakModItem(package, files, modFolder, serverRunning, sourceRootName);
    }

    private static HeadlessModItem BuildPakModItem(string package, IReadOnlyList<string> filePaths, string installPath, bool serverRunning, string sourceLabel)
    {
        var enabled = filePaths.Any(p => !p.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase));
        var extensions = filePaths.Select(p =>
        {
            var normalized = p.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) ? p[..^".disabled".Length] : p;
            return Path.GetExtension(normalized).ToLowerInvariant();
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var hasPak = extensions.Contains(".pak", StringComparer.OrdinalIgnoreCase);
        var ucas = extensions.Contains(".ucas", StringComparer.OrdinalIgnoreCase);
        var utoc = extensions.Contains(".utoc", StringComparer.OrdinalIgnoreCase);
        var incomplete = !hasPak || ucas != utoc;

        var health = !enabled ? "Disabled" : incomplete ? "Misconfigured" : "Active / Unverified";
        var evidence = !enabled
            ? $"{sourceLabel} files are intentionally disabled."
            : incomplete
                ? $"Incomplete {sourceLabel}: {string.Join(", ", extensions)}"
                : serverRunning
                    ? $"{sourceLabel} files are deployed; runtime load cannot be directly confirmed."
                    : $"{sourceLabel} files are deployed; runtime confirmation begins after server start.";

        return new HeadlessModItem(
            "PAK", package, package, installPath, enabled, health,
            enabled ? "Active / Unverified" : "Disabled",
            evidence, extensions.Length, false, string.Empty);
    }

    private IEnumerable<HeadlessModItem> ScanUe4ssMods(
        HeadlessUe4ssStatus ue4ss,
        HashSet<string> enabledPackages,
        HeadlessModRuntimeEvidence runtimeEvidence,
        bool serverRunning)
    {
        if (!Directory.Exists(ue4ss.ActiveModsRoot)) yield break;

        foreach (var directory in Directory.EnumerateDirectories(ue4ss.ActiveModsRoot))
        {
            var name = Path.GetFileName(directory);
            if (KnownRuntimeComponents.Contains(name) ||
                name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                continue;

            var enabled = enabledPackages.Contains(name);
            var enabledMarker = File.Exists(Path.Combine(directory, "enabled.txt"));
            var started = runtimeEvidence.Started.TryGetValue(name, out var startedKind);
            var active = runtimeEvidence.Active.Contains(name);

            string health;
            string runtime;
            string evidence;

            if (!enabled && enabledMarker)
            {
                health = "Attention";
                runtime = "Disabled";
                evidence = "mods.txt disables this MOD but enabled.txt may override it.";
            }
            else if (!enabled)
            {
                health = "Disabled";
                runtime = "Disabled";
                evidence = "UE4SS mods.txt marks this MOD disabled.";
            }
            else if (ue4ss.HasPathMismatch)
            {
                health = "Misconfigured";
                runtime = active ? "Confirmed Active" : started ? "Confirmed Loaded" : "Active / Unverified";
                evidence = ue4ss.WarningMessage;
            }
            else if (active)
            {
                health = "Healthy";
                runtime = "Confirmed Active";
                evidence = $"UE4SS.log shows a Starting {startedKind} mod signature and subsequent [{name}]-tagged runtime output — the MOD is running and producing output, not just loaded.";
            }
            else if (started)
            {
                health = "Healthy";
                runtime = "Confirmed Loaded";
                evidence = $"UE4SS.log shows a Starting {startedKind} mod signature, but no [{name}]-tagged output was observed afterward. This can be normal for a mod that only logs on specific events, or its internal log tag may differ from its package name.";
            }
            else
            {
                health = "Active / Unverified";
                runtime = "Active / Unverified";
                evidence = serverRunning
                    ? "Enabled by mods.txt; no Starting Lua/C++ mod signature found yet in UE4SS.log."
                    : "Enabled by mods.txt; server is stopped.";
            }

            yield return new HeadlessModItem(
                "UE4SS", name, name, directory, enabled, health, runtime, evidence,
                SafeFileCount(directory), started, enabledMarker ? "enabled.txt present" : string.Empty);
        }
    }

    private int TogglePak(string package, bool enabled)
    {
        var flatRoot = Path.Combine(paths.ServerRoot, "Pal", "Content", "Paks", "~mods");
        var changed = 0;
        if (Directory.Exists(flatRoot))
        {
            foreach (var ext in PakExtensions)
            {
                var active = Path.Combine(flatRoot, package + ext);
                var disabled = active + ".disabled";
                if (enabled && !File.Exists(active) && File.Exists(disabled)) { File.Move(disabled, active); changed++; }
                else if (!enabled && File.Exists(active) && !File.Exists(disabled)) { File.Move(active, disabled); changed++; }
            }
            if (changed > 0) return changed;
        }

        var modFolder = FindNestedPakModFolder(package);
        if (modFolder is null) return changed;
        foreach (var file in Directory.EnumerateFiles(modFolder, "*", SearchOption.AllDirectories).ToArray())
        {
            var isDisabled = file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
            var activeName = isDisabled ? file[..^".disabled".Length] : file;
            if (!PakExtensions.Any(ext => activeName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;
            if (enabled && isDisabled) { File.Move(file, activeName); changed++; }
            else if (!enabled && !isDisabled) { File.Move(file, file + ".disabled"); changed++; }
        }
        return changed;
    }

    private string? FindNestedPakModFolder(string package)
    {
        foreach (var rootName in WorkshopPakRootNames)
        {
            var candidate = Path.Combine(paths.ServerRoot, "Pal", "Content", "Paks", rootName, package);
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }

    private int ToggleUe4ss(string package, bool enabled)
    {
        var ue4ss = ResolveUe4ss();
        var folder = Path.Combine(ue4ss.ActiveModsRoot, package);
        var oldDisabledFolder = folder + ".disabled";
        if (Directory.Exists(oldDisabledFolder) && !Directory.Exists(folder))
            Directory.Move(oldDisabledFolder, folder);
        if (!Directory.Exists(folder)) return 0;

        // mods.txt is authoritative. Neutralize enabled.txt so state cannot drift.
        var marker = Path.Combine(folder, "enabled.txt");
        if (File.Exists(marker))
        {
            var neutralized = marker + ".mysttiq-disabled";
            if (File.Exists(neutralized)) File.Delete(marker);
            else File.Move(marker, neutralized);
        }

        var modsTxt = Path.Combine(ue4ss.ActiveModsRoot, "mods.txt");
        var lines = File.Exists(modsTxt) ? File.ReadAllLines(modsTxt).ToList() : [];
        var prefix = package + " :";
        var replacement = $"{package} : {(enabled ? 1 : 0)}";
        var index = lines.FindIndex(line => line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (index >= 0) lines[index] = replacement;
        else
        {
            var keybind = lines.FindIndex(line => line.TrimStart().StartsWith("Keybinds :", StringComparison.OrdinalIgnoreCase));
            if (keybind >= 0) lines.Insert(keybind, replacement);
            else lines.Add(replacement);
        }

        AtomicWrite(modsTxt, string.Join(Environment.NewLine, lines));
        return 1;
    }

    private HeadlessUe4ssStatus ResolveUe4ss()
    {
        var modern = paths.Ue4ssModsRoot;
        var legacy = paths.LegacyUe4ssModsRoot;
        var runtime = TryReadRuntimeModsRoot();

        string active;
        string method;
        if (Directory.Exists(paths.Ue4ssRoot) && Directory.Exists(modern))
        {
            active = modern; method = "Modern UE4SS layout";
        }
        else if (!string.IsNullOrWhiteSpace(runtime.Path))
        {
            active = NormalizeAbsolutePath(runtime.Path!); method = "UE4SS runtime log";
        }
        else if (Directory.Exists(legacy))
        {
            active = legacy; method = "Legacy UE4SS layout";
        }
        else if (Directory.Exists(paths.Ue4ssRoot))
        {
            active = modern; method = "Expected modern UE4SS layout";
        }
        else
        {
            active = legacy; method = "Expected legacy UE4SS layout";
        }

        active = Path.GetFullPath(active);
        var runtimeRoot = string.IsNullOrWhiteSpace(runtime.Path)
            ? null : Path.GetFullPath(NormalizeAbsolutePath(runtime.Path!));
        var verified = runtimeRoot is not null;
        var matches = !verified || PathsEqual(active, runtimeRoot!);
        var warning = verified && !matches
            ? $"UE4SS Mod Root Mismatch. Manager: {active} | Runtime: {runtimeRoot}"
            : Directory.Exists(modern) && Directory.Exists(legacy)
                ? "Both modern and legacy UE4SS Mods roots exist. Modern root is active."
                : string.Empty;

        // v0.7.78.0: direct request -- "runtime health should capture after it has run one time...
        // display something useful like no crashes or anything, or server ran without issue."
        // Before this, HealthState reverted to "Unverified" every time TryReadRuntimeModsRoot()
        // found no current evidence (server stopped, log rotated/cleared, etc.), even on an install
        // that had already run cleanly. A small persisted marker -- written only when verified is
        // actually true, matching the existing Ue4ssInstallManifest pattern just above -- lets a
        // past successful run keep being reported once seen, instead of the signal being lost the
        // moment its one-time evidence source goes away.
        if (verified && matches) WriteUe4ssRuntimeVerification();
        var priorVerification = verified ? null : TryReadUe4ssRuntimeVerification();
        var healthState = verified
            ? (matches ? "Healthy" : "Degraded")
            : priorVerification is not null
                ? $"Confirmed — ran without issue (last verified {priorVerification.VerifiedAtUtc:u})"
                : "Unverified";

        return new HeadlessUe4ssStatus(
            paths.RuntimeBinaryRoot, paths.Ue4ssRoot, modern, legacy, active, runtimeRoot, method,
            Directory.Exists(paths.Ue4ssRoot), Directory.Exists(modern), Directory.Exists(legacy),
            verified, matches, healthState,
            warning, runtime.LogPath, SafeDirectoryCount(active), SafeDirectoryCount(legacy), DetectUe4ssVersion());
    }

    private string Ue4ssRuntimeVerificationPath => Path.Combine(paths.ManagerRuntimeRoot, "ue4ss-runtime-verification.json");

    private void WriteUe4ssRuntimeVerification()
    {
        try
        {
            Directory.CreateDirectory(paths.ManagerRuntimeRoot);
            File.WriteAllText(Ue4ssRuntimeVerificationPath, JsonSerializer.Serialize(new Ue4ssRuntimeVerification(DateTimeOffset.UtcNow)));
        }
        catch { /* Best-effort record -- a failed write just means this falls back to "Unverified" next time, same as before this feature existed. */ }
    }

    private Ue4ssRuntimeVerification? TryReadUe4ssRuntimeVerification()
    {
        try
        {
            if (!File.Exists(Ue4ssRuntimeVerificationPath)) return null;
            return JsonSerializer.Deserialize<Ue4ssRuntimeVerification>(File.ReadAllText(Ue4ssRuntimeVerificationPath));
        }
        catch { return null; }
    }

    // v0.7.60.0: the one reliable source of the real installed UE4SS version -- see the write site
    // in ApplyUe4ssInstallAsync for why this is necessary at all. Stored under ManagerRuntimeRoot,
    // not inside the UE4SS folder itself, so a manual file copy over the top (bypassing MystTiq's
    // own Apply flow, exactly like this project's own real installs were done before this feature
    // existed) correctly leaves no manifest behind and falls back to the honest "unavailable" state,
    // rather than silently reporting a stale recorded tag for files that were never actually
    // installed through MystTiq.
    private string Ue4ssInstallManifestPath => Path.Combine(paths.ManagerRuntimeRoot, "ue4ss-install-manifest.json");

    private void WriteUe4ssInstallManifest(string source, string tagName)
    {
        try
        {
            Directory.CreateDirectory(paths.ManagerRuntimeRoot);
            var manifest = new Ue4ssInstallManifest(source, tagName, DateTimeOffset.UtcNow);
            File.WriteAllText(Ue4ssInstallManifestPath, JsonSerializer.Serialize(manifest));
        }
        catch { /* Best-effort record -- a failed write just means the next check falls back to the existing honest "unavailable" detection. */ }
    }

    private void DeleteUe4ssInstallManifest()
    {
        try { File.Delete(Ue4ssInstallManifestPath); } catch { /* Nothing to clear. */ }
    }

    private Ue4ssInstallManifest? TryReadUe4ssInstallManifest()
    {
        try
        {
            if (!File.Exists(Ue4ssInstallManifestPath)) return null;
            return JsonSerializer.Deserialize<Ue4ssInstallManifest>(File.ReadAllText(Ue4ssInstallManifestPath));
        }
        catch { return null; }
    }

    // Exposed so HeadlessComponentUpdateService can compare the RAW tag exactly against the release
    // catalog, rather than parsing it back out of DetectUe4ssVersion()'s human-readable display string.
    public string? TryGetInstalledUe4ssReleaseTag() => TryReadUe4ssInstallManifest()?.TagName;

    private string DetectUe4ssVersion()
    {
        // Checked first: if MystTiq itself installed the current engine files, it already knows
        // exactly which release that was -- no need to guess from unreliable DLL metadata.
        var manifest = TryReadUe4ssInstallManifest();
        if (manifest is not null && Directory.Exists(paths.Ue4ssRoot))
            return $"{manifest.TagName} ({manifest.Source})";

        foreach (var marker in new[] { "UE4SS.version", "version.txt" })
        {
            var path = Path.Combine(paths.Ue4ssRoot, marker);
            try
            {
                if (File.Exists(path))
                {
                    var value = File.ReadLines(path).FirstOrDefault()?.Trim();
                    if (IsMeaningfulVersion(value)) return value!;
                }
            }
            catch { }
        }

        foreach (var candidate in new[]
        {
            Path.Combine(paths.RuntimeBinaryRoot, "UE4SS.dll"),
            Path.Combine(paths.Ue4ssRoot, "UE4SS.dll"),
            Path.Combine(paths.RuntimeBinaryRoot, "dwmapi.dll")
        })
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(candidate);
                var version = info.ProductVersion ?? info.FileVersion;
                // v0.7.30.0 bug fix: a DLL can have a version resource that technically exists but
                // was never stamped with real numbers, so ProductVersion/FileVersion can legitimately
                // come back as the literal string "0.0.0.0" -- non-null, non-empty, and previously
                // returned as if it were a meaningful version. Treated the same as no version found.
                if (IsMeaningfulVersion(version)) return version!;
            }
            catch { }
        }

        if (Directory.Exists(paths.Ue4ssRoot))
        {
            var hashMatch = TryDetectVersionFromLocalWorkshopHash();
            if (hashMatch is not null) return hashMatch;
        }

        return Directory.Exists(paths.Ue4ssRoot) ? "Installed — version metadata unavailable" : "Not installed";
    }

    // v0.7.77.0: last-resort version detection when nothing else above identified it -- this fork's
    // UE4SS.dll ships with no meaningful FileVersionInfo and no marker file, but a Steam Workshop
    // subscription to a UE4SS runtime distribution carries its own copy of UE4SS.dll AND a real
    // declared version in its Info.json manifest. If the currently-active UE4SS.dll is byte-for-byte
    // identical (SHA-256) to a local Workshop item's own copy, that manifest's version is genuinely
    // the installed version, not a guess -- confirmed live against a real install (hash match,
    // Workshop item "UE4SS Experimental (Palworld)" declaring "2281fa31", which also matches the
    // real upstream release tag the UE4SS Release Catalog already knows about). Windows-only, same
    // as every other Workshop-scan capability -- Workshop subscriptions live on the admin's local
    // Steam client, not a headless Linux host.
    private string? TryDetectVersionFromLocalWorkshopHash()
    {
        if (!OperatingSystem.IsWindows()) return null;

        var activeDllPath = ResolveActiveUe4ssDllPath();
        if (activeDllPath is null) return null;

        string activeHash;
        try { activeHash = ComputeSha256(activeDllPath); }
        catch { return null; }

        foreach (var root in DiscoverWorkshopContentRoots())
        {
            if (!Directory.Exists(root)) continue;
            foreach (var itemDir in Directory.EnumerateDirectories(root))
            {
                var manifest = ReadWorkshopManifest(itemDir);
                var isRuntime = manifest?.Rules.Any(r => string.Equals(r.Type, "UE4SS", StringComparison.OrdinalIgnoreCase)) == true;
                var workshopDllPath = Path.Combine(itemDir, "UE4SS.dll");
                if (!isRuntime || !File.Exists(workshopDllPath)) continue;

                string workshopHash;
                try { workshopHash = ComputeSha256(workshopDllPath); }
                catch { continue; }

                if (!string.Equals(activeHash, workshopHash, StringComparison.OrdinalIgnoreCase)) continue;

                var declaredVersion = ReadWorkshopManifestVersion(itemDir);
                var workshopId = Path.GetFileName(itemDir);
                return string.IsNullOrWhiteSpace(declaredVersion)
                    ? $"Matched local Workshop item {workshopId} (SHA-256 identical) — no declared version"
                    : $"{declaredVersion} (matched via local Workshop item {workshopId}, SHA-256 identical)";
            }
        }
        return null;
    }

    // v0.7.77.0: real bug found live while building the hash-match check above -- this real
    // install has BOTH a modern-layout UE4SS.dll (Pal/Binaries/Win64/ue4ss/UE4SS.dll, the one
    // actually loaded) AND a stale legacy-location one (Pal/Binaries/Win64/UE4SS.dll, 16MB dated
    // Feb 2024 vs the modern one's 20MB dated Sept 2026 -- genuinely different files). A naive
    // "check legacy path first" order picks the wrong, inactive one whenever both exist side by
    // side. This mirrors ResolveUe4ss()'s own modern-vs-legacy preference instead of guessing.
    private string? ResolveActiveUe4ssDllPath()
    {
        if (Directory.Exists(paths.Ue4ssRoot) && Directory.Exists(paths.Ue4ssModsRoot))
        {
            var modernDll = Path.Combine(paths.Ue4ssRoot, "UE4SS.dll");
            if (File.Exists(modernDll)) return modernDll;
        }
        var legacyDll = Path.Combine(paths.RuntimeBinaryRoot, "UE4SS.dll");
        return File.Exists(legacyDll) ? legacyDll : null;
    }

    private static string? ReadWorkshopManifestVersion(string itemDir)
    {
        var infoJson = Path.Combine(itemDir, "Info.json");
        if (!File.Exists(infoJson)) return null;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(infoJson));
            return document.RootElement.TryGetProperty("Version", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
                ? v.GetString() : null;
        }
        catch { return null; }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
    }

    private static bool IsMeaningfulVersion(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim(' ', '.', '0') is { Length: > 0 };

    private (string? Path, string? LogPath) TryReadRuntimeModsRoot()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(paths.RuntimeBinaryRoot, "UE4SS.log"),
            Path.Combine(paths.Ue4ssRoot, "UE4SS.log"),
            Path.Combine(paths.Ue4ssRoot, "Logs", "UE4SS.log"),
            Path.Combine(paths.Ue4ssRoot, "logs", "UE4SS.log")
        };

        try
        {
            if (Directory.Exists(paths.RuntimeBinaryRoot))
                foreach (var p in Directory.EnumerateFiles(paths.RuntimeBinaryRoot, "UE4SS.log", SearchOption.AllDirectories))
                    candidates.Add(p);
        }
        catch { }

        foreach (var log in candidates.Where(File.Exists).OrderByDescending(SafeLastWriteUtc))
        {
            try
            {
                string? last = null;
                foreach (var line in File.ReadLines(log))
                {
                    var match = ModsRootLogPattern.Match(line);
                    if (match.Success) last = match.Groups["path"].Value.Trim().Trim('"', '\'');
                }
                if (!string.IsNullOrWhiteSpace(last)) return (last, log);
            }
            catch { }
        }
        return (null, null);
    }

    // Two tiers of runtime evidence, both drawn from real UE4SS.log signatures observed against a
    // live server: "Starting Lua/C++ mod '<name>'" confirms the mod's code actually began running
    // (covers native/C++ mods too -- the previous check only matched Lua mods, silently missing
    // every C++ mod). A later "[<name>] ..." tagged line is the mod's own runtime output, which is
    // stronger evidence it is not just loaded but actively doing something -- most well-behaved
    // UE4SS mods log through a tag matching their own name (e.g. "[AntiDupe] Config loaded",
    // "[Lua] [AdminCommands] ... loaded successfully!"). A mod whose internal log tag differs from
    // its package name (observed in the wild) will under-report here; that is a real limitation,
    // not a false claim, and is called out in the evidence text rather than hidden.
    private static HeadlessModRuntimeEvidence ReadModRuntimeEvidence(string? logPath)
    {
        var started = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
            return new HeadlessModRuntimeEvidence(started, active);

        try
        {
            var lines = File.ReadLines(logPath).ToArray();
            var startIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < lines.Length; i++)
            {
                var match = StartedModPattern.Match(lines[i]);
                if (!match.Success) continue;
                var name = match.Groups["name"].Value.Trim();
                started[name] = match.Groups["kind"].Value;
                startIndex[name] = i;
            }
            foreach (var (name, index) in startIndex)
            {
                var tag = "[" + name + "]";
                for (var i = index + 1; i < lines.Length; i++)
                {
                    if (lines[i].Contains(tag, StringComparison.OrdinalIgnoreCase)) { active.Add(name); break; }
                }
            }
        }
        catch { }
        return new HeadlessModRuntimeEvidence(started, active);
    }

    private static HashSet<string> ReadEnabledPackages(string root)
    {
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modsTxt = Path.Combine(root, "mods.txt");
        if (!File.Exists(modsTxt)) return enabled;
        try
        {
            foreach (var raw in File.ReadLines(modsTxt))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                var sep = line.LastIndexOf(':');
                if (sep <= 0) continue;
                var name = line[..sep].Trim();
                var state = line[(sep + 1)..].Trim();
                if (state == "1" && name.Length > 0) enabled.Add(name);
            }
        }
        catch { }
        return enabled;
    }

    private static void AtomicWrite(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + $".tmp-{Guid.NewGuid():N}";
        if (File.Exists(path)) File.Copy(path, path + ".pre-mysttiq.bak", overwrite: true);
        try
        {
            File.WriteAllText(temp, content);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private string NormalizeAbsolutePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        return Path.IsPathRooted(expanded) ? expanded : Path.Combine(paths.RuntimeBinaryRoot, expanded);
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
                      Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                      StringComparison.OrdinalIgnoreCase);

    private static DateTime SafeLastWriteUtc(string path)
    {
        try { return File.GetLastWriteTimeUtc(path); } catch { return DateTime.MinValue; }
    }

    private static int SafeDirectoryCount(string path)
    {
        try { return Directory.Exists(path) ? Directory.EnumerateDirectories(path).Count() : 0; }
        catch { return 0; }
    }

    private static int SafeFileCount(string path)
    {
        try { return Directory.Exists(path) ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Take(10000).Count() : 0; }
        catch { return 0; }
    }

    // --- Steam Workshop scan/import. Windows-only: Workshop subscriptions live on the admin's
    // local Steam client, not on a Linux dedicated-server host. ---

    private const int PalworldSteamAppId = 1623730;

    public async Task<HeadlessWorkshopScanResult> ScanWorkshopAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return new HeadlessWorkshopScanResult(false,
                "Steam Workshop scanning requires a local Steam client and is only available on Windows.", []);

        var roots = DiscoverWorkshopContentRoots();
        if (roots.Count == 0)
            return new HeadlessWorkshopScanResult(false,
                $"No local Steam Workshop content folder was found for Palworld (App ID {PalworldSteamAppId}). Subscribe to items in Steam and let them finish downloading first.", []);

        var inventory = await GetInventoryAsync(cancellationToken);
        var installedPackages = new HashSet<string>(inventory.Mods.Select(m => m.Package), StringComparer.OrdinalIgnoreCase);

        var ue4ssInstalled = Directory.Exists(paths.Ue4ssRoot);
        var items = new List<HeadlessWorkshopItem>();
        foreach (var root in roots)
        {
            foreach (var itemDir in Directory.EnumerateDirectories(root))
                items.Add(DescribeWorkshopItem(itemDir, installedPackages, ue4ssInstalled));
        }
        var ordered = items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        return new HeadlessWorkshopScanResult(true, $"{ordered.Length} local Workshop item(s) found across {roots.Count} Steam librar{(roots.Count == 1 ? "y" : "ies")}.", ordered);
    }

    public async Task<HeadlessModMutationResult> ImportWorkshopItemAsync(string workshopId, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return HeadlessModMutationResult.Failure("Steam Workshop import requires Windows.");
        if (!await mutationGate.WaitAsync(0, cancellationToken))
            return HeadlessModMutationResult.Failure("A MOD mutation is already in progress.");

        var staging = Path.Combine(paths.ManagerRuntimeRoot, "mod-staging", Guid.NewGuid().ToString("N"));
        try
        {
            var blocked = await RejectWhenRunningAsync(cancellationToken); if (blocked is not null) return blocked;

            workshopId = Path.GetFileName((workshopId ?? string.Empty).Trim());
            if (string.IsNullOrWhiteSpace(workshopId) || workshopId.Contains(".."))
                return HeadlessModMutationResult.Failure("Invalid Workshop item ID.");

            string? itemDir = null;
            foreach (var root in DiscoverWorkshopContentRoots())
            {
                var candidate = Path.Combine(root, workshopId);
                if (Directory.Exists(candidate)) { itemDir = candidate; break; }
            }
            if (itemDir is null)
                return HeadlessModMutationResult.Failure($"Workshop item {workshopId} was not found in any local Steam library.");

            var manifest = ReadWorkshopManifest(itemDir);
            var name = manifest?.ModName ?? workshopId;
            var package = SanitizePackageName(manifest?.PackageName ?? manifest?.ModName ?? workshopId);
            var importedFiles = 0;
            var notes = new List<string>();

            // Server-targeted rules take precedence; some manifests only ship a client rule per
            // type, so fall back to that when no server-flagged rule of the same type exists.
            var rules = manifest?.Rules ?? [];
            IEnumerable<WorkshopInstallRule> RulesFor(string type)
            {
                var serverRules = rules.Where(r => string.Equals(r.Type, type, StringComparison.OrdinalIgnoreCase) && r.IsServer).ToArray();
                if (serverRules.Length > 0) return serverRules;
                return rules.Where(r => string.Equals(r.Type, type, StringComparison.OrdinalIgnoreCase) && !r.IsServer);
            }

            var pakTargets = RulesFor("Paks").SelectMany(r => r.Targets).ToArray();
            var luaTargets = RulesFor("Lua").SelectMany(r => r.Targets).ToArray();
            var hasUe4ssRuntimeRule = rules.Any(r => string.Equals(r.Type, "UE4SS", StringComparison.OrdinalIgnoreCase));

            // No manifest, or a manifest with no InstallRule at all: fall back to scanning the
            // item's own folder shape directly rather than refusing to import anything.
            if (manifest is null || rules.Count == 0)
            {
                if (Directory.Exists(Path.Combine(itemDir, "Scripts"))) luaTargets = ["Scripts"];
                if (Directory.Exists(Path.Combine(itemDir, "dlls"))) luaTargets = [.. luaTargets, "dlls"];
                if (Directory.EnumerateFiles(itemDir, "*", SearchOption.AllDirectories).Any(f => PakExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
                    pakTargets = ["."];
            }

            if (pakTargets.Length > 0)
            {
                var pakRoot = Path.Combine(paths.ServerRoot, "Pal", "Content", "Paks", "~WorkshopMods", package);
                var pakCount = 0;
                foreach (var target in pakTargets)
                {
                    var sourcePath = Path.GetFullPath(Path.Combine(itemDir, target));
                    if (!sourcePath.StartsWith(Path.GetFullPath(itemDir), StringComparison.OrdinalIgnoreCase)) continue;
                    if (Directory.Exists(sourcePath))
                        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories).Where(f => PakExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
                        { Directory.CreateDirectory(pakRoot); File.Copy(file, Path.Combine(pakRoot, Path.GetFileName(file)), true); pakCount++; }
                    else if (File.Exists(sourcePath) && PakExtensions.Contains(Path.GetExtension(sourcePath), StringComparer.OrdinalIgnoreCase))
                    { Directory.CreateDirectory(pakRoot); File.Copy(sourcePath, Path.Combine(pakRoot, Path.GetFileName(sourcePath)), true); pakCount++; }
                }
                if (pakCount > 0) { importedFiles += pakCount; notes.Add($"{pakCount} PAK file(s)"); }
            }

            if (luaTargets.Length > 0)
            {
                Directory.CreateDirectory(staging);
                var copied = 0;
                foreach (var target in luaTargets)
                {
                    var sourcePath = Path.GetFullPath(Path.Combine(itemDir, target));
                    if (!sourcePath.StartsWith(Path.GetFullPath(itemDir), StringComparison.OrdinalIgnoreCase)) continue;
                    if (Directory.Exists(sourcePath)) copied += CopyDirectoryTree(sourcePath, Path.Combine(staging, Path.GetFileName(target.TrimEnd('/', '\\'))));
                    else if (File.Exists(sourcePath)) { var dest = Path.Combine(staging, Path.GetFileName(sourcePath)); File.Copy(sourcePath, dest, true); copied++; }
                }
                if (copied > 0) { importedFiles += InstallUe4ssFiles(staging, package); notes.Add("UE4SS Lua/config content"); }
            }

            if (hasUe4ssRuntimeRule)
                notes.Add("this item replaces the UE4SS runtime itself and was not imported as a mod; update UE4SS manually if needed");

            if (importedFiles == 0)
                return HeadlessModMutationResult.Failure($"Workshop item {workshopId} ({name}) has no recognized PAK or Lua/UE4SS content to import.");

            activity.Record("Information", "MODs", "Imported Workshop MOD", $"workshopId={workshopId}; package={package}; files={importedFiles}");
            return new(true, "WORKSHOP", package, true, importedFiles, $"Imported {name} ({string.Join(", ", notes)}).");
        }
        catch (Exception ex) { return HeadlessModMutationResult.Failure(ex.Message); }
        finally { TryDeleteDirectory(staging); mutationGate.Release(); }
    }

    // v0.7.77.0: per-MOD "Repair / Re-install" -- direct request, distinct from
    // CheckModUpdateAsync/the Update button above (which only offers to re-import when Steam's
    // local copy is *newer* than what's installed) and distinct from the existing global "Repair"
    // button (RepairAsync, which only neutralizes legacy enabled.txt overrides and ensures
    // mods.txt exists -- it never touches a MOD's own file content). This re-imports from the
    // matched local Workshop source unconditionally, regardless of timestamps, which is exactly
    // what fixes a MOD reported Misconfigured/incomplete (missing files, a mismatched
    // .ucas/.utoc pair, etc.) -- reuses ImportWorkshopItemAsync's own proven copy logic, the same
    // one just used live to properly install PalSchema/QualityOfLife.
    public async Task<HeadlessModMutationResult> RepairModAsync(string type, string package, CancellationToken cancellationToken)
    {
        package = NormalizePackage(package);
        if (!OperatingSystem.IsWindows())
            return HeadlessModMutationResult.Failure("Repair via Steam Workshop requires Windows -- Workshop content lives on the local Steam client.");

        var match = FindMatchingWorkshopItem(package);
        if (match is null)
            return HeadlessModMutationResult.Failure(
                $"No local Steam Workshop source is known for \"{package}\" -- it may have been installed manually, from a ZIP, or from a source other than Steam Workshop. Delete it and re-install from its original source, or install a fresh ZIP if you have one.");

        // v0.7.77.0 real bug found live: ImportWorkshopItemAsync's own UE4SS-Lua copy path
        // (InstallUe4ssFiles) refuses outright when the destination folder already exists --
        // correct, safe behavior for a fresh Import (never silently clobber an existing MOD), but
        // it directly defeats Repair's own purpose of fixing an existing, incomplete install.
        // Delete first -- which already captures its own pre-delete snapshot via CaptureSnapshot,
        // the same safety net every other install/delete in this service already relies on -- then
        // import fresh. If the re-import fails after the delete succeeded, the MOD is genuinely
        // gone but recoverable: Rollback restores exactly the snapshot Delete just took.
        var deleteResult = await DeleteAsync(type, package, cancellationToken);
        if (!deleteResult.Success)
            return HeadlessModMutationResult.Failure($"Repair could not remove the existing incomplete install first: {deleteResult.Message}");

        var importResult = await ImportWorkshopItemAsync(match.WorkshopId, cancellationToken);
        if (!importResult.Success)
            return HeadlessModMutationResult.Failure(
                $"Repair removed the incomplete install but the fresh re-import failed: {importResult.Message} Use Rollback Selected to restore the previous (incomplete) state if needed.");

        return importResult with { Message = $"Repaired {package}: removed the incomplete install and re-imported it fresh from local Steam Workshop item {match.WorkshopId}." };
    }

    // v0.7.41.0: MOD update detection (item 52). Deliberately scoped to what's actually knowable
    // without a live network call: MystTiq has no Steam Web API integration, and non-Workshop
    // MODs (UE4SS/Lua content from Nexus/GitHub/etc.) have no known source to query at all -- that
    // gap is the same one already disclosed for item 40's deferred website-description work, not
    // a new one. What IS real and locally verifiable: whether Steam's own local Workshop content
    // cache for a matching item is newer than what's actually installed -- i.e. Steam already
    // silently re-downloaded an update in the background that hasn't been re-imported yet. No
    // network calls, no fabricated "latest version" claim.
    public Task<HeadlessModUpdateCheckResult> CheckModUpdateAsync(string type, string package, CancellationToken cancellationToken)
    {
        package = NormalizePackage(package);
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(new HeadlessModUpdateCheckResult(false, false, null,
                "Update checking requires Windows -- Steam Workshop content lives on the local Steam client."));

        var match = FindMatchingWorkshopItem(package);
        if (match is null)
            return Task.FromResult(new HeadlessModUpdateCheckResult(false, false, null,
                "No known update source for this MOD. Only MODs that match a locally-scanned Steam Workshop item can be checked; MystTiq has no live update feed for MODs installed by other means."));

        var installedAt = GetInstalledModLastWriteUtc(type, package);
        var workshopFiles = SafeEnumerateFiles(match.LocalPath);
        var workshopAt = workshopFiles.Length > 0 ? workshopFiles.Max(File.GetLastWriteTimeUtc) : (DateTime?)null;

        if (installedAt is null || workshopAt is null)
            return Task.FromResult(new HeadlessModUpdateCheckResult(true, false, match.WorkshopId,
                $"Matched local Steam Workshop item \"{match.Name}\", but could not read file timestamps to compare."));

        var updateAvailable = workshopAt.Value > installedAt.Value.AddSeconds(2);
        var detail = updateAvailable
            ? $"Steam's local Workshop copy of \"{match.Name}\" was last updated {workshopAt.Value:u}, newer than the installed copy ({installedAt.Value:u}). Steam has already downloaded the update locally; use Update to re-import it."
            : $"Installed copy ({installedAt.Value:u}) is at least as recent as Steam's local Workshop copy of \"{match.Name}\" ({workshopAt.Value:u}). This only reflects what Steam has already downloaded locally, not whether a newer version exists on Steam's servers.";
        return Task.FromResult(new HeadlessModUpdateCheckResult(true, updateAvailable, match.WorkshopId, detail));
    }

    // v0.7.55.0: website-sourced MOD descriptions (item 40's deferred half), designed for multiple
    // sources up front per direct instruction, not Workshop-only. Two real, verifiable sources:
    // Steam Workshop (the same local-content match CheckModUpdateAsync already uses above, then a
    // real Steam Web API call for that item's own title/description) and GitHub (when the user sets
    // a manual github.com Source URL for a MOD with no local Workshop match, e.g. a UE4SS/Lua mod --
    // most of those don't come from Steam at all, so there's no ID to auto-detect one from). Fetches
    // only happen on an explicit user request (Fetch/Refresh), never automatically or in the
    // background, and results are cached to disk so repeat views don't re-fetch. The only outbound
    // calls this makes are read-only, unauthenticated GETs/POSTs against Steam's and GitHub's own
    // public REST APIs -- no credentials, no write access -- matching the disclosed-scope pattern
    // already established for HeadlessComponentUpdateService's outbound calls.
    public async Task<HeadlessModDescriptionResult> GetModDescriptionAsync(string type, string package, bool forceRefresh, CancellationToken cancellationToken)
    {
        package = NormalizePackage(package);
        var cache = ReadDescriptionCache(type, package);

        if (!forceRefresh && cache is { Description.Length: > 0 })
            return new HeadlessModDescriptionResult(true, cache.Source, cache.Title, cache.Description, cache.SourceUrl, cache.FetchedAtUtc, "Loaded from local cache.");

        var manualUrl = cache?.ManualSourceUrl;
        if (!string.IsNullOrWhiteSpace(manualUrl))
        {
            var github = await TryFetchGitHubAsync(manualUrl, cancellationToken);
            var entry = github ?? new ModDescriptionCacheEntry { Source = "Manual Link", SourceUrl = manualUrl };
            entry.ManualSourceUrl = manualUrl;
            entry.FetchedAtUtc = DateTime.UtcNow;
            WriteDescriptionCache(type, package, entry);
            var detail = github is not null
                ? "Fetched from GitHub."
                : "This Source URL isn't a recognized GitHub repository (or it couldn't be reached), so no description could be fetched automatically -- open the link directly to view it.";
            return new HeadlessModDescriptionResult(true, entry.Source, entry.Title, entry.Description, entry.SourceUrl, entry.FetchedAtUtc, detail);
        }

        if (!OperatingSystem.IsWindows())
            return new HeadlessModDescriptionResult(false, "None", null, null, null, null,
                "No manual Source URL is set, and Steam Workshop matching requires Windows -- the local Steam client isn't reachable here.");

        var match = FindMatchingWorkshopItem(package);
        if (match is null)
            return new HeadlessModDescriptionResult(false, "None", null, null, null, null,
                "No known description source for this MOD -- it doesn't match a locally-scanned Steam Workshop item. Set a Source URL (e.g. its GitHub repository) to fetch a description from there instead.");

        var fetched = await TryFetchSteamWorkshopAsync(match.WorkshopId, match.Name, cancellationToken);
        if (fetched is null)
            return new HeadlessModDescriptionResult(true, "Steam Workshop", match.Name, null, SteamWorkshopUrl(match.WorkshopId), null,
                $"Matched local Steam Workshop item \"{match.Name}\", but could not reach the Steam Web API to fetch its description.");

        WriteDescriptionCache(type, package, fetched);
        return new HeadlessModDescriptionResult(true, fetched.Source, fetched.Title, fetched.Description, fetched.SourceUrl, fetched.FetchedAtUtc, "Fetched from Steam Workshop.");
    }

    public Task<HeadlessModMutationResult> SetModDescriptionSourceAsync(string type, string package, string sourceUrl, CancellationToken cancellationToken)
    {
        package = NormalizePackage(package);
        sourceUrl = (sourceUrl ?? string.Empty).Trim();
        if (sourceUrl.Length == 0)
        {
            DeleteDescriptionCache(type, package);
            return Task.FromResult(new HeadlessModMutationResult(true, type, package, false, 0, "Source URL cleared. The next Fetch will fall back to a Steam Workshop match, if any."));
        }
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return Task.FromResult(HeadlessModMutationResult.Failure("Source URL must be a valid http:// or https:// address."));

        var entry = new ModDescriptionCacheEntry { Source = "Manual Link", SourceUrl = sourceUrl, ManualSourceUrl = sourceUrl, FetchedAtUtc = DateTime.UtcNow };
        WriteDescriptionCache(type, package, entry);
        return Task.FromResult(new HeadlessModMutationResult(true, type, package, false, 0, "Source URL saved. Use Fetch Description to load it."));
    }

    private static string SteamWorkshopUrl(string workshopId) => $"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopId}";

    private async Task<ModDescriptionCacheEntry?> TryFetchSteamWorkshopAsync(string workshopId, string fallbackName, CancellationToken cancellationToken)
    {
        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["itemcount"] = "1",
                ["publishedfileids[0]"] = workshopId,
            });
            using var response = await DescriptionHttp.PostAsync(
                "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", content, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<SteamPublishedFileResponse>(stream, DescriptionJsonOptions, cancellationToken);
            var detail = payload?.Response?.PublishedFileDetails?.FirstOrDefault();
            if (detail is null || detail.Result != 1) return null;

            return new ModDescriptionCacheEntry
            {
                Source = "Steam Workshop",
                Title = string.IsNullOrWhiteSpace(detail.Title) ? fallbackName : detail.Title,
                Description = StripBasicBbCode(detail.FileDescription ?? string.Empty),
                SourceUrl = SteamWorkshopUrl(workshopId),
                FetchedAtUtc = DateTime.UtcNow,
            };
        }
        catch { return null; }
    }

    private async Task<ModDescriptionCacheEntry?> TryFetchGitHubAsync(string sourceUrl, CancellationToken cancellationToken)
    {
        if (!TryParseGitHubRepo(sourceUrl, out var owner, out var repo)) return null;
        try
        {
            using var response = await DescriptionHttp.GetAsync($"https://api.github.com/repos/{owner}/{repo}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var repoInfo = await JsonSerializer.DeserializeAsync<GitHubRepoDto>(stream, DescriptionJsonOptions, cancellationToken);
            if (repoInfo is null) return null;

            return new ModDescriptionCacheEntry
            {
                Source = "GitHub Repository",
                Title = repoInfo.FullName ?? $"{owner}/{repo}",
                Description = string.IsNullOrWhiteSpace(repoInfo.Description) ? "(This repository has no description set.)" : repoInfo.Description,
                SourceUrl = repoInfo.HtmlUrl ?? sourceUrl,
                FetchedAtUtc = DateTime.UtcNow,
            };
        }
        catch { return null; }
    }

    private static bool TryParseGitHubRepo(string url, out string owner, out string repo)
    {
        owner = repo = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return false;
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2) return false;
        owner = segments[0];
        repo = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? segments[1][..^4] : segments[1];
        return owner.Length > 0 && repo.Length > 0;
    }

    // Steam Workshop descriptions use a lightweight BBCode dialect ([h1], [list], [*], [url=...],
    // [b]/[i], etc.). Stripping tags rather than rendering them keeps this a plain-text panel
    // instead of a second markup renderer to maintain -- disclosed as a real, deliberate limit.
    private static string StripBasicBbCode(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = Regex.Replace(text, @"\[/?[a-zA-Z0-9]+(=[^\]]*)?\]", string.Empty);
        return text.Replace("\r\n", "\n").Trim();
    }

    private string DescriptionCachePath(string type, string package) =>
        Path.Combine(paths.ManagerRuntimeRoot, "mod-descriptions", $"{type.ToUpperInvariant()}_{package}.json");

    private ModDescriptionCacheEntry? ReadDescriptionCache(string type, string package)
    {
        try
        {
            var path = DescriptionCachePath(type, package);
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<ModDescriptionCacheEntry>(File.ReadAllText(path), DescriptionJsonOptions);
        }
        catch { return null; }
    }

    private void WriteDescriptionCache(string type, string package, ModDescriptionCacheEntry entry)
    {
        try
        {
            var path = DescriptionCachePath(type, package);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(entry, DescriptionJsonOptions));
        }
        catch { /* Best-effort cache -- a failed write just means the next load re-fetches. */ }
    }

    private void DeleteDescriptionCache(string type, string package)
    {
        try { File.Delete(DescriptionCachePath(type, package)); } catch { /* Nothing to clear. */ }
    }

    private static HttpClient BuildDescriptionHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MystTiq-Palworld-Server-Manager");
        return client;
    }

    private HeadlessWorkshopItem? FindMatchingWorkshopItem(string package)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var matchSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { package };
        var ue4ssInstalled = Directory.Exists(paths.Ue4ssRoot);
        foreach (var root in DiscoverWorkshopContentRoots())
        {
            if (!Directory.Exists(root)) continue;
            foreach (var itemDir in Directory.EnumerateDirectories(root))
            {
                var described = DescribeWorkshopItem(itemDir, matchSet, ue4ssInstalled);
                if (string.Equals(described.SuggestedPackage, package, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(described.WorkshopId, package, StringComparison.OrdinalIgnoreCase))
                    return described;
            }
        }
        return null;
    }

    private DateTime? GetInstalledModLastWriteUtc(string type, string package)
    {
        if (type.Equals("PAK", StringComparison.OrdinalIgnoreCase))
        {
            var flatRoot = Path.Combine(paths.ServerRoot, "Pal", "Content", "Paks", "~mods");
            var flatFiles = Directory.Exists(flatRoot)
                ? PakExtensions.SelectMany(ext => new[] { ext, ext + ".disabled" })
                    .Select(suffix => Path.Combine(flatRoot, package + suffix)).Where(File.Exists).ToArray()
                : [];
            if (flatFiles.Length > 0) return flatFiles.Max(File.GetLastWriteTimeUtc);

            var nested = FindNestedPakModFolder(package);
            if (nested is null || !Directory.Exists(nested)) return null;
            var nestedFiles = SafeEnumerateFiles(nested);
            return nestedFiles.Length > 0 ? nestedFiles.Max(File.GetLastWriteTimeUtc) : null;
        }
        if (type.Equals("UE4SS", StringComparison.OrdinalIgnoreCase))
        {
            var folder = Path.Combine(ResolveUe4ss().ActiveModsRoot, package);
            if (!Directory.Exists(folder)) return null;
            var files = SafeEnumerateFiles(folder);
            return files.Length > 0 ? files.Max(File.GetLastWriteTimeUtc) : null;
        }
        return null;
    }

    // v0.7.77.0: `ue4ssInstalled` fixes a real bug found live -- a Workshop item whose own manifest
    // declares an InstallRule of type "UE4SS" isn't a mod at all, it's a full UE4SS runtime
    // distribution (confirmed against a real local Workshop item, "UE4SS Experimental (Palworld)":
    // its bundled UE4SS.dll is byte-for-byte identical, SHA-256 verified, to the actually-running
    // one). The old check always looked it up in the MOD inventory, which by definition never
    // contains the runtime itself, so this kind of item showed "NOT INSTALLED" unconditionally
    // regardless of whether UE4SS was actually installed.
    private static HeadlessWorkshopItem DescribeWorkshopItem(string itemDir, HashSet<string> installedPackages, bool ue4ssInstalled)
    {
        var workshopId = Path.GetFileName(itemDir);
        var manifest = ReadWorkshopManifest(itemDir);
        var name = manifest?.ModName ?? workshopId;
        var package = SanitizePackageName(manifest?.PackageName ?? manifest?.ModName ?? workshopId);

        bool hasPak, hasLua, hasRuntime;
        if (manifest is not null && manifest.Rules.Count > 0)
        {
            hasPak = manifest.Rules.Any(r => string.Equals(r.Type, "Paks", StringComparison.OrdinalIgnoreCase));
            hasLua = manifest.Rules.Any(r => string.Equals(r.Type, "Lua", StringComparison.OrdinalIgnoreCase));
            hasRuntime = manifest.Rules.Any(r => string.Equals(r.Type, "UE4SS", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            var files = SafeEnumerateFiles(itemDir);
            hasPak = files.Any(f => PakExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
            hasLua = Directory.Exists(Path.Combine(itemDir, "Scripts")) || Directory.Exists(Path.Combine(itemDir, "dlls"));
            hasRuntime = false;
        }

        var kind = hasRuntime ? "UE4SS Runtime (not a mod)" : (hasPak, hasLua) switch
        {
            (true, true) => "PAK + UE4SS", (true, false) => "PAK", (false, true) => "UE4SS", _ => "Unrecognized"
        };
        var installed = hasRuntime
            ? ue4ssInstalled
            : installedPackages.Contains(package) || installedPackages.Contains(workshopId);
        return new HeadlessWorkshopItem(workshopId, name, package, kind, installed, itemDir);
    }

    private sealed record WorkshopInstallRule(string Type, bool IsServer, IReadOnlyList<string> Targets);
    private sealed record WorkshopManifest(string? ModName, string? PackageName, IReadOnlyList<WorkshopInstallRule> Rules);

    private static WorkshopManifest? ReadWorkshopManifest(string itemDir)
    {
        var infoJson = Path.Combine(itemDir, "Info.json");
        if (!File.Exists(infoJson)) return null;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(infoJson));
            var root = document.RootElement;
            string? Read(string key) => root.TryGetProperty(key, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
            var modName = Read("ModName") ?? Read("Title") ?? Read("Name");
            var packageName = Read("PackageName");

            var rules = new List<WorkshopInstallRule>();
            if (root.TryGetProperty("InstallRule", out var ruleArray) && ruleArray.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var rule in ruleArray.EnumerateArray())
                {
                    if (rule.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
                    var type = rule.TryGetProperty("Type", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String ? t.GetString() ?? "" : "";
                    var isServer = rule.TryGetProperty("IsServer", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.True;
                    var targets = new List<string>();
                    if (rule.TryGetProperty("Targets", out var targetArray) && targetArray.ValueKind == System.Text.Json.JsonValueKind.Array)
                        foreach (var target in targetArray.EnumerateArray())
                            if (target.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var value = target.GetString();
                                if (!string.IsNullOrWhiteSpace(value)) targets.Add(value.TrimStart('.', '/', '\\'));
                            }
                    if (!string.IsNullOrWhiteSpace(type)) rules.Add(new WorkshopInstallRule(type, isServer, targets));
                }
            }
            return new WorkshopManifest(modName, packageName, rules);
        }
        catch { return null; }
    }

    private static string SanitizePackageName(string name)
    {
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "WorkshopMod" : cleaned;
    }

    private static int CopyDirectoryTree(string source, string destination)
    {
        var copied = 0;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
            copied++;
        }
        return copied;
    }

    private static string[] SafeEnumerateFiles(string path)
    {
        try { return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).ToArray(); }
        catch { return []; }
    }

    private static IReadOnlyList<string> DiscoverWorkshopContentRoots()
    {
        if (!OperatingSystem.IsWindows()) return [];
        var roots = new List<string>();
        foreach (var steamPath in DiscoverSteamInstallPaths())
        {
            var normalized = steamPath.Replace('/', Path.DirectorySeparatorChar);
            AddWorkshopRootIfExists(roots, normalized);
            var vdf = Path.Combine(normalized, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            try
            {
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                {
                    var library = match.Groups[1].Value.Replace("\\\\", "\\").Replace('/', Path.DirectorySeparatorChar);
                    AddWorkshopRootIfExists(roots, library);
                }
            }
            catch { }
        }
        return roots;
    }

    private static void AddWorkshopRootIfExists(List<string> roots, string libraryRoot)
    {
        try
        {
            var workshopContent = Path.Combine(libraryRoot, "steamapps", "workshop", "content", PalworldSteamAppId.ToString());
            if (Directory.Exists(workshopContent) && !roots.Contains(workshopContent, StringComparer.OrdinalIgnoreCase))
                roots.Add(workshopContent);
        }
        catch { }
    }

    private static IEnumerable<string> DiscoverSteamInstallPaths()
    {
        if (!OperatingSystem.IsWindows()) yield break;
        string? fromRegistry = null;
        try { fromRegistry = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null)?.ToString(); } catch { }
        if (!string.IsNullOrWhiteSpace(fromRegistry)) yield return fromRegistry!;

        string? fromMachine = null;
        try { fromMachine = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null)?.ToString(); } catch { }
        if (!string.IsNullOrWhiteSpace(fromMachine)) yield return fromMachine!;
    }
}

public sealed record HeadlessModRuntimeEvidence(IReadOnlyDictionary<string, string> Started, IReadOnlySet<string> Active);

public sealed record HeadlessWorkshopItem(
    string WorkshopId, string Name, string SuggestedPackage, string ContentKind, bool AlreadyInstalled, string LocalPath);

public sealed record HeadlessWorkshopScanResult(bool Available, string Detail, IReadOnlyList<HeadlessWorkshopItem> Items);

public sealed record HeadlessUe4ssStatus(
    string RuntimeBinaryRoot, string Ue4ssRoot, string ModernModsRoot, string LegacyModsRoot,
    string ActiveModsRoot, string? RuntimeModsRoot, string DetectionMethod,
    bool HasUe4ssRoot, bool HasModernModsRoot, bool HasLegacyModsRoot,
    bool RuntimeVerified, bool RuntimeMatchesActiveRoot, string HealthState,
    string WarningMessage, string? RuntimeLogPath, int ActiveModDirectoryCount, int LegacyModDirectoryCount,
    string InstalledVersion)
{
    public bool HasPathMismatch => RuntimeVerified && !RuntimeMatchesActiveRoot;
}

// UpdateAvailable/UpdateHint default to false/empty for every construction site below, then get
// filled in once per inventory scan (GetInventoryAsync) by reusing CheckModUpdateAsync's own
// cheap, local, no-network comparison against Steam's local Workshop content cache -- direct
// request: "under installed MODs card it should have the info if there is an updated version."
public sealed record HeadlessModItem(
    string Type, string Package, string Name, string InstallPath, bool Enabled,
    string Health, string RuntimeState, string Evidence, int FileCount,
    bool RuntimeConfirmed, string Attention,
    bool UpdateAvailable = false, string UpdateHint = "", string? InstalledVersion = null);

public sealed record HeadlessModInventory(
    string Platform, bool ServerRunning, int Installed, int RuntimeConfirmed,
    int ActiveUnverified, int Disabled, int ConfirmedIssues, string OverallHealth,
    string Summary, HeadlessUe4ssStatus Ue4ss, IReadOnlyList<HeadlessModItem> Mods,
    DateTimeOffset ObservedAt);

public sealed record HeadlessModVerificationResult(
    int Installed, int VerifiedOrNeutral, int RuntimeConfirmed, int ActiveUnverified,
    int Disabled, int Attention, string OverallHealth, string Summary,
    DateTimeOffset VerifiedAt, IReadOnlyList<HeadlessModItem> Mods);

public sealed record HeadlessModMutationResult(
    bool Success, string Type, string Package, bool Enabled, int Changed, string Message)
{
    public static HeadlessModMutationResult Failure(string message) =>
        new(false, string.Empty, string.Empty, false, 0, message);
}

// v0.7.41.0: HasKnownSource is false when no local Workshop item matches this package at all
// (nothing to compare against); UpdateAvailable is only meaningful when HasKnownSource is true.
// WorkshopId, when present, is what the client passes to the existing ImportWorkshopItemAsync
// route to actually apply the update -- Update reuses that already-built import logic unchanged.
public sealed record HeadlessModUpdateCheckResult(
    bool HasKnownSource, bool UpdateAvailable, string? WorkshopId, string Detail);

// v0.7.55.0: website-sourced MOD descriptions. Source is a display label ("Steam Workshop",
// "GitHub Repository", "Manual Link", or "None"), never a machine-parsed enum -- the desktop only
// ever shows it, never branches on it.
public sealed record HeadlessModDescriptionResult(
    bool Available, string Source, string? Title, string? Description, string? SourceUrl, DateTime? FetchedAtUtc, string Detail);

public sealed class ModDescriptionCacheEntry
{
    public string Source { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? SourceUrl { get; set; }
    // Set only when the user explicitly assigned a Source URL (e.g. a GitHub repo) for a MOD with
    // no local Steam Workshop match. Its presence is what makes GetModDescriptionAsync prefer the
    // manual source over a Workshop match on every subsequent call, not just the one that set it.
    public string? ManualSourceUrl { get; set; }
    public DateTime? FetchedAtUtc { get; set; }
}

internal sealed class SteamPublishedFileResponse
{
    [JsonPropertyName("response")] public SteamPublishedFileResponseBody? Response { get; init; }
}
internal sealed class SteamPublishedFileResponseBody
{
    [JsonPropertyName("publishedfiledetails")] public List<SteamPublishedFileDetail>? PublishedFileDetails { get; init; }
}
internal sealed class SteamPublishedFileDetail
{
    [JsonPropertyName("result")] public int Result { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("file_description")] public string? FileDescription { get; init; }
}
internal sealed class GitHubRepoDto
{
    [JsonPropertyName("full_name")] public string? FullName { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("html_url")] public string? HtmlUrl { get; init; }
}

public sealed record Ue4ssInstallPreview(
    string Token, string Source, string TagName, string? AssetName, long? AssetSizeBytes,
    string AssetDownloadUrl, string CurrentLayout, string CurrentVersion,
    DateTimeOffset PreviewedAt, DateTimeOffset ExpiresAt, string Summary);

public sealed record Ue4ssInstallResult(bool Success, string? InstalledVersion, string? TargetRoot, string Message)
{
    public static Ue4ssInstallResult Failure(string message) => new(false, null, null, message);
}

internal sealed record Ue4ssSnapshotMeta(string[] PlannedPaths, string[] ExistedPaths, DateTimeOffset CapturedAt);

// v0.7.60.0: records which release catalog entry ApplyUe4ssInstallAsync actually installed --
// see that method and DetectUe4ssVersion() for why this is the only reliable source of the real
// installed UE4SS version this fork provides no marker file for.
internal sealed record Ue4ssInstallManifest(string Source, string TagName, DateTimeOffset InstalledAtUtc);

internal sealed record Ue4ssRuntimeVerification(DateTimeOffset VerifiedAtUtc);
