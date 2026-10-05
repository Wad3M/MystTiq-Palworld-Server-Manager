// MystTiq v1.0.0.5: file reviewed for this release (2026-10-05).
namespace MystTiq.Core.Models;

// v0.6.4.0: gained Unknown so this can serve as the canonical per-check severity for the unified
// Doctor/Environment/local-machine diagnostics platform too (PASS/WARNING/FAIL/UNKNOWN per the
// roadmap), not just the network-diagnostics feature that introduced it. Appended at the end
// (not inserted first) deliberately -- this enum serializes as a plain numeric value (no
// JsonStringEnumConverter) and the Desktop's existing NetworkDiagnosticCheckDto.StateText switches
// on that raw int by hardcoded position; inserting Unknown first would have silently relabeled
// every existing Pass/Warning/Fail/Starting/Skipped result already shipped in that feature.
public enum DiagnosticState { Pass, Warning, Fail, Starting, Skipped, Unknown }
// NotRunning (v0.7.102.0) is appended, not inserted, so the numbers the Desktop already reads keep their meaning.
public enum NetworkHealthState { Unknown, Starting, Healthy, Warning, Error, NotRunning }
public sealed record NetworkEndpointInfo(string Protocol,string LocalAddress,int LocalPort,int? OwningProcessId,string? ProcessName=null,string? ExecutablePath=null);
// ServerId (v0.9.6.0): the server a MystTiq rule belongs to, from its description; null for other rules and older ones.
public sealed record FirewallRuleInfo(string Name,bool Enabled,string Direction,string Action,string Protocol,int LocalPort,string Profiles,bool ManagedByMystTiq=false,string? ServerId=null){ public bool CoversCurrentNetwork { get; init; } = true; }
public sealed record NetworkDiagnosticCheck(string Test,DiagnosticState State,string Details,string Recommendation="");
// v0.9.6.0: NeedsElevation says the service may not change the firewall; Script is the change to run as an administrator
// instead (the Desktop runs it elevated on this computer). Removed names the rules for the server's old port.
public sealed record FirewallRepairResult(bool Success,bool Changed,string Message,bool NeedsElevation=false,string? Script=null,IReadOnlyList<string>? Removed=null);
// v0.9.6.0: one server's game port in the firewall: whether a rule allows or blocks it, and this server's rules for a
// port it no longer uses. Supported is false where MystTiq does not change the firewall (Linux), with Commands to run.
// CurrentNetwork (v0.9.8.0): the network profile(s) in use, e.g. "Private".
public sealed record FirewallStatus(int Port,string Protocol,bool Supported,bool Allowed,bool Blocked,IReadOnlyList<FirewallRuleInfo> Rules,IReadOnlyList<string> StaleRules,string Summary,string? Error=null,IReadOnlyList<string>? Commands=null){ public string? CurrentNetwork { get; init; } }
// v0.7.2.0: a standalone "is this candidate port already bound" query, independent of any
// configured/running server profile -- unlike NetworkDiagnosticReport (which only ever checks one
// already-running profile's own configured port), this answers the question a new-server-creation
// flow actually needs: "would this port I'm about to type collide with anything right now."
public sealed record PortCheckResult(int Port,string Protocol,bool InUse,string? ProcessName,int? OwningProcessId);
public sealed record NetworkDiagnosticReport(DateTimeOffset CheckedAt,string RuntimeHealth,NetworkHealthState NetworkHealth,int EffectiveGamePort,bool UsedDefaultGamePort,int? PalServerProcessId,string? PalServerProcessName,string? PalServerExecutablePath,DateTimeOffset? PalServerStartTime,string? BindingDisplay,string? LanEndpoint,IReadOnlyList<NetworkDiagnosticCheck> Checks,string RecommendedAction)
{
 public string ToSupportText(){var l=new List<string>{"MystTiq Palworld Server Diagnostics",$"Timestamp: {CheckedAt:O}",$"Runtime: {RuntimeHealth}",$"Network: {NetworkHealth}",$"Configured game port: UDP {EffectiveGamePort}",$"PID: {PalServerProcessId?.ToString()??"—"}",$"Process: {PalServerProcessName??"—"}",$"Binding: {BindingDisplay??"—"}",$"LAN endpoint: {LanEndpoint??"—"}",""};l.AddRange(Checks.Select(c=>$"{c.Test}: {c.State} — {c.Details}"));if(!string.IsNullOrWhiteSpace(RecommendedAction))l.Add($"Recommended action: {RecommendedAction}");return string.Join(Environment.NewLine,l);}
}
