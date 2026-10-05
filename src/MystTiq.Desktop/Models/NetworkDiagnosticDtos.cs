// MystTiq v1.0.0.4: file reviewed for this release (2026-10-05).
namespace MystTiq.Desktop.Models;
public sealed record NetworkDiagnosticCheckDto(string Test,int State,string Details,string Recommendation)
{
 public string StateText=>State switch{0=>"PASS",1=>"WARNING",2=>"FAIL",3=>"STARTING",4=>"SKIPPED",5=>"UNKNOWN",_=>"UNKNOWN"};
}
public sealed record NetworkDiagnosticReportDto(DateTimeOffset CheckedAt,string RuntimeHealth,int NetworkHealth,int EffectiveGamePort,bool UsedDefaultGamePort,int? PalServerProcessId,string? PalServerProcessName,string? PalServerExecutablePath,DateTimeOffset? PalServerStartTime,string? BindingDisplay,string? LanEndpoint,IReadOnlyList<NetworkDiagnosticCheckDto> Checks,string RecommendedAction)
{
 public string NetworkHealthText=>NetworkHealth switch{1=>"STARTING",2=>"HEALTHY",3=>"WARNING",4=>"ERROR",5=>"NOT RUNNING",_=>"UNKNOWN"};
}
// v0.9.6.0: NeedsElevation and Script: the service may not change the firewall, so the Desktop runs Script as an
// administrator on this computer. Removed names the rules for the server's previous port that were taken away.
public sealed record FirewallRepairResultDto(bool Success,bool Changed,string Message,bool NeedsElevation=false,string? Script=null,IReadOnlyList<string>? Removed=null);
public sealed record FirewallRuleInfoDto(string Name,bool Enabled,string Direction,string Action,string Protocol,int LocalPort,string Profiles,bool ManagedByMystTiq=false,string? ServerId=null);
// v0.9.6.0: one server's game port in the firewall (GET /diagnostics/network/firewall).
public sealed record FirewallStatusDto(int Port,string Protocol,bool Supported,bool Allowed,bool Blocked,IReadOnlyList<FirewallRuleInfoDto> Rules,IReadOnlyList<string> StaleRules,string Summary,string? Error=null,IReadOnlyList<string>? Commands=null);

public sealed record NetworkRecoveryResultDto(bool Success, LifecycleOperationResultDto Restart, NetworkDiagnosticReportDto Diagnostics);

public sealed record WanReachabilityCheckDto(string Test,int State,string Details,string Recommendation)
{
 public string StateText=>State switch{0=>"PASS",1=>"WARNING",2=>"FAIL",3=>"STARTING",4=>"SKIPPED",5=>"UNKNOWN",_=>"UNKNOWN"};
}
// v0.8.12.0: RouterWanIPv4 is the address the router holds on its internet side (UPnP), shown beside its name.
public sealed record WanReachabilityReportDto(DateTimeOffset CheckedAt,string? PublicIPv4,int GamePort,int UpnpState,string? RouterDescription,IReadOnlyList<WanReachabilityCheckDto> Checks,string? RouterWanIPv4=null)
{
 public string UpnpStateText=>UpnpState switch{1=>"MAPPED",2=>"NOT MAPPED",3=>"ROUTER UNREACHABLE",4=>"UNSUPPORTED",_=>"UNKNOWN"};
 public string PublicIpPortText=>string.IsNullOrWhiteSpace(PublicIPv4)?"Unknown":$"{PublicIPv4}:{GamePort}";
}
public sealed record UpnpRepairResultDto(bool Success, bool Changed, string Message);

// v0.7.2.0: a standalone "is this candidate port already bound" query used during new-server
// setup, independent of any configured/running profile.
public sealed record PortCheckResultDto(int Port, string Protocol, bool InUse, string? ProcessName, int? OwningProcessId);
