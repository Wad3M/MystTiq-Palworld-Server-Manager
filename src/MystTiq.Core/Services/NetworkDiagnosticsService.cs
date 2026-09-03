using MystTiq.Core.Models;
namespace MystTiq.Core.Services;
public sealed class NetworkDiagnosticsService
{
 public const int DefaultGamePort=8211; public static readonly TimeSpan DefaultStartupGrace=TimeSpan.FromSeconds(30);
 private readonly INetworkDiagnosticsPlatformService platform;
 private readonly string? configRoot;
 public NetworkDiagnosticsService(INetworkDiagnosticsPlatformService platform,string? configRoot=null){this.platform=platform;this.configRoot=configRoot;}
 public async Task<NetworkDiagnosticReport> RunAsync(ServerLifecycleSnapshot runtime,IReadOnlyList<string> args,TimeSpan grace,CancellationToken token=default)
 {
  var checks=new List<NetworkDiagnosticCheck>();var(port,used,invalid)=ResolveGamePort(args);
  checks.Add(new("Configured Game Port",invalid?DiagnosticState.Fail:used?DiagnosticState.Warning:DiagnosticState.Pass,invalid?"Invalid or conflicting -port configuration.":used?$"Configured value unavailable; defaulting to UDP {port}.":$"Effective game port is UDP {port}.",invalid?"Correct launch arguments.":""));
  var proc=runtime.Processes.FirstOrDefault(p=>p.ProcessId==runtime.NativeProcessId)??runtime.Processes.FirstOrDefault();
  if(proc is null){checks.Add(new("PalServer Process",DiagnosticState.Fail,"Palworld server process is not running.","Start Palworld Server."));return Make(runtime,port,used,null,null,null,null,checks,NetworkHealthState.Error,"Start Palworld Server");}
  DateTimeOffset? started=null;try{using var p=System.Diagnostics.Process.GetProcessById(proc.ProcessId);started=p.StartTime.ToUniversalTime();}catch{}
  checks.Add(new("PalServer Process",DiagnosticState.Pass,$"Running: {proc.ProcessName}, PID {proc.ProcessId}."));
  if(OperatingSystem.IsWindows())try{var fw=await platform.GetInboundFirewallRulesAsync(port,"UDP",token);if(fw.Count==0)checks.Add(new("Windows Firewall",DiagnosticState.Fail,$"No suitable inbound UDP Allow rule exists for {port}.","Add / Repair Firewall Rule."));else{var block=fw.Any(r=>r.Enabled&&r.Action.Equals("Block",StringComparison.OrdinalIgnoreCase));var allow=fw.Any(r=>r.Enabled&&r.Action.Equals("Allow",StringComparison.OrdinalIgnoreCase));checks.Add(new("Windows Firewall",block?DiagnosticState.Fail:allow?DiagnosticState.Pass:DiagnosticState.Warning,string.Join("; ",fw.Select(r=>$"{r.Name} [{r.Action}, Enabled={r.Enabled}, Profiles={r.Profiles}]")),block?"Review blocking rule.":allow?"":"Enable or repair the rule."));}}catch(Exception ex){checks.Add(new("Windows Firewall",DiagnosticState.Warning,$"Inspection unavailable: {ex.Message}","Run elevated for firewall diagnostics."));}
  else checks.Add(new("Firewall",DiagnosticState.Skipped,"Windows firewall inspection does not apply."));
  var eps=await platform.GetUdpEndpointsAsync(token);var same=eps.Where(e=>e.LocalPort==port).ToArray();var owned=same.FirstOrDefault(e=>e.OwningProcessId==proc.ProcessId);
  string? binding=null,lan=null;NetworkHealthState health;string action="";
  if(owned is not null){binding=owned.LocalAddress is "0.0.0.0" or "*"?"All IPv4 interfaces (0.0.0.0)":owned.LocalAddress;var ip=platform.GetLanIPv4Addresses().FirstOrDefault();lan=owned.LocalAddress is "0.0.0.0" or "*"?(ip is null?null:$"{ip}:{port}"):$"{owned.LocalAddress}:{port}";checks.Add(new("Socket Binding",DiagnosticState.Pass,$"PalServer owns UDP {owned.LocalAddress}:{port}."));if(lan is not null)checks.Add(new("LAN Endpoint",DiagnosticState.Pass,lan));health=checks.Any(c=>c.State==DiagnosticState.Fail)?NetworkHealthState.Warning:NetworkHealthState.Healthy;}
  else if(same.Length>0){var x=same[0];checks.Add(new("Socket Binding",DiagnosticState.Fail,$"UDP {port} is already in use by {x.ProcessName??"another process"}, PID {x.OwningProcessId?.ToString()??"unknown"}.","Stop the conflicting process or change port."));health=NetworkHealthState.Error;}
  else{var wrong=eps.FirstOrDefault(e=>e.OwningProcessId==proc.ProcessId&&e.LocalPort!=port);var starting=started is not null&&DateTimeOffset.UtcNow-started<grace;if(starting){checks.Add(new("Socket Binding",DiagnosticState.Starting,$"Palworld process is running; waiting for UDP {port} listener."));health=NetworkHealthState.Starting;}else if(wrong is not null){checks.Add(new("Socket Binding",DiagnosticState.Warning,$"PalServer is listening on UDP {wrong.LocalPort}, not expected UDP {port}.","Review launch configuration."));health=NetworkHealthState.Warning;}else{checks.Add(new("Socket Binding",DiagnosticState.Fail,$"Palworld is running but is not listening on UDP {port}.","Restart Palworld Server."));health=NetworkHealthState.Error;action="Restart Palworld Server";}}
  await AddOptionalServiceChecksAsync(checks, proc.ProcessId, token);
  return Make(runtime,port,used,proc.ProcessId,proc.ProcessName,proc.ExecutablePath,started,checks,health,action,binding,lan);
 }

 private async Task AddOptionalServiceChecksAsync(List<NetworkDiagnosticCheck> checks,int palPid,CancellationToken token)
 {
  if(string.IsNullOrWhiteSpace(configRoot))return;
  var ini=Path.Combine(configRoot,"PalWorldSettings.ini");if(!File.Exists(ini))return;
  string text;try{text=await File.ReadAllTextAsync(ini,token);}catch{return;}
  async Task CheckTcp(string name,bool enabled,int port,bool probeHttp)
  {
   if(!enabled)return;
   var tcp=await platform.GetTcpListenersAsync(token);var ep=tcp.FirstOrDefault(e=>e.LocalPort==port);
   if(ep is null){checks.Add(new(name,DiagnosticState.Warning,$"{name} is enabled but TCP {port} is not listening."));return;}
   if(probeHttp)
   {
    try{using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(2)};var sw=System.Diagnostics.Stopwatch.StartNew();using var response=await client.GetAsync($"http://127.0.0.1:{port}/v1/api/info",token);sw.Stop();checks.Add(new(name,response.IsSuccessStatusCode?DiagnosticState.Pass:DiagnosticState.Warning,$"TCP {port} responded HTTP {(int)response.StatusCode} in {sw.ElapsedMilliseconds} ms."));}
    catch(Exception ex){checks.Add(new(name,DiagnosticState.Warning,$"TCP {port} is listening but REST probe failed: {ex.GetType().Name}."));}
   } else checks.Add(new(name,DiagnosticState.Pass,$"TCP {port} is listening."));
  }
  var rcon=System.Text.RegularExpressions.Regex.IsMatch(text,@"RCONEnabled\s*=\s*True",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
  var rest=System.Text.RegularExpressions.Regex.IsMatch(text,@"RESTAPIEnabled\s*=\s*True",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
  static int Port(string t,string key,int fallback){var m=System.Text.RegularExpressions.Regex.Match(t,key+@"\s*=\s*(\d+)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);return m.Success&&int.TryParse(m.Groups[1].Value,out var p)&&p is>0 and<=65535?p:fallback;}
  await CheckTcp("RCON",rcon,Port(text,"RCONPort",25575),false);
  await CheckTcp("REST API",rest,Port(text,"RESTAPIPort",8212),true);
 }
 public Task<FirewallRepairResult> RepairFirewallAsync(int port,CancellationToken token=default)=>platform.RepairInboundFirewallRuleAsync(port,"UDP",token);
 public static (int Port,bool UsedDefault,bool Invalid) ResolveGamePort(IReadOnlyList<string> args){var ps=new List<int>();foreach(var a in args){if(!a.StartsWith("-port=",StringComparison.OrdinalIgnoreCase))continue;if(!int.TryParse(a[6..],out var p)||p is<1 or>65535)return(DefaultGamePort,false,true);ps.Add(p);}if(ps.Distinct().Count()>1)return(DefaultGamePort,false,true);return ps.Count==0?(DefaultGamePort,true,false):(ps[0],false,false);}
 private static NetworkDiagnosticReport Make(ServerLifecycleSnapshot r,int port,bool used,int? pid,string? n,string? path,DateTimeOffset? st,IReadOnlyList<NetworkDiagnosticCheck> c,NetworkHealthState h,string a,string? b=null,string? l=null)=>new(DateTimeOffset.UtcNow,r.Phase.ToString(),h,port,used,pid,n,path,st,b,l,c,a);
}
