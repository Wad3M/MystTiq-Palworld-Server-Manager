using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class RepairCenterService
{
    private readonly AppSettings settings;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public RepairCenterService(AppSettings settings) => this.settings = settings;

    public RepairCenterSession CreateSession(SaveInspectorSummary inspection, SaveInspectorService inspector)
    {
        var session = new RepairCenterSession
        {
            WorldPath = inspection.WorldPath,
            WorldId = inspection.WorldId,
            SourceHash = File.Exists(inspection.LevelSavePath) ? HashFile(inspection.LevelSavePath) : ""
        };

        foreach (var finding in inspector.AnalyzeIntegrity(inspection))
        {
            var severity = ParseSeverity(finding.Severity);
            if (severity == RepairCenterSeverity.Information) continue;
            session.Items.Add(new RepairCenterItem
            {
                Severity = severity,
                Category = finding.Area,
                Action = ActionFor(finding),
                Target = finding.Finding,
                Reason = finding.Recommendation,
                Risk = severity == RepairCenterSeverity.Critical ? "High" : "Low",
                RequiresDecodedSave = finding.Area.Contains("Ownership", StringComparison.OrdinalIgnoreCase)
                    || finding.Area.Contains("Guild", StringComparison.OrdinalIgnoreCase)
                    || finding.Area.Contains("Player", StringComparison.OrdinalIgnoreCase)
            });
        }

        foreach (var suggestion in inspector.BuildRepairSuggestions(inspection))
        {
            if (session.Items.Any(x => x.Action.Equals(suggestion.Action, StringComparison.OrdinalIgnoreCase)
                && x.Target.Equals(suggestion.Target, StringComparison.OrdinalIgnoreCase))) continue;
            session.Items.Add(new RepairCenterItem
            {
                Severity = suggestion.Risk.Equals("High", StringComparison.OrdinalIgnoreCase)
                    ? RepairCenterSeverity.Warning : RepairCenterSeverity.Recommendation,
                Category = "File hygiene",
                Action = suggestion.Action,
                Target = suggestion.Target,
                Reason = suggestion.Reason,
                Risk = suggestion.Risk
            });
        }

        if (session.Items.Count == 0)
        {
            session.Items.Add(new RepairCenterItem
            {
                Severity = RepairCenterSeverity.Information,
                Category = "World health",
                Action = "No repair required",
                Target = inspection.WorldId,
                Reason = "No repairable file-level findings were detected. Decoded relationship checks can be run when the save codec is available.",
                Risk = "None",
                RequiresServerStopped = false,
                State = RepairCenterState.Skipped
            });
        }
        return session;
    }

    public string CreateBackup(RepairCenterSession session)
    {
        ValidateWorld(session.WorldPath);
        var root = Path.Combine(settings.BackupRoot ?? Path.Combine(settings.ServerRoot ?? "", "Backups"), "RepairCenter");
        Directory.CreateDirectory(root);
        var destination = Path.Combine(root, $"RepairCenter_{session.WorldId}_{DateTime.Now:yyyyMMdd_HHmmss_fff}");
        CopyDirectory(session.WorldPath, destination);
        File.WriteAllText(Path.Combine(destination, "repair-center-backup.json"), JsonSerializer.Serialize(new
        {
            session.SessionId,
            session.WorldId,
            source = session.WorldPath,
            sourceHash = session.SourceHash,
            createdUtc = DateTime.UtcNow
        }, JsonOptions));
        session.BackupPath = destination;
        foreach (var item in session.Items.Where(x => x.Selected)) item.State = RepairCenterState.BackedUp;
        return destination;
    }

    public string WritePreview(RepairCenterSession session)
    {
        if (session.SelectedCount == 0) throw new InvalidOperationException("Select at least one repair action.");
        if (!session.HasBackup) throw new InvalidOperationException("Create a safety backup before preparing repairs.");
        ValidateUnchanged(session);
        var root = Path.Combine(Path.GetTempPath(), "MystTiqPalworldServer", "RepairCenter", session.SessionId);
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "repair-plan.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            session.SessionId,
            session.WorldId,
            session.WorldPath,
            session.SourceHash,
            session.BackupPath,
            createdUtc = DateTime.UtcNow,
            mode = "PreviewOnly",
            operations = session.Items.Where(x => x.Selected).Select(x => new
            {
                x.Id,
                severity = x.Severity.ToString(),
                x.Category,
                x.Action,
                x.Target,
                x.Reason,
                x.Risk,
                x.RequiresDecodedSave,
                x.RequiresServerStopped
            })
        }, JsonOptions));
        session.PreviewPath = path;
        foreach (var item in session.Items.Where(x => x.Selected)) item.State = RepairCenterState.Previewed;
        return path;
    }

    public string BuildReport(RepairCenterSession session, IEnumerable<RepairCenterLogRow> log)
    {
        var lines = new List<string>
        {
            "MystTiq Palworld Server - Repair Center Report",
            $"Session: {session.SessionId}",
            $"World: {session.WorldId}",
            $"Path: {session.WorldPath}",
            $"Created: {session.CreatedUtc:u}",
            $"Source hash: {session.SourceHash}",
            $"Backup: {(session.HasBackup ? session.BackupPath : "Not created")}",
            $"Preview: {(!string.IsNullOrWhiteSpace(session.PreviewPath) ? session.PreviewPath : "Not created")}",
            "",
            "REPAIR ITEMS"
        };
        foreach (var item in session.Items)
            lines.Add($"[{item.Severity}] [{item.State}] {(item.Selected ? "SELECTED" : "NOT SELECTED")} | {item.Category} | {item.Action} | {item.Target} | Risk={item.Risk} | {item.Reason}");
        lines.Add("");
        lines.Add("SESSION LOG");
        foreach (var row in log) lines.Add($"{row.TimestampUtc:u} | {row.Stage} | {row.Result} | {row.Message}");
        lines.Add("");
        lines.Add("No save data is modified by the v2.9.8.3 Repair Center foundation. Plans are preview-only until a validated transactional repair adapter is available.");
        return string.Join(Environment.NewLine, lines);
    }

    public void ValidateUnchanged(RepairCenterSession session)
    {
        ValidateWorld(session.WorldPath);
        var current = HashFile(Path.Combine(session.WorldPath, "Level.sav"));
        if (!string.Equals(current, session.SourceHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Level.sav changed after the repair session was created. Refresh the Repair Center before continuing.");
    }

    private static void ValidateWorld(string worldPath)
    {
        if (string.IsNullOrWhiteSpace(worldPath) || !Directory.Exists(worldPath)) throw new DirectoryNotFoundException("The selected world folder was not found.");
        if (!File.Exists(Path.Combine(worldPath, "Level.sav"))) throw new FileNotFoundException("Level.sav was not found in the selected world.");
    }

    private static RepairCenterSeverity ParseSeverity(string value) => value.ToLowerInvariant() switch
    {
        "critical" or "error" => RepairCenterSeverity.Critical,
        "warning" => RepairCenterSeverity.Warning,
        "recommendation" => RepairCenterSeverity.Recommendation,
        _ => RepairCenterSeverity.Information
    };

    private static string ActionFor(SaveIntegrityRow row)
    {
        if (row.Finding.Contains("missing", StringComparison.OrdinalIgnoreCase)) return "Restore missing data";
        if (row.Finding.Contains("orphan", StringComparison.OrdinalIgnoreCase)) return "Repair ownership";
        if (row.Finding.Contains("duplicate", StringComparison.OrdinalIgnoreCase)) return "Resolve duplicate mapping";
        if (row.Area.Contains("Backup", StringComparison.OrdinalIgnoreCase)) return "Create verified backup";
        return "Review finding";
    }

    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var dir in Directory.EnumerateDirectories(source)) CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }
}
