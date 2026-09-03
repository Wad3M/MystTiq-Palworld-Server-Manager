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

            var arguments = distribution.BuildPalworldServerInstallArguments(
                paths.ServerRoot,
                validate);

            var startInfo = distribution.CreateSteamCmdStartInfo(
                paths.SteamCmdExecutable,
                workingDirectory,
                arguments);

            using var process = new Process { StartInfo = startInfo };
            var output = new List<string>();
            var outputGate = new object();

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    lock (outputGate) output.Add(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    lock (outputGate) output.Add(e.Data);
            };

            if (!process.Start())
                return HeadlessServerDistributionOperationResult.Failure(
                    "SteamCMD could not be started.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort cancellation cleanup.
                }

                throw;
            }

            string[] tail;
            lock (outputGate)
                tail = output.TakeLast(40).ToArray();

            if (process.ExitCode != 0)
            {
                return new HeadlessServerDistributionOperationResult(
                    false,
                    process.ExitCode,
                    validate,
                    File.Exists(paths.ServerExecutable),
                    tail,
                    $"SteamCMD failed with exit code {process.ExitCode}.");
            }

            if (!File.Exists(paths.ServerExecutable))
            {
                return new HeadlessServerDistributionOperationResult(
                    false,
                    process.ExitCode,
                    validate,
                    false,
                    tail,
                    $"SteamCMD completed but PalServer was not found at {paths.ServerExecutable}.");
            }

            return new HeadlessServerDistributionOperationResult(
                true,
                process.ExitCode,
                validate,
                true,
                tail,
                validate
                    ? "Palworld Dedicated Server update/validation completed successfully."
                    : "Palworld Dedicated Server update completed successfully.");
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
