using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using MystTiq.Core.Models;
namespace MystTiq.Core.Services;
[SupportedOSPlatform("linux")]
public sealed class LinuxNetworkDiagnosticsPlatformService:INetworkDiagnosticsPlatformService
{
 public async Task<IReadOnlyList<NetworkEndpointInfo>> GetUdpEndpointsAsync(CancellationToken c=default)=>Parse(await Run("-lunp",c),"UDP");
 public async Task<IReadOnlyList<NetworkEndpointInfo>> GetTcpListenersAsync(CancellationToken c=default)=>Parse(await Run("-lntp",c),"TCP");
 public Task<IReadOnlyList<FirewallRuleInfo>> GetInboundFirewallRulesAsync(int p,string x,CancellationToken c=default)=>Task.FromResult<IReadOnlyList<FirewallRuleInfo>>([]);
 public Task<FirewallRepairResult> RepairInboundFirewallRuleAsync(int p,string x,CancellationToken c=default)=>Task.FromResult(new FirewallRepairResult(false,false,"Automatic firewall repair is Windows-only; Linux firewall mutation remains intentionally unsupported."));
 public IReadOnlyList<string> GetLanIPv4Addresses()=>NetworkAddressHelper.GetLanIPv4Addresses();
 private static IReadOnlyList<NetworkEndpointInfo> Parse(string t,string proto){var r=new List<NetworkEndpointInfo>();foreach(var line in t.Split('\n',StringSplitOptions.RemoveEmptyEntries).Skip(1)){var m=Regex.Match(line,@"\s(?<addr>\*|[0-9a-fA-F:\.]+):(?<port>\d+)\s+");if(!m.Success||!int.TryParse(m.Groups["port"].Value,out var port))continue;var pm=Regex.Match(line,@"pid=(?<pid>\d+)");int? pid=pm.Success&&int.TryParse(pm.Groups["pid"].Value,out var id)?id:null;string? n=null,p=null;if(pid is int i)try{using var q=Process.GetProcessById(i);n=q.ProcessName;p=q.MainModule?.FileName;}catch{}r.Add(new(proto,m.Groups["addr"].Value=="*"?"0.0.0.0":m.Groups["addr"].Value,port,pid,n,p));}return r;}
 private static async Task<string> Run(string a,CancellationToken c){var si=new ProcessStartInfo("ss",a){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};using var p=Process.Start(si)??throw new InvalidOperationException("Unable to start ss.");var o=await p.StandardOutput.ReadToEndAsync(c);await p.WaitForExitAsync(c);return o;}
}
