using System.IO.Compression;
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
            var changed = type.Equals("PAK", StringComparison.OrdinalIgnoreCase)
                ? InstallPakFiles(extracted, package)
                : type.Equals("UE4SS", StringComparison.OrdinalIgnoreCase)
                    ? InstallUe4ssFiles(extracted, package)
                    : throw new InvalidOperationException("Only PAK and UE4SS archives are supported.");
            activity.Record("Information", "MODs", "Installed MOD archive", $"type={type.ToUpperInvariant()}; package={package}; files={changed}");
            return new(true, type.ToUpperInvariant(), package, true, changed, $"Installed {package} from validated ZIP archive.");
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
                    foreach (var entry in zip.Entries) entry.ExtractToFile(Path.Combine(flatRoot, entry.FullName), true);
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

        return new HeadlessUe4ssStatus(
            paths.RuntimeBinaryRoot, paths.Ue4ssRoot, modern, legacy, active, runtimeRoot, method,
            Directory.Exists(paths.Ue4ssRoot), Directory.Exists(modern), Directory.Exists(legacy),
            verified, matches, !verified ? "Unverified" : matches ? "Healthy" : "Degraded",
            warning, runtime.LogPath, SafeDirectoryCount(active), SafeDirectoryCount(legacy), DetectUe4ssVersion());
    }

    private string DetectUe4ssVersion()
    {
        foreach (var marker in new[] { "UE4SS.version", "version.txt" })
        {
            var path = Path.Combine(paths.Ue4ssRoot, marker);
            try
            {
                if (File.Exists(path))
                {
                    var value = File.ReadLines(path).FirstOrDefault()?.Trim();
                    if (!string.IsNullOrWhiteSpace(value)) return value;
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
                if (!string.IsNullOrWhiteSpace(version)) return version;
            }
            catch { }
        }

        return Directory.Exists(paths.Ue4ssRoot) ? "Installed — version metadata unavailable" : "Not installed";
    }

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

        var items = new List<HeadlessWorkshopItem>();
        foreach (var root in roots)
        {
            foreach (var itemDir in Directory.EnumerateDirectories(root))
                items.Add(DescribeWorkshopItem(itemDir, installedPackages));
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

    private static HeadlessWorkshopItem DescribeWorkshopItem(string itemDir, HashSet<string> installedPackages)
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
        var installed = installedPackages.Contains(package) || installedPackages.Contains(workshopId);
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

public sealed record HeadlessModItem(
    string Type, string Package, string Name, string InstallPath, bool Enabled,
    string Health, string RuntimeState, string Evidence, int FileCount,
    bool RuntimeConfirmed, string Attention);

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
