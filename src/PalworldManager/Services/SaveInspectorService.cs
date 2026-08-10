using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class SaveInspectorService
{
    private readonly AppSettings settings;
    private readonly IPalworldSaveCodec codec;
    private readonly Plm1SaveDecoder decoder;
    private readonly ActiveWorldContextService? worldContext;
    private readonly SafeWorldSaveSnapshotService snapshotService;

    public SaveInspectorService(
        AppSettings settings,
        ActiveWorldContextService? worldContext = null,
        SafeWorldSaveSnapshotService? snapshotService = null)
    {
        this.settings = settings;
        this.worldContext = worldContext;
        this.snapshotService = snapshotService ?? new SafeWorldSaveSnapshotService();
        codec = new ProcessPalworldSaveCodec(settings);
        decoder = new Plm1SaveDecoder(codec);
    }

    public string FindActiveWorldPath()
    {
        if (worldContext is not null) return worldContext.Current().WorldPath;
        var root = Path.Combine(settings.SaveRoot, "0");
        if (!Directory.Exists(root)) return "";
        return Directory.EnumerateDirectories(root)
            .Where(path => File.Exists(Path.Combine(path, "Level.sav")))
            .OrderByDescending(path => File.GetLastWriteTimeUtc(Path.Combine(path, "Level.sav")))
            .FirstOrDefault() ?? "";
    }

    public SaveInspectorSummary Inspect(string selectedPath)
    {
        var worldPath = ResolveWorldPath(selectedPath);
        var levelPath = Path.Combine(worldPath, "Level.sav");
        WorldSaveSnapshot? safeSnapshot = null;
        PalworldSaveHeader header;
        DateTime stableWriteUtc;
        try
        {
            safeSnapshot = snapshotService.CreateSnapshot(levelPath);
            header = decoder.Inspect(safeSnapshot.SnapshotPath);
            header.Path = levelPath;
            stableWriteUtc = safeSnapshot.SourceWriteUtc;
        }
        finally
        {
            snapshotService.Release(safeSnapshot);
        }

        var summary = new SaveInspectorSummary
        {
            WorldPath = worldPath,
            WorldId = Path.GetFileName(worldPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            LevelSavePath = levelPath,
            Header = header,
            CodecAvailable = codec.IsAvailable(),
            LastWriteUtc = stableWriteUtc,
            HasLevelMeta = File.Exists(Path.Combine(worldPath, "LevelMeta.sav")),
            HasLocalData = File.Exists(Path.Combine(worldPath, "LocalData.sav")),
            HasWorldOption = File.Exists(Path.Combine(worldPath, "WorldOption.sav"))
        };

        if (safeSnapshot?.RequiredRetry == true)
            summary.Warnings.Add($"Live-save read stabilized after {safeSnapshot.AttemptCount} attempts.");

        summary.CodecStatus = summary.CodecAvailable
            ? "Configured and available"
            : "Not configured — header inspection works, decoded entity inspection is unavailable";

        // Palworld writes through transient ~RF*.TMP files and may atomically replace
        // saves while inspection is running. Work from a snapshot and skip vanished
        // or temporary entries instead of failing the entire inspector.
        foreach (var file in SafeEnumerateWorldFiles(worldPath))
        {
            FileInfo info;
            try
            {
                info = new FileInfo(file);
                if (!info.Exists || IsTransientSaveFile(info.Name)) continue;
                _ = info.Length; // force metadata read while guarded
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            var relative = Path.GetRelativePath(worldPath, file);
            var isBackup = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals("backup", StringComparison.OrdinalIgnoreCase));
            var isDps = file.EndsWith("_dps.sav", StringComparison.OrdinalIgnoreCase);
            var inPlayers = relative.StartsWith("Players" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                         || relative.StartsWith("Players" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

            var category = isBackup ? "Backup" : inPlayers ? (isDps ? "Derived player data" : "Player save") : Classify(file);
            var status = isBackup ? "Excluded from live world" : isDps ? "Not counted as a player" : "Live world file";
            summary.Files.Add(new SaveInspectorFileRow
            {
                Name = info.Name,
                RelativePath = relative,
                Category = category,
                Status = status,
                SizeBytes = info.Length,
                LastWriteUtc = info.LastWriteTimeUtc,
                IsBackup = isBackup,
                IsDerived = isDps,
                IsRequired = !isBackup && IsRequiredWorldFile(info.Name),
                IsOptional = !isBackup && IsOptionalWorldFile(info.Name)
            });
            summary.TotalWorldBytes += info.Length;
            if (!isBackup && inPlayers && file.EndsWith(".sav", StringComparison.OrdinalIgnoreCase))
            {
                if (isDps) summary.DerivedPlayerFileCount++;
                else summary.PlayerSaveCount++;
            }
        }

        summary.BackupFolderCount = SafeEnumerateWorldDirectories(worldPath)
            .Count(path => Path.GetFileName(path).Equals("backup", StringComparison.OrdinalIgnoreCase));
        summary.Files = summary.Files.OrderBy(row => row.Category).ThenBy(row => row.RelativePath).ToList();

        summary.Warnings.AddRange(summary.Header.Warnings);
        if (!summary.HasLevelMeta) summary.Warnings.Add("LevelMeta.sav was not found.");
        if (summary.HasWorldOption) summary.Warnings.Add("WorldOption.sav is present and may override PalWorldSettings.ini.");
        if (summary.PlayerSaveCount == 0) summary.Warnings.Add("No live player save files were found.");
        if (!summary.CodecAvailable) summary.Warnings.Add("Optional Palworld save tooling is not configured. File and header inspection remains available; decoded player, guild and base details require a converter.");
        return summary;
    }

    public string BuildDiagnosticsReport(SaveInspectorSummary summary)
    {
        var health = EvaluateHealth(summary);
        var integrity = AnalyzeIntegrity(summary);
        var repairs = BuildRepairSuggestions(summary);
        var lines = new List<string>
        {
            "MYST PALWORLD SAVE DIAGNOSTICS", $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", $"World: {summary.WorldId}", $"Path: {summary.WorldPath}", $"Container: {summary.ContainerDisplay}", $"Health: {health.Score}% ({health.Overall})", $"Players: {summary.PlayerSaveCount}", $"Derived player files: {summary.DerivedPlayerFileCount}", $"World size: {summary.SizeDisplay}", "", "INTEGRITY FINDINGS"
        };
        lines.AddRange(integrity.Select(x => $"[{x.Severity}] {x.Area}: {x.Finding} Recommendation: {x.Recommendation}"));
        lines.Add(""); lines.Add("REPAIR SUGGESTIONS");
        lines.AddRange(repairs.Select(x => $"[{x.Risk}] {x.Action} -> {x.Target}: {x.Reason}"));
        lines.Add(""); lines.Add("This report is diagnostic only. No save data was modified.");
        return string.Join(Environment.NewLine, lines);
    }

    public List<SaveRepairSuggestion> BuildRepairSuggestions(SaveInspectorSummary summary)
    {
        var list = new List<SaveRepairSuggestion>();
        if (summary.HasWorldOption) list.Add(new() { Action = "Quarantine WorldOption.sav", Target = "WorldOption.sav", Reason = "Prevents archive settings from silently overriding the server configuration.", Risk = "Low" });
        if (summary.DerivedPlayerFileCount > 0) list.Add(new() { Action = "Exclude derived player files", Target = @"Players\*_dps.sav", Reason = "Derived files are not player identities and should not participate in mapping.", Risk = "Low" });
        if (summary.BackupFolderCount > 0) list.Add(new() { Action = "Exclude internal backups", Target = @"backup\", Reason = "Avoid installing stale duplicate world data.", Risk = "Low" });
        if (!summary.HasLevelMeta) list.Add(new() { Action = "Flag incomplete world", Target = summary.WorldId, Reason = "LevelMeta.sav is missing and requires administrator acknowledgement.", Risk = "Medium" });
        if (list.Count == 0) list.Add(new() { Action = "No automatic repairs required", Target = summary.WorldId, Reason = "File-level validation found no repair candidates.", Risk = "None", State = "Informational" });
        return list;
    }

    public List<SaveIntegrityRow> AnalyzeIntegrity(SaveInspectorSummary summary)
    {
        var rows = new List<SaveIntegrityRow>();
        if (!summary.HasLevelMeta) rows.Add(new() { Severity = "Warning", Area = "Metadata", Finding = "LevelMeta.sav is missing.", Recommendation = "Confirm this archive came from a complete world backup." });
        if (!summary.HasLocalData) rows.Add(new() { Severity = "Info", Area = "Local data", Finding = "LocalData.sav is missing.", Recommendation = "Usually safe for dedicated servers; verify client migration expectations." });
        if (summary.HasWorldOption) rows.Add(new() { Severity = "Warning", Area = "Settings", Finding = "WorldOption.sav can override server settings.", Recommendation = "Quarantine or explicitly approve it during import." });
        if (summary.DerivedPlayerFileCount > 0) rows.Add(new() { Severity = "Info", Area = "Players", Finding = $"{summary.DerivedPlayerFileCount} derived _dps file(s) found.", Recommendation = "Do not count these as player identities." });
        if (summary.BackupFolderCount > 0) rows.Add(new() { Severity = "Info", Area = "Backups", Finding = $"{summary.BackupFolderCount} internal backup folder(s) found.", Recommendation = "Keep excluded from the active world payload." });
        if (summary.Header.Kind == PalworldSaveContainerKind.Unknown) rows.Add(new() { Severity = "Info", Area = "Compatibility", Finding = "Readable save container is not classified by the built-in header reader.", Recommendation = "This does not indicate corruption. Configure Palworld save tooling only when decoded entity inspection is required." });
        if (rows.Count == 0) rows.Add(new() { Severity = "Healthy", Area = "World", Finding = "File-level integrity checks passed.", Recommendation = "Proceed to decoded entity validation when a codec is available." });
        return rows;
    }

    public SaveHealthSummary EvaluateHealth(SaveInspectorSummary summary)
    {
        var health = new SaveHealthSummary();
        var levelMissing = !File.Exists(summary.LevelSavePath);
        var levelUnreadable = !levelMissing && summary.Header.Length <= 0;
        var levelTruncated = !levelMissing && summary.Header.Length > 0 && summary.Header.Length < 32;
        var unknownContainer = summary.Header.Kind == PalworldSaveContainerKind.Unknown;

        // The score represents verified save integrity only. Population state, optional
        // decoder capability, WorldOption.sav, LocalData.sav, backup folders and
        // derived _dps files are informational and never lower a healthy fresh world.
        var deductions = 0;

        if (levelMissing)
        {
            health.ErrorCount++;
            deductions += 100;
            health.Findings.Add("Critical • Level.sav is missing. The world cannot be loaded.");
        }
        else if (levelUnreadable)
        {
            health.ErrorCount++;
            deductions += 80;
            health.Findings.Add("Critical • Level.sav could not be read or is empty.");
        }
        else if (levelTruncated)
        {
            health.ErrorCount++;
            deductions += 65;
            health.Findings.Add("Critical • Level.sav is unexpectedly small and may be truncated.");
        }
        else
        {
            health.HealthyCount++;
            health.Findings.Add("Healthy • Level.sav exists and passed the basic size/readability check.");
        }

        if (unknownContainer && !levelMissing && !levelUnreadable && !levelTruncated)
        {
            // New Palworld formats may not yet have a named signature. Treat an
            // otherwise readable save as compatible-but-unclassified, not corrupt.
            health.WarningCount++;
            health.Findings.Add("Advisory • The save container is not classified by the built-in header reader. The file remains structurally readable and no health points were removed.");
        }
        else if (!unknownContainer)
        {
            health.HealthyCount++;
            health.Findings.Add($"Healthy • Recognized {summary.ContainerDisplay} save container.");
        }

        foreach (var warning in summary.Header.Warnings.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (warning.Contains("unexpectedly small", StringComparison.OrdinalIgnoreCase))
                continue; // already represented as a blocking integrity error above
            if (warning.Contains("unknown save header", StringComparison.OrdinalIgnoreCase))
                continue; // represented as a neutral compatibility advisory above
            health.WarningCount++;
            health.Findings.Add("Advisory • Save header: " + warning);
        }

        if (!summary.HasLevelMeta)
            health.Findings.Add("Information • LevelMeta.sav is absent. This is normal for many dedicated-server worlds and does not reduce health.");
        else
            health.HealthyCount++;

        if (!summary.HasLocalData)
            health.Findings.Add("Information • LocalData.sav is absent. This is normal for dedicated servers and does not reduce health.");

        if (summary.HasWorldOption)
            health.Findings.Add("Advisory • WorldOption.sav is present and may override PalWorldSettings.ini. This is a configuration notice, not corruption.");

        if (summary.PlayerSaveCount == 0)
            health.Findings.Add("Information • No live player saves exist yet. This is expected for a brand-new world.");
        else
            health.HealthyCount++;

        if (!summary.CodecAvailable)
            health.Findings.Add("Capability • Entity decoding tools are not configured. File/header integrity checks remain valid and health is unchanged.");

        if (summary.DerivedPlayerFileCount > 0)
            health.Findings.Add($"Information • {summary.DerivedPlayerFileCount} derived _dps file(s) were correctly excluded from the player count.");

        if (summary.BackupFolderCount > 0)
            health.Findings.Add($"Information • {summary.BackupFolderCount} internal backup folder(s) were correctly excluded from live-world analysis.");

        health.Score = Math.Clamp(100 - deductions, 0, 100);
        health.Overall = health.Score switch
        {
            >= 95 => "Excellent",
            >= 85 => "Healthy",
            >= 70 => "Needs Attention",
            >= 40 => "Degraded",
            >= 1 => "Critical",
            _ => "Corrupted"
        };

        if (health.Findings.Count == 0)
            health.Findings.Add("Healthy • No file-level integrity concerns were detected.");

        return health;
    }

    public List<SaveExplorerNode> BuildExplorer(SaveInspectorSummary summary)
    {
        var root = new SaveExplorerNode { Name = summary.WorldId, Kind = "World", Detail = summary.ContainerDisplay, SourcePath = summary.WorldPath };
        foreach (var group in summary.Files.GroupBy(f => f.Category).OrderBy(g => g.Key))
        {
            var branch = new SaveExplorerNode { Name = group.Key, Kind = "Category", Detail = $"{group.Count()} item(s)" };
            foreach (var file in group.OrderBy(f => f.RelativePath))
                branch.Children.Add(new SaveExplorerNode { Name = file.Name, Kind = file.Category, Detail = $"{file.SizeDisplay} • {file.Status}", SourcePath = Path.Combine(summary.WorldPath, file.RelativePath) });
            root.Children.Add(branch);
        }
        return [root];
    }

    public IReadOnlyList<string> GetFileCategories(SaveInspectorSummary summary)
    {
        return summary.Files.Select(x => x.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Prepend("All categories")
            .ToList();
    }

    public IReadOnlyList<SaveInspectorFileRow> FilterFiles(SaveInspectorSummary summary, string? query, string? category)
    {
        IEnumerable<SaveInspectorFileRow> rows = summary.Files;
        var normalizedCategory = category?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedCategory) &&
            !normalizedCategory.Equals("All categories", StringComparison.OrdinalIgnoreCase))
            rows = rows.Where(x => x.Category.Equals(normalizedCategory, StringComparison.OrdinalIgnoreCase));

        var normalizedQuery = query?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
            rows = rows.Where(x => x.RelativePath.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                                || x.Category.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                                || x.Status.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));

        return rows.OrderBy(x => x.Category).ThenBy(x => x.RelativePath).ToList();
    }

    private static bool IsRequiredWorldFile(string name) =>
        name.Equals("Level.sav", StringComparison.OrdinalIgnoreCase);

    private static bool IsOptionalWorldFile(string name) =>
        name.Equals("LevelMeta.sav", StringComparison.OrdinalIgnoreCase)
        || name.Equals("LocalData.sav", StringComparison.OrdinalIgnoreCase)
        || name.Equals("WorldOption.sav", StringComparison.OrdinalIgnoreCase);

    private static string ResolveWorldPath(string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath)) throw new InvalidOperationException("Select a Palworld world folder or Level.sav first.");
        var full = Path.GetFullPath(selectedPath.Trim().Trim('"'));
        if (File.Exists(full))
        {
            if (!Path.GetFileName(full).Equals("Level.sav", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Select Level.sav or its containing world folder.");
            return Path.GetDirectoryName(full) ?? throw new InvalidDataException("The Level.sav parent folder could not be resolved.");
        }
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException(full);
        if (File.Exists(Path.Combine(full, "Level.sav"))) return full;
        var nested = Directory.EnumerateFiles(full, "Level.sav", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(full, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part.Equals("backup", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(path => Path.GetRelativePath(full, path).Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar))
            .FirstOrDefault();
        return nested is null ? throw new FileNotFoundException("No live Level.sav was found under the selected folder.") : Path.GetDirectoryName(nested)!;
    }

    private static IReadOnlyList<string> SafeEnumerateWorldFiles(string worldPath)
    {
        try { return Directory.EnumerateFiles(worldPath, "*", SearchOption.AllDirectories).ToList(); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    private static IReadOnlyList<string> SafeEnumerateWorldDirectories(string worldPath)
    {
        try { return Directory.EnumerateDirectories(worldPath, "*", SearchOption.AllDirectories).ToList(); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    private static bool IsTransientSaveFile(string name) =>
        name.Contains(".TMP", StringComparison.OrdinalIgnoreCase) || name.Contains('~');

    private static string Classify(string path) => Path.GetFileName(path).ToLowerInvariant() switch
    {
        "level.sav" => "World save",
        "levelmeta.sav" => "World metadata",
        "localdata.sav" => "Local world data",
        "worldoption.sav" => "World settings override",
        _ => "Other"
    };
}
