namespace MystTiq.Core.Models;

public enum DiagnosticState { Pass, Warning, Fail, Starting, Skipped }
public enum NetworkHealthState { Unknown, Starting, Healthy, Warning, Error }
public sealed record NetworkEndpointInfo(string Protocol,string LocalAddress,int LocalPort,int? OwningProcessId,string? ProcessName=null,string? ExecutablePath=null);
public sealed record FirewallRuleInfo(string Name,bool Enabled,string Direction,string Action,string Protocol,int LocalPort,string Profiles,bool ManagedByMystTiq=false);
public sealed record NetworkDiagnosticCheck(string Test,DiagnosticState State,string Details,string Recommendation="");
public sealed record FirewallRepairResult(bool Success,bool Changed,string Message);
public sealed record NetworkDiagnosticReport(DateTimeOffset CheckedAt,string RuntimeHealth,NetworkHealthState NetworkHealth,int EffectiveGamePort,bool UsedDefaultGamePort,int? PalServerProcessId,string? PalServerProcessName,string? PalServerExecutablePath,DateTimeOffset? PalServerStartTime,string? BindingDisplay,string? LanEndpoint,IReadOnlyList<NetworkDiagnosticCheck> Checks,string RecommendedAction)
{
 public string ToSupportText(){var l=new List<string>{"MystTiq Palworld Server Diagnostics",$"Timestamp: {CheckedAt:O}",$"Runtime: {RuntimeHealth}",$"Network: {NetworkHealth}",$"Configured game port: UDP {EffectiveGamePort}",$"PID: {PalServerProcessId?.ToString()??"—"}",$"Process: {PalServerProcessName??"—"}",$"Binding: {BindingDisplay??"—"}",$"LAN endpoint: {LanEndpoint??"—"}",""};l.AddRange(Checks.Select(c=>$"{c.Test}: {c.State} — {c.Details}"));if(!string.IsNullOrWhiteSpace(RecommendedAction))l.Add($"Recommended action: {RecommendedAction}");return string.Join(Environment.NewLine,l);}
}
