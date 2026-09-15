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
// v0.7.41.0: MOD update detection (item 52). HasKnownSource false means no local Steam Workshop
// item matches this package at all -- nothing to compare against, distinct from a known source
// that's simply not newer.
public sealed class ModUpdateCheckResultDto
{
    [JsonPropertyName("hasKnownSource")] public bool HasKnownSource { get; init; }
    [JsonPropertyName("updateAvailable")] public bool UpdateAvailable { get; init; }
    [JsonPropertyName("workshopId")] public string? WorkshopId { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}
// v0.7.55.0: website-sourced MOD descriptions (item 40's deferred half). Source is a display
// label only ("Steam Workshop", "GitHub Repository", "Manual Link", "None") -- the UI shows it
// as-is, it never branches on the value.
public sealed class ModDescriptionResultDto
{
    [JsonPropertyName("available")] public bool Available { get; init; }
    [JsonPropertyName("source")] public string Source { get; init; } = "None";
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("sourceUrl")] public string? SourceUrl { get; init; }
    [JsonPropertyName("fetchedAtUtc")] public DateTime? FetchedAtUtc { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}
public sealed record ModDescriptionSourceRequestDto(string SourceUrl);
// v0.7.59.0: Safe-Start MOD Diagnostic. Mirrors HeadlessHost's SafeStartStatus/SafeStartModResult
// field-for-field -- this is a polled status snapshot, not a mutation result, so it has no
// Success/Message shape like ModMutationResultDto.
public sealed class SafeStartModResultDto
{
    [JsonPropertyName("package")] public string Package { get; init; } = string.Empty;
    [JsonPropertyName("ok")] public bool Ok { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}
public sealed class SafeStartStatusDto
{
    [JsonPropertyName("isRunning")] public bool IsRunning { get; init; }
    [JsonPropertyName("completed")] public bool Completed { get; init; }
    [JsonPropertyName("cancelled")] public bool Cancelled { get; init; }
    [JsonPropertyName("notModRelated")] public bool NotModRelated { get; init; }
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("phase")] public string Phase { get; init; } = string.Empty;
    [JsonPropertyName("currentPackage")] public string? CurrentPackage { get; init; }
    [JsonPropertyName("totalCandidates")] public int TotalCandidates { get; init; }
    [JsonPropertyName("testedCount")] public int TestedCount { get; init; }
    [JsonPropertyName("results")] public IReadOnlyList<SafeStartModResultDto> Results { get; init; } = [];
    [JsonPropertyName("finalMessage")] public string FinalMessage { get; init; } = string.Empty;
    [JsonPropertyName("startedAtUtc")] public DateTime StartedAtUtc { get; init; }
    public string ProgressText => TotalCandidates > 0 ? $"{TestedCount} / {TotalCandidates} tested" : string.Empty;
    public bool CompletedUnsuccessfully => Completed && !Success;
}
