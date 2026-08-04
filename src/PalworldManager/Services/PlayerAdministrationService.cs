using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class PlayerAdministrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string filePath;
    private readonly object gate = new();
    private List<PlayerAdministrationRecord> records;

    public PlayerAdministrationService(string dataRoot, AppSettings settings)
    {
        var serverKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(settings.ServerRoot.Trim().ToUpperInvariant())))[..12];
        filePath = Path.Combine(dataRoot, $"player-administration-{serverKey}.json");
        records = Load();
    }

    public string FilePath => filePath;

    public PlayerAdministrationSummary GetSummary(string playerKey)
    {
        lock (gate)
        {
            var record = Find(playerKey);
            if (record is null) return new(false, false, false, null, 0, 0, 0);
            var temporaryActive = record.TemporaryBanUntilUtc is DateTime until && until > DateTime.UtcNow;
            return new(
                record.IsAdmin,
                record.IsWhitelisted,
                record.IsPermanentlyBanned || temporaryActive,
                temporaryActive ? record.TemporaryBanUntilUtc : null,
                record.Notes.Count,
                record.Warnings.Count(w => w.IsActive),
                record.Warnings.Count);
        }
    }

    public IReadOnlyList<PlayerAdministrationNote> GetNotes(string playerKey)
    {
        lock (gate)
            return Find(playerKey)?.Notes.OrderByDescending(n => n.CreatedUtc).Select(Clone).ToList() ?? [];
    }

    public IReadOnlyList<PlayerWarningRecord> GetWarnings(string playerKey)
    {
        lock (gate)
            return Find(playerKey)?.Warnings.OrderByDescending(w => w.IssuedUtc).Select(Clone).ToList() ?? [];
    }

    public void SetAdmin(string playerKey, string displayName, bool value)
    {
        lock (gate)
        {
            var record = GetOrCreate(playerKey, displayName);
            record.IsAdmin = value;
            TouchAndSave(record);
        }
    }

    public void SetWhitelisted(string playerKey, string displayName, bool value)
    {
        lock (gate)
        {
            var record = GetOrCreate(playerKey, displayName);
            record.IsWhitelisted = value;
            TouchAndSave(record);
        }
    }

    public void AddNote(string playerKey, string displayName, string category, string text, string administrator)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        lock (gate)
        {
            var record = GetOrCreate(playerKey, displayName);
            record.Notes.Add(new PlayerAdministrationNote
            {
                Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
                Text = text.Trim(),
                Administrator = string.IsNullOrWhiteSpace(administrator) ? Environment.UserName : administrator.Trim()
            });
            TouchAndSave(record);
        }
    }

    public void IssueWarning(string playerKey, string displayName, string reason, DateTime? expiresUtc, string administrator)
    {
        if (string.IsNullOrWhiteSpace(reason)) return;
        lock (gate)
        {
            var record = GetOrCreate(playerKey, displayName);
            record.Warnings.Add(new PlayerWarningRecord
            {
                Reason = reason.Trim(),
                ExpiresUtc = expiresUtc,
                IssuedBy = string.IsNullOrWhiteSpace(administrator) ? Environment.UserName : administrator.Trim()
            });
            TouchAndSave(record);
        }
    }

    public int ClearActiveWarnings(string playerKey, string administrator)
    {
        lock (gate)
        {
            var record = Find(playerKey);
            if (record is null) return 0;
            var count = 0;
            foreach (var warning in record.Warnings.Where(w => w.IsActive))
            {
                warning.ClearedUtc = DateTime.UtcNow;
                warning.ClearedBy = administrator;
                count++;
            }
            if (count > 0) TouchAndSave(record);
            return count;
        }
    }

    public void SetBan(string playerKey, string displayName, DateTime? untilUtc, bool permanent)
    {
        lock (gate)
        {
            var record = GetOrCreate(playerKey, displayName);
            record.IsPermanentlyBanned = permanent;
            record.TemporaryBanUntilUtc = permanent ? null : untilUtc;
            TouchAndSave(record);
        }
    }

    public void ClearBan(string playerKey)
    {
        lock (gate)
        {
            var record = Find(playerKey);
            if (record is null) return;
            record.IsPermanentlyBanned = false;
            record.TemporaryBanUntilUtc = null;
            TouchAndSave(record);
        }
    }

    public IReadOnlyList<(string PlayerKey, string DisplayName)> GetExpiredTemporaryBans()
    {
        lock (gate)
        {
            var now = DateTime.UtcNow;
            return records
                .Where(r => !r.IsPermanentlyBanned && r.TemporaryBanUntilUtc is DateTime until && until <= now)
                .Select(r => (r.PlayerKey, r.DisplayName))
                .ToList();
        }
    }

    public void MarkTemporaryBanProcessed(string playerKey)
    {
        lock (gate)
        {
            var record = Find(playerKey);
            if (record is null) return;
            record.TemporaryBanUntilUtc = null;
            TouchAndSave(record);
        }
    }

    private PlayerAdministrationRecord GetOrCreate(string playerKey, string displayName)
    {
        var record = Find(playerKey);
        if (record is not null)
        {
            if (!string.IsNullOrWhiteSpace(displayName)) record.DisplayName = displayName;
            return record;
        }
        record = new PlayerAdministrationRecord { PlayerKey = playerKey, DisplayName = displayName };
        records.Add(record);
        return record;
    }

    private PlayerAdministrationRecord? Find(string playerKey) =>
        string.IsNullOrWhiteSpace(playerKey) ? null : records.FirstOrDefault(r => r.PlayerKey.Equals(playerKey, StringComparison.OrdinalIgnoreCase));

    private void TouchAndSave(PlayerAdministrationRecord record)
    {
        record.UpdatedUtc = DateTime.UtcNow;
        SaveLocked();
    }

    private List<PlayerAdministrationRecord> Load()
    {
        try
        {
            if (!File.Exists(filePath)) return [];
            return JsonSerializer.Deserialize<List<PlayerAdministrationRecord>>(File.ReadAllText(filePath), JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveLocked()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temp = filePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(records, JsonOptions), new UTF8Encoding(false));
        File.Move(temp, filePath, true);
    }

    private static PlayerAdministrationNote Clone(PlayerAdministrationNote source) => new()
    {
        Id = source.Id,
        CreatedUtc = source.CreatedUtc,
        Administrator = source.Administrator,
        Category = source.Category,
        Text = source.Text
    };

    private static PlayerWarningRecord Clone(PlayerWarningRecord source) => new()
    {
        Id = source.Id,
        IssuedUtc = source.IssuedUtc,
        IssuedBy = source.IssuedBy,
        Reason = source.Reason,
        ExpiresUtc = source.ExpiresUtc,
        ClearedUtc = source.ClearedUtc,
        ClearedBy = source.ClearedBy
    };
}
