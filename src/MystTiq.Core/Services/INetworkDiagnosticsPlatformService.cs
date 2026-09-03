using MystTiq.Core.Models;
namespace MystTiq.Core.Services;
public interface INetworkDiagnosticsPlatformService
{
 Task<IReadOnlyList<NetworkEndpointInfo>> GetUdpEndpointsAsync(CancellationToken cancellationToken=default);
 Task<IReadOnlyList<NetworkEndpointInfo>> GetTcpListenersAsync(CancellationToken cancellationToken=default);
 Task<IReadOnlyList<FirewallRuleInfo>> GetInboundFirewallRulesAsync(int localPort,string protocol,CancellationToken cancellationToken=default);
 Task<FirewallRepairResult> RepairInboundFirewallRuleAsync(int localPort,string protocol,CancellationToken cancellationToken=default);
 IReadOnlyList<string> GetLanIPv4Addresses();
}
public static class NetworkDiagnosticsPlatformService
{
 public static INetworkDiagnosticsPlatformService ForCurrentPlatform()=>OperatingSystem.IsWindows()?new WindowsNetworkDiagnosticsPlatformService():OperatingSystem.IsLinux()?new LinuxNetworkDiagnosticsPlatformService():new Unsupported();
 private sealed class Unsupported:INetworkDiagnosticsPlatformService{
 public Task<IReadOnlyList<NetworkEndpointInfo>> GetUdpEndpointsAsync(CancellationToken c=default)=>Task.FromResult<IReadOnlyList<NetworkEndpointInfo>>([]);
 public Task<IReadOnlyList<NetworkEndpointInfo>> GetTcpListenersAsync(CancellationToken c=default)=>Task.FromResult<IReadOnlyList<NetworkEndpointInfo>>([]);
 public Task<IReadOnlyList<FirewallRuleInfo>> GetInboundFirewallRulesAsync(int p,string x,CancellationToken c=default)=>Task.FromResult<IReadOnlyList<FirewallRuleInfo>>([]);
 public Task<FirewallRepairResult> RepairInboundFirewallRuleAsync(int p,string x,CancellationToken c=default)=>Task.FromResult(new FirewallRepairResult(false,false,"Unsupported platform."));
 public IReadOnlyList<string> GetLanIPv4Addresses()=>[];}
}
