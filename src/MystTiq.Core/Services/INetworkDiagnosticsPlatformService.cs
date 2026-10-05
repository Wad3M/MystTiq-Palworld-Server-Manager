// MystTiq v1.0.0.5: file reviewed for this release (2026-10-05).
using MystTiq.Core.Models;
namespace MystTiq.Core.Services;
public interface INetworkDiagnosticsPlatformService
{
 Task<IReadOnlyList<NetworkEndpointInfo>> GetUdpEndpointsAsync(CancellationToken cancellationToken=default);
 Task<IReadOnlyList<NetworkEndpointInfo>> GetTcpListenersAsync(CancellationToken cancellationToken=default);
 Task<IReadOnlyList<FirewallRuleInfo>> GetInboundFirewallRulesAsync(int localPort,string protocol,CancellationToken cancellationToken=default);
 // v0.9.6.0: serverId tags the rule, and that server's rules for any other port are removed (see FirewallRules).
 Task<FirewallRepairResult> RepairInboundFirewallRuleAsync(int localPort,string protocol,string? serverId,CancellationToken cancellationToken=default);
 // v0.9.6.0: every rule MystTiq made, with the port and server each one is for.
 Task<IReadOnlyList<FirewallRuleInfo>> GetMystTiqFirewallRulesAsync(CancellationToken cancellationToken=default);
 // v0.9.8.0: the network profile(s) the computer is on ("Private"), or null where that does not apply.
 string? GetCurrentFirewallNetwork()=>null;
 IReadOnlyList<string> GetLanIPv4Addresses();
}
public static class NetworkDiagnosticsPlatformService
{
 public static INetworkDiagnosticsPlatformService ForCurrentPlatform()=>OperatingSystem.IsWindows()?new WindowsNetworkDiagnosticsPlatformService():OperatingSystem.IsLinux()?new LinuxNetworkDiagnosticsPlatformService():new Unsupported();
 private sealed class Unsupported:INetworkDiagnosticsPlatformService{
 public Task<IReadOnlyList<NetworkEndpointInfo>> GetUdpEndpointsAsync(CancellationToken c=default)=>Task.FromResult<IReadOnlyList<NetworkEndpointInfo>>([]);
 public Task<IReadOnlyList<NetworkEndpointInfo>> GetTcpListenersAsync(CancellationToken c=default)=>Task.FromResult<IReadOnlyList<NetworkEndpointInfo>>([]);
 public Task<IReadOnlyList<FirewallRuleInfo>> GetInboundFirewallRulesAsync(int p,string x,CancellationToken c=default)=>Task.FromResult<IReadOnlyList<FirewallRuleInfo>>([]);
 public Task<FirewallRepairResult> RepairInboundFirewallRuleAsync(int p,string x,string? id,CancellationToken c=default)=>Task.FromResult(new FirewallRepairResult(false,false,"Unsupported platform."));
 public Task<IReadOnlyList<FirewallRuleInfo>> GetMystTiqFirewallRulesAsync(CancellationToken c=default)=>Task.FromResult<IReadOnlyList<FirewallRuleInfo>>([]);
 public IReadOnlyList<string> GetLanIPv4Addresses()=>[];}
}
