using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v0.7.72.0: installs/removes MystTiqConsoleProxy.dll -- the DSOUND.dll proxy scaffolded in
/// v0.7.57.0 -- as `dsound.dll` next to a Windows profile's PalServer executable. The proxy hooks
/// WriteConsoleA/WriteConsoleW in PalServer-Win64-Shipping-Cmd.exe's own import table and writes
/// every byte the game's console window would have shown into a plain text file, which
/// HeadlessMonitoringService.ResolveConsoleSources already knows how to read as a console source.
///
/// Deliberately opt-in, never automatic: this installs native code that runs inside the game
/// process on every future launch, a materially different risk category than every other console
/// source this app reads (all of which are pre-existing files the game or a mod already writes).
/// InstallAsync/UninstallAsync only ever run when explicitly invoked via the API -- nothing in the
/// lifecycle/launch path calls this on its own.
/// </summary>
public sealed class HeadlessConsoleCaptureProxyService(IServerPathProfile paths)
{
    private string ProxyDestinationPath => Path.Combine(paths.RuntimeBinaryRoot, "dsound.dll");

    private static string? FindPackagedProxySource()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "native", "MystTiqConsoleProxy.dll");
        return File.Exists(candidate) ? candidate : null;
    }

    public HeadlessConsoleCaptureProxyStatus GetStatus()
    {
        if (paths.PlatformId != "windows")
        {
            return new HeadlessConsoleCaptureProxyStatus(false, false, null,
                "Native console capture is Windows-only -- DLL proxying has no equivalent technique on Linux.");
        }

        var packagedSource = FindPackagedProxySource();
        var installed = File.Exists(ProxyDestinationPath);
        var captureLogPath = Path.Combine(paths.RuntimeBinaryRoot, "MystTiqConsoleProxy-Capture.log");
        var hasCaptured = File.Exists(captureLogPath);

        string detail;
        if (installed)
        {
            detail = hasCaptured
                ? "Installed and has captured console output from at least one PalServer session."
                : "Installed, but PalServer hasn't been launched since installation, so no capture yet.";
        }
        else if (packagedSource is null)
        {
            detail = "Not installed. The native proxy DLL wasn't found in this build -- run scripts/Build-ConsoleProxy.ps1 and rebuild/republish before it can be installed.";
        }
        else
        {
            detail = "Not installed. Installing places dsound.dll next to PalServer's own executable; it takes effect on the next PalServer launch, not the current session.";
        }

        return new HeadlessConsoleCaptureProxyStatus(packagedSource is not null, installed, packagedSource, detail);
    }

    public async Task<HeadlessConsoleCaptureProxyOperationResult> InstallAsync(CancellationToken cancellationToken = default)
    {
        if (paths.PlatformId != "windows")
            return new HeadlessConsoleCaptureProxyOperationResult(false, "Native console capture is Windows-only.");

        var source = FindPackagedProxySource();
        if (source is null)
            return new HeadlessConsoleCaptureProxyOperationResult(false,
                "The native proxy DLL wasn't found in this build's own native/MystTiqConsoleProxy.dll -- run scripts/Build-ConsoleProxy.ps1 and republish before installing.");

        if (!Directory.Exists(paths.RuntimeBinaryRoot))
            return new HeadlessConsoleCaptureProxyOperationResult(false,
                $"PalServer runtime folder was not found: {paths.RuntimeBinaryRoot}. Install the dedicated server before installing console capture.");

        var destination = ProxyDestinationPath;
        // A genuine, if unlikely, real dsound.dll could already be present (a prior manual install,
        // or a future PalServer build that ships one) -- refuse rather than silently overwrite
        // something this service didn't put there itself. The proxy forwards every real DSOUND
        // export it's confirmed PalServer calls, but "confirmed against this build" is not the same
        // guarantee as "definitely identical to whatever the real file actually was."
        if (File.Exists(destination))
        {
            try
            {
                var existingLength = new FileInfo(destination).Length;
                var packagedLength = new FileInfo(source).Length;
                if (existingLength != packagedLength)
                {
                    return new HeadlessConsoleCaptureProxyOperationResult(false,
                        $"A different dsound.dll already exists at {destination} (not one this service installed). Refusing to overwrite it -- remove it manually first if you're sure it's safe to replace.");
                }
                // Same size as the packaged proxy: almost certainly our own prior install (or an
                // identical rebuild). Proceed to refresh it below rather than treating this as a
                // foreign file.
            }
            catch (IOException)
            {
                return new HeadlessConsoleCaptureProxyOperationResult(false,
                    $"Could not inspect the existing file at {destination} -- it may be locked by a running PalServer. Stop the server first.");
            }
        }

        try
        {
            File.Copy(source, destination, overwrite: true);
        }
        catch (IOException ex)
        {
            return new HeadlessConsoleCaptureProxyOperationResult(false,
                $"Could not install dsound.dll -- it may be locked by a running PalServer. Stop the server first. ({ex.Message})");
        }

        return new HeadlessConsoleCaptureProxyOperationResult(true,
            "Installed. Takes effect on the next PalServer launch (Restart to apply immediately).", cancellationToken.IsCancellationRequested);
    }

    public Task<HeadlessConsoleCaptureProxyOperationResult> UninstallAsync()
    {
        if (paths.PlatformId != "windows")
            return Task.FromResult(new HeadlessConsoleCaptureProxyOperationResult(false, "Native console capture is Windows-only."));

        var destination = ProxyDestinationPath;
        if (!File.Exists(destination))
            return Task.FromResult(new HeadlessConsoleCaptureProxyOperationResult(true, "Already not installed."));

        try
        {
            File.Delete(destination);
        }
        catch (IOException ex)
        {
            return Task.FromResult(new HeadlessConsoleCaptureProxyOperationResult(false,
                $"Could not remove dsound.dll -- it is likely locked by a running PalServer. Stop the server first. ({ex.Message})"));
        }

        return Task.FromResult(new HeadlessConsoleCaptureProxyOperationResult(true,
            "Removed. PalServer's own real audio DLL resolution is unaffected -- this only ever forwarded to it."));
    }
}

public sealed record HeadlessConsoleCaptureProxyStatus(bool PackagedProxyAvailable, bool Installed, string? PackagedSourcePath, string Detail);

public sealed record HeadlessConsoleCaptureProxyOperationResult(bool Success, string Detail, bool CancellationRequestedButIgnored = false);
