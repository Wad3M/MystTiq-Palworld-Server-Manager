using PalworldManager.Services.Infrastructure;
using System.IO.Compression;
using System.Text.Json.Nodes;
using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class OwnershipEngineService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] OwnershipKeys =
    {
        "group_id", "guild_id", "owner_group_id", "owner_guild_id", "owner_id", "camp_owner_group_id",
        "base_camp_id", "base_id", "palbox_id", "worker", "work", "container", "build", "structure"
    };

    private readonly AppSettings settings;
    private readonly PalworldSaveCodec codec;
    private readonly WorldTransactionJournalService journalService;

    public OwnershipEngineService(AppSettings settings)
    {
        this.settings = settings;
        codec = new PalworldSaveCodec(settings);
        journalService = new WorldTransactionJournalService(settings);
    }

    public OwnershipPreview Preview(BaseManagerSummary summary, BaseManagerRow row, OwnershipOperationType operation, string targetGuildId, bool serverRunning)
    {
        if (summary is null) throw new ArgumentNullException(nameof(summary));
        if (row is null) throw new ArgumentNullException(nameof(row));
        var level = Path.Combine(summary.WorldPath, "Level.sav");
        var preview = new OwnershipPreview
        {
            WorldPath = summary.WorldPath,
            LevelSavePath = level,
            SourceHash = File.Exists(level) ? PalworldSaveCodec.HashFile(level) : "",
            Base = row,
            Operation = operation,
            TargetGuildId = Normalize(targetGuildId),
            CodecAvailable = !string.IsNullOrWhiteSpace(codec.FindConverter()),
            ServerMustBeStopped = serverRunning
        };

        if (!File.Exists(level)) preview.Findings.Add("Level.sav is missing from the active world.");
        if (!preview.CodecAvailable) preview.Findings.Add("palworld-save-tools is not configured; Level.sav cannot be edited transactionally.");
        if (serverRunning) preview.Findings.Add("PalServer is running. Stop it before applying ownership changes.");
        if (operation == OwnershipOperationType.TransferOwnership && string.IsNullOrWhiteSpace(preview.TargetGuildId))
            preview.Findings.Add("A destination guild ID is required for ownership transfer.");

        if (preview.CodecAvailable && File.Exists(level))
        {
            var temp = CreateTempDirectory("Preview");
            try
            {
                var staged = Path.Combine(temp, "Level.sav");
                File.Copy(level, staged, true);
                var jsonPath = codec.Decode(staged, true);
                var root = JsonNode.Parse(File.ReadAllText(jsonPath)) ?? throw new InvalidDataException("Decoded Level.sav JSON is empty.");
                var baseTokens = BuildBaseTokens(row);
                var scopes = FindOwningScopes(root, baseTokens);
                preview.MatchedScopeCount = scopes.Count;
                preview.BaseReferenceCount = CountToken(root, Normalize(row.BaseId));
                preview.PalboxReferenceCount = CountToken(root, Normalize(row.PalboxId));
                preview.GuildReferenceCount = CountToken(root, Normalize(row.GuildId));
                foreach (var scope in scopes)
                {
                    var category = Classify(scope.Path);
                    preview.Categories[category] = preview.Categories.GetValueOrDefault(category) + 1;
                    if (preview.SamplePaths.Count < 12) preview.SamplePaths.Add(scope.Path);
                }
                if (scopes.Count == 0) preview.Findings.Add("No decoded ownership scope matched the selected Base ID or Palbox ID.");
                else preview.Findings.Add($"Located {scopes.Count} ownership scope(s) associated with the selected base.");
            }
            catch (Exception ex) { preview.Findings.Add("Ownership preview failed: " + ex.Message); }
            finally { TryDeleteDirectory(temp); }
        }
        return preview;
    }

    public OwnershipTransactionResult Apply(OwnershipPreview preview)
    {
        if (!preview.CanApply) throw new InvalidOperationException("The ownership preview contains blocking conditions. Refresh and resolve them before applying.");
        if (!File.Exists(preview.LevelSavePath)) throw new FileNotFoundException("Level.sav was not found.", preview.LevelSavePath);
        if (!PalworldSaveCodec.HashFile(preview.LevelSavePath).Equals(preview.SourceHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Level.sav changed after the preview. Scan again before applying ownership changes.");

        var transactionId = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..8];
        var backupRoot = Path.Combine(settings.BackupRoot, "OwnershipEngine");
        Directory.CreateDirectory(backupRoot);
        var backupPath = Path.Combine(backupRoot, $"Ownership_{SafeName(preview.Base.Name)}_{transactionId}.zip");
        var working = Path.Combine(preview.WorldPath, ".mysttiq-transactions", "ownership-" + transactionId);
        Directory.CreateDirectory(working);
        var original = Path.Combine(working, "Level.original.sav");
        var replacement = Path.Combine(preview.WorldPath, $"Level.sav.mysttiq-ownership-{transactionId}.tmp");
        var result = new OwnershipTransactionResult { TransactionId = transactionId, BackupPath = backupPath };
        var journal = journalService.Create(
            transactionId,
            preview.Operation.ToString(),
            preview.WorldPath,
            preview.Base.BaseId,
            preview.SourceHash);

        try
        {
            CreateBackup(preview, backupPath, transactionId);
            journal.BackupPath = backupPath;
            journalService.Advance(journal, WorldTransactionState.BackedUp, "Complete rollback ZIP created and verified.");
            File.Copy(preview.LevelSavePath, original, true);
            var staged = Path.Combine(working, "Level.input.sav");
            File.Copy(preview.LevelSavePath, staged, true);
            var jsonPath = codec.Decode(staged, true);
            var root = JsonNode.Parse(File.ReadAllText(jsonPath)) ?? throw new InvalidDataException("Decoded Level.sav JSON is empty.");
            var tokens = BuildBaseTokens(preview.Base);

            if (preview.Operation == OwnershipOperationType.TransferOwnership)
            {
                var scopes = FindOwningScopes(root, tokens);
                foreach (var scope in scopes)
                {
                    var changed = ReplaceOwnershipValues(scope.Node, Normalize(preview.Base.GuildId), preview.TargetGuildId);
                    if (changed > 0) { result.ScopesChanged++; result.ValuesChanged += changed; }
                }
                if (result.ValuesChanged == 0)
                    throw new InvalidDataException("No supported ownership fields were changed. The decoded save layout may not match this engine version.");
            }
            else
            {
                result.ScopesChanged = RemoveOwningScopes(root, tokens);
                result.ValuesChanged = result.ScopesChanged;
                if (result.ScopesChanged == 0)
                    throw new InvalidDataException("No supported base ownership scopes were removed.");
            }

            journalService.Advance(journal, WorldTransactionState.Staged,
                $"Decoded transaction staged. Scopes changed: {result.ScopesChanged}; values changed: {result.ValuesChanged}.");
            var modifiedJson = Path.Combine(working, "Level.modified.sav.json");
            File.WriteAllText(modifiedJson, root.ToJsonString(JsonOptions), new UTF8Encoding(false));
            var stagedOutput = Path.Combine(working, "Level.repaired.sav");
            codec.Encode(modifiedJson, stagedOutput);
            ValidateEncodedSave(stagedOutput, preview.LevelSavePath);
            journalService.Advance(journal, WorldTransactionState.Encoded, "Modified JSON encoded to a staged Level.sav and passed size validation.");

            var verify = Path.Combine(working, "Level.verify.sav");
            File.Copy(stagedOutput, verify, true);
            var verifyJson = codec.Decode(verify, true);
            var verifyRoot = JsonNode.Parse(File.ReadAllText(verifyJson)) ?? throw new InvalidDataException("Verification decode was empty.");
            if (preview.Operation == OwnershipOperationType.DeleteBaseAndOwnedObjects)
            {
                var remaining = BuildBaseTokens(preview.Base).Sum(token => CountToken(verifyRoot, token));
                if (remaining > 0) throw new InvalidDataException($"Verification found {remaining} remaining Base/Palbox identifier reference(s).");
            }
            else
            {
                var scopes = FindOwningScopes(verifyRoot, tokens);
                var targetCount = scopes.Sum(x => CountToken(x.Node, preview.TargetGuildId));
                if (targetCount == 0) throw new InvalidDataException("Verification did not find the destination guild ID in the repaired ownership scopes.");
            }

            journalService.Advance(journal, WorldTransactionState.Verified, "Staged save independently decoded and relationship checks passed.");
            File.Copy(stagedOutput, replacement, true);
            File.Replace(replacement, preview.LevelSavePath, null, true);
            result.VerificationPassed = File.Exists(preview.LevelSavePath) && new FileInfo(preview.LevelSavePath).Length > 1024;
            if (!result.VerificationPassed) throw new InvalidDataException("The active Level.sav failed final validation.");
            result.Messages.Add("A complete rollback package was created before Level.sav was modified.");
            result.Messages.Add("The repaired save was encoded and independently decoded for verification before activation.");
            result.ReportPath = WriteReport(preview, result, working);
            result.Success = true;
            journal.ResultHash = PalworldSaveCodec.HashFile(preview.LevelSavePath);
            journal.ReportPath = result.ReportPath;
            journalService.Advance(journal, WorldTransactionState.Committed, "Verified save committed atomically to the active world.");
            return result;
        }
        catch (Exception ex)
        {
            var rolledBack = false;
            try
            {
                if (File.Exists(original))
                {
                    File.Copy(original, preview.LevelSavePath, true);
                    rolledBack = File.Exists(preview.LevelSavePath) &&
                        PalworldSaveCodec.HashFile(preview.LevelSavePath).Equals(preview.SourceHash, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { rolledBack = false; }
            journalService.Fail(journal, ex, rolledBack);
            throw;
        }
        finally
        {
            TryDeleteFile(replacement);
            if (result.Success) TryDeleteDirectory(working);
        }
    }

    private static List<(JsonNode Node, string Path)> FindOwningScopes(JsonNode root, HashSet<string> tokens)
    {
        var candidates = new List<(JsonNode Node, string Path)>();
        Walk(root, "root", (node, path) =>
        {
            if (node is not JsonObject obj) return;
            if (!ContainsAnyToken(obj, tokens)) return;
            var pathLower = path.ToLowerInvariant();
            var keyText = string.Join(' ', obj.Select(x => x.Key)).ToLowerInvariant();
            if (OwnershipKeys.Any(token => pathLower.Contains(token, StringComparison.OrdinalIgnoreCase) || keyText.Contains(token, StringComparison.OrdinalIgnoreCase)))
                candidates.Add((obj, path));
        });
        return candidates
            .Where(x => !candidates.Any(y => !ReferenceEquals(x.Node, y.Node) && x.Path.StartsWith(y.Path + ".", StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static int RemoveOwningScopes(JsonNode root, HashSet<string> tokens)
    {
        var removed = 0;
        RemoveFrom(root, tokens, ref removed);
        return removed;
    }

    private static void RemoveFrom(JsonNode node, HashSet<string> tokens, ref int removed)
    {
        if (node is JsonArray array)
        {
            for (var i = array.Count - 1; i >= 0; i--)
            {
                var child = array[i];
                if (child is null) continue;
                if (IsOwnershipScope(child) && ContainsAnyToken(child, tokens)) { array.RemoveAt(i); removed++; }
                else RemoveFrom(child, tokens, ref removed);
            }
        }
        else if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(x => x.Key).ToList())
            {
                var child = obj[key];
                if (child is null) continue;
                if (IsOwnershipScope(child) && ContainsAnyToken(child, tokens)) { obj.Remove(key); removed++; }
                else RemoveFrom(child, tokens, ref removed);
            }
        }
    }

    private static bool IsOwnershipScope(JsonNode node)
    {
        if (node is not JsonObject obj) return false;
        var text = string.Join(' ', obj.Select(x => x.Key));
        return OwnershipKeys.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    private static int ReplaceOwnershipValues(JsonNode node, string oldGuildId, string newGuildId)
    {
        var changed = 0;
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(x => x.Key).ToList())
            {
                var child = obj[key];
                if (child is JsonValue value && IsOwnershipKey(key) && TryString(value, out var text) && Normalize(text).Equals(oldGuildId, StringComparison.OrdinalIgnoreCase))
                {
                    obj[key] = PreserveGuidFormatting(text, newGuildId); changed++;
                }
                else if (child is not null) changed += ReplaceOwnershipValues(child, oldGuildId, newGuildId);
            }
        }
        else if (node is JsonArray array)
            foreach (var child in array) if (child is not null) changed += ReplaceOwnershipValues(child, oldGuildId, newGuildId);
        return changed;
    }

    private static bool IsOwnershipKey(string key) => key.Contains("guild", StringComparison.OrdinalIgnoreCase) || key.Contains("group", StringComparison.OrdinalIgnoreCase) || key.Contains("owner", StringComparison.OrdinalIgnoreCase);
    private static string PreserveGuidFormatting(string original, string compact)
    {
        if (original.Contains('-') && compact.Length == 32)
            return $"{compact[..8]}-{compact[8..12]}-{compact[12..16]}-{compact[16..20]}-{compact[20..]}".ToLowerInvariant();
        return compact;
    }

    private static HashSet<string> BuildBaseTokens(BaseManagerRow row) => new(new[] { Normalize(row.BaseId), Normalize(row.PalboxId) }.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
    private static bool ContainsAnyToken(JsonNode node, HashSet<string> tokens) => tokens.Any(token => CountToken(node, token) > 0);
    private static int CountToken(JsonNode node, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return 0;
        var count = 0;
        Walk(node, "root", (current, _) => { if (current is JsonValue value && TryString(value, out var text) && Normalize(text).Equals(token, StringComparison.OrdinalIgnoreCase)) count++; });
        return count;
    }

    private static void Walk(JsonNode node, string path, Action<JsonNode, string> visitor)
    {
        visitor(node, path);
        if (node is JsonObject obj)
            foreach (var pair in obj) if (pair.Value is not null) Walk(pair.Value, path + "." + pair.Key, visitor);
        else if (node is JsonArray array)
            for (var i = 0; i < array.Count; i++) if (array[i] is not null) Walk(array[i]!, path + $"[{i}]", visitor);
    }

    private static bool TryString(JsonValue value, out string text)
    {
        try { text = value.GetValue<string>(); return true; }
        catch { text = ""; return false; }
    }

    private static string Classify(string path)
    {
        if (path.Contains("palbox", StringComparison.OrdinalIgnoreCase)) return "Palboxes";
        if (path.Contains("container", StringComparison.OrdinalIgnoreCase)) return "Containers";
        if (path.Contains("work", StringComparison.OrdinalIgnoreCase)) return "Work assignments";
        if (path.Contains("build", StringComparison.OrdinalIgnoreCase) || path.Contains("structure", StringComparison.OrdinalIgnoreCase)) return "Structures";
        if (path.Contains("base", StringComparison.OrdinalIgnoreCase) || path.Contains("camp", StringComparison.OrdinalIgnoreCase)) return "Bases";
        return "Related ownership records";
    }

    private static void CreateBackup(OwnershipPreview preview, string backupPath, string transactionId)
    {
        using var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(preview.LevelSavePath, "World/Level.sav", CompressionLevel.Optimal);
        var metadata = archive.CreateEntry("OwnershipTransaction.json", CompressionLevel.Optimal);
        using var writer = new StreamWriter(metadata.Open(), new UTF8Encoding(false));
        writer.Write(JsonSerializer.Serialize(new { format = "MystTiqOwnershipBackup", version = 1, transactionId, createdUtc = DateTime.UtcNow, preview.Operation, preview.Base, preview.TargetGuildId, preview.SourceHash }, JsonOptions));
    }

    private static string WriteReport(OwnershipPreview preview, OwnershipTransactionResult result, string working)
    {
        var path = Path.Combine(working, "ownership-transaction-report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { product = "MystTiq Palworld Server", version = ApplicationVersion.Version, preview, result }, JsonOptions), new UTF8Encoding(false));
        var permanentRoot = Path.Combine(Path.GetDirectoryName(result.BackupPath)!, "Reports");
        Directory.CreateDirectory(permanentRoot);
        var permanent = Path.Combine(permanentRoot, Path.GetFileNameWithoutExtension(result.BackupPath) + ".json");
        File.Copy(path, permanent, true);
        return permanent;
    }

    private static void ValidateEncodedSave(string output, string original)
    {
        if (!File.Exists(output)) throw new InvalidDataException("The save encoder did not create Level.sav.");
        var length = new FileInfo(output).Length;
        if (length < 1024) throw new InvalidDataException("The encoded Level.sav is unexpectedly small.");
        var originalLength = new FileInfo(original).Length;
        if (originalLength > 0 && length > originalLength * 8) throw new InvalidDataException("The encoded Level.sav is unexpectedly larger than the source save.");
    }

    private static string Normalize(string value) => new string((value ?? "").Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
    private static string SafeName(string value) => string.Concat((string.IsNullOrWhiteSpace(value) ? "Base" : value).Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
    private static string CreateTempDirectory(string purpose) { var path = Path.Combine(Path.GetTempPath(), "MystTiq", "OwnershipEngine", purpose, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
    private static void TryDeleteFile(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
