using PalworldManager.Services;

namespace PalworldManager.Models;

public enum ModStartupGateStatus
{
    Ready,
    ReadyWithWarnings,
    Blocked
}

public sealed record ModRepairRecommendation(
    string Package,
    string Name,
    string Severity,
    string Action,
    string Reason);

public sealed record ModLifecycleReport(
    DateTime CheckedAt,
    ModStartupGateStatus GateStatus,
    ModStateRepairResult Reconciliation,
    IReadOnlyList<ModRow> Mods,
    IReadOnlyList<ModRepairRecommendation> Recommendations,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> Warnings)
{
    public bool CanStart => GateStatus != ModStartupGateStatus.Blocked;
}

public sealed record ModVerificationExportResult(
    string TextPath,
    string JsonPath,
    int ModCount,
    DateTime ExportedAt);
