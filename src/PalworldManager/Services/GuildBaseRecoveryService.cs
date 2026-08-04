using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class GuildBaseRecoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly AppSettings settings;
    private readonly GuildService guildService;
    private readonly ProcessPalworldSaveCodec codec;

    public GuildBaseRecoveryService(AppSettings settings)
    {
        this.settings = settings;
        guildService = new GuildService(settings);
        codec = new ProcessPalworldSaveCodec(settings);
    }

    public GuildBaseRecoverySummary Scan(string worldPath)
    {
        var snapshot = guildService.LoadSnapshot(worldPath);
        var summary = new GuildBaseRecoverySummary
        {
            WorldPath = worldPath,
            SourceHash = snapshot.SourceHash,
            CodecAvailable = codec.IsAvailable(),
            Guilds = snapshot.Guilds,
            Players = snapshot.Players,
            Warnings = snapshot.Warnings
        };

        foreach (var guild in snapshot.Guilds)
        {
            if (guild.IsOrphaned)
            {
                summary.Findings.Add(new GuildBaseRecoveryFinding
                {
                    Severity = "Warning", Category = "Guild", Action = "Claim orphaned guild",
                    TargetName = guild.Name, TargetId = guild.GuildId,
                    CurrentValue = string.IsNullOrWhiteSpace(guild.LeaderUid) ? "No valid leader" : guild.LeaderUid,
                    ProposedValue = "Select a valid world player", Risk = "Medium",
                    Description = "The guild leader is missing, unresolved, or has no matching player save."
                });
            }

            foreach (var member in guild.Members.Where(m => !m.PlayerSaveExists))
            {
                summary.Findings.Add(new GuildBaseRecoveryFinding
                {
                    Severity = "Warning", Category = "Membership", Action = "Repair membership",
                    TargetName = member.PlayerName, TargetId = member.PlayerUid,
                    CurrentValue = guild.Name, ProposedValue = "Map to a valid player or remove broken reference", Risk = "Medium",
                    Description = "Guild membership references a player whose primary save file was not found."
                });
            }

            foreach (var baseRow in guild.Bases)
            {
                if (string.IsNullOrWhiteSpace(baseRow.OwnerGuildId) || !baseRow.OwnerGuildId.Equals(guild.GuildId, StringComparison.OrdinalIgnoreCase))
                {
                    summary.Findings.Add(new GuildBaseRecoveryFinding
                    {
                        Severity = "Warning", Category = "Base", Action = "Reassign base",
                        TargetName = baseRow.Name, TargetId = baseRow.BaseId,
                        CurrentValue = string.IsNullOrWhiteSpace(baseRow.OwnerGuildId) ? "No guild" : baseRow.OwnerGuildId,
                        ProposedValue = guild.GuildId, Risk = "High",
                        Description = "Base ownership does not match the guild relationship discovered in the save."
                    });
                }
            }
        }

        if (snapshot.Guilds.Count == 0 && snapshot.Warnings.Count == 0)
            summary.Warnings.Add("No guild records were discovered. This is normal for a fresh world, but decoded save tooling is required for imported-world ownership recovery.");
        return summary;
    }

    public GuildBaseRecoveryPlan BuildPlan(GuildBaseRecoverySummary summary, IEnumerable<GuildBaseRecoveryFinding> selected)
    {
        var operations = selected.Select(CloneFinding).ToList();
        var plan = new GuildBaseRecoveryPlan
        {
            WorldPath = summary.WorldPath,
            SourceHash = summary.SourceHash,
            CodecAvailable = summary.CodecAvailable,
            Operations = operations
        };
        if (operations.Count == 0) plan.ValidationMessages.Add("ERROR: Select at least one recovery operation.");
        if (!File.Exists(Path.Combine(summary.WorldPath, "Level.sav"))) plan.ValidationMessages.Add("ERROR: Level.sav is missing.");
        if (string.IsNullOrWhiteSpace(summary.SourceHash)) plan.ValidationMessages.Add("ERROR: The source save hash could not be calculated.");
        if (!summary.CodecAvailable) plan.ValidationMessages.Add("Save codec is not configured. The plan can be exported, but coordinated Level.sav writing remains disabled.");
        if (operations.Any(o => o.Risk.Equals("High", StringComparison.OrdinalIgnoreCase)))
            plan.ValidationMessages.Add("High-risk base ownership changes require a stopped server, complete world backup, encode/decode round trip, and post-write relationship validation.");
        plan.ValidationMessages.Add("No file-only ownership edits are permitted. Guild, player and base references must be updated transactionally.");
        return plan;
    }

    public string CreateSafetyBackup(string worldPath)
    {
        if (!Directory.Exists(worldPath)) throw new DirectoryNotFoundException(worldPath);
        var root = Path.Combine(settings.BackupRoot, "GuildBaseRecovery");
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, $"GuildBaseRecovery_{Path.GetFileName(worldPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(worldPath, zipPath, CompressionLevel.Optimal, false);
        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.GetEntry("Level.sav") is null) throw new InvalidDataException("Safety backup verification failed: Level.sav is missing from the archive.");
        return zipPath;
    }

    public string SavePlan(GuildBaseRecoveryPlan plan)
    {
        var root = Path.Combine(settings.BackupRoot, "GuildBaseRecovery", "Plans");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"guild-base-recovery-{DateTime.Now:yyyyMMdd_HHmmss}-{plan.PlanId[..8]}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(plan, JsonOptions), Encoding.UTF8);
        return path;
    }

    public string ExportReport(GuildBaseRecoverySummary summary, GuildBaseRecoveryPlan? plan)
    {
        var root = Path.Combine(settings.BackupRoot, "GuildBaseRecovery", "Reports");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"guild-base-recovery-report-{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        var lines = new List<string>
        {
            "MystTiq Guild & Base Recovery Report", $"Generated UTC: {DateTime.UtcNow:O}", $"World: {summary.WorldPath}",
            $"Source SHA-256: {summary.SourceHash}", $"Codec available: {summary.CodecAvailable}",
            $"Guilds: {summary.Guilds.Count}", $"Orphaned guilds: {summary.OrphanedGuildCount}",
            $"Bases: {summary.BaseCount}", $"Missing player saves: {summary.MissingPlayerSaveCount}", ""
        };
        lines.AddRange(summary.Warnings.Select(w => "WARNING: " + w));
        lines.Add(""); lines.Add("FINDINGS");
        lines.AddRange(summary.Findings.Select(f => $"[{f.Severity}] {f.Category} / {f.Action}: {f.TargetName} ({f.TargetId}) - {f.Description}"));
        if (plan is not null)
        {
            lines.Add(""); lines.Add("SELECTED OPERATIONS");
            lines.AddRange(plan.Operations.Select(o => $"{o.Action}: {o.TargetName} ({o.TargetId}) | {o.CurrentValue} -> {o.ProposedValue} | Risk {o.Risk}"));
            lines.AddRange(plan.ValidationMessages.Select(m => "VALIDATION: " + m));
        }
        File.WriteAllLines(path, lines, Encoding.UTF8);
        return path;
    }

    public bool VerifySourceUnchanged(GuildBaseRecoveryPlan plan)
    {
        var level = Path.Combine(plan.WorldPath, "Level.sav");
        return File.Exists(level) && Hash(level).Equals(plan.SourceHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static GuildBaseRecoveryFinding CloneFinding(GuildBaseRecoveryFinding source) => new()
    {
        IsSelected = source.IsSelected, Severity = source.Severity, Category = source.Category, Action = source.Action,
        TargetName = source.TargetName, TargetId = source.TargetId, CurrentValue = source.CurrentValue,
        ProposedValue = source.ProposedValue, Risk = source.Risk, State = source.State, Description = source.Description
    };
}
