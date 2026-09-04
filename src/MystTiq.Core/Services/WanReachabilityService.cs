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

  UpnpMappingState upnpState;string? routerDescription=null;
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

  return new(DateTimeOffset.UtcNow,publicIp,gamePort,upnpState,routerDescription,checks);
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

 private static async Task<Uri?> SsdpSearchAsync(string deviceType,CancellationToken token)
 {
  var request=Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: "+deviceType+"\r\n\r\n");
  using var udp=new UdpClient(0){EnableBroadcast=true};
  var target=new IPEndPoint(IPAddress.Parse("239.255.255.250"),1900);
  await udp.SendAsync(request,request.Length,target);
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
   var controlUrl=Uri.TryCreate(controlUrlText,UriKind.Absolute,out var abs)?abs:new Uri(new Uri($"{location.Scheme}://{location.Authority}"),controlUrlText);
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
