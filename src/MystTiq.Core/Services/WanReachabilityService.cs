using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using MystTiq.Core.Models;
namespace MystTiq.Core.Services;

// Platform-independent: UPnP IGD (SSDP + SOAP) and the public-IP lookup are pure .NET networking
// with no OS-specific behavior, unlike WindowsNetworkDiagnosticsPlatformService/Linux's PowerShell
// and netstat use. Only GetLanIPv4Addresses() is borrowed from the existing platform service, as
// the AddPortMapping target.
public sealed class WanReachabilityService(INetworkDiagnosticsPlatformService platform)
{
 private static readonly string[] ServiceTypes=["urn:schemas-upnp-org:service:WANIPConnection:1","urn:schemas-upnp-org:service:WANPPPConnection:1","urn:schemas-upnp-org:service:WANIPConnection:2"];
 private static readonly string[] DeviceTypes=["urn:schemas-upnp-org:device:InternetGatewayDevice:1","urn:schemas-upnp-org:device:InternetGatewayDevice:2"];

 public async Task<WanReachabilityReport> RunAsync(int gamePort,CancellationToken token=default)
 {
  var checks=new List<WanReachabilityCheck>();
  string? publicIp=null;
  try
  {
   using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(6)};
   publicIp=(await http.GetStringAsync("https://api.ipify.org?format=text",token)).Trim();
   checks.Add(new("Public IP Address",DiagnosticState.Pass,$"Detected public IPv4: {publicIp}."));
  }
  catch(Exception ex)
  {
   checks.Add(new("Public IP Address",DiagnosticState.Skipped,$"Could not detect public IP: {ex.Message}.","Check your internet connection, or find it via your router's WAN status page."));
  }

  UpnpMappingState upnpState;string? routerDescription=null;string? routerWanIp=null;var routerRefusedWan=false;
  try
  {
   var igd=await DiscoverAsync(token);
   if(igd is null)
   {
    upnpState=UpnpMappingState.Unsupported;
    checks.Add(new("Router UPnP Discovery",DiagnosticState.Warning,"No UPnP Internet Gateway Device responded on the LAN.","Enable UPnP on your router, or forward the port manually in your router's admin page."));
   }
   else
   {
    routerDescription=igd.Value.FriendlyName;
    // v0.8.12.0: the router's own internet-side address, for the second-NAT check below. A router that will not say
    // is not an error; the check then reports it could not tell.
    // The real router answers this intermittently (UPnP error 501 on some tries), so a refusal is asked once more.
    try
    {
     (routerWanIp,routerRefusedWan)=await GetExternalIpAsync(igd.Value,token);
     if(routerRefusedWan){await Task.Delay(500,token);(routerWanIp,routerRefusedWan)=await GetExternalIpAsync(igd.Value,token);}
    }
    catch(Exception ex) when(ex is not OperationCanceledException){routerWanIp=null;}
    var entry=await GetSpecificPortMappingEntryAsync(igd.Value,gamePort,"UDP",token);
    if(entry is null)
    {
     upnpState=UpnpMappingState.NotMapped;
     checks.Add(new("Router Port Mapping",DiagnosticState.Fail,$"{igd.Value.FriendlyName} has no UPnP mapping for UDP {gamePort}.","Use \"Add UPnP Port Mapping\" below, or forward the port manually."));
    }
    else
    {
     upnpState=UpnpMappingState.Mapped;
     checks.Add(new("Router Port Mapping",DiagnosticState.Pass,$"{igd.Value.FriendlyName} maps external UDP {gamePort} -> {entry}."));
    }
   }
  }
  catch(Exception ex)
  {
   upnpState=UpnpMappingState.RouterUnreachable;
   checks.Add(new("Router UPnP Discovery",DiagnosticState.Warning,$"UPnP inspection unavailable: {ex.Message}.","Forward the port manually in your router's admin page."));
  }

  // v0.8.12.0: a forward on the router only helps when no second NAT sits between it and the internet (NatTopology).
  // The router's own WAN address decides it; without UPnP, the first hops of the route out are the (weaker) evidence.
  var secondNat=NatTopology.Classify(publicIp,routerWanIp);
  if(secondNat.State==DiagnosticState.Skipped)
  {
   try{secondNat=NatTopology.ClassifyRoute(await TraceFirstHopsAsync(token),routerRefusedWan)??secondNat;}
   catch(Exception ex) when(ex is not OperationCanceledException){}
  }
  checks.Add(secondNat);
  // v0.8.12.0: say plainly what no check here can prove.
  checks.Add(new(OutsideInTestName,DiagnosticState.Skipped,
   "MystTiq cannot send a real Palworld packet from outside your network, so it cannot prove the server is reachable from the internet. The checks above find the usual blockers.",
   (publicIp is null?$"Ask a friend outside your network to join your public address on port {gamePort}.":$"Ask a friend outside your network to join {publicIp}:{gamePort}.")+" Port-checker websites mostly test TCP; Palworld uses UDP, so their answer is not reliable here."));

  return new(DateTimeOffset.UtcNow,publicIp,gamePort,upnpState,routerDescription,checks,routerWanIp);
 }

 public const string OutsideInTestName="Outside-in test";

 // v0.8.12.0: only an http(s) URL counts as absolute. On Linux "/ctl/IPConn" parses as an absolute file:// URI, which made
 // every UPnP call fail there with "The 'file' scheme is not supported" (seen on the test VM). Pure, for the harness.
 public static Uri ResolveControlUrl(Uri location,string controlUrlText)=>
  Uri.TryCreate(controlUrlText,UriKind.Absolute,out var abs)&&(abs.Scheme==Uri.UriSchemeHttp||abs.Scheme==Uri.UriSchemeHttps)
   ?abs:new Uri(new Uri($"{location.Scheme}://{location.Authority}"),controlUrlText);

 // v0.8.12.0: the first few hops toward a public anycast address, like tracert -d -h 4 (ICMP echo with a rising TTL).
 // A hop that does not answer is null. Stops at the target.
 // Linux (checked on the test VM): an unprivileged process can only ping through the system ping tool, which takes the
 // default payload only (a custom one throws PlatformNotSupportedException), and a hop answers as TimeExceeded there
 // where Windows says TtlExpired.
 private static async Task<IReadOnlyList<string?>> TraceFirstHopsAsync(CancellationToken token,int maxHops=4)
 {
  var target=IPAddress.Parse("1.1.1.1");
  var hops=new List<string?>();
  using var ping=new System.Net.NetworkInformation.Ping();
  for(var ttl=1;ttl<=maxHops;ttl++)
  {
   token.ThrowIfCancellationRequested();
   var reply=await ping.SendPingAsync(target,TimeSpan.FromSeconds(1),null,new System.Net.NetworkInformation.PingOptions(ttl,true),token);
   var answered=reply.Status is System.Net.NetworkInformation.IPStatus.TtlExpired or System.Net.NetworkInformation.IPStatus.TimeExceeded or System.Net.NetworkInformation.IPStatus.Success;
   hops.Add(answered&&reply.Address is not null&&!reply.Address.Equals(IPAddress.Any)?reply.Address.ToString():null);
   if(reply.Status==System.Net.NetworkInformation.IPStatus.Success)break;
  }
  return hops;
 }

 // v0.8.12.0: UPnP IGD GetExternalIPAddress -- the address the router itself holds on its internet side.
 // Refused: the router answered with a SOAP fault (e.g. 501) instead of an address.
 private static async Task<(string? Address,bool Refused)> GetExternalIpAsync(IgdEndpoint igd,CancellationToken token)
 {
  var(success,xml)=await SoapCallAsync(igd,"GetExternalIPAddress",Soap(igd.ServiceType,"GetExternalIPAddress"),token);
  if(!success)return(null,true);
  var value=XDocument.Parse(xml).Descendants().FirstOrDefault(e=>e.Name.LocalName=="NewExternalIPAddress")?.Value?.Trim();
  return(string.IsNullOrEmpty(value)?null:value,false);
 }

 public async Task<UpnpRepairResult> RepairUpnpMappingAsync(int gamePort,CancellationToken token=default)
 {
  var igd=await DiscoverAsync(token);
  if(igd is null)return new(false,false,"No UPnP Internet Gateway Device responded on the LAN. Forward the port manually in your router's admin page.");
  var lan=platform.GetLanIPv4Addresses().FirstOrDefault();
  if(lan is null)return new(false,false,"Could not determine this machine's LAN IPv4 address.");
  var body=Soap(igd.Value.ServiceType,"AddPortMapping",
   ("NewRemoteHost",""),("NewExternalPort",gamePort.ToString()),("NewProtocol","UDP"),("NewInternalPort",gamePort.ToString()),
   ("NewInternalClient",lan),("NewEnabled","1"),("NewPortMappingDescription",$"MystTiq Palworld Server - Game UDP {gamePort}"),("NewLeaseDuration","0"));
  try
  {
   var(success,response)=await SoapCallAsync(igd.Value,"AddPortMapping",body,token);
   if(!success)return new(false,false,$"{igd.Value.FriendlyName} rejected the mapping request: {ExtractFaultMessage(response)}");
   return new(true,true,$"Mapped external UDP {gamePort} -> {lan}:{gamePort} on {igd.Value.FriendlyName}.");
  }
  catch(Exception ex){return new(false,false,$"UPnP mapping request failed: {ex.Message}.");}
 }

 private static string ExtractFaultMessage(string soapFaultXml)
 {
  try{var doc=XDocument.Parse(soapFaultXml);return doc.Descendants().FirstOrDefault(e=>e.Name.LocalName=="errorDescription")?.Value??"router did not accept the mapping.";}
  catch{return "router did not accept the mapping.";}
 }

 private readonly record struct IgdEndpoint(Uri ControlUrl,string ServiceType,string FriendlyName);

 private static async Task<IgdEndpoint?> DiscoverAsync(CancellationToken token)
 {
  foreach(var deviceType in DeviceTypes)
  {
   var location=await SsdpSearchAsync(deviceType,token);
   if(location is null)continue;
   var igd=await DescribeAsync(location,token);
   if(igd is not null)return igd;
  }
  return null;
 }

 // v0.8.12.0: the search goes out from every LAN interface that has a gateway, in parallel, and the first router to
 // answer wins. An unbound socket sent it out one interface of Windows' choosing: on this project's own server (a
 // second interface on the same subnet, the Hyper-V switch for the test VM) the router never heard it, so UPnP had
 // always looked "unsupported" there. Bound to either LAN interface, the same router answers at once.
 private static async Task<Uri?> SsdpSearchAsync(string deviceType,CancellationToken token)
 {
  var request=Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: "+deviceType+"\r\n\r\n");
  var searches=SearchInterfaces().Select(s=>SsdpSearchFromAsync(request,s.Local,s.Gateways,token)).ToList();
  while(searches.Count>0)
  {
   var done=await Task.WhenAny(searches);searches.Remove(done);
   var location=await done;
   if(location is not null)return location;
  }
  return null;
 }

 // Each up, non-loopback interface's IPv4 address with its IPv4 gateways; the unbound socket alone when there is none.
 private static IReadOnlyList<(IPAddress Local,IReadOnlyList<IPAddress> Gateways)> SearchInterfaces()
 {
  var result=new List<(IPAddress,IReadOnlyList<IPAddress>)>();
  try
  {
   foreach(var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
   {
    if(nic.OperationalStatus!=System.Net.NetworkInformation.OperationalStatus.Up||nic.NetworkInterfaceType==System.Net.NetworkInformation.NetworkInterfaceType.Loopback)continue;
    var props=nic.GetIPProperties();
    var gateways=props.GatewayAddresses.Select(g=>g.Address).Where(a=>a.AddressFamily==AddressFamily.InterNetwork&&!a.Equals(IPAddress.Any)).ToArray();
    if(gateways.Length==0)continue;
    foreach(var address in props.UnicastAddresses.Select(u=>u.Address).Where(a=>a.AddressFamily==AddressFamily.InterNetwork))
     result.Add((address,gateways));
   }
  }
  catch(System.Net.NetworkInformation.NetworkInformationException){}
  if(result.Count==0)result.Add((IPAddress.Any,Array.Empty<IPAddress>()));
  return result;
 }

 private static async Task<Uri?> SsdpSearchFromAsync(byte[] request,IPAddress local,IReadOnlyList<IPAddress> gateways,CancellationToken token)
 {
  using var udp=new UdpClient(new IPEndPoint(local,0)){EnableBroadcast=true};
  if(!local.Equals(IPAddress.Any))
  {
   try{udp.Client.SetSocketOption(SocketOptionLevel.IP,SocketOptionName.MulticastInterface,local.GetAddressBytes());}catch(SocketException){}
  }
  var target=new IPEndPoint(IPAddress.Parse("239.255.255.250"),1900);
  try{await udp.SendAsync(request,request.Length,target);}catch(SocketException){}
  // The same search straight to the gateway too, for routers that answer that sooner.
  foreach(var gateway in gateways)
  {
   try{await udp.SendAsync(request,request.Length,new IPEndPoint(gateway,1900));}catch(SocketException){}
  }
  using var cts=CancellationTokenSource.CreateLinkedTokenSource(token);cts.CancelAfter(TimeSpan.FromSeconds(3));
  try
  {
   while(!cts.IsCancellationRequested)
   {
    var result=await udp.ReceiveAsync(cts.Token);
    var text=Encoding.ASCII.GetString(result.Buffer);
    var line=text.Split("\r\n",StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(l=>l.StartsWith("LOCATION:",StringComparison.OrdinalIgnoreCase)||l.StartsWith("location:",StringComparison.OrdinalIgnoreCase));
    if(line is not null){var url=line[(line.IndexOf(':')+1)..].Trim();if(Uri.TryCreate(url,UriKind.Absolute,out var uri))return uri;}
   }
  }
  catch(OperationCanceledException){}
  catch(SocketException){}
  return null;
 }

 private static async Task<IgdEndpoint?> DescribeAsync(Uri location,CancellationToken token)
 {
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};
  var xml=await http.GetStringAsync(location,token);
  var doc=XDocument.Parse(xml);
  XNamespace ns=doc.Root?.GetDefaultNamespace()??"urn:schemas-upnp-org:device-1-0";
  var friendlyName=doc.Descendants(ns+"friendlyName").FirstOrDefault()?.Value??"Router";
  foreach(var serviceType in ServiceTypes)
  {
   var service=doc.Descendants(ns+"service").FirstOrDefault(s=>(string?)s.Element(ns+"serviceType")==serviceType);
   var controlUrlText=(string?)service?.Element(ns+"controlURL");
   if(string.IsNullOrWhiteSpace(controlUrlText))continue;
   var controlUrl=ResolveControlUrl(location,controlUrlText);
   return new IgdEndpoint(controlUrl,serviceType,friendlyName);
  }
  return null;
 }

 private static async Task<string?> GetSpecificPortMappingEntryAsync(IgdEndpoint igd,int port,string protocol,CancellationToken token)
 {
  var body=Soap(igd.ServiceType,"GetSpecificPortMappingEntry",("NewRemoteHost",""),("NewExternalPort",port.ToString()),("NewProtocol",protocol));
  var(success,xml)=await SoapCallAsync(igd,"GetSpecificPortMappingEntry",body,token);
  if(!success)return null; // router reachable but reported no matching entry (SOAP fault 714) -- genuinely not mapped, not an error
  var doc=XDocument.Parse(xml);
  var client=doc.Descendants().FirstOrDefault(e=>e.Name.LocalName=="NewInternalClient")?.Value;
  var internalPort=doc.Descendants().FirstOrDefault(e=>e.Name.LocalName=="NewInternalPort")?.Value;
  return client is null?null:$"{client}:{internalPort??port.ToString()}";
 }

 private static string Soap(string serviceType,string action,params (string Name,string Value)[] args)
 {
  var inner=string.Concat(args.Select(a=>$"<{a.Name}>{System.Security.SecurityElement.Escape(a.Value)}</{a.Name}>"));
  return $"<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body><u:{action} xmlns:u=\"{serviceType}\">{inner}</u:{action}></s:Body></s:Envelope>";
 }

 // Network-level failures (unreachable, timeout, DNS) propagate as exceptions -- the router never
 // responded. A SOAP fault (router responded, action failed -- e.g. error 714 "no such entry" for a
 // not-yet-mapped port) is a normal, expected outcome, not a connectivity error, so it comes back as
 // (false, faultBody) instead of throwing.
 private static async Task<(bool Success,string Body)> SoapCallAsync(IgdEndpoint igd,string action,string body,CancellationToken token)
 {
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(6)};
  using var request=new HttpRequestMessage(HttpMethod.Post,igd.ControlUrl){Content=new StringContent(body,Encoding.UTF8,"text/xml")};
  request.Headers.TryAddWithoutValidation("SOAPAction",$"\"{igd.ServiceType}#{action}\"");
  using var response=await http.SendAsync(request,token);
  var text=await response.Content.ReadAsStringAsync(token);
  return(response.IsSuccessStatusCode,text);
 }
}
