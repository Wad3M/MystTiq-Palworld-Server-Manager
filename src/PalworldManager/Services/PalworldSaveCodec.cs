using System.Diagnostics;
using System.Security.Cryptography;
using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class PalworldSaveCodec
{
    private readonly AppSettings settings;
    public PalworldSaveCodec(AppSettings settings) => this.settings = settings;
    public string FindConverter()
    {
        var candidates = new[] {
            settings.PalworldSaveToolsPath,
            Path.Combine(AppContext.BaseDirectory,"Tools","palworld-save-tools","convert.py"),
            Path.Combine(settings.ServerRoot ?? "","Tools","palworld-save-tools","convert.py")
        };
        return candidates.FirstOrDefault(File.Exists) ?? "";
    }

    public string FindPlmConverter()
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

    public string FindConverterForSave(string savePath)
    {
        var container = PalworldSaveContainerDetector.Inspect(savePath);
        return container.IsPlm ? FindPlmConverter() : FindConverter();
    }
    public string Decode(string levelSavePath, bool force = false)
    {
        var container = PalworldSaveContainerDetector.Inspect(levelSavePath);
        var isPlm = container.IsPlm;
        var converter = isPlm ? FindPlmConverter() : FindConverter();
        if (string.IsNullOrWhiteSpace(converter))
            throw new InvalidOperationException(isPlm
                ? "The active save uses the PlM/Oodle container, but the PlM/Oodle converter was not found under Tools\\palworld-plm-tools. Detected signature: " + container.DisplaySignature
                : "palworld-save-tools convert.py was not found. Configure PalworldSaveToolsPath or place it under Tools\\palworld-save-tools.");
        var output = levelSavePath + ".json";
        if (force && File.Exists(output)) File.Delete(output);
        RunPython(converter, $"\"{levelSavePath}\" --to-json --minify-json --force --output \"{output}\"");
        if (!File.Exists(output)) throw new InvalidOperationException($"The { (isPlm ? "PlM/Oodle" : "PlZ") } converter completed without producing Level.sav.json.");
        return output;
    }
    public string Encode(string jsonPath,string outputSav)
    {
        var converter=FindConverter();
        if(string.IsNullOrWhiteSpace(converter)) throw new InvalidOperationException("palworld-save-tools convert.py was not found.");
        RunPython(converter,$"\"{jsonPath}\" --from-json --force --output \"{outputSav}\"");
        if(!File.Exists(outputSav)) throw new InvalidOperationException("The converter completed without producing a SAV file.");
        return outputSav;
    }
    private void RunPython(string converter,string arguments)
    {
        Exception? last=null;
        foreach(var exe in ResolvePythonCandidates())
        {
            try {
                using var p=Process.Start(new ProcessStartInfo { FileName=exe, Arguments=$"\"{converter}\" {arguments}", UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true, CreateNoWindow=true }) ?? throw new InvalidOperationException("Could not start Python.");
                var stdout=p.StandardOutput.ReadToEnd(); var stderr=p.StandardError.ReadToEnd(); p.WaitForExit();
                if(p.ExitCode==0) return;
                last=new InvalidOperationException($"{exe} exited with code {p.ExitCode}. {stderr} {stdout}".Trim());
            } catch(Exception ex){last=ex;}
        }
        throw new InvalidOperationException($"Unable to run palworld-save-tools. Python candidates: {string.Join(", ", ResolvePythonCandidates())}. Converter: {converter}",last);
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
        Add("python"); Add("python3"); Add("py");
        return candidates;
    }

    public static string HashFile(string path)
    { using var sha=SHA256.Create(); using var fs=File.OpenRead(path); return Convert.ToHexString(sha.ComputeHash(fs)); }
}
