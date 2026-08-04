using System.Text.Json.Nodes;
using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class CharacterResetService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> IdentityKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "player_uid", "player_id", "account_uid", "owner_player_uid", "individual_character_id",
        "individual_character_handle_id", "character_id", "cached_player_uid", "last_owner_player_uid"
    };
    private static readonly string[] ContextTokens =
    {
        "player", "guild", "respawn", "spawn", "character", "owner", "member", "registration"
    };

    private readonly AppSettings settings;
    private readonly PalworldSaveCodec codec;

    public CharacterResetService(AppSettings settings)
    {
        this.settings = settings;
        codec = new PalworldSaveCodec(settings);
    }

    public CharacterResetPreview Preview(PlayerRow player, string worldPath, bool serverRunning)
    {
        if (string.IsNullOrWhiteSpace(worldPath) || !Directory.Exists(worldPath))
            throw new DirectoryNotFoundException("The active Palworld world folder could not be located.");

        var identifiers = BuildIdentifiers(player);
        if (identifiers.Count == 0)
            throw new InvalidOperationException("The selected player has no usable Player ID, User ID, or Steam ID.");

        var playerSave = ResolvePlayerSave(player, worldPath, identifiers);
        var companion = string.IsNullOrWhiteSpace(playerSave)
            ? ""
            : Path.Combine(Path.GetDirectoryName(playerSave)!, Path.GetFileNameWithoutExtension(playerSave) + "_dps.sav");
        var level = Path.Combine(worldPath, "Level.sav");
        var preview = new CharacterResetPreview
        {
            PlayerName = player.Name,
            PlayerGuid = identifiers.FirstOrDefault(IsGuidToken) ?? player.PlayerId,
            WorldPath = worldPath,
            LevelSavePath = level,
            PlayerSavePath = playerSave,
            CompanionSavePath = File.Exists(companion) ? companion : "",
            CodecAvailable = !string.IsNullOrWhiteSpace(codec.FindConverter()),
            ServerMustBeStopped = serverRunning,
            Identifiers = identifiers.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            PlayerSaveSizeBytes = File.Exists(playerSave) ? new FileInfo(playerSave).Length : 0
        };

        if (!File.Exists(level)) preview.Findings.Add("Level.sav is missing from the selected world.");
        if (!File.Exists(playerSave)) preview.Findings.Add("The primary player save could not be resolved.");
        if (!preview.CodecAvailable) preview.Findings.Add("palworld-save-tools is not configured; Level.sav cannot be transactionally repaired.");
        if (serverRunning) preview.Findings.Add("PalServer is running. Stop it before applying a character reset.");

        if (preview.CodecAvailable && File.Exists(level))
        {
            var temp = CreateTempDirectory("Preview");
            try
            {
                var stagedLevel = Path.Combine(temp, "Level.sav");
                File.Copy(level, stagedLevel, true);
                var jsonPath = codec.Decode(stagedLevel, true);
                var text = File.ReadAllText(jsonPath);
                preview.ExactReferenceCount = identifiers.Sum(id => CountOccurrences(text, id));
                preview.Findings.Add($"Decoded Level.sav contains {preview.ExactReferenceCount} exact identifier occurrence(s) for this player.");
            }
            catch (Exception ex)
            {
                preview.Findings.Add("Level.sav preview decode failed: " + ex.Message);
            }
            finally { TryDeleteDirectory(temp); }
        }

        if (preview.Findings.Count == 0)
            preview.Findings.Add("The player save and Level.sav are ready for a transactional reset.");
        return preview;
    }

    public CharacterResetResult Apply(CharacterResetPreview preview)
    {
        if (preview.ServerMustBeStopped)
            throw new InvalidOperationException("PalServer must be stopped before resetting a character.");
        if (!preview.CodecAvailable)
            throw new InvalidOperationException("palworld-save-tools is required to repair Level.sav safely.");
        if (!File.Exists(preview.LevelSavePath))
            throw new FileNotFoundException("Level.sav was not found.", preview.LevelSavePath);
        if (!File.Exists(preview.PlayerSavePath))
            throw new FileNotFoundException("The selected player save was not found.", preview.PlayerSavePath);

        var transactionId = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..8];
        var backupRoot = Path.Combine(settings.BackupRoot, "CharacterReset");
        Directory.CreateDirectory(backupRoot);
        var backupPath = Path.Combine(backupRoot, $"CharacterReset_{SafeName(preview.PlayerName)}_{transactionId}.zip");
        var working = Path.Combine(preview.WorldPath, ".mysttiq-transactions", transactionId);
        Directory.CreateDirectory(working);
        var result = new CharacterResetResult { TransactionId = transactionId, BackupPath = backupPath };
        var originalLevelCopy = Path.Combine(working, "Level.original.sav");
        var replacementTemp = Path.Combine(preview.WorldPath, $"Level.sav.mysttiq-{transactionId}.tmp");

        try
        {
            CreateBackup(preview, backupPath, transactionId);
            File.Copy(preview.LevelSavePath, originalLevelCopy, true);

            var stagedInput = Path.Combine(working, "Level.input.sav");
            File.Copy(preview.LevelSavePath, stagedInput, true);
            var decodedPath = codec.Decode(stagedInput, true);
            var root = JsonNode.Parse(File.ReadAllText(decodedPath))
                ?? throw new InvalidDataException("Decoded Level.sav JSON is empty.");

            var identifiers = new HashSet<string>(preview.Identifiers, StringComparer.OrdinalIgnoreCase);
            result.ReferencesRemoved = ScrubNode(root, identifiers, "root");
            var modifiedJson = Path.Combine(working, "Level.modified.sav.json");
            File.WriteAllText(modifiedJson, root.ToJsonString(JsonOptions), new UTF8Encoding(false));

            var stagedOutput = Path.Combine(working, "Level.repaired.sav");
            codec.Encode(modifiedJson, stagedOutput);
            ValidateEncodedSave(stagedOutput, preview.LevelSavePath);

            // Verification decode before touching the active world.
            var verifyCopy = Path.Combine(working, "Level.verify.sav");
            File.Copy(stagedOutput, verifyCopy, true);
            var verifyJson = codec.Decode(verifyCopy, true);
            var verifyText = File.ReadAllText(verifyJson);
            var remaining = identifiers.Sum(id => CountOccurrences(verifyText, id));
            if (remaining > 0)
                throw new InvalidDataException($"Verification found {remaining} remaining exact player identifier occurrence(s) in the repaired save.");

            File.Copy(stagedOutput, replacementTemp, true);
            File.Replace(replacementTemp, preview.LevelSavePath, null, true);

            if (preview.Options.RemovePlayerSave && File.Exists(preview.PlayerSavePath))
            {
                File.Delete(preview.PlayerSavePath);
                result.PlayerSaveRemoved = true;
            }
            if (preview.Options.RemovePlayerSave && File.Exists(preview.CompanionSavePath))
            {
                File.Delete(preview.CompanionSavePath);
                result.CompanionSaveRemoved = true;
            }

            result.VerificationPassed = File.Exists(preview.LevelSavePath) && new FileInfo(preview.LevelSavePath).Length > 1024;
            if (!result.VerificationPassed) throw new InvalidDataException("The repaired Level.sav failed the final file validation.");
            result.Messages.Add("Level.sav was decoded, repaired, re-encoded, and verified before activation.");
            result.Messages.Add("Player registration, guild/member references, ownership references, and respawn references matching the selected identifiers were removed when represented in supported JSON structures.");
            result.Messages.Add("The primary player save was removed so Palworld can create a new character on the next login.");
            result.ReportPath = WriteReport(preview, result, working);
            result.Success = true;
            return result;
        }
        catch
        {
            try
            {
                if (File.Exists(originalLevelCopy)) File.Copy(originalLevelCopy, preview.LevelSavePath, true);
                RestorePlayerFilesFromBackup(preview, backupPath);
            }
            catch { /* Preserve the original exception; backup remains available for manual recovery. */ }
            throw;
        }
        finally
        {
            if (File.Exists(replacementTemp)) TryDeleteFile(replacementTemp);
            if (result.Success) TryDeleteDirectory(working);
        }
    }

    private static List<string> BuildIdentifiers(PlayerRow player)
    {
        var values = new[] { player.PlayerId, player.UserId, player.SteamId }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var value in values.ToList())
        {
            var compact = Regex.Replace(value, "[^0-9A-Fa-f]", "");
            if (compact.Length == 32 && !values.Contains(compact, StringComparer.OrdinalIgnoreCase)) values.Add(compact.ToUpperInvariant());
        }
        return values;
    }

    private static string ResolvePlayerSave(PlayerRow player, string worldPath, IReadOnlyCollection<string> identifiers)
    {
        if (!string.IsNullOrWhiteSpace(player.SavePath) && File.Exists(player.SavePath)) return player.SavePath;
        var players = Path.Combine(worldPath, "Players");
        if (!Directory.Exists(players)) return "";
        foreach (var id in identifiers)
        {
            var compact = Regex.Replace(id, "[^0-9A-Fa-f]", "");
            if (compact.Length != 32) continue;
            var candidate = Path.Combine(players, compact.ToUpperInvariant() + ".sav");
            if (File.Exists(candidate)) return candidate;
        }
        return "";
    }

    private static int ScrubNode(JsonNode? node, HashSet<string> identifiers, string context)
    {
        if (node is JsonArray array)
        {
            var removed = 0;
            for (var i = array.Count - 1; i >= 0; i--)
            {
                var item = array[i];
                if (ScalarMatches(item, identifiers) || (item is JsonObject obj && ShouldRemoveObject(obj, identifiers, context)))
                {
                    array.RemoveAt(i);
                    removed++;
                }
                else removed += ScrubNode(item, identifiers, context);
            }
            return removed;
        }
        if (node is not JsonObject jsonObject) return 0;

        var total = 0;
        foreach (var property in jsonObject.ToList())
        {
            var propertyContext = context + "/" + property.Key;
            if (identifiers.Contains(property.Key) && IsRelevantContext(propertyContext))
            {
                jsonObject.Remove(property.Key);
                total++;
                continue;
            }
            if (ScalarMatches(property.Value, identifiers))
            {
                jsonObject.Remove(property.Key);
                total++;
                continue;
            }
            if (property.Value is JsonObject childObject && ShouldRemoveObject(childObject, identifiers, propertyContext))
            {
                jsonObject.Remove(property.Key);
                total++;
                continue;
            }
            total += ScrubNode(property.Value, identifiers, propertyContext);
        }
        return total;
    }

    private static bool ShouldRemoveObject(JsonObject obj, HashSet<string> identifiers, string context)
    {
        foreach (var property in obj)
        {
            if (IdentityKeys.Contains(property.Key) && ScalarMatches(property.Value, identifiers)) return true;
            if (IsRelevantContext(context + "/" + property.Key) && ScalarMatches(property.Value, identifiers)) return true;
        }
        return false;
    }

    private static bool ScalarMatches(JsonNode? node, HashSet<string> identifiers)
    {
        if (node is not JsonValue value) return false;
        if (value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
            return identifiers.Contains(text.Trim()) || identifiers.Contains(Regex.Replace(text, "[^0-9A-Fa-f]", ""));
        return false;
    }

    private static bool IsRelevantContext(string context) => ContextTokens.Any(token => context.Contains(token, StringComparison.OrdinalIgnoreCase));
    private static bool IsGuidToken(string value) => Regex.IsMatch(value ?? "", "^[0-9A-Fa-f]{32}$");
    private static int CountOccurrences(string text, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase)) >= 0) { count++; index += value.Length; }
        return count;
    }

    private static void ValidateEncodedSave(string staged, string original)
    {
        if (!File.Exists(staged)) throw new InvalidDataException("The save codec did not produce a repaired Level.sav.");
        var stagedLength = new FileInfo(staged).Length;
        var originalLength = new FileInfo(original).Length;
        if (stagedLength < 1024) throw new InvalidDataException("The repaired Level.sav is unexpectedly small.");
        if (originalLength > 0 && stagedLength < originalLength / 4)
            throw new InvalidDataException("The repaired Level.sav is substantially smaller than the original and was rejected.");
    }

    private static void CreateBackup(CharacterResetPreview preview, string output, string transactionId)
    {
        using var archive = ZipFile.Open(output, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(preview.LevelSavePath, "World/Level.sav", CompressionLevel.Optimal);
        var meta = Path.Combine(preview.WorldPath, "LevelMeta.sav");
        if (File.Exists(meta)) archive.CreateEntryFromFile(meta, "World/LevelMeta.sav", CompressionLevel.Optimal);
        archive.CreateEntryFromFile(preview.PlayerSavePath, "Players/" + Path.GetFileName(preview.PlayerSavePath), CompressionLevel.Optimal);
        if (File.Exists(preview.CompanionSavePath)) archive.CreateEntryFromFile(preview.CompanionSavePath, "Players/" + Path.GetFileName(preview.CompanionSavePath), CompressionLevel.Optimal);
        var entry = archive.CreateEntry("MystTiqCharacterReset.json", CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(JsonSerializer.Serialize(new
        {
            format = "MystTiqCharacterResetBackup",
            transactionId,
            createdAt = DateTimeOffset.Now,
            preview.PlayerName,
            preview.PlayerGuid,
            preview.Identifiers,
            preview.WorldPath,
            warning = "Restore this package before starting PalServer if the reset must be rolled back."
        }, JsonOptions));
    }

    private static void RestorePlayerFilesFromBackup(CharacterResetPreview preview, string backupPath)
    {
        if (!File.Exists(backupPath)) return;
        using var archive = ZipFile.OpenRead(backupPath);
        var playerEntry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("Players/" + Path.GetFileName(preview.PlayerSavePath), StringComparison.OrdinalIgnoreCase));
        playerEntry?.ExtractToFile(preview.PlayerSavePath, true);
        if (!string.IsNullOrWhiteSpace(preview.CompanionSavePath))
        {
            var companionEntry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("Players/" + Path.GetFileName(preview.CompanionSavePath), StringComparison.OrdinalIgnoreCase));
            companionEntry?.ExtractToFile(preview.CompanionSavePath, true);
        }
    }

    private static string WriteReport(CharacterResetPreview preview, CharacterResetResult result, string working)
    {
        var report = Path.Combine(Path.GetDirectoryName(result.BackupPath)!, Path.GetFileNameWithoutExtension(result.BackupPath) + ".report.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new { preview, result, completedAt = DateTimeOffset.Now }, JsonOptions), new UTF8Encoding(false));
        return report;
    }

    private static string CreateTempDirectory(string purpose)
    {
        var path = Path.Combine(Path.GetTempPath(), "MystTiq", "CharacterReset", purpose, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    private static string SafeName(string value) => string.Concat((string.IsNullOrWhiteSpace(value) ? "Player" : value).Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
    private static void TryDeleteFile(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
