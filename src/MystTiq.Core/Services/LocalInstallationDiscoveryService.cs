using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MystTiq.Core.Services;

public enum LocalMystTiqServiceState { Running, Stopped, NotInstalled, Unreachable, Unknown }
public enum LocalPalServerInstallationState { Found, NotFound, Unknown }

public sealed record LocalInstallationSnapshot(
    DateTimeOffset CheckedAt,
    string Platform,
    LocalMystTiqServiceState ServiceState,
    bool ServiceInstalled,
    int? ServiceProcessId,
    LocalPalServerInstallationState PalServerState,
    string? ServerRoot,
    string? ServerExecutable,
    bool ServerExecutableExists,
    bool PalServerProcessDetected,
    int? PalServerProcessId,
    string? SteamCmdPath,
    bool SteamCmdExists,
    string? ConfigurationPath,
    string? SaveRoot,
    string? LogsRoot,
    string? BackupRoot,
    string ApiBaseAddress,
    bool ApiAuthenticationEnabled,
    bool ApiTlsEnabled,
    string DiscoverySource,
    string Detail);

public interface ILocalInstallationDiscoveryService
{
    Task<LocalInstallationSnapshot> DiscoverAsync(CancellationToken cancellationToken = default);
}

public static class LocalInstallationDiscoveryService
{
    public static ILocalInstallationDiscoveryService ForCurrentPlatform() => new CrossPlatformLocalInstallationDiscoveryService();
}

internal sealed class CrossPlatformLocalInstallationDiscoveryService : ILocalInstallationDiscoveryService
{
    private const string WindowsServiceName = "MystTiqPalworld";
    private const string LinuxServiceName = "mysttiq-palworld";

    public async Task<LocalInstallationSnapshot> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var platform = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsLinux() ? "linux" : "unsupported";
        var configPath = FindConfigurationPath();
        var legacyPath = OperatingSystem.IsWindows() ? FindLegacySettingsPath() : null;
        var configured = ReadConfiguration(configPath);
        var legacy = ReadLegacySettings(legacyPath);

        var serverRoot = FirstNonEmpty(
            Environment.GetEnvironmentVariable("MYSTTIQ_SERVER_ROOT"), configured.ServerRoot, legacy.ServerRoot,
            OperatingSystem.IsWindows() ? @"C:\GameServers\Palworld\Server" : OperatingSystem.IsLinux() ? "/opt/mysttiq/palserver" : null);
        var steamCmdPath = FirstNonEmpty(
            Environment.GetEnvironmentVariable("MYSTTIQ_STEAMCMD_PATH"), configured.SteamCmdPath, legacy.SteamCmdPath,
            OperatingSystem.IsWindows() ? @"C:\GameServers\Palworld\SteamCMD\steamcmd.exe" : OperatingSystem.IsLinux() ? "/opt/mysttiq/steamcmd/steamcmd.sh" : null);
        var backupRoot = FirstNonEmpty(configured.BackupRoot, legacy.BackupRoot, serverRoot is null ? null : Path.Combine(serverRoot, "Backups"));
        var serverExecutable = serverRoot is null ? null : Path.Combine(serverRoot, OperatingSystem.IsWindows() ? "PalServer.exe" : "PalServer.sh");
        var executableExists = serverExecutable is not null && File.Exists(serverExecutable);
        var process = FindPalServerProcess(serverRoot);
        var service = await GetServiceStatusAsync(cancellationToken);
        var apiAddress = configured.ApiBaseAddress ?? "http://127.0.0.1:8213";
        var source = configured.ServerRoot is not null ? "MystTiq persistent-service configuration" :
            legacy.ServerRoot is not null ? "Legacy MystTiq settings-v2.1.json" :
            Environment.GetEnvironmentVariable("MYSTTIQ_SERVER_ROOT") is not null ? "MYSTTIQ_SERVER_ROOT environment" : "Platform default";
        var state = executableExists || process.ProcessId is not null ? LocalPalServerInstallationState.Found : LocalPalServerInstallationState.NotFound;
        var saveRoot = serverRoot is null ? null : Path.Combine(serverRoot, "Pal", "Saved", "SaveGames");
        var logsRoot = serverRoot is null ? null : Path.Combine(serverRoot, "Pal", "Saved", "Logs");
        var detail = $"Discovery source: {source}. Server root: {serverRoot ?? "unresolved"}. " +
                     (executableExists ? "PalServer entry point found. " : process.ProcessId is not null ? $"PalServer process detected (PID {process.ProcessId}). " : "PalServer entry point was not found. ") +
                     $"MystTiq service: {service.State}.";

        return new LocalInstallationSnapshot(DateTimeOffset.UtcNow, platform, service.State, service.Installed, service.ProcessId, state,
            serverRoot, serverExecutable, executableExists, process.ProcessId is not null, process.ProcessId, steamCmdPath,
            steamCmdPath is not null && File.Exists(steamCmdPath), configPath ?? legacyPath, saveRoot, logsRoot, backupRoot,
            apiAddress, configured.AuthenticationEnabled, configured.TlsEnabled, source, detail);
    }

    private static string? FindConfigurationPath()
    {
        var explicitPath = Environment.GetEnvironmentVariable("MYSTTIQ_CONFIG_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)) return Path.GetFullPath(explicitPath);
        var candidates = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            candidates.Add(Path.Combine(common, "MystTiqPalworldServer", "mysttiq.json"));
            candidates.Add(Path.Combine(common, "MystTiq", "mysttiq.json"));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "mysttiq.json"));
        }
        else if (OperatingSystem.IsLinux())
        {
            candidates.Add("/etc/mysttiq/mysttiq.json");
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "mysttiq.json"));
        }
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindLegacySettingsPath()
    {
        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var path = Path.Combine(common, "MystTiqPalworldServer", "settings-v2.1.json");
        return File.Exists(path) ? path : null;
    }

    private static (string? ServerRoot, string? SteamCmdPath, string? BackupRoot, string? ApiBaseAddress, bool AuthenticationEnabled, bool TlsEnabled) ReadConfiguration(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return (null, null, null, null, false, false);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement; var server = TryProperty(root, "Server"); var api = TryProperty(root, "Api");
            var bind = ReadString(api, "BindAddress") ?? "127.0.0.1"; var port = ReadInt(api, "Port") ?? 8213;
            var auth = ReadBool(TryProperty(api, "Authentication"), "Enabled") ?? false; var tls = ReadBool(TryProperty(api, "Tls"), "Enabled") ?? false;
            if (bind is "0.0.0.0" or "::") bind = "127.0.0.1";
            return (ReadString(server, "ServerRoot"), ReadString(server, "SteamCmdPath"), ReadString(server, "BackupRoot"), $"{(tls ? "https" : "http")}://{bind}:{port}", auth, tls);
        }
        catch { return (null, null, null, null, false, false); }
    }

    private static (string? ServerRoot, string? SteamCmdPath, string? BackupRoot) ReadLegacySettings(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return (null, null, null);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path)); var root = document.RootElement;
            return (ReadString(root, "ServerRoot"), ReadString(root, "SteamCmdPath"), ReadString(root, "BackupRoot"));
        }
        catch { return (null, null, null); }
    }

    private static async Task<(LocalMystTiqServiceState State, bool Installed, int? ProcessId)> GetServiceStatusAsync(CancellationToken token)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var result = await RunAsync("sc.exe", ["queryex", WindowsServiceName], token);
                if (result.ExitCode == 1060 || result.Output.Contains("FAILED 1060", StringComparison.OrdinalIgnoreCase)) return (LocalMystTiqServiceState.NotInstalled, false, null);
                var state = Regex.Match(result.Output, @"STATE\s*:\s*(\d+)\s+([A-Z_]+)"); var pid = Regex.Match(result.Output, @"PID\s*:\s*(\d+)");
                var mapped = (state.Success ? state.Groups[2].Value : "UNKNOWN") switch
                {
                    "RUNNING" => LocalMystTiqServiceState.Running,
                    "STOPPED" or "START_PENDING" or "STOP_PENDING" => LocalMystTiqServiceState.Stopped,
                    _ => LocalMystTiqServiceState.Unknown
                };
                int? processId = pid.Success && int.TryParse(pid.Groups[1].Value, out var value) && value > 0 ? value : null;
                return (mapped, true, processId);
            }
            catch { return (LocalMystTiqServiceState.Unreachable, true, null); }
        }
        if (OperatingSystem.IsLinux())
        {
            try
            {
                var active = await RunAsync("systemctl", ["is-active", LinuxServiceName], token);
                var enabled = await RunAsync("systemctl", ["is-enabled", LinuxServiceName], token);
                if (enabled.ExitCode != 0 && active.Output.Trim() == "inactive") return (LocalMystTiqServiceState.NotInstalled, false, null);
                return active.Output.Trim() switch
                {
                    "active" => (LocalMystTiqServiceState.Running, true, null),
                    "inactive" or "failed" => (LocalMystTiqServiceState.Stopped, true, null),
                    _ => (LocalMystTiqServiceState.Unknown, true, null)
                };
            }
            catch { return (LocalMystTiqServiceState.Unreachable, true, null); }
        }
        return (LocalMystTiqServiceState.Unknown, false, null);
    }

    private static (int? ProcessId, string? ExecutablePath) FindPalServerProcess(string? serverRoot)
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (!process.ProcessName.StartsWith("PalServer", StringComparison.OrdinalIgnoreCase)) continue;
                string? path = null; try { path = process.MainModule?.FileName; } catch { }
                if (serverRoot is null || path is null || IsPathInside(path, serverRoot)) return (process.Id, path);
            }
            catch { }
            finally { process.Dispose(); }
        }
        return (null, null);
    }

    private static bool IsPathInside(string path, string root)
    {
        try
        {
            var fullPath = Path.GetFullPath(path); var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch { return false; }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken token)
    {
        var info = new ProcessStartInfo { FileName = fileName, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Unable to start {fileName}.");
        var stdout = process.StandardOutput.ReadToEndAsync(token); var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token); return (process.ExitCode, (await stdout) + (await stderr));
    }

    private static JsonElement TryProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return default;
        foreach (var property in element.EnumerateObject()) if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        return default;
    }
    private static string? ReadString(JsonElement element, string name) { var value = TryProperty(element, name); return value.ValueKind == JsonValueKind.String ? value.GetString() : null; }
    private static int? ReadInt(JsonElement element, string name) { var value = TryProperty(element, name); return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) ? result : null; }
    private static bool? ReadBool(JsonElement element, string name) { var value = TryProperty(element, name); return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null; }
    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
