namespace MystTiq.Desktop.Models;
public sealed record NetworkDiagnosticCheckDto(string Test,int State,string Details,string Recommendation)
{
 public string StateText=>State switch{0=>"PASS",1=>"WARNING",2=>"FAIL",3=>"STARTING",4=>"SKIPPED",_=>"UNKNOWN"};
}
public sealed record NetworkDiagnosticReportDto(DateTimeOffset CheckedAt,string RuntimeHealth,int NetworkHealth,int EffectiveGamePort,bool UsedDefaultGamePort,int? PalServerProcessId,string? PalServerProcessName,string? PalServerExecutablePath,DateTimeOffset? PalServerStartTime,string? BindingDisplay,string? LanEndpoint,IReadOnlyList<NetworkDiagnosticCheckDto> Checks,string RecommendedAction)
{
 public string NetworkHealthText=>NetworkHealth switch{1=>"STARTING",2=>"HEALTHY",3=>"WARNING",4=>"ERROR",_=>"UNKNOWN"};
}
public sealed record FirewallRepairResultDto(bool Success,bool Changed,string Message);

public sealed record NetworkRecoveryResultDto(bool Success, LifecycleOperationResultDto Restart, NetworkDiagnosticReportDto Diagnostics);
