namespace MystTiq.Core.Models;

// v0.6.4.0: gained Unknown so this can serve as the canonical per-check severity for the unified
// Doctor/Environment/local-machine diagnostics platform too (PASS/WARNING/FAIL/UNKNOWN per the
// roadmap), not just the network-diagnostics feature that introduced it. Appended at the end
// (not inserted first) deliberately -- this enum serializes as a plain numeric value (no
// JsonStringEnumConverter) and the Desktop's existing NetworkDiagnosticCheckDto.StateText switches
// on that raw int by hardcoded position; inserting Unknown first would have silently relabeled
// every existing Pass/Warning/Fail/Starting/Skipped result already shipped in that feature.
public enum DiagnosticState { Pass, Warning, Fail, Starting, Skipped, Unknown }
public enum NetworkHealthState { Unknown, Starting, Healthy, Warning, Error }
public sealed record NetworkEndpointInfo(string Protocol,string LocalAddress,int LocalPort,int? OwningProcessId,string? ProcessName=null,string? ExecutablePath=null);
public sealed record FirewallRuleInfo(string Name,bool Enabled,string Direction,string Action,string Protocol,int LocalPort,string Profiles,bool ManagedByMystTiq=false);
public sealed record NetworkDiagnosticCheck(string Test,DiagnosticState State,string Details,string Recommendation="");
public sealed record FirewallRepairResult(bool Success,bool Changed,string Message);
public sealed record NetworkDiagnosticReport(DateTimeOffset CheckedAt,string RuntimeHealth,NetworkHealthState NetworkHealth,int EffectiveGamePort,bool UsedDefaultGamePort,int? PalServerProcessId,string? PalServerProcessName,string? PalServerExecutablePath,DateTimeOffset? PalServerStartTime,string? BindingDisplay,string? LanEndpoint,IReadOnlyList<NetworkDiagnosticCheck> Checks,string RecommendedAction)
{
 public string ToSupportText(){var l=new List<string>{"MystTiq Palworld Server Diagnostics",$"Timestamp: {CheckedAt:O}",$"Runtime: {RuntimeHealth}",$"Network: {NetworkHealth}",$"Configured game port: UDP {EffectiveGamePort}",$"PID: {PalServerProcessId?.ToString()??"—"}",$"Process: {PalServerProcessName??"—"}",$"Binding: {BindingDisplay??"—"}",$"LAN endpoint: {LanEndpoint??"—"}",""};l.AddRange(Checks.Select(c=>$"{c.Test}: {c.State} — {c.Details}"));if(!string.IsNullOrWhiteSpace(RecommendedAction))l.Add($"Recommended action: {RecommendedAction}");return string.Join(Environment.NewLine,l);}
}
