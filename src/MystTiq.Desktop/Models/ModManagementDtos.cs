using System.Text.Json.Serialization;
namespace MystTiq.Desktop.Models;

public sealed class Ue4ssStatusDto
{
    [JsonPropertyName("activeModsRoot")] public string ActiveModsRoot { get; init; } = string.Empty;
    [JsonPropertyName("runtimeModsRoot")] public string? RuntimeModsRoot { get; init; }
    [JsonPropertyName("detectionMethod")] public string DetectionMethod { get; init; } = string.Empty;
    [JsonPropertyName("runtimeVerified")] public bool RuntimeVerified { get; init; }
    [JsonPropertyName("runtimeMatchesActiveRoot")] public bool RuntimeMatchesActiveRoot { get; init; }
    [JsonPropertyName("healthState")] public string HealthState { get; init; } = string.Empty;
    [JsonPropertyName("warningMessage")] public string WarningMessage { get; init; } = string.Empty;
    [JsonPropertyName("installedVersion")] public string InstalledVersion { get; init; } = "Not detected";
    public string RuntimeRootText => RuntimeModsRoot ?? "Not reported";
}
public sealed class ModItemDto
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("package")] public string Package { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("installPath")] public string InstallPath { get; init; } = string.Empty;
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("health")] public string Health { get; init; } = string.Empty;
    [JsonPropertyName("runtimeState")] public string RuntimeState { get; init; } = string.Empty;
    [JsonPropertyName("evidence")] public string Evidence { get; init; } = string.Empty;
    [JsonPropertyName("runtimeConfirmed")] public bool RuntimeConfirmed { get; init; }
    public string EnabledText => Enabled ? "Enabled" : "Disabled";
}
public sealed class ModInventoryDto
{
    [JsonPropertyName("installed")] public int Installed { get; init; }
    [JsonPropertyName("runtimeConfirmed")] public int RuntimeConfirmed { get; init; }
    [JsonPropertyName("activeUnverified")] public int ActiveUnverified { get; init; }
    [JsonPropertyName("disabled")] public int Disabled { get; init; }
    [JsonPropertyName("confirmedIssues")] public int ConfirmedIssues { get; init; }
    [JsonPropertyName("overallHealth")] public string OverallHealth { get; init; } = string.Empty;
    [JsonPropertyName("summary")] public string Summary { get; init; } = string.Empty;
    [JsonPropertyName("ue4ss")] public Ue4ssStatusDto Ue4ss { get; init; } = new();
    [JsonPropertyName("mods")] public IReadOnlyList<ModItemDto> Mods { get; init; } = [];
}
public sealed class ModVerificationResultDto
{
    [JsonPropertyName("installed")] public int Installed { get; init; }
    [JsonPropertyName("runtimeConfirmed")] public int RuntimeConfirmed { get; init; }
    [JsonPropertyName("activeUnverified")] public int ActiveUnverified { get; init; }
    [JsonPropertyName("disabled")] public int Disabled { get; init; }
    [JsonPropertyName("attention")] public int Attention { get; init; }
    [JsonPropertyName("overallHealth")] public string OverallHealth { get; init; } = string.Empty;
    [JsonPropertyName("summary")] public string Summary { get; init; } = string.Empty;
    [JsonPropertyName("mods")] public IReadOnlyList<ModItemDto> Mods { get; init; } = [];
}
public sealed class ModMutationResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}
public sealed class WorkshopItemDto
{
    [JsonPropertyName("workshopId")] public string WorkshopId { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("suggestedPackage")] public string SuggestedPackage { get; init; } = string.Empty;
    [JsonPropertyName("contentKind")] public string ContentKind { get; init; } = string.Empty;
    [JsonPropertyName("alreadyInstalled")] public bool AlreadyInstalled { get; init; }
    [JsonPropertyName("localPath")] public string LocalPath { get; init; } = string.Empty;
    public string StatusText => AlreadyInstalled ? "INSTALLED" : "NOT INSTALLED";
}
public sealed class WorkshopScanResultDto
{
    [JsonPropertyName("available")] public bool Available { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
    [JsonPropertyName("items")] public IReadOnlyList<WorkshopItemDto> Items { get; init; } = [];
}
