// MystTiq v0.9.7.0: file reviewed for this release (2026-09-29).
using System.Diagnostics;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessServerDistributionService
{
    private readonly IServerPathProfile paths;
    private readonly IServerLifecycleService lifecycle;
    private readonly IServerDistributionPlatformService distribution;
    private readonly SemaphoreSlim operationGate = new(1, 1);

    public HeadlessServerDistributionService(
        IServerPathProfile paths,
        IServerLifecycleService lifecycle,
        IServerDistributionPlatformService distribution)
    {
        this.paths = paths;
        this.lifecycle = lifecycle;
        this.distribution = distribution;
    }

    public HeadlessServerDistributionStatus GetStatus()
    {
        var steamCmdExists = File.Exists(paths.SteamCmdExecutable);
        var serverRootExists = Directory.Exists(paths.ServerRoot);
        var serverExecutableExists = File.Exists(paths.ServerExecutable);

        return new HeadlessServerDistributionStatus(
            distribution.PlatformId,
            paths.SteamCmdExecutable,
            steamCmdExists,
            paths.ServerRoot,
            serverRootExists,
            paths.ServerExecutable,
            serverExecutableExists,
            distribution.SteamCmdPackageUri.ToString(),
            DateTimeOffset.UtcNow,
            serverExecutableExists
                ? $"Palworld Dedicated Server is installed at {paths.ServerRoot}."
                : "Palworld Dedicated Server is not fully installed.");
    }

    public HeadlessServerDistributionPlan GetPlan(bool validate)
    {
        var arguments = distribution.BuildPalworldServerInstallArguments(
            paths.ServerRoot,
            validate);

        return new HeadlessServerDistributionPlan(
            distribution.PlatformId,
            paths.SteamCmdExecutable,
            paths.ServerRoot,
            validate,
            arguments,
            DateTimeOffset.UtcNow);
    }

    private sealed record SteamCmdRun(int ExitCode, IReadOnlyList<string> Output, IReadOnlyList<string> ContentLog);

    // One SteamCMD install/update run. ContentLog is what SteamCMD wrote to logs/content_log.txt during the run: its
    // stdout says only "state is 0x6 after update job", the reason ("Access Denied", "No connection") is logged there.
    private async Task<SteamCmdRun> RunSteamCmdAsync(string workingDirectory, bool validate, CancellationToken cancellationToken)
    {
        var contentLog = Path.Combine(workingDirectory, "logs", "content_log.txt");
        long before = 0;
        try { if (File.Exists(contentLog)) before = new FileInfo(contentLog).Length; } catch { }

        var startInfo = distribution.CreateSteamCmdStartInfo(
            paths.SteamCmdExecutable,
            workingDirectory,
            distribution.BuildPalworldServerInstallArguments(paths.ServerRoot, validate));
        using var process = new Process { StartInfo = startInfo };
        var output = new List<string>();
        var outputGate = new object();
        process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) lock (outputGate) output.Add(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) lock (outputGate) output.Add(e.Data); };
        if (!process.Start())
            return new SteamCmdRun(-1, ["SteamCMD could not be started."], []);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }

        string[] lines;
        lock (outputGate) lines = output.ToArray();
        return new SteamCmdRun(process.ExitCode, lines, ReadAppended(contentLog, before));
    }

    private static IReadOnlyList<string> ReadAppended(string path, long from)
    {
        try
        {
            if (!File.Exists(path)) return [];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < from) from = 0; // the log was rotated during the run
            stream.Seek(from, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch { return []; }
    }

    public async Task<HeadlessServerDistributionOperationResult> UpdateAsync(
        bool validate,
        CancellationToken cancellationToken)
    {
        if (!await operationGate.WaitAsync(0, cancellationToken))
            return HeadlessServerDistributionOperationResult.Conflict(
                "A server distribution operation is already in progress.");

        try
        {
            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.NativeProcessId.HasValue || status.Ready)
            {
                return HeadlessServerDistributionOperationResult.Failure(
                    "Stop PalServer before running SteamCMD update/validation.");
            }

            if (!File.Exists(paths.SteamCmdExecutable))
            {
                var provision = await ProvisionSteamCmdAsync(cancellationToken);
                if (!provision.Success)
                    return HeadlessServerDistributionOperationResult.Failure(provision.Message);
            }

            Directory.CreateDirectory(paths.ServerRoot);

            var workingDirectory = Path.GetDirectoryName(paths.SteamCmdExecutable)
                ?? throw new InvalidOperationException("SteamCMD path has no parent directory.");

            var run = await RunSteamCmdAsync(workingDirectory, validate, cancellationToken);

            // v0.9.5.0: Steam can refuse the manifest of the build that is installed ("Failed to get manifest request code,
            // 'Access Denied'"), which SteamCMD needs to work out what changed, so every update failed with exit code 8
            // (seen live on 2026-09-28, build 25080279 -> 25247047). Moving the app manifest aside makes SteamCMD check
            // every file against the new build instead and download only what differs. The old manifest is kept next to
            // it, and put back if the retry fails too.
            string? retryNote = null;
            if (run.ExitCode != 0 && SteamCmdFailure.IsManifestAccessDenied(run.Output, run.ContentLog))
            {
                var manifest = Path.Combine(paths.ServerRoot, "steamapps", "appmanifest_2394010.acf");
                if (File.Exists(manifest))
                {
                    var keptAs = manifest + ".before-update-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
                    File.Move(manifest, keptAs);
                    var retry = await RunSteamCmdAsync(workingDirectory, validate: true, cancellationToken);
                    if (retry.ExitCode != 0 && !File.Exists(manifest)) File.Move(keptAs, manifest);
                    retryNote = retry.ExitCode == 0
                        ? $" Steam refused the installed build's manifest, so MystTiq checked every file against the new build instead (the old manifest is kept as {Path.GetFileName(keptAs)})."
                        : " Steam refused the installed build's manifest; checking every file against the new build failed too.";
                    run = retry;
                    validate = true;
                }
            }

            var tail = run.Output.TakeLast(40).ToArray();
            if (run.ExitCode != 0)
            {
                return new HeadlessServerDistributionOperationResult(
                    false,
                    run.ExitCode,
                    validate,
                    File.Exists(paths.ServerExecutable),
                    tail,
                    $"SteamCMD failed with exit code {run.ExitCode}: {SteamCmdFailure.Describe(run.Output, run.ContentLog)}{retryNote}");
            }
            if (!File.Exists(paths.ServerExecutable))
            {
                return new HeadlessServerDistributionOperationResult(
                    false,
                    run.ExitCode,
                    validate,
                    false,
                    tail,
                    $"SteamCMD completed but PalServer was not found at {paths.ServerExecutable}.");
            }

            return new HeadlessServerDistributionOperationResult(
                true,
                run.ExitCode,
                validate,
                true,
                tail,
                (validate
                    ? "Palworld Dedicated Server update/validation completed successfully."
                    : "Palworld Dedicated Server update completed successfully.") + retryNote);
        }
        catch (OperationCanceledException)
        {
            return HeadlessServerDistributionOperationResult.Failure(
                "SteamCMD operation was cancelled.");
        }
        catch (Exception ex)
        {
            return HeadlessServerDistributionOperationResult.Failure(ex.Message);
        }
        finally
        {
            operationGate.Release();
        }
    }
    private async Task<(bool Success, string Message)> ProvisionSteamCmdAsync(CancellationToken cancellationToken)
    {
        try
        {
            var destination = Path.GetDirectoryName(paths.SteamCmdExecutable)
                ?? throw new InvalidOperationException("SteamCMD path has no parent directory.");
            Directory.CreateDirectory(destination);
            var extension = distribution.PlatformId == "windows" ? ".zip" : ".tar.gz";
            var packagePath = Path.Combine(Path.GetTempPath(), $"mysttiq-steamcmd-{Guid.NewGuid():N}{extension}");
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                using var response = await http.GetAsync(distribution.SteamCmdPackageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using (var output = File.Create(packagePath))
                    await response.Content.CopyToAsync(output, cancellationToken);
                distribution.ExtractSteamCmdPackage(packagePath, destination);
            }
            finally
            {
                try { if (File.Exists(packagePath)) File.Delete(packagePath); } catch { }
            }

            if (!File.Exists(paths.SteamCmdExecutable))
                return (false, $"SteamCMD package was extracted but {paths.SteamCmdExecutable} was not created.");

            if (!OperatingSystem.IsWindows())
            {
                try { File.SetUnixFileMode(paths.SteamCmdExecutable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute); } catch { }
            }
            return (true, $"SteamCMD provisioned at {paths.SteamCmdExecutable}.");
        }
        catch (Exception ex)
        {
            return (false, $"SteamCMD provisioning failed: {ex.Message}");
        }
    }


}

public sealed record HeadlessServerDistributionStatus(
    string Platform,
    string SteamCmdPath,
    bool SteamCmdExists,
    string ServerRoot,
    bool ServerRootExists,
    string ServerExecutable,
    bool ServerExecutableExists,
    string SteamCmdPackageUri,
    DateTimeOffset ObservedAt,
    string Detail);

public sealed record HeadlessServerDistributionPlan(
    string Platform,
    string SteamCmdPath,
    string ServerRoot,
    bool Validate,
    IReadOnlyList<string> Arguments,
    DateTimeOffset ObservedAt);

public sealed record HeadlessServerDistributionOperationResult(
    bool Success,
    int ExitCode,
    bool Validate,
    bool ServerExecutableExists,
    IReadOnlyList<string> OutputTail,
    string Message)
{
    public static HeadlessServerDistributionOperationResult Failure(string message) =>
        new(false, -1, false, false, [], message);

    public static HeadlessServerDistributionOperationResult Conflict(string message) =>
        new(false, -1, false, false, [], message);
}

// v0.9.5.0: what a failed SteamCMD run means, from its output and the lines it added to logs/content_log.txt.
public static class SteamCmdFailure
{
    public static bool IsManifestAccessDenied(IEnumerable<string> output, IEnumerable<string> contentLog) =>
        output.Concat(contentLog).Any(line =>
            line.Contains("Failed to get manifest request code", StringComparison.OrdinalIgnoreCase) &&
            line.Contains("Access Denied", StringComparison.OrdinalIgnoreCase));

    // The most telling line: Steam's own reason for cancelling, else SteamCMD's error line, else a plain fallback.
    public static string Describe(IReadOnlyList<string> output, IReadOnlyList<string> contentLog)
    {
        if (IsManifestAccessDenied(output, contentLog))
            return "Steam refused the manifest of the installed build (\"Access Denied\").";
        var canceled = contentLog.LastOrDefault(line => line.Contains("update canceled", StringComparison.OrdinalIgnoreCase));
        if (canceled is not null)
            return "Steam cancelled the update: " + canceled[(canceled.IndexOf("update canceled", StringComparison.OrdinalIgnoreCase) + "update canceled".Length)..].Trim(' ', ':') + ".";
        var error = output.LastOrDefault(line => line.Contains("Error!", StringComparison.OrdinalIgnoreCase) || line.Contains("ERROR", StringComparison.Ordinal));
        return error is not null ? error.Trim() : "SteamCMD gave no reason; see its logs folder.";
    }
}