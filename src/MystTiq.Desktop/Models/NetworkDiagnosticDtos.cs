namespace MystTiq.Desktop.Models;
public sealed record NetworkDiagnosticCheckDto(string Test,int State,string Details,string Recommendation)
{
 public string StateText=>State switch{0=>"PASS",1=>"WARNING",2=>"FAIL",3=>"STARTING",4=>"SKIPPED",5=>"UNKNOWN",_=>"UNKNOWN"};
}
public sealed record NetworkDiagnosticReportDto(DateTimeOffset CheckedAt,string RuntimeHealth,int NetworkHealth,int EffectiveGamePort,bool UsedDefaultGamePort,int? PalServerProcessId,string? PalServerProcessName,string? PalServerExecutablePath,DateTimeOffset? PalServerStartTime,string? BindingDisplay,string? LanEndpoint,IReadOnlyList<NetworkDiagnosticCheckDto> Checks,string RecommendedAction)
{
 public string NetworkHealthText=>NetworkHealth switch{1=>"STARTING",2=>"HEALTHY",3=>"WARNING",4=>"ERROR",_=>"UNKNOWN"};
}
public sealed record FirewallRepairResultDto(bool Success,bool Changed,string Message);

public sealed record NetworkRecoveryResultDto(bool Success, LifecycleOperationResultDto Restart, NetworkDiagnosticReportDto Diagnostics);

public sealed record WanReachabilityCheckDto(string Test,int State,string Details,string Recommendation)
{
 public string StateText=>State switch{0=>"PASS",1=>"WARNING",2=>"FAIL",3=>"STARTING",4=>"SKIPPED",5=>"UNKNOWN",_=>"UNKNOWN"};
}
public sealed record WanReachabilityReportDto(DateTimeOffset CheckedAt,string? PublicIPv4,int GamePort,int UpnpState,string? RouterDescription,IReadOnlyList<WanReachabilityCheckDto> Checks)
{
 public string UpnpStateText=>UpnpState switch{1=>"MAPPED",2=>"NOT MAPPED",3=>"ROUTER UNREACHABLE",4=>"UNSUPPORTED",_=>"UNKNOWN"};
 public string PublicIpPortText=>string.IsNullOrWhiteSpace(PublicIPv4)?"Unknown":$"{PublicIPv4}:{GamePort}";
}
public sealed record UpnpRepairResultDto(bool Success, bool Changed, string Message);
