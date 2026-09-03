using System.Diagnostics;

namespace MystTiq.HeadlessHost;

// Wraps the same Python-based palworld-save-tools / PlM-Oodle converter that
// HeadlessCrashAndSaveToolsService already discovers for read-only diagnostics.
// This is the missing prerequisite the Desktop UI's Guild/Base repair pages
// call out as "BACKEND REQUIRED": a server-side Decode/Encode capability for
// Level.sav so a transactional mutation engine has something to operate on.
//
// Palworld saves come in two container formats (a "PlZ"-signed plain zlib
// container and a "PlM"-signed PlM/Oodle container), each requiring its own
// convert.py under a different Tools\ folder. Mixing them silently fails, so
// every transaction resolves the matching converter once from the actual
// source Level.sav's header and reuses that same converter for decode,
// encode, and the post-encode verification re-decode.
public sealed class HeadlessSaveCodecService
{
    private readonly HeadlessCrashAndSaveToolsService saveTools;

    public HeadlessSaveCodecService(HeadlessCrashAndSaveToolsService saveTools)
    {
        this.saveTools = saveTools;
    }

    // Convenience check used where no specific save file is known yet (e.g. showing
    // "a codec is configured" in general diagnostics). Prefer ResolveConverterAsync
    // when a concrete Level.sav path is available, since it's container-aware.
    public async Task<HeadlessSaveCodecAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        var diagnostics = await saveTools.DiagnoseSaveToolsAsync(false, cancellationToken);
        var converter = diagnostics.LegacyConverterPath ?? diagnostics.PlmConverterPath;
        return new HeadlessSaveCodecAvailability(
            diagnostics.PythonPath is not null && converter is not null,
            diagnostics.PythonPath,
            converter,
            diagnostics.PythonPath is null
                ? "Python was not found on this server."
                : converter is null
                    ? "No supported palworld-save-tools/PlM-Oodle converter was found under Tools\\."
                    : "Python and a converter are available.");
    }

    // Inspects levelSavePath's actual container signature (PlM vs PlZ) and resolves
    // the specific python + converter pair required for that container. Returns null
    // with a human-readable reason if the required converter isn't available.
    public async Task<(HeadlessSaveConverterMatch? Match, string Detail)> ResolveConverterAsync(string levelSavePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(levelSavePath))
            return (null, "Level.sav was not found.");

        var diagnostics = await saveTools.DiagnoseSaveToolsAsync(false, cancellationToken);
        if (diagnostics.PythonPath is null)
            return (null, "Python was not found on this server.");

        var kind = DetectContainerKind(levelSavePath);
        var converter = kind == "PlM" ? diagnostics.PlmConverterPath : diagnostics.LegacyConverterPath;
        if (converter is null)
            return (null, kind == "PlM"
                ? "The active save uses the PlM/Oodle container, but no PlM converter was found under Tools\\palworld-plm-tools."
                : $"The active save uses the {(kind == "Unknown" ? "PlZ" : kind)} container, but palworld-save-tools convert.py was not found under Tools\\palworld-save-tools.");

        return (new HeadlessSaveConverterMatch(diagnostics.PythonPath, converter, kind), $"{kind} converter resolved.");
    }

    // Decodes levelSavePath into a fresh, minified JSON file at outputJsonPath using the
    // resolved converter and returns that JSON file's path.
    public async Task<string> DecodeAsync(HeadlessSaveConverterMatch converter, string levelSavePath, string outputJsonPath, CancellationToken cancellationToken)
    {
        if (File.Exists(outputJsonPath)) File.Delete(outputJsonPath);
        await RunConverterAsync(converter, [levelSavePath, "--to-json", "--minify-json", "--force", "--output", outputJsonPath], cancellationToken);
        if (!File.Exists(outputJsonPath))
            throw new InvalidOperationException("The converter completed without producing decoded JSON.");
        return outputJsonPath;
    }

    // Encodes jsonPath (produced by DecodeAsync, then mutated) back into a .sav file at
    // outputSavPath using the SAME converter that decoded the source save. Never writes
    // directly to a live/active save path; callers are responsible for atomic activation
    // once the encoded output is independently verified.
    public async Task<string> EncodeAsync(HeadlessSaveConverterMatch converter, string jsonPath, string outputSavPath, CancellationToken cancellationToken)
    {
        if (File.Exists(outputSavPath)) File.Delete(outputSavPath);
        await RunConverterAsync(converter, [jsonPath, "--from-json", "--force", "--output", outputSavPath], cancellationToken);
        if (!File.Exists(outputSavPath))
            throw new InvalidOperationException("The converter completed without producing an encoded SAV file.");
        return outputSavPath;
    }

    private static async Task RunConverterAsync(HeadlessSaveConverterMatch converter, IReadOnlyList<string> converterArguments, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));

        var info = new ProcessStartInfo(converter.PythonPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.ArgumentList.Add(converter.ConverterPath);
        foreach (var argument in converterArguments) info.ArgumentList.Add(argument);

        using var process = Process.Start(info) ?? throw new InvalidOperationException("The save-codec converter process did not start.");
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        var stdout = await outputTask;
        var stderr = await errorTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"The save-codec converter exited with code {process.ExitCode}. {stderr} {stdout}".Trim());
    }

    // Mirrors the legacy WPF app's PalworldSaveContainerDetector: scan the first bytes
    // of the file for the earliest "PlM" or "PlZ" signature.
    private static string DetectContainerKind(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var length = (int)Math.Min(64, stream.Length);
        var buffer = new byte[length];
        var count = stream.Read(buffer, 0, buffer.Length);
        var scanned = buffer.AsSpan(0, count);

        var plmIndex = IndexOf(scanned, "PlM"u8);
        var plzIndex = IndexOf(scanned, "PlZ"u8);
        if (plmIndex < 0 && plzIndex < 0) return "Unknown";
        if (plmIndex < 0) return "PlZ";
        if (plzIndex < 0) return "PlM";
        return plmIndex <= plzIndex ? "PlM" : "PlZ";
    }

    private static int IndexOf(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty || source.Length < value.Length) return -1;
        for (var index = 0; index <= source.Length - value.Length; index++)
            if (source.Slice(index, value.Length).SequenceEqual(value)) return index;
        return -1;
    }
}

public sealed record HeadlessSaveCodecAvailability(bool Available, string? PythonPath, string? ConverterPath, string Detail);
public sealed record HeadlessSaveConverterMatch(string PythonPath, string ConverterPath, string ContainerKind);
