using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using MystTiq.Core.Services;

namespace MystTiq.Desktop.Services;

public sealed record LocalManagementBootstrapResult(bool Available, bool Started, string Endpoint, string Detail, string? BackendVersion = null,
    bool StaleInstanceDetected = false, string? StaleInstanceEndpoint = null, string? StaleInstanceVersion = null);

public interface ILocalManagementBootstrapper
{
    // v0.6.2.0: expectedServerProfileId defaults to "default" (the sole profile in a non-fleet
    // deployment) -- the "already reachable, reuse it" fast path now also verifies that profile
    // is actually present in the probed instance's /healthz serverProfileIds list, not just that
    // *some* compatible-looking process answered. Closes a real cross-talk bug where a "local"
    // connection profile could silently attach to a different already-running MystTiq instance.
    Task<LocalManagementBootstrapResult> EnsureAvailableAsync(LocalInstallationSnapshot snapshot, string expectedServerProfileId = "default", CancellationToken cancellationToken = default);
    Task<bool> StopOwnedSidecarAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Ensures that the local Avalonia client has a headless API to connect to.
/// It never manages PalServer directly: it only starts the packaged headless sidecar when
/// no persistent management API is reachable. The sidecar is intentionally not tied to
/// the GUI process lifetime, so closing the GUI does not stop PalServer management.
/// </summary>
public sealed class LocalManagementBootstrapper : ILocalManagementBootstrapper
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(8);
    private readonly SemaphoreSlim gate = new(1, 1);
    private int? ownedSidecarProcessId;
    private string? ownedSidecarExecutable;

    public async Task<LocalManagementBootstrapResult> EnsureAvailableAsync(LocalInstallationSnapshot snapshot, string expectedServerProfileId = "default", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var requestedEndpoint = string.IsNullOrWhiteSpace(snapshot.ApiBaseAddress) ? "http://127.0.0.1:8213" : snapshot.ApiBaseAddress.TrimEnd('/');
        var existing = await ProbeAsync(requestedEndpoint, cancellationToken);
        if (existing.Compatible && !existing.AuthenticationEnabled && !existing.TlsEnabled && HasExpectedProfile(existing, expectedServerProfileId) && VersionMatches(existing.Version))
            return new(true, false, requestedEndpoint, $"Compatible MystTiq management API is already reachable ({existing.Version ?? "unknown version"}).", existing.Version);

        // A same-machine instance that IS reachable/compatible but reports a DIFFERENT version is a
        // real, previously-silent bug: apiVersion (the wire-protocol integer above) has stayed 1
        // across every release, so an old instance always passed the compatibility check and got
        // silently reused, driving a stale backend without any indication to the user. Flagged here
        // rather than fixed by force: this bootstrapper never kills a process it didn't itself
        // start, so the stale instance is left running and the user is told about it explicitly.
        var staleDetected = existing.Reachable && existing.Compatible && !VersionMatches(existing.Version);
        var staleEndpoint = staleDetected ? requestedEndpoint : null;
        var staleVersion = staleDetected ? existing.Version : null;

        var endpoint = requestedEndpoint;
        await gate.WaitAsync(cancellationToken);
        try
        {
            existing = await ProbeAsync(requestedEndpoint, cancellationToken);
            if (existing.Compatible && !existing.AuthenticationEnabled && !existing.TlsEnabled && HasExpectedProfile(existing, expectedServerProfileId) && VersionMatches(existing.Version))
                return new(true, false, requestedEndpoint, $"Compatible MystTiq management API became reachable while bootstrap was waiting ({existing.Version ?? "unknown version"}).", existing.Version);
            if (existing.Reachable && existing.Compatible && !VersionMatches(existing.Version))
            {
                staleDetected = true;
                staleEndpoint = requestedEndpoint;
                staleVersion = existing.Version;
            }

            // If the configured loopback port is occupied by an older/incompatible/mismatched-version
            // process, do not reuse it merely because /healthz answers. Launch this packaged sidecar
            // on a private free loopback port and return that exact endpoint to the GUI instead.
            endpoint = existing.Reachable ? FindAvailableLoopbackEndpoint() : NormalizePrivateLoopbackEndpoint(requestedEndpoint);

            var executable = FindPackagedHeadlessExecutable();
            if (executable is null)
                return new(false, false, endpoint, "Packaged MystTiq headless sidecar was not found next to the desktop application.",
                    null, staleDetected, staleEndpoint, staleVersion);

            var info = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            info.ArgumentList.Add("api-run");
            info.ArgumentList.Add("--desktop-sidecar");

            var persistentConfig = snapshot.ConfigurationPath;
            if (!string.IsNullOrWhiteSpace(persistentConfig) &&
                Path.GetFileName(persistentConfig).Equals("mysttiq.json", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(persistentConfig))
            {
                info.ArgumentList.Add("--config");
                info.ArgumentList.Add(persistentConfig);
            }
            // Legacy settings are discovery input only; never pass settings-v2.1.json to the headless schema-v2 loader.
            // The desktop-owned sidecar is always bound to the exact loopback endpoint selected above, even
            // when a persistent configuration file exists, so a stale/occupied configured port cannot hijack it.
            info.ArgumentList.Add("--bind-address");
            info.ArgumentList.Add("127.0.0.1");
            info.ArgumentList.Add("--api-port");
            info.ArgumentList.Add(new Uri(endpoint).Port.ToString(System.Globalization.CultureInfo.InvariantCulture));

            AddOverride(info, "--server-root", snapshot.ServerRoot);
            AddOverride(info, "--steamcmd", snapshot.SteamCmdPath);
            AddOverride(info, "--backup-root", snapshot.BackupRoot);

            var runtimeRoot = GetLocalRuntimeRoot();
            Directory.CreateDirectory(runtimeRoot);
            AddOverride(info, "--runtime-root", runtimeRoot);

            using (var started = Process.Start(info))
            {
                if (started is not null)
                {
                    ownedSidecarProcessId = started.Id;
                    ownedSidecarExecutable = executable;
                }
            }

            var deadline = DateTimeOffset.UtcNow + StartupTimeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var probe = await ProbeAsync(endpoint, cancellationToken);
                if (probe.Compatible)
                {
                    var detail = staleDetected
                        ? $"Started a fresh MystTiq {probe.Version ?? "unknown version"} backend on {endpoint} because a different version ({staleVersion ?? "unknown"}) is already running at {staleEndpoint}. That older instance was left running -- close it manually if you don't need it."
                        : $"Started the packaged MystTiq headless API for this machine ({probe.Version ?? "unknown version"}).";
                    return new(true, true, endpoint, detail, probe.Version, staleDetected, staleEndpoint, staleVersion);
                }
                await Task.Delay(250, cancellationToken);
            }

            return new(false, true, endpoint, "The packaged headless process was started, but its health endpoint did not become reachable before timeout.",
                null, staleDetected, staleEndpoint, staleVersion);
        }
        catch (Exception ex)
        {
            return new(false, false, endpoint, $"Unable to start the local MystTiq headless API: {ex.Message}");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> StopOwnedSidecarAsync(CancellationToken cancellationToken = default)
    {
        var processId = ownedSidecarProcessId;
        var expectedExecutable = ownedSidecarExecutable;
        if (processId is null || string.IsNullOrWhiteSpace(expectedExecutable)) return false;

        try
        {
            using var process = Process.GetProcessById(processId.Value);
            string? actualPath = null;
            try { actualPath = process.MainModule?.FileName; } catch { }
            if (!string.IsNullOrWhiteSpace(actualPath) &&
                !Path.GetFullPath(actualPath).Equals(Path.GetFullPath(expectedExecutable), StringComparison.OrdinalIgnoreCase))
                return false;

            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
            ownedSidecarProcessId = null;
            ownedSidecarExecutable = null;
            return true;
        }
        catch (ArgumentException)
        {
            ownedSidecarProcessId = null;
            ownedSidecarExecutable = null;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void AddOverride(ProcessStartInfo info, string option, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        info.ArgumentList.Add(option);
        info.ArgumentList.Add(value);
    }

    private static string GetLocalRuntimeRoot()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MystTiq", "runtime");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "state", "mysttiq", "runtime");
    }

    private static string? FindPackagedHeadlessExecutable()
    {
        var file = OperatingSystem.IsWindows() ? "mysttiq-server.exe" : "mysttiq-server";
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "headless", file),
            Path.Combine(AppContext.BaseDirectory, file)
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    // Real bug this closes: apiVersion (the coarse wire-protocol integer in /healthz) has stayed 1
    // across every v0.6.x.0 release, so it alone never distinguishes "this is the same version I'm
    // bundling" from "this is some other release entirely" -- an old instance always looked
    // "compatible" and got silently reused. A missing probed version (pre-version-reporting builds)
    // is treated leniently, matching HasExpectedProfile's own fallback posture below.
    private static bool VersionMatches(string? probedVersion)
    {
        if (string.IsNullOrWhiteSpace(probedVersion)) return true;
        var ownVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(4);
        return string.IsNullOrWhiteSpace(ownVersion) || probedVersion.Equals(ownVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasExpectedProfile(ProbeResult probe, string expectedServerProfileId) =>
        // Older (pre-v0.6.2.0) instances never report serverProfileIds at all -- treat an absent
        // list as "unknown, assume compatible" so a fresh desktop build against an older sidecar
        // doesn't regress into always relaunching its own copy.
        probe.ServerProfileIds is null || probe.ServerProfileIds.Count == 0 ||
        probe.ServerProfileIds.Any(id => id.Equals(expectedServerProfileId, StringComparison.OrdinalIgnoreCase));

    private sealed record ProbeResult(bool Reachable, bool Compatible, string? Version, bool AuthenticationEnabled, bool TlsEnabled, string Detail, IReadOnlyList<string>? ServerProfileIds = null);

    private static async Task<ProbeResult> ProbeAsync(string endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = static (_, _, _, _) => true
            };
            using var client = new HttpClient(handler) { Timeout = ProbeTimeout };
            using var response = await client.GetAsync(endpoint.TrimEnd('/') + "/healthz", cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
                return new(false, false, null, false, false, $"Health probe returned HTTP {(int)response.StatusCode}.");

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var component = root.TryGetProperty("component", out var componentProperty) ? componentProperty.GetString() : null;
            var version = root.TryGetProperty("version", out var versionProperty) ? versionProperty.GetString() : null;
            var apiVersion = root.TryGetProperty("apiVersion", out var apiVersionProperty) && apiVersionProperty.TryGetInt32(out var parsedApiVersion) ? parsedApiVersion : 0;
            var authentication = root.TryGetProperty("authentication", out var authenticationProperty) &&
                (authenticationProperty.ValueKind is JsonValueKind.True or JsonValueKind.False) && authenticationProperty.GetBoolean();
            var tls = root.TryGetProperty("tls", out var tlsProperty) &&
                (tlsProperty.ValueKind is JsonValueKind.True or JsonValueKind.False) && tlsProperty.GetBoolean();
            List<string>? serverProfileIds = null;
            if (root.TryGetProperty("serverProfileIds", out var profilesProperty) && profilesProperty.ValueKind == JsonValueKind.Array)
                serverProfileIds = profilesProperty.EnumerateArray().Select(e => e.GetString() ?? string.Empty).Where(s => s.Length > 0).ToList();
            var compatible = string.Equals(component, "mysttiq-headless", StringComparison.OrdinalIgnoreCase) && apiVersion >= 1;
            return new(true, compatible, version, authentication, tls, compatible
                ? "Compatible MystTiq management API."
                : "A process answered /healthz, but it is not a compatible MystTiq management API for this desktop build.",
                serverProfileIds);
        }
        catch (Exception ex)
        {
            return new(false, false, null, false, false, ex.Message);
        }
    }

    private static string NormalizePrivateLoopbackEndpoint(string requestedEndpoint)
    {
        if (Uri.TryCreate(requestedEndpoint, UriKind.Absolute, out var requested) && requested.Port is > 0 and <= 65535)
            return $"http://127.0.0.1:{requested.Port}";
        return "http://127.0.0.1:8213";
    }

    private static string FindAvailableLoopbackEndpoint()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            return $"http://127.0.0.1:{port}";
        }
        finally
        {
            listener.Stop();
        }
    }
}
