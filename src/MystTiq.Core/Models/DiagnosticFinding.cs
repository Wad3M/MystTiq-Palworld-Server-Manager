using MystTiq.Core.Operations;

namespace MystTiq.Core.Models;

// v0.6.4.0 "Troubleshooting & Diagnostics Platform": the one unified evidence shape production
// health (HeadlessDoctorService), setup/tooling completeness (HeadlessEnvironmentChecklistService),
// and the new client-side local-machine checks all report through -- so "no health deduction
// exists without a corresponding visible Doctor finding" per the roadmap. Category distinguishes
// where a finding came from without needing three different record shapes:
//   "Health"        - HeadlessDoctorService (ongoing production health)
//   "Setup"         - HeadlessEnvironmentChecklistService (first-run/setup completeness)
//   "Local Machine" - LocalDiagnosticsService, Desktop-side (the admin's own machine, not the server)
public sealed record DiagnosticFinding(
    string Id,
    string Category,
    string Component,
    DiagnosticState State,
    string Location,
    string Evidence,
    string Recommendation,
    string? ActionKind,
    bool ActionSupported,
    string? UnavailableReason,
    DateTimeOffset ObservedAt,
    TimeSpan Duration);

public sealed record DiagnosticsReport(
    ServerHealthState OverallHealth,
    string OverallHealthDetail,
    int Passed,
    int Warnings,
    int Failures,
    DateTimeOffset ObservedAt,
    IReadOnlyList<DiagnosticFinding> Findings);
