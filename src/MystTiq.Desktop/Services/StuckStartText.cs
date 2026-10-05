// MystTiq v1.0.0.5: file reviewed for this release (2026-10-05).
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

/// <summary>v1.0.0.1: the Dashboard's addresses line, from GET /network/addresses. Pure, so the harness checks it.</summary>
public static class HostAddressText
{
    // (local addresses with the port, the public address with the port, where it came from or why it is missing)
    public static (string Local, string Public, string Note) Describe(HostAddressesDto addresses)
    {
        var port = addresses.GamePort;
        var local = addresses.Local.Count == 0 ? "—" : string.Join("  ·  ", addresses.Local.Select(a => $"{a.Address}:{port}"));
        if (!string.IsNullOrWhiteSpace(addresses.PublicAddress))
            return (local, $"{addresses.PublicAddress}:{port}", addresses.PublicSource == "router" ? "from your router" : "from api.ipify.org");
        return (local, "—", string.IsNullOrWhiteSpace(addresses.PublicError) ? "Not checked yet." : addresses.PublicError!);
    }
}

/// <summary>v1.0.0.1: the stuck-start panel's text for a running or finished test. Pure, so the harness checks it.</summary>
public static class StuckStartText
{
    public static string Describe(SafeStartStatusDto? status)
    {
        if (status is null) return string.Empty;
        var lines = new List<string>();
        if (status.IsRunning)
        {
            lines.Add(status.Phase);
            if (status.Mode != "TestLoad" && status.TotalCandidates > 0) lines.Add($"MODs tested: {status.TestedCount} of {status.TotalCandidates}.");
        }
        foreach (var result in status.Results)
            lines.Add(result.Package == "(no MODs)" ? $"Without MODs: {result.Detail}" : $"{result.Package}: {result.Detail}");
        if (status.Completed && !string.IsNullOrWhiteSpace(status.FinalMessage)) lines.Add(status.FinalMessage);
        return string.Join('\n', lines);
    }
}