using PalworldManager.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PalworldManager.Services;

/// <summary>
/// Runs the dedicated-server SteamCMD update/validation workflow.
/// </summary>
public sealed class SteamServerUpdateService
{
    private readonly AppSettings settings;
    private readonly Func<bool> isServerRunning;
    private readonly Action<string>? output;
    private readonly IServerDistributionPlatformService distribution;

    public SteamServerUpdateService(
        AppSettings settings,
        Func<bool> isServerRunning,
        Action<string>? output,
        IServerDistributionPlatformService? distribution = null)
    {
        this.settings = settings;
        this.isServerRunning = isServerRunning;
        this.output = output;
        this.distribution = distribution ?? ServerDistributionPlatformService.ForCurrentPlatform();
    }

    public async Task<ServerUpdateResult> UpdateAsync(
        Action<ServerUpdateState, string>? statusChanged,
        CancellationToken token)
    {
        if (isServerRunning())
            return Error("The Palworld server must be stopped before checking for updates.");

        if (string.IsNullOrWhiteSpace(settings.SteamCmdPath) || !File.Exists(settings.SteamCmdPath))
            return Error($"SteamCMD was not found at: {settings.SteamCmdPath}");

        if (string.IsNullOrWhiteSpace(settings.ServerRoot))
            return Error("The Palworld server folder is not configured.");

        Directory.CreateDirectory(settings.ServerRoot);
        var steamCmdDirectory = Path.GetDirectoryName(settings.SteamCmdPath) ?? settings.ServerRoot;
        var startInfo = distribution.CreateSteamCmdStartInfo(
            settings.SteamCmdPath,
            steamCmdDirectory,
            distribution.BuildPalworldServerInstallArguments(settings.ServerRoot, validate: true));
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var outputLines = new ConcurrentQueue<string>();
        var sawUpdating = false;
        var sawUpToDate = false;
        var sawError = false;

        void ProcessLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            outputLines.Enqueue(line);
            output?.Invoke("[SteamCMD] " + line);

            if (line.Contains("ERROR!", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("FAILED", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("fatal", StringComparison.OrdinalIgnoreCase))
                sawError = true;

            if (line.Contains("already up to date", StringComparison.OrdinalIgnoreCase))
            {
                sawUpToDate = true;
                statusChanged?.Invoke(ServerUpdateState.UpToDate, "Server is already up to date.");
            }
            else if (line.Contains("downloading", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Update state", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("progress", StringComparison.OrdinalIgnoreCase))
            {
                if (!sawUpdating)
                {
                    sawUpdating = true;
                    statusChanged?.Invoke(ServerUpdateState.Updating,
                        "SteamCMD is updating and validating the server files...");
                }
            }
        }

        process.OutputDataReceived += (_, eventArgs) => ProcessLine(eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => ProcessLine(eventArgs.Data);
        statusChanged?.Invoke(ServerUpdateState.Checking,
            "Checking SteamCMD for a Palworld server update...");

        try
        {
            if (!process.Start())
                return Error("SteamCMD could not be started.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        catch (Exception exception)
        {
            return Error(exception.Message);
        }

        if (process.ExitCode != 0 || sawError)
        {
            var finalLine = outputLines.LastOrDefault(line => !string.IsNullOrWhiteSpace(line));
            return new ServerUpdateResult(ServerUpdateState.Error, process.ExitCode,
                string.IsNullOrWhiteSpace(finalLine)
                    ? $"SteamCMD failed with exit code {process.ExitCode}."
                    : $"SteamCMD failed: {finalLine}");
        }

        if (sawUpToDate)
            return new ServerUpdateResult(ServerUpdateState.UpToDate, process.ExitCode,
                "The Palworld server is already up to date.");

        return new ServerUpdateResult(ServerUpdateState.Complete, process.ExitCode,
            sawUpdating
                ? "Server update and validation completed successfully."
                : "Server update check and validation completed successfully.");
    }

    private static ServerUpdateResult Error(string message) =>
        new(ServerUpdateState.Error, -1, message);
}
