// MystTiq v1.0.5.0: file reviewed for this release (2026-10-06).
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

// v0.9.6.0: the Windows Firewall rule MystTiq keeps for one server's game port. Each rule carries the server's id in its
// description, so when a server's port changes, allowing the new port removes that server's rule for the old one instead
// of leaving it open. Rules made before v0.9.6.0 have no id; they are kept and still count for their port.
//
// The scripts are built here, not inside the platform service, so the logic harness can check them without touching the
// firewall, and so the Desktop can run the same script elevated when the service itself may not change the firewall.
public static class FirewallRules
{
    public const string NamePrefix = "MystTiq Palworld Server - ";
    public const string Group = "MystTiq Palworld Server";
    private const string TagPrefix = "MystTiq server: ";

    public static string RuleName(int port, string protocol) => $"{NamePrefix}Game {protocol.ToUpperInvariant()} {port}";

    public static string Tag(string serverId) => TagPrefix + serverId;

    /// <summary>The server id a rule's description names, or null for a rule without one.</summary>
    public static string? ServerIdFromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var text = description.Trim();
        return text.StartsWith(TagPrefix, StringComparison.Ordinal) && text.Length > TagPrefix.Length ? text[TagPrefix.Length..].Trim() : null;
    }

    /// <summary>
    /// A PowerShell script that allows <paramref name="port"/> for one server: it removes that server's own rules for any
    /// other port, then creates or repairs the rule for this one (enabled, inbound, allow, every profile) and tags it.
    /// It prints CREATED or REPAIRED, then one REMOVED line per old rule.
    /// </summary>
    public static string AllowScript(int port, string protocol, string? serverId)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        var proto = Protocol(protocol);
        var name = Quote(RuleName(port, proto));
        var tag = string.IsNullOrWhiteSpace(serverId) ? null : Quote(Tag(serverId.Trim()));
        var cleanup = tag is null
            ? string.Empty
            : "$old=@(Get-NetFirewallRule -DisplayName '" + Quote(NamePrefix) + "*' -ErrorAction SilentlyContinue|Where-Object{$_.Description -eq $d -and $_.DisplayName -ne $n});"
              + "foreach($o in $old){$o|Remove-NetFirewallRule -ErrorAction Stop;$r+='REMOVED '+$o.DisplayName};";
        var describe = tag is null ? string.Empty : " -Description $d";
        return "$ErrorActionPreference='Stop';$n='" + name + "';$p=" + port + ";$proto='" + proto + "';" + (tag is null ? string.Empty : "$d='" + tag + "';")
            + "$r=@();$e=Get-NetFirewallRule -DisplayName $n -ErrorAction SilentlyContinue;"
            + "if($e){$e|Set-NetFirewallRule -Enabled True -Direction Inbound -Action Allow -Profile Any" + describe + ";$e|Get-NetFirewallPortFilter|Set-NetFirewallPortFilter -Protocol $proto -LocalPort $p;$r+='REPAIRED'}"
            + "else{New-NetFirewallRule -DisplayName $n -Group '" + Quote(Group) + "' -Direction Inbound -Action Allow -Enabled True -Profile Any -Protocol $proto -LocalPort $p" + describe + "|Out-Null;$r+='CREATED'};"
            + cleanup
            + "$r";
    }

    /// <summary>The commands a Linux administrator runs to open the port (MystTiq does not change a Linux firewall).</summary>
    public static IReadOnlyList<string> LinuxCommands(int port, string protocol)
    {
        var proto = Protocol(protocol).ToLowerInvariant();
        return
        [
            $"sudo ufw allow {port}/{proto} comment 'MystTiq Palworld Server'",
            $"sudo firewall-cmd --permanent --add-port={port}/{proto} && sudo firewall-cmd --reload",
        ];
    }

    /// <summary>Whether a failed firewall change failed because the caller is not an administrator.</summary>
    public static bool IsAccessDenied(string? message) =>
        !string.IsNullOrWhiteSpace(message)
        && (message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("PermissionDenied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("0x80070005", StringComparison.OrdinalIgnoreCase)
            || (message.Contains("Administrator", StringComparison.OrdinalIgnoreCase) && message.Contains("requires", StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// The state of one server's port from the rules that cover it and every MystTiq rule: allowed, blocked, and which of
    /// this server's own rules are for a port it no longer uses.
    /// </summary>
    // v0.9.8.0: currentNetwork names the network profile(s) in use ("Private"); a rule only counts where it covers one of them
    // (FirewallRuleInfo.CoversCurrentNetwork). A Private-only rule used to read as "allowed" on a Public network too.
    public static FirewallStatus Evaluate(int port, string protocol, string? serverId, IReadOnlyList<FirewallRuleInfo> portRules, IReadOnlyList<FirewallRuleInfo> mysttiqRules, bool supported = true, string? error = null, string? currentNetwork = null)
    {
        var proto = Protocol(protocol);
        var active = portRules.Where(r => r.CoversCurrentNetwork).ToArray();
        var blocked = active.Any(r => r.Enabled && r.Action.Equals("Block", StringComparison.OrdinalIgnoreCase));
        var allowed = !blocked && active.Any(r => r.Enabled && r.Action.Equals("Allow", StringComparison.OrdinalIgnoreCase));
        var otherNetworks = !allowed && !blocked && portRules.Any(r => !r.CoversCurrentNetwork && r.Enabled && r.Action.Equals("Allow", StringComparison.OrdinalIgnoreCase));
        var stale = string.IsNullOrWhiteSpace(serverId)
            ? []
            : mysttiqRules.Where(r => string.Equals(r.ServerId, serverId, StringComparison.OrdinalIgnoreCase) && r.LocalPort != port).Select(r => r.Name).Distinct().ToArray();
        var summary = !supported ? $"MystTiq cannot change this machine's firewall. Open {proto} {port} yourself."
            : error is not null ? $"The firewall could not be read: {error}"
            : blocked ? $"A rule blocks {proto} {port}. Players cannot join until it is removed or disabled."
            : allowed ? $"{proto} {port} is allowed through Windows Firewall."
            : otherNetworks ? $"A rule allows {proto} {port} only on other networks ({string.Join(", ", portRules.Where(r => !r.CoversCurrentNetwork).Select(r => r.Profiles).Distinct())}); this computer is on a {currentNetwork ?? "different"} network."
            : portRules.Count > 0 ? $"A rule for {proto} {port} exists but is turned off."
            : $"No rule allows {proto} {port}. Players on other computers cannot join.";
        if (supported && error is null && stale.Length > 0)
            summary += $" {stale.Length} old rule(s) for this server's previous port are still open.";
        return new FirewallStatus(port, proto, supported, allowed, blocked, portRules, stale, summary, error) { CurrentNetwork = currentNetwork };
    }

    /// <summary>Whether a rule's protocol number (17 UDP, 6 TCP, 256 any) covers <paramref name="protocol"/>.</summary>
    public static bool CoversProtocol(int ruleProtocol, string protocol) =>
        ruleProtocol == 256 || ruleProtocol == (Protocol(protocol) == "UDP" ? 17 : 6);

    /// <summary>Whether a rule's local ports ("*", "8211", "8211,27015", "8000-9000") include <paramref name="port"/>.</summary>
    public static bool CoversPort(string? localPorts, int port)
    {
        if (string.IsNullOrWhiteSpace(localPorts) || localPorts.Trim() == "*") return true;
        foreach (var raw in localPorts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dash = raw.IndexOf('-');
            if (dash > 0 && int.TryParse(raw[..dash], out var low) && int.TryParse(raw[(dash + 1)..], out var high))
            {
                if (port >= low && port <= high) return true;
            }
            else if (int.TryParse(raw, out var single) && single == port) return true;
        }

        return false;
    }

    /// <summary>The first port a rule names (0 when it names none), for MystTiq's own single-port rules.</summary>
    public static int FirstPort(string? localPorts)
    {
        var first = localPorts?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        var dash = first.IndexOf('-');
        return int.TryParse(dash > 0 ? first[..dash] : first, out var port) ? port : 0;
    }

    // The game binary that binds the port. Not PalServer.exe: that launcher only starts it, and a program rule covers only
    // the process that owns the socket (this machine had a "Palworld Server EXE" rule for the launcher, opening nothing).
    private static readonly string[] ServerExecutables = ["PalServer-Win64-Shipping-Cmd.exe", "PalServer-Win64-Shipping.exe"];

    /// <summary>
    /// Whether a rule can let traffic reach the dedicated server: one for every program, or for PalServer itself. A rule
    /// for another program (Steam, the Palworld game client), for a Windows service, or for Store apps (a package id, or
    /// an owner: those apply only inside app containers, so "Xbox" or "ChatGPT" with every port open nothing for PalServer)
    /// does not count.
    /// </summary>
    public static bool AppliesToServer(string? application, string? service, string? package = null, string? owner = null)
    {
        if (!string.IsNullOrWhiteSpace(service) && service.Trim() != "*") return false;
        if (!string.IsNullOrWhiteSpace(package) || !string.IsNullOrWhiteSpace(owner)) return false;
        if (string.IsNullOrWhiteSpace(application)) return true;
        var file = Path.GetFileName(application.Trim().Trim('"'));
        return ServerExecutables.Any(name => string.Equals(name, file, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether a rule's profile bits cover any of the profiles in use (0 in use: no network, so every rule counts).</summary>
    public static bool CoversNetwork(int ruleProfiles, int currentProfiles) => currentProfiles == 0 || (ruleProfiles & currentProfiles & 7) != 0;

    /// <summary>A rule's profile bits (1 Domain, 2 Private, 4 Public) as Windows shows them.</summary>
    public static string ProfileNames(int profiles)
    {
        if ((profiles & 7) == 7) return "Any";
        var names = new List<string>();
        if ((profiles & 1) != 0) names.Add("Domain");
        if ((profiles & 2) != 0) names.Add("Private");
        if ((profiles & 4) != 0) names.Add("Public");
        return names.Count == 0 ? "None" : string.Join(", ", names);
    }

    private static string Protocol(string protocol) => protocol.Trim().ToUpperInvariant() switch
    {
        "UDP" or "17" => "UDP",
        "TCP" or "6" => "TCP",
        _ => throw new ArgumentException($"Unsupported protocol '{protocol}'.", nameof(protocol)),
    };

    private static string Quote(string text) => text.Replace("'", "''");
}
