using System.Diagnostics;
using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class ProcessPalworldSaveCodec : IPalworldSaveCodec
{
    private readonly AppSettings settings;
    public string Name => "Palworld Save Tools (PlZ/PlM routed)";
    public ProcessPalworldSaveCodec(AppSettings settings) => this.settings = settings;
    public bool IsAvailable() => !string.IsNullOrWhiteSpace(FindConverter());

    public async Task<SaveCodecResult> DecodeAsync(string savePath, string outputDirectory, CancellationToken cancellationToken)
    {
        var container = PalworldSaveContainerDetector.Inspect(savePath);
        Directory.CreateDirectory(outputDirectory);
        var output = Path.Combine(outputDirectory, Path.GetFileName(savePath) + ".json");
        var plm = container.IsPlm;
        var converter = plm ? FindPlmConverter() : FindConverter();
        if (string.IsNullOrWhiteSpace(converter))
            throw new InvalidOperationException(plm
                ? "The active world uses the PlM/Oodle save container, but the PlM/Oodle Decoder is not installed. Install it from Server Setup."
                : "The Palworld Save Tools converter is not installed.");
        return await RunAsync(savePath, converter, $"\"{savePath}\" --to-json --minify-json --force --output \"{output}\"", output, cancellationToken);
    }

    public async Task<SaveCodecResult> EncodeAsync(string jsonPath, string outputSavePath, CancellationToken cancellationToken)
        => await RunAsync(jsonPath, FindConverter(), $"\"{jsonPath}\" --from-json --force --output \"{outputSavePath}\"", outputSavePath, cancellationToken);

    private async Task<SaveCodecResult> RunAsync(string source, string converter, string args, string expected, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(converter)) throw new InvalidOperationException("Palworld save converter was not found.");
        Exception? last = null;
        foreach (var exe in ResolvePythonCandidates())
        {
            try
            {
                using var process = new Process { StartInfo = new ProcessStartInfo { FileName = exe, Arguments = $"\"{converter}\" {args}", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
                process.Start();
                var stdout = process.StandardOutput.ReadToEndAsync(ct);
                var stderr = process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);
                var result = new SaveCodecResult { SourcePath = source, JsonPath = expected, CodecName = Name };
                result.Diagnostics.Add(await stdout); result.Diagnostics.Add(await stderr);
                result.Success = process.ExitCode == 0 && File.Exists(expected) && new FileInfo(expected).Length > 0;
                if (result.Success) return result;
                last = new InvalidOperationException($"{exe} exited with code {process.ExitCode}.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { last = ex; }
        }
        throw new InvalidOperationException($"Unable to run the Palworld save converter. Python candidates: {string.Join(", ", ResolvePythonCandidates())}. Converter: {converter}", last);
    }


    private IReadOnlyList<string> ResolvePythonCandidates()
    {
        var candidates = new List<string>();
        void Add(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !candidates.Contains(value, StringComparer.OrdinalIgnoreCase)) candidates.Add(value);
        }

        Add(settings.PythonExecutable);
        Add(@"C:\Program Files\Python310\python.exe");
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (Directory.Exists(programFiles))
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(programFiles, "python.exe", SearchOption.AllDirectories)
                             .Where(path => path.Contains("Python", StringComparison.OrdinalIgnoreCase))
                             .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)) Add(path);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
        Add("python");
        Add("python3");
        Add("py");
        return candidates;
    }


    private string FindPlmConverter()
    {
        var root = Path.Combine(settings.ServerRoot ?? "", "Tools", "palworld-plm-tools");
        var candidates = new[]
        {
            Path.Combine(root, "convert.py"),
            Path.Combine(root, "tools", "convert.py"),
            Path.Combine(root, "PalworldSaveTools", "convert.py")
        };
        return candidates.FirstOrDefault(File.Exists) ?? "";
    }

    private string FindConverter()
    {
        var candidates = new[] { settings.PalworldSaveToolsPath, Path.Combine(AppContext.BaseDirectory, "Tools", "palworld-save-tools", "convert.py"), Path.Combine(settings.ServerRoot ?? "", "Tools", "palworld-save-tools", "convert.py") };
        return candidates.FirstOrDefault(File.Exists) ?? "";
    }
}
