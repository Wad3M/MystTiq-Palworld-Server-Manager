namespace MystTiq.Core.Models;

// v0.6.11.0: external/WAN reachability is the genuine gap left after v0.6.4.0's Local Machine
// Diagnostics and the already-shipped inbound-firewall check (both LAN/host-local). True automated
// "is this UDP port reachable from the internet" confirmation needs a cooperating external listener
// we don't own, so this stays honest: automate what's genuinely automatable (public IP, UPnP router
// mapping) and hand off the rest (see WanReachabilityReport.ExternalCheckerHint).
public enum UpnpMappingState { Unknown, Mapped, NotMapped, RouterUnreachable, Unsupported }
public sealed record WanReachabilityCheck(string Test,DiagnosticState State,string Details,string Recommendation="");
public sealed record UpnpRepairResult(bool Success,bool Changed,string Message);
public sealed record WanReachabilityReport(DateTimeOffset CheckedAt,string? PublicIPv4,int GamePort,UpnpMappingState UpnpState,string? RouterDescription,IReadOnlyList<WanReachabilityCheck> Checks);
