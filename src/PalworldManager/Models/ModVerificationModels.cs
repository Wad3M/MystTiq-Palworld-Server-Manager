namespace PalworldManager.Models;

public enum ModHealthStatus
{
    Unknown,
    Healthy,
    Attention,
    Failed,
    Disabled
}

public sealed class VerificationResult
{
    public string Package { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "Unknown";
    public bool FilesPresent { get; init; }
    public bool Enabled { get; init; }
    public bool RuntimeEvidenceFound { get; init; }
    public bool RuntimeErrorFound { get; init; }
    public bool DuplicateDetected { get; init; }
    public string FilesStatus { get; init; } = "Unknown";
    public string RuntimeStatus { get; init; } = "Unknown";
    public string ErrorSummary { get; init; } = "None";
    public string Details { get; init; } = "";
    public int HealthScore { get; init; }
    public ModHealthStatus HealthStatus { get; init; } = ModHealthStatus.Unknown;
    public DateTime VerifiedAt { get; init; } = DateTime.Now;
}

public interface IModVerifier
{
    bool CanVerify(ModRow mod);
    VerificationResult Verify(ModRow mod, ModVerificationContext context);
}

public sealed class ModVerificationContext
{
    public required AppSettings Settings { get; init; }
    public required IReadOnlyList<string> LogLines { get; init; }
    public required IReadOnlyList<string> LogFiles { get; init; }
    public required IReadOnlyDictionary<string, int> LogicalInstallCounts { get; init; }
    public bool ServerRunning { get; init; }
}
