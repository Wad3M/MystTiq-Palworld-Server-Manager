namespace PalworldManager.Models;

public sealed class EnvironmentComponentRow
{
    public string Component { get; set; } = "";
    public string Status { get; set; } = "UNKNOWN";
    public string Location { get; set; } = "";
    public string Details { get; set; } = "";
    public string Action { get; set; } = "";
    public bool IsReady => Status == "READY";
    public bool IsOptional => Status == "OPTIONAL" || Status == "DISABLED";
}

public sealed class LocalModRow
{
    public string Name { get; set; } = "";
    public string WorkshopId { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string Type { get; set; } = "Unknown";
    public string Compatibility { get; set; } = "Unknown";
    public string ServerStatus { get; set; } = "Not installed";
    public string InstalledVersion { get; set; } = "—";
    public string AvailableVersion { get; set; } = "Local copy";
    public string UpdateStatus { get; set; } = "Not installed";
    public int VariantCount { get; set; }
    public string Variants => VariantCount <= 1 ? "Single package" : $"{VariantCount} grouped options";
    public long SizeBytes { get; set; }
    public string Size => SizeBytes < 1024*1024 ? $"{SizeBytes/1024.0:0.0} KB" : $"{SizeBytes/1024.0/1024.0:0.0} MB";
    public DateTime LastUpdated { get; set; }
    public string Author { get; set; } = "Unknown";
    public string Description { get; set; } = "No Workshop description was found in the local files.";
    public string Details => $"{Type} • {Size} • Updated {LastUpdated:g}";
}

public sealed class UpdateCenterRow
{
    public string Group { get; set; } = "Other";
    public string Component { get; set; } = "";
    public string Installed { get; set; } = "—";
    public string Available { get; set; } = "—";
    public string Status { get; set; } = "UNKNOWN";
    public string Source { get; set; } = "";
    public string LastChecked { get; set; } = "Never";
    public string LastUpdated { get; set; } = "Unknown";
    public string Action { get; set; } = "CHECK";
    public string Recommendation { get; set; } = "";
}
