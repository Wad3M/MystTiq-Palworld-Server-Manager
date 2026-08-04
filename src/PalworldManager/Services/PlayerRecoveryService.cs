using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class PlayerRecoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly AppSettings settings;
    private readonly PlayerHistoryService history;
    private readonly ProcessPalworldSaveCodec codec;

    public PlayerRecoveryService(AppSettings settings, PlayerHistoryService history)
    {
        this.settings = settings;
        this.history = history;
        codec = new ProcessPalworldSaveCodec(settings);
    }

    public bool CodecAvailable => codec.IsAvailable();

    public PlayerRecoverySummary Scan(string worldPath)
    {
        if (string.IsNullOrWhiteSpace(worldPath) || !Directory.Exists(worldPath))
            throw new DirectoryNotFoundException("Select a valid Palworld world folder.");

        var playersPath = Path.Combine(worldPath, "Players");
        var summary = new PlayerRecoverySummary { WorldPath = worldPath };
        if (!Directory.Exists(playersPath)) return summary;

        var historyRows = history.Snapshot();
        foreach (var savePath in Directory.EnumerateFiles(playersPath, "*.sav", SearchOption.TopDirectoryOnly)
                     .Where(path => !Path.GetFileNameWithoutExtension(path).EndsWith("_dps", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var guid = Path.GetFileNameWithoutExtension(savePath).Trim();
            if (!IsGuidToken(guid)) continue;
            var info = new FileInfo(savePath);
            var match = historyRows.FirstOrDefault(row =>
                row.PlayerId.Equals(guid, StringComparison.OrdinalIgnoreCase) ||
                row.UserId.Equals(guid, StringComparison.OrdinalIgnoreCase));
            var companion = Path.Combine(playersPath, guid + "_dps.sav");
            var isHost = guid.Equals("00000000000000000000000000000001", StringComparison.OrdinalIgnoreCase);
            summary.Players.Add(new PlayerRecoveryRow
            {
                PlayerGuid = guid,
                DisplayName = string.IsNullOrWhiteSpace(match?.Name) ? (isHost ? "Local/co-op host" : "Unknown imported player") : match!.Name,
                PlatformId = match?.SteamId ?? match?.UserId ?? "",
                SavePath = savePath,
                CompanionPath = File.Exists(companion) ? companion : "",
                SizeBytes = info.Length,
                LastWriteUtc = info.LastWriteTimeUtc,
                IsHostCandidate = isHost,
                HasCompanion = File.Exists(companion),
                HasHistoryMatch = match is not null,
                Status = match is not null ? "Matched to player history" : isHost ? "Host migration candidate" : "Unmapped save"
            });
        }
        return summary;
    }

    public PlayerRecoveryPlan BuildPlan(PlayerRecoveryRow source, string destinationGuid, string worldPath)
    {
        destinationGuid = NormalizeGuid(destinationGuid);
        var playersPath = Path.Combine(worldPath, "Players");
        var destinationPath = Path.Combine(playersPath, destinationGuid + ".sav");
        var plan = new PlayerRecoveryPlan
        {
            WorldPath = worldPath,
            SourcePlayerGuid = source.PlayerGuid,
            DestinationPlayerGuid = destinationGuid,
            SourceSavePath = source.SavePath,
            DestinationSavePath = destinationPath,
            SourceIsHostCandidate = source.IsHostCandidate,
            DestinationExists = File.Exists(destinationPath),
            CodecAvailable = CodecAvailable,
            MappingMethod = source.IsHostCandidate ? "Host migration" : "Manual GUID rebind"
        };

        if (!File.Exists(source.SavePath)) plan.ValidationMessages.Add("Source player save is missing.");
        if (source.PlayerGuid.Equals(destinationGuid, StringComparison.OrdinalIgnoreCase)) plan.ValidationMessages.Add("Source and destination GUIDs are identical.");
        if (plan.DestinationExists) plan.ValidationMessages.Add("A player save already exists for the destination GUID.");
        if (!CodecAvailable) plan.ValidationMessages.Add("Palworld save tooling is not configured. The plan can be exported, but coordinated Level.sav/player-save rewriting cannot run.");
        plan.ValidationMessages.Add("Player migration requires coordinated replacement of player, guild, ownership and Level.sav references. File renaming alone is intentionally blocked.");
        return plan;
    }

    public string CreateSafetyBackup(PlayerRecoveryRow source, string worldPath)
    {
        var root = Path.Combine(settings.BackupRoot, "PlayerRecovery", DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + source.PlayerGuid[..Math.Min(8, source.PlayerGuid.Length)]);
        Directory.CreateDirectory(root);
        File.Copy(source.SavePath, Path.Combine(root, Path.GetFileName(source.SavePath)), true);
        if (source.HasCompanion && File.Exists(source.CompanionPath))
            File.Copy(source.CompanionPath, Path.Combine(root, Path.GetFileName(source.CompanionPath)), true);
        var level = Path.Combine(worldPath, "Level.sav");
        var meta = Path.Combine(worldPath, "LevelMeta.sav");
        if (File.Exists(level)) File.Copy(level, Path.Combine(root, "Level.sav"), true);
        if (File.Exists(meta)) File.Copy(meta, Path.Combine(root, "LevelMeta.sav"), true);
        return root;
    }

    public string ExportPlayerPackage(PlayerRecoveryRow source, string worldPath, string outputZip)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputZip)!);
        if (File.Exists(outputZip)) File.Delete(outputZip);
        using var archive = ZipFile.Open(outputZip, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(source.SavePath, "Players/" + Path.GetFileName(source.SavePath), CompressionLevel.Optimal);
        if (source.HasCompanion && File.Exists(source.CompanionPath))
            archive.CreateEntryFromFile(source.CompanionPath, "Players/" + Path.GetFileName(source.CompanionPath), CompressionLevel.Optimal);
        var metadata = JsonSerializer.Serialize(new
        {
            source.PlayerGuid,
            source.DisplayName,
            source.PlatformId,
            source.IsHostCandidate,
            WorldId = Path.GetFileName(worldPath),
            ExportedUtc = DateTime.UtcNow
        }, JsonOptions);
        var entry = archive.CreateEntry("player-recovery-manifest.json", CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(metadata);
        return outputZip;
    }

    public string SavePlan(PlayerRecoveryPlan plan)
    {
        var root = Path.Combine(settings.BackupRoot, "PlayerRecovery", "Plans");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"player-recovery-{DateTime.Now:yyyyMMdd_HHmmss}-{plan.PlanId[..8]}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(plan, JsonOptions));
        return path;
    }

    public static bool IsGuidToken(string value) => Regex.IsMatch(value ?? "", "^[0-9A-Fa-f]{32}$");

    public static string NormalizeGuid(string value)
    {
        var normalized = Regex.Replace(value ?? "", "[^0-9A-Fa-f]", "").ToUpperInvariant();
        if (!IsGuidToken(normalized)) throw new InvalidDataException("Destination player GUID must contain exactly 32 hexadecimal characters.");
        return normalized;
    }
}
