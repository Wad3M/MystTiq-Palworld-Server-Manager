using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Media;
using PalworldManager.Services;

namespace PalworldManager;

public partial class MainWindow
{
    private string saveToolsLastDiagnostics = "No diagnostics have been run.";

    private async void SaveToolsRunAll_Click(object sender, RoutedEventArgs e)
    {
        var report = new StringBuilder();
        report.AppendLine("=== MystTiq Palworld Save Tools Diagnostics ===");
        report.AppendLine($"Generated: {DateTime.Now:O}");
        report.AppendLine($"Server root: {settings.ServerRoot}");
        report.AppendLine();
        await AppendPythonDiagnosticsAsync(report);
        await AppendConverterDiagnosticsAsync(report);
        await AppendWorldConversionDiagnosticsAsync(report);
        SetSaveToolsDiagnostics(report.ToString());
    }

    private async void SaveToolsTestPython_Click(object sender, RoutedEventArgs e)
    {
        var report = new StringBuilder("=== Python Diagnostics ===\n");
        await AppendPythonDiagnosticsAsync(report);
        SetSaveToolsDiagnostics(report.ToString());
    }

    private async void SaveToolsTestConverter_Click(object sender, RoutedEventArgs e)
    {
        var report = new StringBuilder("=== Converter Diagnostics ===\n");
        await AppendPythonDiagnosticsAsync(report);
        await AppendConverterDiagnosticsAsync(report);
        SetSaveToolsDiagnostics(report.ToString());
    }

    private async void SaveToolsTestWorld_Click(object sender, RoutedEventArgs e)
    {
        var report = new StringBuilder("=== Active World Conversion Diagnostics ===\n");
        await AppendWorldConversionDiagnosticsAsync(report);
        SetSaveToolsDiagnostics(report.ToString());
    }

    private void SaveToolsCopyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(saveToolsLastDiagnostics);
        AppDialog.Show("Palworld Save Tools diagnostics copied to the clipboard.", "Diagnostics Copied", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveToolsOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var converter = ResolveSaveToolsConverter();
        var folder = !string.IsNullOrWhiteSpace(converter) ? Path.GetDirectoryName(converter)! : Path.Combine(settings.ServerRoot ?? string.Empty, "Tools", "palworld-save-tools");
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    private async void SaveToolsRepair_Click(object sender, RoutedEventArgs e)
    {
        if (AppDialog.Show("Install or repair Python packages and Palworld Save Tools?", "Install / Repair Save Tools", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await RunExclusive(async ct => await installer.InstallComponentAsync("Palworld Save Tools", CreateInstallProgress(), ct));
        SaveToolsTestConverter_Click(sender, e);
    }

    private async Task AppendPythonDiagnosticsAsync(StringBuilder report)
    {
        var python = ResolvePythonExecutable();
        SaveToolsPythonPath.Text = string.IsNullOrWhiteSpace(python) ? "Not found" : python;
        report.AppendLine("[Python]");
        report.AppendLine($"Configured: {settings.PythonExecutable}");
        report.AppendLine($"Resolved: {python}");
        if (string.IsNullOrWhiteSpace(python))
        {
            SaveToolsPythonStatus.Text = "MISSING";
            SaveToolsPythonStatus.Foreground = Brushes.IndianRed;
            report.AppendLine("Result: Python executable was not found.");
            report.AppendLine();
            return;
        }
        var version = await RunDiagnosticProcessAsync(python, ["--version"], settings.ServerRoot);
        var pip = await RunDiagnosticProcessAsync(python, ["-m", "pip", "--version"], settings.ServerRoot);
        report.Append(version.Format("python --version"));
        report.Append(pip.Format("python -m pip --version"));
        var ok = version.ExitCode == 0 && pip.ExitCode == 0;
        SaveToolsPythonStatus.Text = ok ? "READY" : "BROKEN";
        SaveToolsPythonStatus.Foreground = ok ? Brushes.LightGreen : Brushes.IndianRed;
        report.AppendLine();
    }

    private async Task AppendConverterDiagnosticsAsync(StringBuilder report)
    {
        var python = ResolvePythonExecutable();
        var legacy = ResolveLegacySaveToolsConverter();
        var plm = ResolvePlmSaveToolsConverter();
        var displayed = !string.IsNullOrWhiteSpace(plm) ? plm : legacy;
        SaveToolsConverterPath.Text = string.IsNullOrWhiteSpace(displayed) ? "Not found" : displayed;
        report.AppendLine("[Save Decoders]");
        report.AppendLine($"Legacy PlZ converter: {legacy}");
        report.AppendLine($"PlM/Oodle converter: {plm}");
        if (string.IsNullOrWhiteSpace(python) || (string.IsNullOrWhiteSpace(legacy) && string.IsNullOrWhiteSpace(plm)))
        {
            SaveToolsConverterStatus.Text = "MISSING";
            SaveToolsConverterStatus.Foreground = Brushes.IndianRed;
            report.AppendLine("Result: Python or both converter paths are missing.");
            report.AppendLine();
            return;
        }

        var allOk = true;
        if (!string.IsNullOrWhiteSpace(legacy))
        {
            var legacyResult = await RunDiagnosticProcessAsync(python, [legacy, "--help"], Path.GetDirectoryName(legacy));
            report.Append(legacyResult.Format("legacy PlZ convert.py --help"));
            allOk &= legacyResult.ExitCode == 0;
        }
        if (!string.IsNullOrWhiteSpace(plm))
        {
            var plmResult = await RunDiagnosticProcessAsync(python, [plm, "--help"], Path.GetDirectoryName(plm));
            report.Append(plmResult.Format("PlM/Oodle convert.py --help"));
            allOk &= plmResult.ExitCode == 0;
        }
        SaveToolsConverterStatus.Text = allOk ? "READY" : "FAILED";
        SaveToolsConverterStatus.Foreground = allOk ? Brushes.LightGreen : Brushes.IndianRed;
        report.AppendLine();
    }

    private async Task AppendWorldConversionDiagnosticsAsync(StringBuilder report)
    {
        var context = activeWorldContext.Current(forceRefresh: true);
        SaveToolsWorldPath.Text = string.IsNullOrWhiteSpace(context.LevelSavePath) ? "No active Level.sav" : context.LevelSavePath;
        report.AppendLine("[Active World]");
        report.AppendLine($"World ID: {context.WorldId}");
        report.AppendLine($"Level.sav: {context.LevelSavePath}");
        report.AppendLine($"Exists: {File.Exists(context.LevelSavePath)}");
        report.AppendLine($"Size: {context.LevelLength:N0} bytes");
        if (string.IsNullOrWhiteSpace(context.LevelSavePath) || !File.Exists(context.LevelSavePath))
        {
            SaveToolsWorldStatus.Text = "NOT FOUND";
            SaveToolsWorldStatus.Foreground = Brushes.IndianRed;
            report.AppendLine("Result: Active Level.sav could not be resolved.");
            return;
        }

        var container = PalworldSaveContainerDetector.Inspect(context.LevelSavePath);
        var isPlm = container.IsPlm;
        var decoderName = isPlm ? "PlM/Oodle" : "Legacy PlZ";
        var python = ResolvePythonExecutable();
        var converter = isPlm ? ResolvePlmSaveToolsConverter() : ResolveLegacySaveToolsConverter();
        report.AppendLine($"Save signature: {container.DisplaySignature}");
        report.AppendLine($"Header bytes: {container.HeaderHex}");
        report.AppendLine($"Header text: {container.HeaderText}");
        report.AppendLine($"Selected decoder: {decoderName}");
        report.AppendLine($"Selected converter: {converter}");
        if (string.IsNullOrWhiteSpace(python) || string.IsNullOrWhiteSpace(converter))
        {
            SaveToolsWorldStatus.Text = "BLOCKED";
            SaveToolsWorldStatus.Foreground = Brushes.IndianRed;
            report.AppendLine($"Result: Python or the {decoderName} converter is missing.");
            return;
        }

        var decoderFolder = isPlm ? "palworld-plm-tools" : "palworld-save-tools";
        var outputDir = Path.Combine(settings.ServerRoot ?? AppContext.BaseDirectory, "Tools", decoderFolder, "diagnostics");
        Directory.CreateDirectory(outputDir);
        var output = Path.Combine(outputDir, $"Level_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        var result = await RunDiagnosticProcessAsync(python, [converter, context.LevelSavePath, "--to-json", "--output", output, "--force", "--minify-json"], Path.GetDirectoryName(converter));
        report.Append(result.Format($"active Level.sav conversion via {decoderName}"));
        report.AppendLine($"Output: {output}");
        report.AppendLine($"Output exists: {File.Exists(output)}");
        report.AppendLine($"Output size: {(File.Exists(output) ? new FileInfo(output).Length : 0):N0} bytes");
        var ok = result.ExitCode == 0 && File.Exists(output) && new FileInfo(output).Length > 0;
        SaveToolsWorldStatus.Text = ok ? "DECODED" : "FAILED";
        SaveToolsWorldStatus.Foreground = ok ? Brushes.LightGreen : Brushes.IndianRed;
        if (ok) worldDiscovery.Invalidate("Active world decoded successfully through the routed save decoder.");
    }

    private string ResolvePythonExecutable()
    {
        var candidates = new List<string?> { settings.PythonExecutable, @"C:\Program Files\Python310\python.exe", "python", "python3", "py" };
        return candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate) && (Path.IsPathRooted(candidate) ? File.Exists(candidate) : CanStart(candidate))) ?? string.Empty;
    }

    private string ResolveSaveToolsConverter() => ResolveLegacySaveToolsConverter();

    private string ResolveLegacySaveToolsConverter()
    {
        var candidates = new[] { settings.PalworldSaveToolsPath, Path.Combine(settings.ServerRoot ?? string.Empty, "Tools", "palworld-save-tools", "convert.py"), Path.Combine(AppContext.BaseDirectory, "Tools", "palworld-save-tools", "convert.py") };
        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private string ResolvePlmSaveToolsConverter()
    {
        var root = Path.Combine(settings.ServerRoot ?? string.Empty, "Tools", "palworld-plm-tools");
        var candidates = new[] { Path.Combine(root, "convert.py"), Path.Combine(root, "tools", "convert.py"), Path.Combine(root, "PalworldSaveTools", "convert.py") };
        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }


    private static bool CanStart(string executable)
    {
        try { using var process = Process.Start(new ProcessStartInfo(executable, "--version") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }); process?.WaitForExit(3000); return process is not null; }
        catch { return false; }
    }

    private static async Task<DiagnosticProcessResult> RunDiagnosticProcessAsync(string executable, IReadOnlyList<string> arguments, string? workingDirectory)
    {
        var start = DateTime.Now;
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory! : AppContext.BaseDirectory };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        try
        {
            using var process = new Process { StartInfo = info };
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return new DiagnosticProcessResult(executable, string.Join(" ", arguments.Select(QuoteArgument)), info.WorkingDirectory, process.ExitCode, await stdoutTask, await stderrTask, DateTime.Now - start, null);
        }
        catch (Exception ex)
        {
            return new DiagnosticProcessResult(executable, string.Join(" ", arguments.Select(QuoteArgument)), info.WorkingDirectory, -1, string.Empty, string.Empty, DateTime.Now - start, ex.ToString());
        }
    }

    private void SetSaveToolsDiagnostics(string text)
    {
        saveToolsLastDiagnostics = text;
        SaveToolsDiagnosticsText.Text = text;
    }

    private static string QuoteArgument(string value) => value.Contains(' ') ? $"\"{value}\"" : value;

    private sealed record DiagnosticProcessResult(string Executable, string Arguments, string WorkingDirectory, int ExitCode, string StdOut, string StdErr, TimeSpan Duration, string? Exception)
    {
        public string Format(string label) => $"Test: {label}\nCommand: {QuoteArgument(Executable)} {Arguments}\nWorking directory: {WorkingDirectory}\nExit code: {ExitCode}\nDuration: {Duration.TotalSeconds:0.000} sec\nSTDOUT:\n{StdOut}\nSTDERR:\n{StdErr}\nException:\n{Exception}\n";
    }
}
