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
 public async Task<IReadOnlyList<NetworkEndpointInfo>> GetUdpEndpointsAsync(CancellationToken c=default)=>Parse(await Run("netstat.exe","-ano -p udp",c),"UDP");
 public async Task<IReadOnlyList<NetworkEndpointInfo>> GetTcpListenersAsync(CancellationToken c=default)=>Parse(await Run("netstat.exe","-ano -p tcp",c),"TCP");
 public async Task<IReadOnlyList<FirewallRuleInfo>> GetInboundFirewallRulesAsync(int port,string protocol,CancellationToken c=default)
 {
  var script="$p="+port+";$proto='"+protocol.ToUpperInvariant()+"';Get-NetFirewallRule -Direction Inbound -ErrorAction SilentlyContinue|%{$r=$_;$r|Get-NetFirewallPortFilter -ErrorAction SilentlyContinue|%{$f=$_;if(([string]$f.LocalPort -eq [string]$p -or [string]$f.LocalPort -eq 'Any') -and (([string]$f.Protocol -eq $proto)-or($proto -eq 'UDP'-and[string]$f.Protocol -eq '17')-or($proto -eq 'TCP'-and[string]$f.Protocol -eq '6'))){[pscustomobject]@{Name=$r.DisplayName;Enabled=([string]$r.Enabled -eq 'True');Direction=[string]$r.Direction;Action=[string]$r.Action;Protocol=$proto;LocalPort=$p;Profiles=[string]$r.Profile;ManagedByMystTiq=($r.DisplayName -like 'MystTiq Palworld Server -*')}}}}|ConvertTo-Json -Compress";
  var json=await PS(script,c);if(string.IsNullOrWhiteSpace(json))return[];
  using var doc=JsonDocument.Parse(json);var es=doc.RootElement.ValueKind==JsonValueKind.Array?doc.RootElement.EnumerateArray().ToArray():[doc.RootElement];
  return es.Select(e=>new FirewallRuleInfo(e.GetProperty("Name").GetString()??"Unnamed",e.GetProperty("Enabled").GetBoolean(),e.GetProperty("Direction").GetString()??"Inbound",e.GetProperty("Action").GetString()??"",e.GetProperty("Protocol").GetString()??protocol,e.GetProperty("LocalPort").GetInt32(),e.GetProperty("Profiles").GetString()??"",e.GetProperty("ManagedByMystTiq").GetBoolean())).ToArray();
 }
 public async Task<FirewallRepairResult> RepairInboundFirewallRuleAsync(int port,string protocol,CancellationToken c=default)
 {
  var name=$"MystTiq Palworld Server - Game {protocol.ToUpperInvariant()} {port}";var safe=name.Replace("'","''");
  var script="$n='"+safe+"';$p="+port+";$proto='"+protocol.ToUpperInvariant()+"';$e=Get-NetFirewallRule -DisplayName $n -ErrorAction SilentlyContinue;if($e){$e|Set-NetFirewallRule -Enabled True -Direction Inbound -Action Allow -Profile Any;$e|Get-NetFirewallPortFilter|Set-NetFirewallPortFilter -Protocol $proto -LocalPort $p;'REPAIRED'}else{New-NetFirewallRule -DisplayName $n -Direction Inbound -Action Allow -Enabled True -Profile Any -Protocol $proto -LocalPort $p|Out-Null;'CREATED'}";
  try{var r=(await PS(script,c)).Trim();return new(true,true,$"{r}: {name}");}catch(Exception ex){return new(false,false,$"Firewall repair failed: {ex.Message}");}
 }
 public IReadOnlyList<string> GetLanIPv4Addresses()=>NetworkAddressHelper.GetLanIPv4Addresses();
 private static IReadOnlyList<NetworkEndpointInfo> Parse(string text,string proto){var r=new List<NetworkEndpointInfo>();foreach(var raw in text.Split('\n',StringSplitOptions.RemoveEmptyEntries)){var a=raw.Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);if(a.Length<4||!a[0].Equals(proto,StringComparison.OrdinalIgnoreCase))continue;var local=a[1];var i=local.LastIndexOf(':');if(i<0||!int.TryParse(local[(i+1)..],out var port)||!int.TryParse(a[^1],out var pid))continue;string? n=null,p=null;try{using var x=Process.GetProcessById(pid);n=x.ProcessName;p=x.MainModule?.FileName;}catch{}r.Add(new(proto,local[..i].Trim('[',']'),port,pid,n,p));}return r;}
 private static Task<string> PS(string s,CancellationToken c)=>Run("powershell.exe","-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \""+s.Replace("\"","\\\"")+"\"",c);
 private static async Task<string> Run(string f,string a,CancellationToken c){var si=new ProcessStartInfo(f,a){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};using var p=Process.Start(si)??throw new InvalidOperationException($"Unable to start {f}.");var o=await p.StandardOutput.ReadToEndAsync(c);var e=await p.StandardError.ReadToEndAsync(c);await p.WaitForExitAsync(c);if(p.ExitCode!=0)throw new InvalidOperationException($"{f} exited {p.ExitCode}: {e.Trim()}");return o;}
}
internal static class NetworkAddressHelper
{
 public static IReadOnlyList<string> GetLanIPv4Addresses()=>NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback).SelectMany(n=>n.GetIPProperties().UnicastAddresses).Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(a.Address)&&!a.Address.ToString().StartsWith("169.254.")).Select(a=>a.Address.ToString()).Distinct().ToArray();
}
