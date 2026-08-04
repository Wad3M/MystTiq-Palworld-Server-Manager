using System.Text.Json.Nodes;
using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class CharacterCloneService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly Dictionary<string, string[]> CategoryTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Inventory"] = ["inventory", "itemcontainer", "item_container", "bag", "storage"],
        ["Equipment"] = ["equipment", "equip", "weapon", "armor", "shield", "accessory"],
        ["Technology"] = ["technology", "tech", "recipe", "unlock", "learned"],
        ["Level and Stats"] = ["level", "exp", "experience", "status_point", "stat", "hp", "stamina", "attack", "defense", "work_speed", "weight"],
        ["Appearance"] = ["appearance", "body", "face", "hair", "voice", "gender", "color", "character_make"],
        ["Fast Travel"] = ["fasttravel", "fast_travel", "warp", "teleport", "map_object_unlock"],
        ["Map Discovery"] = ["map", "fog", "discovery", "explored", "map_data"],
        ["Paldeck"] = ["paldeck", "pal_deck", "encyclopedia", "capture_count", "pal_record"],
        ["Palbox"] = ["palbox", "pal_box", "pal_storage", "container_id", "owned_pal"]
    };

    private readonly AppSettings settings;
    private readonly PalworldSaveCodec codec;

    public CharacterCloneService(AppSettings settings)
    {
        this.settings = settings;
        codec = new PalworldSaveCodec(settings);
    }

    public CharacterClonePreview Preview(PlayerRow source, PlayerRow destination, string worldPath, bool serverRunning, CharacterCloneOptions options)
    {
        if (ReferenceEquals(source, destination) || SameIdentity(source, destination))
            throw new InvalidOperationException("Source and destination must be different players.");

        var sourceSave = ResolveSave(source, worldPath);
        var destinationSave = ResolveSave(destination, worldPath);
        var preview = new CharacterClonePreview
        {
            SourcePlayer = source,
            DestinationPlayer = destination,
            SourceSavePath = sourceSave,
            DestinationSavePath = destinationSave,
            WorldPath = worldPath,
            CodecAvailable = !string.IsNullOrWhiteSpace(codec.FindConverter()),
            ServerMustBeStopped = serverRunning,
            Options = options,
            SourceHash = File.Exists(sourceSave) ? PalworldSaveCodec.HashFile(sourceSave) : "",
            DestinationHash = File.Exists(destinationSave) ? PalworldSaveCodec.HashFile(destinationSave) : ""
        };

        if (!preview.CodecAvailable) preview.Findings.Add("palworld-save-tools is not configured.");
        if (!File.Exists(sourceSave)) preview.Findings.Add("The source player save could not be resolved.");
        if (!File.Exists(destinationSave)) preview.Findings.Add("The destination player save could not be resolved.");
        if (serverRunning) preview.Findings.Add("PalServer is running. Stop it before applying a character clone.");
        if (options.SelectedCategories().Count == 0) preview.Findings.Add("No clone categories were selected.");

        if (preview.CodecAvailable && File.Exists(sourceSave) && File.Exists(destinationSave))
        {
            var temp = CreateTempDirectory("Preview");
            try
            {
                var sourceJson = DecodeCopy(sourceSave, Path.Combine(temp, "Source.sav"));
                var destinationJson = DecodeCopy(destinationSave, Path.Combine(temp, "Destination.sav"));
                var sourceRoot = JsonNode.Parse(File.ReadAllText(sourceJson)) ?? throw new InvalidDataException("The decoded source player save is empty.");
                var destinationRoot = JsonNode.Parse(File.ReadAllText(destinationJson)) ?? throw new InvalidDataException("The decoded destination player save is empty.");

                foreach (var category in options.SelectedCategories())
                {
                    var sourceCount = FindMatchingProperties(sourceRoot, Tokens(category)).Count;
                    var destinationCount = FindMatchingProperties(destinationRoot, Tokens(category)).Count;
                    preview.Categories.Add(new CharacterCloneCategoryPreview
                    {
                        Category = category,
                        SourceNodes = sourceCount,
                        DestinationNodes = destinationCount
                    });
                }

                if (preview.Categories.Any(x => x.Category == "Palbox" && !x.CanCopy))
                    preview.Findings.Add("Palbox data is commonly stored in Level.sav rather than the individual player save. This category will only apply when matching player-save nodes are present.");
                if (!preview.Categories.Any(x => x.CanCopy))
                    preview.Findings.Add("None of the selected categories could be matched safely in both player saves.");
            }
            catch (Exception ex)
            {
                preview.Findings.Add("Character clone preview failed: " + ex.Message);
            }
            finally { TryDeleteDirectory(temp); }
        }

        if (preview.Findings.Count == 0)
            preview.Findings.Add("The selected categories are ready for a transactional clone.");
        return preview;
    }

    public CharacterCloneResult Apply(CharacterClonePreview preview)
    {
        if (!preview.CanApply) throw new InvalidOperationException("The character clone preview is not eligible to apply.");
        if (!File.Exists(preview.SourceSavePath) || !File.Exists(preview.DestinationSavePath))
            throw new FileNotFoundException("One or both player saves are no longer available.");
        if (!string.Equals(PalworldSaveCodec.HashFile(preview.SourceSavePath), preview.SourceHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The source player save changed after preview. Refresh the preview before applying.");
        if (!string.Equals(PalworldSaveCodec.HashFile(preview.DestinationSavePath), preview.DestinationHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The destination player save changed after preview. Refresh the preview before applying.");

        var transactionId = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..8];
        var backupRoot = Path.Combine(settings.BackupRoot, "CharacterClone");
        Directory.CreateDirectory(backupRoot);
        var backupPath = Path.Combine(backupRoot, $"CharacterClone_{SafeName(preview.SourcePlayer.Name)}_to_{SafeName(preview.DestinationPlayer.Name)}_{transactionId}.zip");
        var working = Path.Combine(preview.WorldPath, ".mysttiq-transactions", transactionId);
        Directory.CreateDirectory(working);
        var originalDestination = Path.Combine(working, "Destination.original.sav");
        var result = new CharacterCloneResult { TransactionId = transactionId, BackupPath = backupPath };

        try
        {
            CreateBackup(preview, backupPath, transactionId);
            File.Copy(preview.DestinationSavePath, originalDestination, true);

            var sourceJson = DecodeCopy(preview.SourceSavePath, Path.Combine(working, "Source.sav"));
            var destinationJson = DecodeCopy(preview.DestinationSavePath, Path.Combine(working, "Destination.sav"));
            var sourceRoot = JsonNode.Parse(File.ReadAllText(sourceJson)) ?? throw new InvalidDataException("The decoded source player save is empty.");
            var destinationRoot = JsonNode.Parse(File.ReadAllText(destinationJson)) ?? throw new InvalidDataException("The decoded destination player save is empty.");

            foreach (var category in preview.Categories.Where(x => x.CanCopy))
            {
                var copied = CopyCategory(sourceRoot, destinationRoot, Tokens(category.Category));
                if (copied <= 0) continue;
                result.NodesCopied += copied;
                result.CategoriesCopied.Add(category.Category);
            }

            if (result.NodesCopied == 0)
                throw new InvalidDataException("No selected character data could be copied.");

            var modifiedJson = Path.Combine(working, "Destination.modified.sav.json");
            File.WriteAllText(modifiedJson, destinationRoot.ToJsonString(JsonOptions), new UTF8Encoding(false));
            var stagedOutput = Path.Combine(working, "Destination.repaired.sav");
            codec.Encode(modifiedJson, stagedOutput);
            ValidateEncodedSave(stagedOutput, preview.DestinationSavePath);

            var verifyCopy = Path.Combine(working, "Destination.verify.sav");
            File.Copy(stagedOutput, verifyCopy, true);
            var verifyJson = codec.Decode(verifyCopy, true);
            var verifyRoot = JsonNode.Parse(File.ReadAllText(verifyJson)) ?? throw new InvalidDataException("Verification decode returned empty JSON.");
            foreach (var category in result.CategoriesCopied)
            {
                if (FindMatchingProperties(verifyRoot, Tokens(category)).Count == 0)
                    throw new InvalidDataException($"Verification could not locate cloned {category} data in the destination save.");
            }

            var replacement = preview.DestinationSavePath + ".mysttiq-clone.tmp";
            File.Copy(stagedOutput, replacement, true);
            File.Replace(replacement, preview.DestinationSavePath, null, true);
            result.VerificationPassed = File.Exists(preview.DestinationSavePath) && new FileInfo(preview.DestinationSavePath).Length > 512;
            if (!result.VerificationPassed) throw new InvalidDataException("The destination save failed final validation.");

            result.Messages.Add("The destination player save was backed up before modification.");
            result.Messages.Add("Only categories matched in both decoded player saves were copied.");
            result.Messages.Add("The rebuilt destination save was independently decoded and verified before activation.");
            result.ReportPath = WriteReport(preview, result, working);
            result.Success = true;
            return result;
        }
        catch
        {
            try { if (File.Exists(originalDestination)) File.Copy(originalDestination, preview.DestinationSavePath, true); }
            catch { }
            throw;
        }
        finally
        {
            if (result.Success) TryDeleteDirectory(working);
        }
    }

    private string DecodeCopy(string original, string staged)
    {
        File.Copy(original, staged, true);
        return codec.Decode(staged, true);
    }

    private static int CopyCategory(JsonNode sourceRoot, JsonNode destinationRoot, IReadOnlyCollection<string> tokens)
    {
        var sourceMatches = FindMatchingProperties(sourceRoot, tokens);
        var destinationMatches = FindMatchingProperties(destinationRoot, tokens);
        var copied = 0;
        foreach (var destination in destinationMatches)
        {
            var source = sourceMatches.FirstOrDefault(x => x.Path.Equals(destination.Path, StringComparison.OrdinalIgnoreCase))
                ?? sourceMatches.FirstOrDefault(x => x.Name.Equals(destination.Name, StringComparison.OrdinalIgnoreCase));
            if (source?.Value is null || destination.Parent is null) continue;
            destination.Parent[destination.Name] = source.Value.DeepClone();
            copied++;
        }
        return copied;
    }

    private sealed record PropertyMatch(JsonObject Parent, string Name, JsonNode? Value, string Path);

    private static List<PropertyMatch> FindMatchingProperties(JsonNode? node, IReadOnlyCollection<string> tokens)
    {
        var matches = new List<PropertyMatch>();
        Walk(node, "root", matches, tokens);
        return matches;
    }

    private static void Walk(JsonNode? node, string path, List<PropertyMatch> matches, IReadOnlyCollection<string> tokens)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                var childPath = path + "/" + property.Key;
                if (tokens.Any(token => Normalize(property.Key).Contains(Normalize(token), StringComparison.OrdinalIgnoreCase)))
                    matches.Add(new PropertyMatch(obj, property.Key, property.Value, childPath));
                Walk(property.Value, childPath, matches, tokens);
            }
        }
        else if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++) Walk(array[i], path + $"[{i}]", matches, tokens);
        }
    }

    private static IReadOnlyCollection<string> Tokens(string category) => CategoryTokens.TryGetValue(category, out var tokens) ? tokens : [category];
    private static string Normalize(string value) => Regex.Replace(value ?? "", "[^a-zA-Z0-9]", "").ToLowerInvariant();
    private static bool SameIdentity(PlayerRow a, PlayerRow b) => new[] { a.UserId, a.SteamId, a.PlayerId }.Where(x => !string.IsNullOrWhiteSpace(x)).Any(x =>
        new[] { b.UserId, b.SteamId, b.PlayerId }.Any(y => !string.IsNullOrWhiteSpace(y) && x.Equals(y, StringComparison.OrdinalIgnoreCase)));

    private static string ResolveSave(PlayerRow player, string worldPath)
    {
        if (!string.IsNullOrWhiteSpace(player.SavePath) && File.Exists(player.SavePath)) return player.SavePath;
        var players = Path.Combine(worldPath, "Players");
        if (!Directory.Exists(players)) return "";
        foreach (var id in new[] { player.PlayerId, player.UserId, player.SteamId }.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var compact = Regex.Replace(id, "[^0-9A-Fa-f]", "");
            if (compact.Length != 32) continue;
            var candidate = Path.Combine(players, compact.ToUpperInvariant() + ".sav");
            if (File.Exists(candidate)) return candidate;
        }
        return "";
    }

    private static void ValidateEncodedSave(string staged, string original)
    {
        if (!File.Exists(staged)) throw new InvalidDataException("The save codec did not produce a destination player save.");
        var stagedLength = new FileInfo(staged).Length;
        var originalLength = new FileInfo(original).Length;
        if (stagedLength < 512) throw new InvalidDataException("The rebuilt destination player save is unexpectedly small.");
        if (originalLength > 0 && stagedLength < originalLength / 4)
            throw new InvalidDataException("The rebuilt destination player save is substantially smaller than the original and was rejected.");
    }

    private static void CreateBackup(CharacterClonePreview preview, string output, string transactionId)
    {
        using var archive = ZipFile.Open(output, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(preview.SourceSavePath, "Source/" + Path.GetFileName(preview.SourceSavePath), CompressionLevel.Optimal);
        archive.CreateEntryFromFile(preview.DestinationSavePath, "Destination/" + Path.GetFileName(preview.DestinationSavePath), CompressionLevel.Optimal);
        AddCompanion(archive, preview.SourceSavePath, "Source");
        AddCompanion(archive, preview.DestinationSavePath, "Destination");
        var entry = archive.CreateEntry("MystTiqCharacterClone.json", CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(JsonSerializer.Serialize(new
        {
            format = "MystTiqCharacterCloneBackup",
            transactionId,
            createdAt = DateTimeOffset.Now,
            source = preview.SourcePlayer.Name,
            destination = preview.DestinationPlayer.Name,
            categories = preview.Options.SelectedCategories(),
            preview.SourceHash,
            preview.DestinationHash
        }, JsonOptions));
    }

    private static void AddCompanion(ZipArchive archive, string savePath, string folder)
    {
        var companion = Path.Combine(Path.GetDirectoryName(savePath)!, Path.GetFileNameWithoutExtension(savePath) + "_dps.sav");
        if (File.Exists(companion)) archive.CreateEntryFromFile(companion, folder + "/" + Path.GetFileName(companion), CompressionLevel.Optimal);
    }

    private static string WriteReport(CharacterClonePreview preview, CharacterCloneResult result, string working)
    {
        var root = Path.Combine(Path.GetDirectoryName(working)!, "Reports");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"CharacterClone_{result.TransactionId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            format = "MystTiqCharacterCloneReport",
            version = "2.11.7.0",
            createdAt = DateTimeOffset.Now,
            result.TransactionId,
            source = new { preview.SourcePlayer.Name, preview.SourcePlayer.PlayerId, preview.SourceSavePath },
            destination = new { preview.DestinationPlayer.Name, preview.DestinationPlayer.PlayerId, preview.DestinationSavePath },
            selectedCategories = preview.Options.SelectedCategories(),
            result.CategoriesCopied,
            result.NodesCopied,
            result.VerificationPassed,
            result.BackupPath,
            result.Messages
        }, JsonOptions), new UTF8Encoding(false));
        return path;
    }

    private static string CreateTempDirectory(string suffix)
    {
        var path = Path.Combine(Path.GetTempPath(), "MystTiq", "CharacterClone", suffix + "_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
    private static string SafeName(string value) => string.Concat((string.IsNullOrWhiteSpace(value) ? "Player" : value).Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
}
