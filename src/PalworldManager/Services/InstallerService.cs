using System.Text.RegularExpressions;
using System.Security.Cryptography;
using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class InstallerService
{
    private const string SteamCmdUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
    private const string Ue4ssExperimentalReleaseApi = "https://api.github.com/repos/UE4SS-RE/RE-UE4SS/releases/tags/experimental-latest";
    private const string PythonFtpRoot = "https://www.python.org/ftp/python/";
    private const string PalworldSaveToolsLatestReleaseApi = "https://api.github.com/repos/cheahjs/palworld-save-tools/releases/latest";
    private const string PlmDecoderSourceZip = "https://github.com/deafdudecomputers/PalworldSaveTools/archive/refs/heads/master.zip";
    private const string PyOozSourceZip = "https://github.com/oMaN-Rod/pyooz/archive/refs/heads/main.zip";
    private const string OozSourceZip = "https://github.com/oMaN-Rod/ooz/archive/refs/heads/master.zip";
    private const string SimdeSourceZip = "https://github.com/simd-everywhere/simde/archive/refs/heads/master.zip";
    private const string VsBuildToolsBootstrapperUrl = "https://aka.ms/vs/17/release/vs_BuildTools.exe";
    private readonly AppSettings settings;
    private readonly HttpClient http;

    public InstallerService(AppSettings settings)
    {
        this.settings = settings;
        http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MystTiqPalworldServer/0.2.12");
    }


    public async Task InstallComponentAsync(string component, IProgress<InstallProgressInfo>? progress, CancellationToken ct, bool skipWhenVerified = false)
    {
        if (skipWhenVerified && IsComponentVerified(component))
        {
            progress?.Report(new()
            {
                Component = component,
                Message = component + " is already installed and verified.",
                Percent = 100
            });
            return;
        }

        switch (component)
        {
            case "SteamCMD":
                await InstallSteamCmdAsync(progress, ct);
                break;
            case "Palworld Dedicated Server":
                await InstallPalworldServerAsync(progress, ct);
                break;
            case "UE4SS Runtime":
                await InstallUe4ssAsync(progress, ct);
                break;
            case "Python Runtime":
                await InstallPythonAsync(progress, ct);
                break;
            case "Palworld Save Tools":
                await InstallPalworldSaveToolsAsync(progress, ct);
                break;
            case "Microsoft C++ Build Tools":
                await InstallCppBuildToolsAsync(progress, ct);
                break;
            case "PlM/Oodle Decoder":
                await InstallPlmDecoderAsync(progress, ct);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(component), component, "No installer is registered for this component.");
        }

        if (!IsComponentVerified(component))
            throw new InvalidOperationException(component + " installation finished, but post-install verification failed.");

        progress?.Report(new()
        {
            Component = component,
            Message = component + " installation completed and post-install verification passed.",
            Percent = 100
        });
    }

    private bool IsComponentVerified(string component)
    {
        return component switch
        {
            "SteamCMD" => File.Exists(settings.SteamCmdPath) && new FileInfo(settings.SteamCmdPath).Length > 0,
            "Palworld Dedicated Server" => File.Exists(settings.ServerExe) && new FileInfo(settings.ServerExe).Length > 0,
            "UE4SS Runtime" => IsUe4ssInstalled(),
            "Python Runtime" => !string.IsNullOrWhiteSpace(ResolvePythonExecutable()),
            "Palworld Save Tools" => File.Exists(ResolveSaveToolsConverter()),
            "Microsoft C++ Build Tools" => IsCppBuildToolsInstalled(),
            "PlM/Oodle Decoder" => File.Exists(ResolvePlmConverter()) && File.Exists(Path.Combine(PlmToolsFolder, ".myst-install.json")),
            _ => false
        };
    }

    private bool IsUe4ssInstalled()
    {
        var win64 = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64");
        return File.Exists(Path.Combine(win64, "dwmapi.dll")) ||
               File.Exists(Path.Combine(win64, "UE4SS.dll")) ||
               Directory.Exists(Path.Combine(win64, "ue4ss"));
    }



    private string ResolvePythonExecutable()
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.PythonExecutable)) candidates.Add(settings.PythonExecutable.Trim());
        candidates.AddRange(new[] { "py", "python", "python3" });
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
                if (process is null) continue;
                if (process.WaitForExit(5000) && process.ExitCode == 0) return candidate;
                try { process.Kill(true); } catch { }
            }
            catch { }
        }
        return string.Empty;
    }

    public async Task InstallPythonAsync(IProgress<InstallProgressInfo>? progress, CancellationToken ct)
    {
        var existing = ResolvePythonExecutable();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            settings.PythonExecutable = existing;
            progress?.Report(new() { Component = "Python Runtime", Message = "Python and pip are already available.", Percent = 100 });
            return;
        }

        progress?.Report(new() { Component = "Python Runtime", Message = "Resolving the latest official 64-bit Python release...", Percent = 10 });
        var listing = await http.GetStringAsync(PythonFtpRoot, ct);
        var versions = Regex.Matches(listing, "href=\"(3\\.\\d+\\.\\d+)/\"", RegexOptions.IgnoreCase)
            .Select(match => Version.TryParse(match.Groups[1].Value, out var version) ? version : null)
            .Where(version => version is not null && version.Major == 3)
            .Cast<Version>()
            .OrderByDescending(version => version)
            .ToList();
        if (versions.Count == 0) throw new InvalidOperationException("The official Python download index did not contain a stable Python 3 release.");

        string? downloadUrl = null;
        Version? selected = null;
        foreach (var version in versions)
        {
            var candidate = $"{PythonFtpRoot}{version}/python-{version}-amd64.exe";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, candidate);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode) continue;
                downloadUrl = candidate;
                selected = version;
                break;
            }
            catch { }
        }
        if (downloadUrl is null || selected is null) throw new InvalidOperationException("A current official 64-bit Python installer could not be located.");

        var installerPath = Path.Combine(Path.GetTempPath(), $"myst-python-{selected}-{Guid.NewGuid():N}.exe");
        try
        {
            await DownloadAsync(downloadUrl, installerPath, progress, "Python Runtime", 15, 65, ct);
            progress?.Report(new() { Component = "Python Runtime", Message = $"Installing Python {selected} with pip...", Percent = 72 });
            await RunProcessAsync(installerPath, new[]
            {
                "/quiet", "InstallAllUsers=1", "PrependPath=1", "Include_pip=1", "Include_launcher=1", "Include_test=0", "Shortcuts=0"
            }, settings.ServerRoot, progress, "Python Runtime", ct);

            var python = ResolvePythonExecutable();
            if (string.IsNullOrWhiteSpace(python))
            {
                var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python" + selected.Major + selected.Minor, "python.exe");
                if (File.Exists(expected)) python = expected;
            }
            if (string.IsNullOrWhiteSpace(python)) throw new InvalidOperationException("Python installation completed, but python.exe could not be located.");
            await RunProcessAsync(python, new[] { "-m", "pip", "--version" }, settings.ServerRoot, progress, "Python Runtime", ct);
            settings.PythonExecutable = python;
            progress?.Report(new() { Component = "Python Runtime", Message = $"Python {selected} and pip installed and verified.", Percent = 100 });
        }
        finally
        {
            try { if (File.Exists(installerPath)) File.Delete(installerPath); } catch { }
        }
    }

    private string SaveToolsFolder => Path.Combine(settings.ServerRoot, "Tools", "palworld-save-tools");

    private string ResolveSaveToolsConverter()
    {
        var candidates = new[]
        {
            settings.PalworldSaveToolsPath,
            Path.Combine(SaveToolsFolder, "convert.py"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "palworld-save-tools", "convert.py")
        };
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)) ?? string.Empty;
    }

    public async Task InstallPalworldSaveToolsAsync(IProgress<InstallProgressInfo>? progress, CancellationToken ct)
    {
        progress?.Report(new() { Component = "Palworld Save Tools", Message = "Checking Python availability...", Percent = 5 });
        var python = ResolvePythonExecutable();
        if (string.IsNullOrWhiteSpace(python))
        {
            await InstallPythonAsync(progress, ct);
            python = ResolvePythonExecutable();
        }
        if (string.IsNullOrWhiteSpace(python)) throw new InvalidOperationException("Python installation or detection failed.");
        settings.PythonExecutable = python;
        await RunProcessAsync(python, new[] { "--version" }, settings.ServerRoot, progress, "Palworld Save Tools", ct);

        progress?.Report(new() { Component = "Palworld Save Tools", Message = "Resolving the latest official cheahjs release...", Percent = 15 });
        using var response = await http.GetAsync(PalworldSaveToolsLatestReleaseApi, ct);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = document.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagNode) ? tagNode.GetString() ?? "latest" : "latest";
        var zipUrl = root.TryGetProperty("zipball_url", out var zipNode) ? zipNode.GetString() : null;
        if (string.IsNullOrWhiteSpace(zipUrl))
            throw new InvalidOperationException("The latest palworld-save-tools release did not provide a source archive.");

        var zip = Path.Combine(Path.GetTempPath(), $"myst-palworld-save-tools-{Guid.NewGuid():N}.zip");
        var extract = Path.Combine(Path.GetTempPath(), $"myst-palworld-save-tools-{Guid.NewGuid():N}");
        try
        {
            await DownloadAsync(zipUrl, zip, progress, "Palworld Save Tools", 20, 60, ct);
            progress?.Report(new() { Component = "Palworld Save Tools", Message = "Extracting the converter...", Percent = 65 });
            Directory.CreateDirectory(extract);
            ZipFile.ExtractToDirectory(zip, extract, true);
            var converter = Directory.EnumerateFiles(extract, "convert.py", SearchOption.AllDirectories).FirstOrDefault();
            if (converter is null)
                throw new FileNotFoundException("convert.py was not present in the downloaded official release.");
            var sourceRoot = Path.GetDirectoryName(converter)!;
            if (Directory.Exists(SaveToolsFolder)) Directory.Delete(SaveToolsFolder, true);
            CopyDirectory(sourceRoot, SaveToolsFolder);

            var requirements = Path.Combine(SaveToolsFolder, "requirements.txt");
            if (File.Exists(requirements))
            {
                progress?.Report(new() { Component = "Palworld Save Tools", Message = "Installing Python dependencies...", Percent = 75 });
                await RunProcessAsync(python, new[] { "-m", "pip", "install", "--disable-pip-version-check", "-r", requirements }, SaveToolsFolder, progress, "Palworld Save Tools", ct);
            }
            else
            {
                progress?.Report(new() { Component = "Palworld Save Tools", Message = "Installing the official Python package dependencies...", Percent = 75 });
                await RunProcessAsync(python, new[] { "-m", "pip", "install", "--disable-pip-version-check", "palworld-save-tools" }, SaveToolsFolder, progress, "Palworld Save Tools", ct);
            }

            var installedConverter = Path.Combine(SaveToolsFolder, "convert.py");
            progress?.Report(new() { Component = "Palworld Save Tools", Message = "Validating convert.py...", Percent = 92 });
            await RunProcessAsync(python, new[] { installedConverter, "--help" }, SaveToolsFolder, progress, "Palworld Save Tools", ct);
            settings.PalworldSaveToolsPath = installedConverter;
            File.WriteAllText(Path.Combine(SaveToolsFolder, ".myst-install.json"), JsonSerializer.Serialize(new { source = "cheahjs/palworld-save-tools", version = tag, installedAt = DateTimeOffset.Now, converter = installedConverter }, new JsonSerializerOptions { WriteIndented = true }));
            progress?.Report(new() { Component = "Palworld Save Tools", Message = $"palworld-save-tools {tag} installed and validated.", Percent = 100 });
        }
        finally
        {
            try { if (File.Exists(zip)) File.Delete(zip); } catch { }
            try { if (Directory.Exists(extract)) Directory.Delete(extract, true); } catch { }
        }
    }


    private static bool IsCppBuildToolsInstalled()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };
        return roots.Where(path => !string.IsNullOrWhiteSpace(path)).Any(root =>
            Directory.Exists(Path.Combine(root, "Microsoft Visual Studio")) &&
            Directory.EnumerateFiles(Path.Combine(root, "Microsoft Visual Studio"), "cl.exe", SearchOption.AllDirectories).Any());
    }

    public async Task InstallCppBuildToolsAsync(IProgress<InstallProgressInfo>? progress, CancellationToken ct)
    {
        if (IsCppBuildToolsInstalled())
        {
            progress?.Report(new() { Component = "Microsoft C++ Build Tools", Message = "MSVC compiler already detected.", Percent = 100 });
            return;
        }

        var winget = ResolveCommandOnPath("winget.exe") ?? ResolveCommandOnPath("winget");
        if (!string.IsNullOrWhiteSpace(winget))
        {
            progress?.Report(new() { Component = "Microsoft C++ Build Tools", Message = "Installing the Visual Studio C++ workload with WinGet...", Percent = 10 });
            try
            {
                await RunProcessAsync(winget, new[]
                {
                    "install", "--id", "Microsoft.VisualStudio.2022.BuildTools", "--exact",
                    "--accept-package-agreements", "--accept-source-agreements", "--silent",
                    "--override", "--wait --quiet --norestart --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
                }, settings.ServerRoot, progress, "Microsoft C++ Build Tools", ct);
            }
            catch (Exception ex)
            {
                progress?.Report(new() { Component = "Microsoft C++ Build Tools", Message = "WinGet installation failed; switching to the official Microsoft bootstrapper. " + ex.Message, Percent = 25 });
            }
        }
        else
        {
            progress?.Report(new() { Component = "Microsoft C++ Build Tools", Message = "WinGet is unavailable. Using the official Microsoft Build Tools installer...", Percent = 20 });
        }

        if (!IsCppBuildToolsInstalled())
        {
            var installerPath = Path.Combine(Path.GetTempPath(), $"vs_BuildTools_{Guid.NewGuid():N}.exe");
            try
            {
                await DownloadAsync(VsBuildToolsBootstrapperUrl, installerPath, progress, "Microsoft C++ Build Tools", 25, 45, ct);
                progress?.Report(new() { Component = "Microsoft C++ Build Tools", Message = "Installing Desktop C++ build tools. This can take several minutes...", Percent = 50 });
                await RunProcessAsync(installerPath, new[]
                {
                    "--quiet", "--wait", "--norestart",
                    "--add", "Microsoft.VisualStudio.Workload.VCTools",
                    "--includeRecommended"
                }, settings.ServerRoot, progress, "Microsoft C++ Build Tools", ct);
            }
            finally
            {
                try { if (File.Exists(installerPath)) File.Delete(installerPath); } catch { }
            }
        }

        if (!IsCppBuildToolsInstalled())
            throw new InvalidOperationException("Microsoft C++ Build Tools installation completed, but cl.exe was not detected. Restart Windows, then select Verify or retry the PlM decoder installation.");

        progress?.Report(new() { Component = "Microsoft C++ Build Tools", Message = "MSVC C++ Build Tools installed and verified.", Percent = 100 });
    }

    private static string? ResolveCommandOnPath(string command)
    {
        if (Path.IsPathRooted(command) && File.Exists(command)) return command;
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(directory, command);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    private string PlmToolsFolder => Path.Combine(settings.ServerRoot, "Tools", "palworld-plm-tools");

    private string ResolvePlmConverter()
    {
        var candidates = new[]
        {
            Path.Combine(PlmToolsFolder, "convert.py"),
            Path.Combine(PlmToolsFolder, "tools", "convert.py"),
            Path.Combine(PlmToolsFolder, "PalworldSaveTools", "convert.py")
        };
        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private string EnsurePlmConverterShim()
    {
        var existing = ResolvePlmConverter();
        if (!string.IsNullOrWhiteSpace(existing)) return existing;

        // Current PalworldSaveTools sources expose the PlM-capable converter as
        // the installed `palsav` console entry point rather than a repository-level
        // convert.py file. MystTiq creates a tiny compatibility shim so the rest of
        // the application can keep using the same converter invocation contract.
        var shim = Path.Combine(PlmToolsFolder, "convert.py");
        Directory.CreateDirectory(PlmToolsFolder);
        File.WriteAllText(shim,
            "from palsav.cli import main\n\n" +
            "if __name__ == '__main__':\n" +
            "    main()\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return shim;
    }

    public async Task InstallPlmDecoderAsync(IProgress<InstallProgressInfo>? progress, CancellationToken ct)
    {
        progress?.Report(new() { Component = "PlM/Oodle Decoder", Message = "Checking Python and pip...", Percent = 5 });
        var python = ResolvePythonExecutable();
        if (string.IsNullOrWhiteSpace(python))
        {
            await InstallPythonAsync(progress, ct);
            python = ResolvePythonExecutable();
        }
        if (string.IsNullOrWhiteSpace(python))
            throw new InvalidOperationException("Python is required before the PlM/Oodle decoder can be installed.");

        if (!IsCppBuildToolsInstalled())
        {
            progress?.Report(new() { Component = "PlM/Oodle Decoder", Message = "Microsoft C++ Build Tools are required to compile pyooz. Installing the C++ workload...", Percent = 10 });
            await InstallCppBuildToolsAsync(progress, ct);
        }

        var pyOozRoot = Path.Combine(settings.ServerRoot, "Tools", "plm-decoder", "pyooz");
        var pyOozZip = Path.Combine(Path.GetTempPath(), $"myst-pyooz-{Guid.NewGuid():N}.zip");
        var pyOozExtract = Path.Combine(Path.GetTempPath(), $"myst-pyooz-{Guid.NewGuid():N}");
        var oozZip = Path.Combine(Path.GetTempPath(), $"myst-ooz-{Guid.NewGuid():N}.zip");
        var oozExtract = Path.Combine(Path.GetTempPath(), $"myst-ooz-{Guid.NewGuid():N}");
        var simdeZip = Path.Combine(Path.GetTempPath(), $"myst-simde-{Guid.NewGuid():N}.zip");
        var simdeExtract = Path.Combine(Path.GetTempPath(), $"myst-simde-{Guid.NewGuid():N}");

        progress?.Report(new() { Component = "PlM/Oodle Decoder", Message = "Downloading complete pyooz source and native dependencies...", Percent = 18 });
        try
        {
            await DownloadAsync(PyOozSourceZip, pyOozZip, progress, "PlM/Oodle Decoder", 18, 28, ct);
            await DownloadAsync(OozSourceZip, oozZip, progress, "PlM/Oodle Decoder", 28, 38, ct);
            await DownloadAsync(SimdeSourceZip, simdeZip, progress, "PlM/Oodle Decoder", 38, 48, ct);

            Directory.CreateDirectory(pyOozExtract);
            Directory.CreateDirectory(oozExtract);
            Directory.CreateDirectory(simdeExtract);
            ZipFile.ExtractToDirectory(pyOozZip, pyOozExtract, true);
            ZipFile.ExtractToDirectory(oozZip, oozExtract, true);
            ZipFile.ExtractToDirectory(simdeZip, simdeExtract, true);

            var pyOozSource = Directory.EnumerateDirectories(pyOozExtract).FirstOrDefault() ?? pyOozExtract;
            var oozSource = Directory.EnumerateDirectories(oozExtract).FirstOrDefault() ?? oozExtract;
            var simdeSource = Directory.EnumerateDirectories(simdeExtract).FirstOrDefault() ?? simdeExtract;

            if (Directory.Exists(pyOozRoot)) Directory.Delete(pyOozRoot, true);
            CopyDirectory(pyOozSource, pyOozRoot);

            var oozDependency = Path.Combine(pyOozRoot, "ooz", "dep", "ooz");
            Directory.CreateDirectory(oozDependency);
            CopyDirectory(oozSource, oozDependency);

            var simdeDependency = Path.Combine(oozDependency, "simde");
            if (Directory.Exists(simdeDependency)) Directory.Delete(simdeDependency, true);
            CopyDirectory(simdeSource, simdeDependency);

            var requiredSources = new[]
            {
                Path.Combine(oozDependency, "bitknit.cpp"),
                Path.Combine(oozDependency, "kraken.cpp"),
                Path.Combine(oozDependency, "lzna.cpp"),
                Path.Combine(oozDependency, "compress.cpp"),
                Path.Combine(simdeDependency, "simde", "simde-common.h")
            };
            var missingSource = requiredSources.FirstOrDefault(path => !File.Exists(path));
            if (missingSource is not null)
                throw new FileNotFoundException("The PlM decoder source checkout is incomplete. A required pyooz/ooz source file was not downloaded.", missingSource);

            progress?.Report(new() { Component = "PlM/Oodle Decoder", Message = "Building pyooz from the complete local source checkout...", Percent = 52 });
            await RunProcessAsync(python, new[] { "-m", "pip", "install", "--disable-pip-version-check", "--upgrade", "." }, pyOozRoot, progress, "PlM/Oodle Decoder", ct);
        }
        finally
        {
            try { if (File.Exists(pyOozZip)) File.Delete(pyOozZip); } catch { }
            try { if (File.Exists(oozZip)) File.Delete(oozZip); } catch { }
            try { if (File.Exists(simdeZip)) File.Delete(simdeZip); } catch { }
            try { if (Directory.Exists(pyOozExtract)) Directory.Delete(pyOozExtract, true); } catch { }
            try { if (Directory.Exists(oozExtract)) Directory.Delete(oozExtract, true); } catch { }
            try { if (Directory.Exists(simdeExtract)) Directory.Delete(simdeExtract, true); } catch { }
        }

        var zip = Path.Combine(Path.GetTempPath(), $"myst-plm-tools-{Guid.NewGuid():N}.zip");
        var extract = Path.Combine(Path.GetTempPath(), $"myst-plm-tools-{Guid.NewGuid():N}");
        try
        {
            await DownloadAsync(PlmDecoderSourceZip, zip, progress, "PlM/Oodle Decoder", 62, 78, ct);
            Directory.CreateDirectory(extract);
            ZipFile.ExtractToDirectory(zip, extract, true);
            var sourceRoot = Directory.EnumerateDirectories(extract).FirstOrDefault() ?? extract;
            if (Directory.Exists(PlmToolsFolder)) Directory.Delete(PlmToolsFolder, true);
            CopyDirectory(sourceRoot, PlmToolsFolder);

            var requirements = Directory.EnumerateFiles(PlmToolsFolder, "requirements.txt", SearchOption.AllDirectories).FirstOrDefault();
            if (requirements is not null)
            {
                var requirementsDirectory = Path.GetDirectoryName(requirements)!;
                var filteredRequirements = Path.Combine(requirementsDirectory, ".myst-requirements.txt");
                var filteredLines = File.ReadAllLines(requirements)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Where(line => !line.StartsWith("#", StringComparison.Ordinal))
                    // The repository bundles palsav under src\palsav. Installing that entry
                    // through normal dependency resolution asks PyPI for the unpublished
                    // 'palooz' package. MystTiq installs the bundled package separately below
                    // with --no-deps because pyooz/ooz was already built and validated.
                    .Where(line => !line.Contains("src/palsav", StringComparison.OrdinalIgnoreCase))
                    .Where(line => !line.Contains("src\\palsav", StringComparison.OrdinalIgnoreCase))
                    .Where(line => !line.StartsWith("palsav-flex", StringComparison.OrdinalIgnoreCase))
                    .Where(line => !line.StartsWith("palooz", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                File.WriteAllLines(filteredRequirements, filteredLines);
                if (filteredLines.Length > 0)
                {
                    progress?.Report(new()
                    {
                        Component = "PlM/Oodle Decoder",
                        Message = "Installing PlM tool runtime dependencies...",
                        Percent = 80
                    });
                    await RunProcessAsync(python,
                        new[] { "-m", "pip", "install", "--disable-pip-version-check", "-r", filteredRequirements },
                        requirementsDirectory, progress, "PlM/Oodle Decoder", ct);
                }

                var bundledPalSav = Directory.EnumerateDirectories(PlmToolsFolder, "palsav", SearchOption.AllDirectories)
                    .FirstOrDefault(directory =>
                        File.Exists(Path.Combine(directory, "pyproject.toml")) ||
                        File.Exists(Path.Combine(directory, "setup.py")));

                if (!string.IsNullOrWhiteSpace(bundledPalSav))
                {
                    progress?.Report(new()
                    {
                        Component = "PlM/Oodle Decoder",
                        Message = "Installing the bundled palsav parser without unresolved PyPI dependencies...",
                        Percent = 86
                    });
                    await RunProcessAsync(python,
                        new[] { "-m", "pip", "install", "--disable-pip-version-check", "--upgrade", "--force-reinstall", "--no-deps", bundledPalSav },
                        bundledPalSav, progress, "PlM/Oodle Decoder", ct);
                }
                else
                {
                    throw new DirectoryNotFoundException("The PlM tools package did not contain its bundled src\\palsav parser.");
                }

                // palsav expects a module named `palooz` in palsav/lib/windows, while
                // oMaN-Rod/pyooz installs its ABI-compatible extension as `ooz`.
                // Create a small compatibility module in the exact location searched by
                // palsav so PlM decompression can use the locally compiled extension.
                progress?.Report(new()
                {
                    Component = "PlM/Oodle Decoder",
                    Message = "Wiring the compiled Oodle extension into palsav...",
                    Percent = 89
                });
                const string paloozShimScript = "import pathlib,sysconfig\n" +
                    "root=pathlib.Path(sysconfig.get_paths()['purelib'])/'palsav'/'lib'/'windows'\n" +
                    "root.mkdir(parents=True,exist_ok=True)\n" +
                    "(root/'palooz.py').write_text('from ooz import compress, decompress\\n',encoding='utf-8')\n";
                await RunProcessAsync(python,
                    new[] { "-c", paloozShimScript },
                    PlmToolsFolder, progress, "PlM/Oodle Decoder", ct);
                await RunProcessAsync(python,
                    new[] { "-c", "import ooz,sysconfig,pathlib,sys; p=pathlib.Path(sysconfig.get_paths()['purelib'])/'palsav'/'lib'/'windows'; sys.path.insert(0,str(p)); import palooz; assert callable(palooz.decompress); print(p)" },
                    PlmToolsFolder, progress, "PlM/Oodle Decoder", ct);
            }

            var converter = EnsurePlmConverterShim();
            if (string.IsNullOrWhiteSpace(converter) || !File.Exists(converter))
                throw new FileNotFoundException("MystTiq could not create the PlM converter compatibility shim.");

            progress?.Report(new() { Component = "PlM/Oodle Decoder", Message = "Validating the PlM converter...", Percent = 92 });
            await RunProcessAsync(python, new[] { converter, "--help" }, Path.GetDirectoryName(converter)!, progress, "PlM/Oodle Decoder", ct);
            File.WriteAllText(Path.Combine(PlmToolsFolder, ".myst-install.json"), JsonSerializer.Serialize(new
            {
                source = "deafdudecomputers/PalworldSaveTools",
                oodle = "oMaN-Rod/pyooz + oMaN-Rod/ooz + simd-everywhere/simde",
                pyOozSource = pyOozRoot,
                installedAt = DateTimeOffset.Now,
                converter
            }, new JsonSerializerOptions { WriteIndented = true }));
            progress?.Report(new() { Component = "PlM/Oodle Decoder", Message = "PlM/Oodle decoder installed and validated.", Percent = 100 });
        }
        finally
        {
            try { if (File.Exists(zip)) File.Delete(zip); } catch { }
            try { if (Directory.Exists(extract)) Directory.Delete(extract, true); } catch { }
        }
    }

    public async Task InstallSteamCmdAsync(IProgress<InstallProgressInfo>? progress, CancellationToken ct)
    {
        var folder = Path.GetDirectoryName(settings.SteamCmdPath) ?? throw new InvalidOperationException("SteamCMD path is invalid.");
        Directory.CreateDirectory(folder);
        var zip = Path.Combine(Path.GetTempPath(), $"myst-steamcmd-{Guid.NewGuid():N}.zip");
        progress?.Report(new() { Component="SteamCMD", Message="Downloading SteamCMD from Valve...", Percent=10 });
        await DownloadAsync(SteamCmdUrl, zip, progress, "SteamCMD", 10, 65, ct);
        progress?.Report(new() { Component="SteamCMD", Message="Extracting SteamCMD...", Percent=72 });
        ZipFile.ExtractToDirectory(zip, folder, true);
        File.Delete(zip);
        if (!File.Exists(settings.SteamCmdPath)) throw new FileNotFoundException("steamcmd.exe was not found after extraction.", settings.SteamCmdPath);
        progress?.Report(new() { Component="SteamCMD", Message="Running SteamCMD self-update...", Percent=82 });
        Exception? selfUpdateFailure = null;
        try
        {
            await RunProcessAsync(settings.SteamCmdPath, "+quit", folder, progress, "SteamCMD", ct);
        }
        catch (Exception ex)
        {
            selfUpdateFailure = ex;
        }

        if (!File.Exists(settings.SteamCmdPath) || new FileInfo(settings.SteamCmdPath).Length == 0)
            throw new FileNotFoundException("SteamCMD self-update failed and steamcmd.exe could not be verified.", settings.SteamCmdPath, selfUpdateFailure);

        progress?.Report(new()
        {
            Component="SteamCMD",
            Message=selfUpdateFailure is null
                ? "SteamCMD installed and verified."
                : $"SteamCMD was extracted and verified. Self-update returned a warning: {selfUpdateFailure.Message}",
            Percent=100
        });
    }

    public async Task InstallPalworldServerAsync(IProgress<InstallProgressInfo>? progress, CancellationToken ct)
    {
        if (!File.Exists(settings.SteamCmdPath))
            throw new InvalidOperationException("Install SteamCMD first.");

        Directory.CreateDirectory(settings.ServerRoot);
        var steamCmdFolder = Path.GetDirectoryName(settings.SteamCmdPath)
            ?? throw new InvalidOperationException("SteamCMD path is invalid.");

        progress?.Report(new()
        {
            Component = "Palworld Dedicated Server",
            Message = "Downloading and validating Palworld Dedicated Server (App ID 2394010)...",
            Percent = 5
        });

        var failures = new List<string>();

        // Use ProcessStartInfo.ArgumentList so paths containing spaces are passed to
        // SteamCMD exactly as intended. The first attempt follows the official
        // install command and validates the downloaded files.
        var validateArgs = new[]
        {
            "+force_install_dir", settings.ServerRoot,
            "+login", "anonymous",
            "+app_update", "2394010", "validate",
            "+quit"
        };

        try
        {
            await RunProcessAsync(settings.SteamCmdPath, validateArgs, steamCmdFolder, progress, "Palworld Dedicated Server", ct);
        }
        catch (Exception ex)
        {
            failures.Add("Validated install: " + ex.Message);
        }

        // SteamCMD can return a non-zero code before validation completes. Retry the
        // download without validate, then verify the actual executable ourselves.
        if (!IsServerExecutableValid())
        {
            progress?.Report(new()
            {
                Component = "Palworld Dedicated Server",
                Message = "The validated install did not finish. Retrying the download without validation...",
                Percent = 45
            });

            var installArgs = new[]
            {
                "+force_install_dir", settings.ServerRoot,
                "+login", "anonymous",
                "+app_update", "2394010",
                "+quit"
            };

            try
            {
                await RunProcessAsync(settings.SteamCmdPath, installArgs, steamCmdFolder, progress, "Palworld Dedicated Server", ct);
            }
            catch (Exception ex)
            {
                failures.Add("Install retry: " + ex.Message);
            }
        }

        // Some SteamCMD builds ignore force_install_dir and use their default
        // steamapps/common/PalServer directory. Detect that result and copy it into
        // the configured server root rather than reporting a false failure.
        if (!IsServerExecutableValid())
            TryRecoverDefaultSteamCmdInstall(steamCmdFolder, progress);

        if (!IsServerExecutableValid())
        {
            var detail = failures.Count == 0
                ? "SteamCMD finished without creating the server executable."
                : string.Join(Environment.NewLine, failures);

            throw new FileNotFoundException(
                "Palworld Dedicated Server could not be verified. PalServer.exe was not created at:" +
                Environment.NewLine + settings.ServerExe + Environment.NewLine + Environment.NewLine +
                detail + Environment.NewLine + Environment.NewLine +
                "Check free disk space, antivirus protection, and SteamCMD network access, then try Repair.",
                settings.ServerExe);
        }

        progress?.Report(new()
        {
            Component = "Palworld Dedicated Server",
            Message = failures.Count == 0
                ? "Dedicated server installed and verified."
                : "PalServer.exe was installed and verified after SteamCMD returned a warning.",
            Percent = 100
        });
    }

    private bool IsServerExecutableValid()
    {
        return File.Exists(settings.ServerExe) && new FileInfo(settings.ServerExe).Length > 0;
    }

    private void TryRecoverDefaultSteamCmdInstall(string steamCmdFolder, IProgress<InstallProgressInfo>? progress)
    {
        var defaultRoot = Path.Combine(steamCmdFolder, "steamapps", "common", "PalServer");
        var defaultExe = Path.Combine(defaultRoot, "PalServer.exe");
        if (!File.Exists(defaultExe) || new FileInfo(defaultExe).Length == 0)
            return;

        progress?.Report(new()
        {
            Component = "Palworld Dedicated Server",
            Message = "SteamCMD installed the server in its default library. Moving it to the configured server folder...",
            Percent = 85
        });

        CopyDirectory(defaultRoot, settings.ServerRoot);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    public static string GenerateSecureAdminPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++) chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }

    public void CreateDefaultConfiguration(string serverName, string description, string adminPassword, string serverPassword, int maxPlayers, int publicPort, int restPort)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settings.ConfigFile)!);
        var source = Path.Combine(settings.ServerRoot, "DefaultPalWorldSettings.ini");
        var text = File.Exists(source) ? File.ReadAllText(source) : "[/Script/Pal.PalGameWorldSettings]\nOptionSettings=()\n";

        // Palworld's REST/admin features require a non-empty AdminPassword.
        // A safe starter value is written when the setup field is left blank so a
        // newly installed server never starts with an unusable REST configuration.
        var effectiveAdminPassword = string.IsNullOrWhiteSpace(adminPassword)
            ? GenerateSecureAdminPassword()
            : adminPassword.Trim();

        var values = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServerName"] = Quote(serverName), ["ServerDescription"] = Quote(description),
            ["AdminPassword"] = Quote(effectiveAdminPassword), ["ServerPassword"] = Quote(serverPassword),
            ["ServerPlayerMaxNum"] = maxPlayers.ToString(), ["PublicPort"] = publicPort.ToString(),
            ["RESTAPIEnabled"] = "True", ["RESTAPIPort"] = restPort.ToString(),
            ["RCONEnabled"] = "True", ["RCONPort"] = "25575",
            ["bIsUseBackupSaveData"] = "True"
        };
        foreach (var item in values) text = SetOption(text, item.Key, item.Value);
        File.WriteAllText(settings.ConfigFile, text);
        Directory.CreateDirectory(settings.BackupRoot);
    }

    public async Task InstallUe4ssAsync(IProgress<InstallProgressInfo>? progress, CancellationToken ct)
    {
        var win64 = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64");
        if (!Directory.Exists(win64)) throw new DirectoryNotFoundException("Install the Palworld dedicated server first.");
        progress?.Report(new() { Component="UE4SS Runtime", Message="Finding the latest experimental UE4SS release...", Percent=8 });
        using var response = await http.GetAsync(Ue4ssExperimentalReleaseApi, ct); response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        string? url=null;
        foreach (var asset in document.RootElement.GetProperty("assets").EnumerateArray())
        {
            var name=asset.GetProperty("name").GetString() ?? "";
            if (name.EndsWith(".zip",StringComparison.OrdinalIgnoreCase) && !name.Contains("zDEV",StringComparison.OrdinalIgnoreCase))
            { url=asset.GetProperty("browser_download_url").GetString(); break; }
        }
        if (url is null) throw new InvalidOperationException("No suitable non-development ZIP was found in the latest experimental UE4SS release.");
        var zip=Path.Combine(Path.GetTempPath(),$"myst-ue4ss-{Guid.NewGuid():N}.zip");
        await DownloadAsync(url,zip,progress,"UE4SS Runtime",15,70,ct);
        progress?.Report(new() { Component="UE4SS Runtime", Message="Extracting UE4SS to the server Win64 folder...", Percent=78 });
        ZipFile.ExtractToDirectory(zip,win64,true); File.Delete(zip);
        var detected=File.Exists(Path.Combine(win64,"dwmapi.dll")) || File.Exists(Path.Combine(win64,"UE4SS.dll")) || Directory.Exists(Path.Combine(win64,"ue4ss"));
        if(!detected) throw new InvalidOperationException("UE4SS files were extracted, but the runtime could not be verified.");
        progress?.Report(new() { Component="UE4SS Runtime", Message="UE4SS installed and verified.", Percent=100 });
    }

    public async Task InstallRequiredAsync(string serverName,string description,string adminPassword,string serverPassword,int maxPlayers,int publicPort,int restPort,IProgress<InstallProgressInfo>? progress,CancellationToken ct)
    {
        await InstallComponentAsync("Python Runtime", progress, ct, skipWhenVerified: true);
        await InstallComponentAsync("SteamCMD", progress, ct, skipWhenVerified: true);
        await InstallComponentAsync("Palworld Dedicated Server", progress, ct, skipWhenVerified: true);
        await InstallComponentAsync("UE4SS Runtime", progress, ct, skipWhenVerified: true);
        await InstallComponentAsync("Palworld Save Tools", progress, ct, skipWhenVerified: true);
        await InstallComponentAsync("Microsoft C++ Build Tools", progress, ct, skipWhenVerified: true);
        await InstallComponentAsync("PlM/Oodle Decoder", progress, ct, skipWhenVerified: true);
        if(!File.Exists(settings.ConfigFile))
            CreateDefaultConfiguration(serverName,description,adminPassword,serverPassword,maxPlayers,publicPort,restPort);
        else
            EnsureRemoteAdministrationEnabled(adminPassword, restPort);
        Directory.CreateDirectory(settings.BackupRoot);
        Directory.CreateDirectory(settings.ModsRoot);
        Directory.CreateDirectory(settings.WorkshopRoot);
        Directory.CreateDirectory(settings.ManagedModsRoot);
        progress?.Report(new(){Component="Required Components",Message="Python, Microsoft C++ Build Tools, SteamCMD, the dedicated server, latest experimental UE4SS, Palworld Save Tools, configuration, backup storage, and mod folders are installed and verified.",Percent=100});
    }


    public void EnsureRemoteAdministrationEnabled(string? adminPassword = null, int restPort = 8212, int rconPort = 25575)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settings.ConfigFile)!);
        var text = File.Exists(settings.ConfigFile)
            ? File.ReadAllText(settings.ConfigFile)
            : "[/Script/Pal.PalGameWorldSettings]\nOptionSettings=()\n";

        var currentAdminPassword = ReadOption(text, "AdminPassword");
        var effectivePassword = !string.IsNullOrWhiteSpace(adminPassword)
            ? adminPassword.Trim()
            : !string.IsNullOrWhiteSpace(currentAdminPassword)
                ? currentAdminPassword
                : GenerateSecureAdminPassword();

        text = SetOption(text, "AdminPassword", Quote(effectivePassword));
        text = SetOption(text, "RESTAPIEnabled", "True");
        text = SetOption(text, "RESTAPIPort", restPort.ToString());
        text = SetOption(text, "RCONEnabled", "True");
        text = SetOption(text, "RCONPort", rconPort.ToString());
        File.WriteAllText(settings.ConfigFile, text);
    }

    private static string? ReadOption(string text, string key)
    {
        var pattern = @"(?:^|,)\s*" + Regex.Escape(key) + @"\s*=\s*(?:""(?<quoted>(?:\\.|[^""])*)""|(?<plain>[^,)]*))";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        var value = match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value.Trim();
        return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
    }

    private async Task DownloadAsync(string url,string target,IProgress<InstallProgressInfo>? progress,string component,int start,int end,CancellationToken ct)
    {
        using var response=await http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,ct); response.EnsureSuccessStatusCode();
        var total=response.Content.Headers.ContentLength; await using var input=await response.Content.ReadAsStreamAsync(ct); await using var output=File.Create(target);
        var buffer=new byte[81920]; long readTotal=0; int read;
        while((read=await input.ReadAsync(buffer,ct))>0){await output.WriteAsync(buffer.AsMemory(0,read),ct);readTotal+=read;if(total>0){var pct=start+(int)((end-start)*(readTotal/(double)total.Value));progress?.Report(new(){Component=component,Message="Downloading...",Percent=Math.Clamp(pct,start,end)});}}
    }

    private static Task RunProcessAsync(string file, string args, string working, IProgress<InstallProgressInfo>? progress, string component, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(file, args)
        {
            WorkingDirectory = working,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        return RunProcessAsync(startInfo, progress, component, ct);
    }

    private static Task RunProcessAsync(string file, IEnumerable<string> args, string working, IProgress<InstallProgressInfo>? progress, string component, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(file)
        {
            WorkingDirectory = working,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in args)
            startInfo.ArgumentList.Add(argument);
        return RunProcessAsync(startInfo, progress, component, ct);
    }

    private static async Task RunProcessAsync(ProcessStartInfo startInfo, IProgress<InstallProgressInfo>? progress, string component, CancellationToken ct)
    {
        var outputTail = new Queue<string>();
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        void Capture(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            progress?.Report(new() { Component = component, Message = line, Percent = 90 });
            outputTail.Enqueue(line);
            while (outputTail.Count > 12) outputTail.Dequeue();
        }

        process.OutputDataReceived += (_, e) => Capture(e.Data);
        process.ErrorDataReceived += (_, e) => Capture(e.Data);

        if (!process.Start())
            throw new InvalidOperationException($"Could not start {Path.GetFileName(startInfo.FileName)}.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var tail = outputTail.Count == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, outputTail);
            throw new InvalidOperationException($"{Path.GetFileName(startInfo.FileName)} exited with code {process.ExitCode}.{tail}");
        }
    }
    private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    private static string SetOption(string text, string key, string value)
    {
        var pattern = $"(?<prefix>(?:^|,){Regex.Escape(key)}=)(?:\"(?:\\\\.|[^\"])*\"|[^,)]*)";
        if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
            return Regex.Replace(text, pattern, match => match.Groups["prefix"].Value + value, RegexOptions.IgnoreCase);
        var close = text.LastIndexOf(')');
        return close >= 0 ? text.Insert(close, $",{key}={value}") : text + $"\nOptionSettings=({key}={value})\n";
    }
}
