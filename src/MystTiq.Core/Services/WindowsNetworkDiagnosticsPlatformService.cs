// MystTiq v0.9.6.0: file reviewed for this release (2026-09-29).
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text.Json;
using MystTiq.Core.Models;
namespace MystTiq.Core.Services;
[SupportedOSPlatform("windows")]
public sealed class WindowsNetworkDiagnosticsPlatformService:INetworkDiagnosticsPlatformService
{
 public async Task<IReadOnlyList<NetworkEndpointInfo>> GetUdpEndpointsAsync(CancellationToken c=default)=>Parse(await Run(SystemTool("netstat.exe"),"-ano -p udp",c),"UDP");
 public async Task<IReadOnlyList<NetworkEndpointInfo>> GetTcpListenersAsync(CancellationToken c=default)=>Parse(await Run(SystemTool("netstat.exe"),"-ano -p tcp",c),"TCP");
 // v0.9.6.0: read through the firewall's own COM API (HNetCfg.FwPolicy2): every rule in about a tenth of a second, where
 // PowerShell's Get-NetFirewallPortFilter took half a minute on this machine. A rule counts for the port only when it
 // covers the port and is not limited to some other program or service (a game's own "Palworld.exe" rule opens nothing
 // for the dedicated server). Each rule also reports the server id in its description (FirewallRules).
 public Task<IReadOnlyList<FirewallRuleInfo>> GetInboundFirewallRulesAsync(int port,string protocol,CancellationToken c=default)=>Task.Run<IReadOnlyList<FirewallRuleInfo>>(()=>
  ReadRules().Where(r=>r.Inbound&&FirewallRules.CoversProtocol(r.Protocol,protocol)&&FirewallRules.CoversPort(r.LocalPorts,port)&&FirewallRules.AppliesToServer(r.Application,r.Service,r.Package,r.Owner)).Select(r=>r.ToInfo(protocol.ToUpperInvariant(),port)).ToArray(),c);
 public Task<IReadOnlyList<FirewallRuleInfo>> GetMystTiqFirewallRulesAsync(CancellationToken c=default)=>Task.Run<IReadOnlyList<FirewallRuleInfo>>(()=>
  ReadRules().Where(r=>r.Name.StartsWith(FirewallRules.NamePrefix,StringComparison.Ordinal)).Select(r=>r.ToInfo(r.Protocol==6?"TCP":"UDP",FirewallRules.FirstPort(r.LocalPorts))).ToArray(),c);
 private sealed record ComRule(string Name,string? Description,bool Enabled,bool Inbound,bool Allow,int Protocol,string LocalPorts,string? Application,string? Service,int Profiles,string? Package,string? Owner)
 {
  public FirewallRuleInfo ToInfo(string protocol,int port)=>new(Name,Enabled,Inbound?"Inbound":"Outbound",Allow?"Allow":"Block",protocol,port,FirewallRules.ProfileNames(Profiles),Name.StartsWith(FirewallRules.NamePrefix,StringComparison.Ordinal),FirewallRules.ServerIdFromDescription(Description));
 }
 private static List<ComRule> ReadRules()
 {
  var type=Type.GetTypeFromProgID("HNetCfg.FwPolicy2")??throw new InvalidOperationException("Windows Firewall is not available on this computer.");
  dynamic policy=Activator.CreateInstance(type)!;var list=new List<ComRule>();
  foreach(dynamic r in policy.Rules)
  {
   try{list.Add(new ComRule((string)(r.Name??""),(string?)r.Description,(bool)r.Enabled,(int)r.Direction==1,(int)r.Action==1,(int)r.Protocol,(string)(r.LocalPorts??"*"),(string?)r.ApplicationName,(string?)r.serviceName,(int)r.Profiles,Optional(()=>(string?)r.LocalAppPackageId),Optional(()=>(string?)r.LocalUserOwner)));}
   catch{}
  }
  return list;
 }
 // LocalAppPackageId and LocalUserOwner come from INetFwRule3, which an older Windows may not have.
 private static string? Optional(Func<string?> read){try{return read();}catch{return null;}}
 // v0.9.6.0: the rule is tagged with the server's id and that server's rules for an old port are removed. Without
 // administrator rights the change is refused; the result then carries the script for the Desktop to run elevated.
 public async Task<FirewallRepairResult> RepairInboundFirewallRuleAsync(int port,string protocol,string? serverId,CancellationToken c=default)
 {
  var name=FirewallRules.RuleName(port,protocol);var script=FirewallRules.AllowScript(port,protocol,serverId);
  try
  {
   var lines=(await PS(script,c)).Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
   var removed=lines.Where(l=>l.StartsWith("REMOVED ",StringComparison.Ordinal)).Select(l=>l[8..]).ToArray();
   var verb=lines.Contains("CREATED")?"Created":"Repaired";
   return new(true,true,$"{verb}: {name}"+(removed.Length>0?$". Removed the rule(s) for this server's old port: {string.Join(", ",removed)}.":""),Removed:removed);
  }
  catch(Exception ex) when(FirewallRules.IsAccessDenied(ex.Message)){return new(false,false,$"Windows refused the change: MystTiq's service is not running as an administrator. Allow it as an administrator to add {name}.",NeedsElevation:true,Script:script);}
  catch(Exception ex){return new(false,false,$"Firewall repair failed: {ex.Message}");}
 }
 public IReadOnlyList<string> GetLanIPv4Addresses()=>NetworkAddressHelper.GetLanIPv4Addresses();
 private static IReadOnlyList<NetworkEndpointInfo> Parse(string text,string proto){var r=new List<NetworkEndpointInfo>();foreach(var raw in text.Split('\n',StringSplitOptions.RemoveEmptyEntries)){var a=raw.Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);if(a.Length<4||!a[0].Equals(proto,StringComparison.OrdinalIgnoreCase))continue;var local=a[1];var i=local.LastIndexOf(':');if(i<0||!int.TryParse(local[(i+1)..],out var port)||!int.TryParse(a[^1],out var pid))continue;string? n=null,p=null;try{using var x=Process.GetProcessById(pid);n=x.ProcessName;p=x.MainModule?.FileName;}catch{}r.Add(new(proto,local[..i].Trim('[',']'),port,pid,n,p));}return r;}
 // v0.9.6.0: by full path. This machine's PATH had System32 but not System32\WindowsPowerShell\v1.0, so starting a bare
 // "powershell.exe" failed ("cannot find the file specified") and the firewall could be neither checked nor repaired.
 private static string SystemTool(string name){var sys=Environment.GetFolderPath(Environment.SpecialFolder.System);var p=Path.Combine(sys,name);return File.Exists(p)?p:name;}
 private static string WindowsPowerShell(){var p=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");return File.Exists(p)?p:"powershell.exe";}
 // v0.9.6.0: -EncodedCommand, so quotes in a rule name or a server id cannot break the script.
 private static Task<string> PS(string s,CancellationToken c)=>Run(WindowsPowerShell(),"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand "+Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes("$ProgressPreference='SilentlyContinue';"+s)),c);
 private static async Task<string> Run(string f,string a,CancellationToken c){var si=new ProcessStartInfo(f,a){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};using var p=Process.Start(si)??throw new InvalidOperationException($"Unable to start {f}.");var o=await p.StandardOutput.ReadToEndAsync(c);var e=await p.StandardError.ReadToEndAsync(c);await p.WaitForExitAsync(c);if(p.ExitCode!=0)throw new InvalidOperationException($"{f} exited {p.ExitCode}: {e.Trim()}");return o;}
}
internal static class NetworkAddressHelper
{
 public static IReadOnlyList<string> GetLanIPv4Addresses()=>NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback).SelectMany(n=>n.GetIPProperties().UnicastAddresses).Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(a.Address)&&!a.Address.ToString().StartsWith("169.254.")).Select(a=>a.Address.ToString()).Distinct().ToArray();
}
