// MystTiq v1.0.4.0: file reviewed for this release (2026-10-05).
// MystTiq v1.0.0.1 launcher workspace + troubleshooting presets (2026-10-01).
namespace MystTiq.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const string LauncherOptionPrefix = "@mysttiq:";
    private bool _launcherSyncing;

    private bool _configLauncherPortEnabled = true;
    private int _configLauncherPort = 8211;
    private bool _configLauncherLog = true;
    private bool _configLauncherStdout = true;
    private bool _configLauncherFullStdOutLogOutput = true;
    private bool _configLauncherAbsLog = true;
    private string _configLauncherAbsLogPath = string.Empty;
    private bool _configLauncherUnattended;
    private bool _configLauncherUsePerfThreads;
    private bool _configLauncherNoAsyncLoadingThread;
    private bool _configLauncherUseMultithreadForDs;
    private bool _configLauncherLogFormatText;
    private bool _configLauncherNoMods;
    private string _configLauncherCustomArguments = string.Empty;

    private string _configLauncherExecutable = "PalServer.exe";
    private string _configLauncherCustomExecutablePath = string.Empty;
    private string _configLauncherWorkingDirectory = "Server root";
    private string _configLauncherCustomWorkingDirectory = string.Empty;
    private bool _configLauncherUseShellExecute;
    private bool _configLauncherCreateNoWindow;
    private string _configLauncherWindowStyle = "Hidden";
    private bool _configLauncherRedirectStandardOutput;
    private bool _configLauncherRedirectStandardError;
    private bool _configLauncherCaptureRedirectedOutput = true;
    private bool _configLauncherHideManagedWindows = true;
    private string _configLauncherEffectiveCommand = "Load configuration to preview the launcher command.";
    private string _configLauncherProcessSummary = string.Empty;
    private string _configLauncherWarning = string.Empty;
    private string _configLauncherPresetStatus = "Choose a troubleshooting preset or adjust individual options below.";
    private System.Windows.Input.ICommand? _applyLauncherPresetCommand;

    public IReadOnlyList<string> ConfigLauncherExecutableOptions { get; } =
        ["PalServer.exe", "Shipping-Cmd.exe", "Shipping.exe", "Test-Cmd.exe", "Test.exe", "Custom path"];

    public IReadOnlyList<string> ConfigLauncherWorkingDirectoryOptions { get; } =
        ["Server root", "Executable folder", "Custom path"];

    public IReadOnlyList<string> ConfigLauncherWindowStyleOptions { get; } =
        ["Hidden", "Normal", "Minimized", "Maximized"];

    public bool ConfigLauncherPortEnabled { get => _configLauncherPortEnabled; set => SetLauncherField(ref _configLauncherPortEnabled, value); }
    public int ConfigLauncherPort { get => _configLauncherPort; set => SetLauncherField(ref _configLauncherPort, Math.Clamp(value, 1, 65535)); }
    public bool ConfigLauncherLog { get => _configLauncherLog; set => SetLauncherField(ref _configLauncherLog, value); }
    public bool ConfigLauncherStdout { get => _configLauncherStdout; set => SetLauncherField(ref _configLauncherStdout, value); }
    public bool ConfigLauncherFullStdOutLogOutput { get => _configLauncherFullStdOutLogOutput; set => SetLauncherField(ref _configLauncherFullStdOutLogOutput, value); }
    public bool ConfigLauncherAbsLog { get => _configLauncherAbsLog; set => SetLauncherField(ref _configLauncherAbsLog, value); }
    public string ConfigLauncherAbsLogPath { get => _configLauncherAbsLogPath; set => SetLauncherField(ref _configLauncherAbsLogPath, value ?? string.Empty); }
    public bool ConfigLauncherUnattended { get => _configLauncherUnattended; set => SetLauncherField(ref _configLauncherUnattended, value); }
    public bool ConfigLauncherUsePerfThreads { get => _configLauncherUsePerfThreads; set => SetLauncherField(ref _configLauncherUsePerfThreads, value); }
    public bool ConfigLauncherNoAsyncLoadingThread { get => _configLauncherNoAsyncLoadingThread; set => SetLauncherField(ref _configLauncherNoAsyncLoadingThread, value); }
    public bool ConfigLauncherUseMultithreadForDs { get => _configLauncherUseMultithreadForDs; set => SetLauncherField(ref _configLauncherUseMultithreadForDs, value); }
    public bool ConfigLauncherLogFormatText { get => _configLauncherLogFormatText; set => SetLauncherField(ref _configLauncherLogFormatText, value); }
    public bool ConfigLauncherNoMods { get => _configLauncherNoMods; set => SetLauncherField(ref _configLauncherNoMods, value); }
    public string ConfigLauncherCustomArguments { get => _configLauncherCustomArguments; set => SetLauncherField(ref _configLauncherCustomArguments, value ?? string.Empty); }

    public string ConfigLauncherExecutable { get => _configLauncherExecutable; set => SetLauncherField(ref _configLauncherExecutable, value ?? "PalServer.exe"); }
    public string ConfigLauncherCustomExecutablePath { get => _configLauncherCustomExecutablePath; set => SetLauncherField(ref _configLauncherCustomExecutablePath, value ?? string.Empty); }
    public string ConfigLauncherWorkingDirectory { get => _configLauncherWorkingDirectory; set => SetLauncherField(ref _configLauncherWorkingDirectory, value ?? "Server root"); }
    public string ConfigLauncherCustomWorkingDirectory { get => _configLauncherCustomWorkingDirectory; set => SetLauncherField(ref _configLauncherCustomWorkingDirectory, value ?? string.Empty); }
    public bool ConfigLauncherUseShellExecute { get => _configLauncherUseShellExecute; set => SetLauncherField(ref _configLauncherUseShellExecute, value); }
    public bool ConfigLauncherCreateNoWindow { get => _configLauncherCreateNoWindow; set => SetLauncherField(ref _configLauncherCreateNoWindow, value); }
    public string ConfigLauncherWindowStyle { get => _configLauncherWindowStyle; set => SetLauncherField(ref _configLauncherWindowStyle, value ?? "Hidden"); }
    public bool ConfigLauncherRedirectStandardOutput { get => _configLauncherRedirectStandardOutput; set => SetLauncherField(ref _configLauncherRedirectStandardOutput, value); }
    public bool ConfigLauncherRedirectStandardError { get => _configLauncherRedirectStandardError; set => SetLauncherField(ref _configLauncherRedirectStandardError, value); }
    public bool ConfigLauncherCaptureRedirectedOutput { get => _configLauncherCaptureRedirectedOutput; set => SetLauncherField(ref _configLauncherCaptureRedirectedOutput, value); }
    public bool ConfigLauncherHideManagedWindows { get => _configLauncherHideManagedWindows; set => SetLauncherField(ref _configLauncherHideManagedWindows, value); }

    public string ConfigLauncherEffectiveCommand { get => _configLauncherEffectiveCommand; private set => SetField(ref _configLauncherEffectiveCommand, value); }
    public string ConfigLauncherProcessSummary { get => _configLauncherProcessSummary; private set => SetField(ref _configLauncherProcessSummary, value); }
    public string ConfigLauncherWarning { get => _configLauncherWarning; private set => SetField(ref _configLauncherWarning, value); }
    public string ConfigLauncherPresetStatus { get => _configLauncherPresetStatus; private set => SetField(ref _configLauncherPresetStatus, value); }
    public System.Windows.Input.ICommand ApplyLauncherPresetCommand => _applyLauncherPresetCommand ??= new RelayCommand<string>(ApplyLauncherPreset);

    private void ApplyLauncherPreset(string? preset)
    {
        var mode = (preset ?? string.Empty).Trim();
        _launcherSyncing = true;
        try
        {
            // Presets intentionally preserve the configured game port and absolute-log path, because
            // those identify this server. They reset launch mechanics and custom arguments so each
            // troubleshooting run is reproducible.
            ConfigLauncherExecutable = "PalServer.exe";
            ConfigLauncherCustomExecutablePath = string.Empty;
            ConfigLauncherWorkingDirectory = "Server root";
            ConfigLauncherCustomWorkingDirectory = string.Empty;
            ConfigLauncherUseShellExecute = false;
            ConfigLauncherCreateNoWindow = false;
            ConfigLauncherRedirectStandardOutput = false;
            ConfigLauncherRedirectStandardError = false;
            ConfigLauncherCaptureRedirectedOutput = true;
            ConfigLauncherPortEnabled = true;
            ConfigLauncherLog = true;
            ConfigLauncherStdout = true;
            ConfigLauncherFullStdOutLogOutput = true;
            ConfigLauncherAbsLog = true;
            if (string.IsNullOrWhiteSpace(ConfigLauncherAbsLogPath)) ConfigLauncherAbsLogPath = DefaultIdentityLogPath();
            ConfigLauncherUnattended = false;
            ConfigLauncherUsePerfThreads = false;
            ConfigLauncherNoAsyncLoadingThread = false;
            ConfigLauncherUseMultithreadForDs = false;
            ConfigLauncherLogFormatText = false;
            ConfigLauncherCustomArguments = string.Empty;

            if (mode.Equals("DoubleClick", StringComparison.OrdinalIgnoreCase))
            {
                // v1.0.0.1 (2026-10-04): exactly what double-clicking PalServer.exe in the server folder does, the start the
                // owner found keeps players' characters: started through the Windows shell, a normal window, no arguments
                // (only -port= for a server not on 8211), nothing hidden afterwards.
                ConfigLauncherUseShellExecute = true;
                ConfigLauncherPortEnabled = ConfigLauncherPort != 8211;
                ConfigLauncherLog = false;
                ConfigLauncherStdout = false;
                ConfigLauncherFullStdOutLogOutput = false;
                ConfigLauncherAbsLog = false;
                ConfigLauncherNoMods = false;
                ConfigLauncherWindowStyle = "Normal";
                ConfigLauncherHideManagedWindows = false;
                ConfigLauncherPresetStatus = "Like double-click preset applied: PalServer.exe started through Windows, as a double-click in its folder does, with a normal window and no arguments. Click Save Launcher Settings before testing.";
            }
            else if (mode.Equals("NoMods", StringComparison.OrdinalIgnoreCase))
            {
                ConfigLauncherNoMods = true;
                ConfigLauncherWindowStyle = "Hidden";
                ConfigLauncherHideManagedWindows = true;
                ConfigLauncherPresetStatus = "No Mods preset applied (-NoMods). Port and log path were preserved. Click Save Launcher Settings before testing.";
            }
            else if (mode.Equals("ShowWindow", StringComparison.OrdinalIgnoreCase))
            {
                ConfigLauncherNoMods = false;
                ConfigLauncherWindowStyle = "Normal";
                ConfigLauncherHideManagedWindows = false;
                ConfigLauncherPresetStatus = "Show Window preset applied. PalServer uses a normal visible window with no post-launch hiding. Click Save Launcher Settings before testing.";
            }
            else
            {
                ConfigLauncherNoMods = false;
                ConfigLauncherWindowStyle = "Hidden";
                ConfigLauncherHideManagedWindows = true;
                ConfigLauncherPresetStatus = "Default preset applied. Mods remain enabled; MystTiq hides the managed PalServer window. Port and log path were preserved. Click Save Launcher Settings before testing.";
            }
        }
        finally
        {
            _launcherSyncing = false;
        }

        SyncConfigLaunchArgumentsFromLauncherEditor();
        UpdateLauncherPreview();
    }

    private void SetLauncherField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!SetField(ref field, value, propertyName)) return;
        LauncherConfigurationChanged();
    }

    private void LauncherConfigurationChanged()
    {
        if (_launcherSyncing) return;
        ConfigLauncherPresetStatus = "Custom launcher settings modified. Click Save Launcher Settings before testing.";
        SyncConfigLaunchArgumentsFromLauncherEditor();
        UpdateLauncherPreview();
    }

    private void LoadLauncherConfiguration(IReadOnlyList<string> storedArguments)
    {
        _launcherSyncing = true;
        try
        {
            var arguments = storedArguments
                .Where(argument => !string.IsNullOrWhiteSpace(argument))
                .Select(argument => argument.Trim())
                .ToArray();
            var options = ParseLauncherOptions(arguments);
            var explicitLauncher = options.ContainsKey("launcherVersion");

            ConfigLauncherExecutable = ReadLauncherOption(options, "executable", "PalServer.exe");
            ConfigLauncherCustomExecutablePath = ReadLauncherOption(options, "customExecutablePath", string.Empty);
            ConfigLauncherWorkingDirectory = ReadLauncherOption(options, "workingDirectory", "Server root");
            ConfigLauncherCustomWorkingDirectory = ReadLauncherOption(options, "customWorkingDirectory", string.Empty);
            ConfigLauncherUseShellExecute = ReadLauncherBool(options, "useShellExecute", false);
            ConfigLauncherCreateNoWindow = ReadLauncherBool(options, "createNoWindow", false);
            ConfigLauncherWindowStyle = ReadLauncherOption(options, "windowStyle", "Hidden");
            ConfigLauncherRedirectStandardOutput = ReadLauncherBool(options, "redirectStandardOutput", false);
            ConfigLauncherRedirectStandardError = ReadLauncherBool(options, "redirectStandardError", false);
            ConfigLauncherCaptureRedirectedOutput = ReadLauncherBool(options, "captureRedirectedOutput", true);
            ConfigLauncherHideManagedWindows = ReadLauncherBool(options, "hideManagedWindows", true);

            var portArgument = arguments.LastOrDefault(a => a.StartsWith("-port=", StringComparison.OrdinalIgnoreCase));
            ConfigLauncherPortEnabled = portArgument is not null || !explicitLauncher;
            ConfigLauncherPort = portArgument is not null && int.TryParse(portArgument["-port=".Length..], out var parsedPort)
                ? Math.Clamp(parsedPort, 1, 65535)
                : 8211;

            if (explicitLauncher)
            {
                ConfigLauncherLog = HasExact(arguments, "-log");
                ConfigLauncherStdout = HasExact(arguments, "-stdout");
                ConfigLauncherFullStdOutLogOutput = HasExact(arguments, "-FullStdOutLogOutput");
                ConfigLauncherUnattended = HasExact(arguments, "-unattended");
                ConfigLauncherUsePerfThreads = HasExact(arguments, "-useperfthreads");
                ConfigLauncherNoAsyncLoadingThread = HasExact(arguments, "-NoAsyncLoadingThread");
                ConfigLauncherUseMultithreadForDs = HasExact(arguments, "-UseMultithreadForDS");
                ConfigLauncherLogFormatText = arguments.Any(a => a.Equals("-logformat=text", StringComparison.OrdinalIgnoreCase));
                ConfigLauncherNoMods = HasExact(arguments, "-NoMods");
                ConfigLauncherAbsLog = arguments.Any(a => a.StartsWith("-abslog=", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                // Match the effective behavior of the previous manual-parity hotfix until the first
                // explicit save from the new launcher editor.
                ConfigLauncherLog = true;
                ConfigLauncherStdout = true;
                ConfigLauncherFullStdOutLogOutput = true;
                ConfigLauncherAbsLog = true;
                ConfigLauncherUnattended = false;
                ConfigLauncherUsePerfThreads = false;
                ConfigLauncherNoAsyncLoadingThread = false;
                ConfigLauncherUseMultithreadForDs = false;
                ConfigLauncherLogFormatText = false;
                ConfigLauncherNoMods = false;
            }

            var absLogArgument = arguments.LastOrDefault(a => a.StartsWith("-abslog=", StringComparison.OrdinalIgnoreCase));
            ConfigLauncherAbsLogPath = absLogArgument is null
                ? DefaultIdentityLogPath()
                : absLogArgument["-abslog=".Length..].Trim().Trim('"');

            ConfigLauncherCustomArguments = string.Join(Environment.NewLine,
                arguments.Where(argument =>
                    !argument.StartsWith(LauncherOptionPrefix, StringComparison.OrdinalIgnoreCase) &&
                    !IsKnownLauncherArgument(argument) &&
                    (explicitLauncher || !IsHistoricalSuppressedArgument(argument))));
        }
        finally
        {
            _launcherSyncing = false;
        }

        ConfigLauncherPresetStatus = "Saved launcher settings loaded. Choose a preset or adjust individual options, then save before testing.";
        SyncConfigLaunchArgumentsFromLauncherEditor();
        UpdateLauncherPreview();
    }

    private IReadOnlyList<string> BuildLauncherConfigurationArguments()
    {
        var result = new List<string>
        {
            "@mysttiq:launcherVersion=1",
            $"@mysttiq:executable={ConfigLauncherExecutable}",
            $"@mysttiq:customExecutablePath={ConfigLauncherCustomExecutablePath.Trim()}",
            $"@mysttiq:workingDirectory={ConfigLauncherWorkingDirectory}",
            $"@mysttiq:customWorkingDirectory={ConfigLauncherCustomWorkingDirectory.Trim()}",
            $"@mysttiq:useShellExecute={ConfigLauncherUseShellExecute.ToString().ToLowerInvariant()}",
            $"@mysttiq:createNoWindow={ConfigLauncherCreateNoWindow.ToString().ToLowerInvariant()}",
            $"@mysttiq:windowStyle={ConfigLauncherWindowStyle}",
            $"@mysttiq:redirectStandardOutput={ConfigLauncherRedirectStandardOutput.ToString().ToLowerInvariant()}",
            $"@mysttiq:redirectStandardError={ConfigLauncherRedirectStandardError.ToString().ToLowerInvariant()}",
            $"@mysttiq:captureRedirectedOutput={ConfigLauncherCaptureRedirectedOutput.ToString().ToLowerInvariant()}",
            $"@mysttiq:hideManagedWindows={ConfigLauncherHideManagedWindows.ToString().ToLowerInvariant()}"
        };

        if (ConfigLauncherPortEnabled) result.Add($"-port={ConfigLauncherPort}");
        if (ConfigLauncherLog) result.Add("-log");
        if (ConfigLauncherLogFormatText) result.Add("-logformat=text");
        if (ConfigLauncherStdout) result.Add("-stdout");
        if (ConfigLauncherFullStdOutLogOutput) result.Add("-FullStdOutLogOutput");
        if (ConfigLauncherAbsLog)
        {
            var path = string.IsNullOrWhiteSpace(ConfigLauncherAbsLogPath) ? DefaultIdentityLogPath() : ConfigLauncherAbsLogPath.Trim();
            result.Add($"-abslog={path}");
        }
        if (ConfigLauncherUnattended) result.Add("-unattended");
        if (ConfigLauncherUsePerfThreads) result.Add("-useperfthreads");
        if (ConfigLauncherNoAsyncLoadingThread) result.Add("-NoAsyncLoadingThread");
        if (ConfigLauncherUseMultithreadForDs) result.Add("-UseMultithreadForDS");
        if (ConfigLauncherNoMods) result.Add("-NoMods");

        foreach (var argument in SplitArgumentLines(ConfigLauncherCustomArguments))
        {
            if (argument.StartsWith(LauncherOptionPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (IsKnownLauncherArgument(argument)) continue;
            result.Add(argument);
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private void SyncConfigLaunchArgumentsFromLauncherEditor()
    {
        if (_launcherSyncing) return;
        var generated = string.Join(Environment.NewLine, BuildLauncherConfigurationArguments());
        if (_configLaunchArguments == generated) return;
        _configLaunchArguments = generated;
        RaisePropertyChanged(nameof(ConfigLaunchArguments));
    }

    private void UpdateLauncherPreview()
    {
        var executable = ResolvePreviewExecutable();
        var workingDirectory = ResolvePreviewWorkingDirectory(executable);
        var actualArguments = BuildLauncherConfigurationArguments()
            .Where(a => !a.StartsWith(LauncherOptionPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        ConfigLauncherEffectiveCommand = string.Join(" ", new[] { QuotePreview(executable) }.Concat(actualArguments.Select(QuotePreview)));
        ConfigLauncherProcessSummary =
            $"Working directory: {workingDirectory} · UseShellExecute={ConfigLauncherUseShellExecute} · CreateNoWindow={ConfigLauncherCreateNoWindow} · " +
            $"WindowStyle={ConfigLauncherWindowStyle} · Redirect stdout={ConfigLauncherRedirectStandardOutput} · Redirect stderr={ConfigLauncherRedirectStandardError} · " +
            $"Capture redirected output={ConfigLauncherCaptureRedirectedOutput} · Hide managed windows={ConfigLauncherHideManagedWindows}";

        var warnings = new List<string>();
        if (ConfigLauncherUseShellExecute && (ConfigLauncherRedirectStandardOutput || ConfigLauncherRedirectStandardError))
            warnings.Add("UseShellExecute cannot be combined with redirected stdout/stderr.");
        if (ConfigLauncherExecutable == "Custom path" && string.IsNullOrWhiteSpace(ConfigLauncherCustomExecutablePath))
            warnings.Add("Custom executable is selected but no path is set.");
        if (ConfigLauncherWorkingDirectory == "Custom path" && string.IsNullOrWhiteSpace(ConfigLauncherCustomWorkingDirectory))
            warnings.Add("Custom working directory is selected but no path is set.");
        if (!ConfigLauncherAbsLog && !ConfigLauncherRedirectStandardOutput && !ConfigLauncherRedirectStandardError)
            warnings.Add("No -abslog and no redirected streams are enabled, so PalServer output may be limited in MystTiq Live Console.");
        if (ConfigLauncherExecutable != "PalServer.exe")
            warnings.Add("Direct Shipping executable modes bypass PalServer.exe and are intended for diagnosis/testing.");

        ConfigLauncherWarning = string.Join(" ", warnings);
    }

    private string ResolvePreviewExecutable()
    {
        if (ConfigLauncherExecutable.Equals("Shipping-Cmd.exe", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(ConfigServerRoot, "Pal", "Binaries", "Win64", "PalServer-Win64-Shipping-Cmd.exe");
        if (ConfigLauncherExecutable.Equals("Shipping.exe", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(ConfigServerRoot, "Pal", "Binaries", "Win64", "PalServer-Win64-Shipping.exe");
        if (ConfigLauncherExecutable.Equals("Test-Cmd.exe", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(ConfigServerRoot, "Pal", "Binaries", "Win64", "PalServer-Win64-Test-Cmd.exe");
        if (ConfigLauncherExecutable.Equals("Test.exe", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(ConfigServerRoot, "Pal", "Binaries", "Win64", "PalServer-Win64-Test.exe");
        if (ConfigLauncherExecutable.Equals("Custom path", StringComparison.OrdinalIgnoreCase))
            return ExpandPreviewPath(ConfigLauncherCustomExecutablePath);
        return Path.Combine(ConfigServerRoot, "PalServer.exe");
    }

    private string ResolvePreviewWorkingDirectory(string executable)
    {
        if (ConfigLauncherWorkingDirectory.Equals("Executable folder", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(executable) ?? ConfigServerRoot;
        if (ConfigLauncherWorkingDirectory.Equals("Custom path", StringComparison.OrdinalIgnoreCase))
            return ExpandPreviewPath(ConfigLauncherCustomWorkingDirectory);
        return ConfigServerRoot;
    }

    private string ExpandPreviewPath(string value)
    {
        var logsRoot = string.IsNullOrWhiteSpace(ConfigServerRoot)
            ? string.Empty
            : Path.Combine(ConfigServerRoot, "Pal", "Saved", "Logs");
        return Environment.ExpandEnvironmentVariables(value ?? string.Empty)
            .Replace("%SERVERROOT%", ConfigServerRoot ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("%LOGSROOT%", logsRoot, StringComparison.OrdinalIgnoreCase)
            .Trim().Trim('"');
    }

    private string DefaultIdentityLogPath() =>
        string.IsNullOrWhiteSpace(ConfigServerRoot)
            ? @"%LOGSROOT%\Identity-Diagnostic.log"
            : Path.Combine(ConfigServerRoot, "Pal", "Saved", "Logs", "Identity-Diagnostic.log");

    private static Dictionary<string, string> ParseLauncherOptions(IEnumerable<string> arguments)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var argument in arguments)
        {
            if (!argument.StartsWith(LauncherOptionPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            var payload = argument[LauncherOptionPrefix.Length..];
            var separator = payload.IndexOf('=');
            if (separator < 0) values[payload] = "true";
            else values[payload[..separator]] = payload[(separator + 1)..];
        }
        return values;
    }

    private static string ReadLauncherOption(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

    private static bool ReadLauncherBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static bool HasExact(IEnumerable<string> arguments, string value) =>
        arguments.Any(a => a.Equals(value, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> SplitArgumentLines(string text) =>
        (text ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsKnownLauncherArgument(string argument)
    {
        var value = argument.Trim();
        return value.StartsWith("-port=", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-log", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-logformat=text", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-stdout", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-FullStdOutLogOutput", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("-abslog=", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-unattended", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-useperfthreads", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-NoAsyncLoadingThread", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-UseMultithreadForDS", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("-NoMods", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHistoricalSuppressedArgument(string argument) =>
        argument.Equals("-unattended", StringComparison.OrdinalIgnoreCase) ||
        argument.Equals("-useperfthreads", StringComparison.OrdinalIgnoreCase) ||
        argument.Equals("-NoAsyncLoadingThread", StringComparison.OrdinalIgnoreCase) ||
        argument.Equals("-UseMultithreadForDS", StringComparison.OrdinalIgnoreCase) ||
        argument.StartsWith("-logformat=", StringComparison.OrdinalIgnoreCase);

    private static string QuotePreview(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "\"\"";
        return value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
    }
}
