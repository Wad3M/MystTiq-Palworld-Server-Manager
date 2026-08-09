namespace PalworldManager.Models;

public sealed record PlayerRow(
    string Status,
    string Name,
    string UserId,
    string SteamId,
    string PlayerId,
    string Ip,
    string Ping,
    string Platform,
    string Level,
    string BuildingCount,
    string FirstSeen,
    string LastSeen,
    int Sessions,
    string Banned,
    string Notes,
    string Source,
    string SaveStatus,
    string SavePath);

public sealed class PlayerHistoryRecord
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string UserId { get; set; } = "";
    public string SteamId { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Ping { get; set; } = "";
    public string Platform { get; set; } = "Unknown";
    public string Level { get; set; } = "";
    public string BuildingCount { get; set; } = "";
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public int ObservedSessions { get; set; }
    public bool IsOnline { get; set; }
    public bool IsBanned { get; set; }
    public string Notes { get; set; } = "";
    public string Source { get; set; } = "REST";
}

public sealed record LivePlayerSnapshot(
    string Name,
    string UserId,
    string SteamId,
    string PlayerId,
    string Ip,
    string Ping,
    string Platform,
    string Level,
    string BuildingCount);
public sealed record BackupRow(
    string FilePath,
    DateTime Created,
    double SizeMb,
    string Status,
    string VerifiedAt);

public sealed class DoctorCheckRow
{
    public string Component { get; set; } = "";
    public string Status { get; set; } = "Unknown";
    public string Detail { get; set; } = "";
    public string Recommendation { get; set; } = "";
}


public sealed class ModRow
{
    public bool Enabled { get; set; }
    public string Name { get; set; } = "";
    public string Package { get; set; } = "";
    public string Version { get; set; } = "";
    public bool Deployed { get; set; }
    public string Source { get; set; } = "Server";
    public string Type { get; set; } = "Unknown";
    public string Description { get; set; } = "No description metadata was found for this mod.";
    public string EnableReason { get; set; } = "State not evaluated.";
    public bool PresentInActiveRuntime { get; set; }
    public bool LoadedByUe4ss { get; set; }
    public IReadOnlyList<string> RuntimeAliases { get; set; } = [];
    public string RuntimeLocationStatus => Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) || Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase)
        ? PresentInActiveRuntime ? "Active" : "Missing"
        : "N/A";
    public string RuntimeLoadedStatus => Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) || Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase)
        ? LoadedByUe4ss ? "Loaded" : "Not loaded"
        : "N/A";
    public string Status => !Deployed ? "Missing files" :
        (Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) || Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase)) && !PresentInActiveRuntime ? "Misconfigured" :
        Enabled ? "Enabled" : "Disabled";
}

public sealed class ModDashboardRow
{
    public string Package { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Unknown";
    public string FilesStatus { get; set; } = "Unknown";
    public string EnabledStatus { get; set; } = "Unknown";
    public string RuntimeStatus { get; set; } = "Not checked";
    public string ErrorStatus { get; set; } = "None";
    public string DependencyStatus { get; set; } = "Not scanned";
    public string ConflictStatus { get; set; } = "Not scanned";
    public string VersionStatus { get; set; } = "Not scanned";
    public string Compatibility { get; set; } = "Not scanned";
    public string Health { get; set; } = "Unknown";
    public int HealthScore { get; set; }
    public string ScoreDisplay => HealthScore <= 0 && Health == "Unknown" ? "—" : $"{HealthScore}%";
    public string Details { get; set; } = "";
    public string LastVerified { get; set; } = "Never";
}

public sealed class SettingRow : INotifyPropertyChanged
{
    private string value = "";
    private string originalValue = "";

    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Category { get; init; } = "Other";
    public string Description { get; init; } = "";
    public string DefaultValue { get; init; } = "";
    public string ValidationMessage => ConfigValueValidator.Validate(Name, Value);
    public bool IsValid => string.IsNullOrEmpty(ValidationMessage);

    public string Value
    {
        get => value;
        set
        {
            if (this.value == value) return;
            this.value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsModified)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDirty)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsValid)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationMessage)));
        }
    }

    public bool IsModified => !string.Equals(
        Normalize(Value),
        Normalize(DefaultValue),
        StringComparison.OrdinalIgnoreCase);

    public bool IsDirty => !string.Equals(
        Normalize(Value),
        Normalize(originalValue),
        StringComparison.Ordinal);

    public void MarkLoaded()
    {
        originalValue = Value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDirty)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string Normalize(string input) => input.Trim();
}

public sealed class QolOption : INotifyPropertyChanged
{
    private bool isEnabled;
    private double percentage;
    private string resultText = "Default";

    public string Category { get; init; } = "";
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public string[] SettingNames { get; init; } = [];
    public bool HigherRawIsBenefit { get; init; } = true;
    public bool WholeNumber { get; init; }
    public string DisplayKind { get; init; } = "Percent";
    public string ValueSuffix { get; init; } = "%";
    public double Minimum { get; init; }
    public double Maximum { get; init; } = 500;
    public double TickFrequency { get; init; } = 5;

    public bool IsEnabled
    {
        get => isEnabled;
        set { if (isEnabled == value) return; isEnabled = value; OnChanged(); }
    }

    public double Percentage
    {
        get => percentage;
        set { if (Math.Abs(percentage - value) < 0.0001) return; percentage = value; OnChanged(); }
    }

    public string ResultText
    {
        get => resultText;
        set { if (resultText == value) return; resultText = value; OnChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public static class ConfigValueValidator
{
    public static string Validate(string name, string? raw)
    {
        var value = (raw ?? string.Empty).Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        // Boolean values must be recognized before name-based checks. Settings such as
        // bAllowGlobalPalboxExport contain the letters "Port" inside "Export" and were
        // previously misclassified as TCP/UDP port settings.
        if (value.Equals("True", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("False", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        // Only setting names that actually end in Port represent network ports.
        if (name.EndsWith("Port", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(value, out var port) && port is >= 1 and <= 65535 ? string.Empty : "Port must be between 1 and 65535.";

        if (name.Equals("ServerPlayerMaxNum", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(value, out var players) && players is >= 1 and <= 128 ? string.Empty : "Player limit must be between 1 and 128.";

        // Validate only the numeric settings managed by the QoL editor. A broad
        // "contains MaxNum" rule incorrectly rejected newer Palworld settings whose
        // values are enums or other server-defined tokens.
        if (name.Equals("BaseCampWorkerMaxNum", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("BaseCampMaxNum", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("BaseCampMaxNumInGuild", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("SupplyDropSpan", StringComparison.OrdinalIgnoreCase))
            return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) && number >= 0 ? string.Empty : "Enter a non-negative number.";

        if (name.EndsWith("Rate", StringComparison.OrdinalIgnoreCase))
            return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rate) && rate >= 0 && rate <= 100 ? string.Empty : "Rate must be a number from 0 to 100.";

        return string.Empty;
    }
}

public sealed record BackupInventorySummary(
    int TotalArchives,
    int ServerBackups,
    int WorldArchives,
    int RepairBackups,
    int ModBackups,
    int OtherBackups,
    long TotalBytes,
    int VerifiedServerBackups,
    int RetentionCandidates)
{
    public string TotalSizeDisplay => TotalBytes < 1024L * 1024L
        ? $"{TotalBytes / 1024d:N1} KB"
        : $"{TotalBytes / 1024d / 1024d:N2} MB";
}
