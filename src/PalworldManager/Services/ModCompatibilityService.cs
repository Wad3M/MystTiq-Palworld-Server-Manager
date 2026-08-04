using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Performs conservative, evidence-based compatibility checks. The scanner reports
/// confirmed file overlap only when MystTiq installation manifests prove that two mods
/// own the same destination file. Name-family matches are reported as potential
/// conflicts rather than confirmed failures.
/// </summary>
public sealed class ModCompatibilityService(AppSettings settings)
{
    private static readonly string[] DependencyPropertyNames =
    [
        "Dependencies", "dependencies", "RequiredMods", "requiredMods", "Requires", "requires"
    ];

    private static readonly (string Family, string[] Tokens)[] FeatureFamilies =
    [
        ("base range", ["baserange", "baseradius", "extendedbase", "biggerbase"]),
        ("carry weight", ["carryweight", "weightlimit", "playerweight"]),
        ("stack size", ["stacksize", "largerstack", "biggerstack"]),
        ("pal AI", ["betterpalai", "palai", "aifixes"]),
        ("map", ["fullmap", "mapreveal", "minimap"])
    ];

    public ModCompatibilitySummary Scan(IEnumerable<ModRow> mods)
    {
        var installed = mods.ToList();
        var fileOwners = BuildFileOwners();
        var ruleConflicts = ReadKnownConflictRules();
        var results = installed.Select(mod => ScanOne(mod, installed, fileOwners, ruleConflicts)).ToList();
        return new ModCompatibilitySummary { Results = results };
    }

    private ModCompatibilityResult ScanOne(
        ModRow mod,
        IReadOnlyList<ModRow> installed,
        IReadOnlyDictionary<string, IReadOnlyList<string>> fileOwners,
        IReadOnlyDictionary<string, IReadOnlyList<string>> ruleConflicts)
    {
        var dependencies = ReadDependencies(mod).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var missing = dependencies.Where(dep => !DependencySatisfied(dep, installed)).ToList();
        var satisfied = dependencies.Where(dep => DependencySatisfied(dep, installed)).ToList();
        var redundant = dependencies.Where(dep => IsFrameworkDependencyAlreadyProvided(dep)).ToList();

        var sharedFiles = fileOwners
            .Where(pair => pair.Value.Any(owner => owner.Equals(mod.Package, StringComparison.OrdinalIgnoreCase)) &&
                           pair.Value.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(pair => pair.Key)
            .Take(8)
            .ToList();

        var conflictMessages = new List<string>();
        if (sharedFiles.Count > 0)
            conflictMessages.Add($"Confirmed file overlap on {sharedFiles.Count} managed destination file(s).");

        foreach (var other in installed.Where(item => !ReferenceEquals(item, mod) && item.Enabled))
        {
            var family = FindSharedFeatureFamily(mod, other);
            if (!string.IsNullOrWhiteSpace(family))
                conflictMessages.Add($"Potential {family} overlap with {other.Name}.");
        }

        if (ruleConflicts.TryGetValue(Normalize(mod.Package), out var known))
        {
            foreach (var otherKey in known)
            {
                var other = installed.FirstOrDefault(item => Normalize(item.Package) == otherKey || Normalize(item.Name) == otherKey);
                if (other is not null)
                    conflictMessages.Add($"Known conflict rule matches {other.Name}.");
            }
        }

        conflictMessages = conflictMessages.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var (availableVersion, updateAvailable, versionStatus) = DetermineVersionStatus(mod);
        var dependencyStatus = dependencies.Count == 0
            ? "None declared"
            : missing.Count > 0
                ? $"Missing {missing.Count}"
                : redundant.Count > 0
                    ? $"Satisfied by server ({redundant.Count})"
                    : "Complete";
        var conflictStatus = sharedFiles.Count > 0 ? "Confirmed conflict" : conflictMessages.Count > 0 ? "Potential conflict" : "No known conflict";

        ModCompatibilityState overall;
        if (!mod.Deployed) overall = ModCompatibilityState.Failed;
        else if (sharedFiles.Count > 0) overall = ModCompatibilityState.Conflict;
        else if (missing.Count > 0 || conflictMessages.Count > 0 || updateAvailable) overall = ModCompatibilityState.Attention;
        else overall = ModCompatibilityState.Compatible;

        var details = new List<string>();
        if (missing.Count > 0) details.Add("Missing: " + string.Join(", ", missing));
        if (redundant.Count > 0) details.Add("Already satisfied by the server: " + string.Join(", ", redundant) + ". Do not install duplicate runtime/framework copies.");
        if (conflictMessages.Count > 0) details.Add(string.Join(" ", conflictMessages));
        if (updateAvailable) details.Add($"Update available: {mod.Version} → {availableVersion}.");
        if (details.Count == 0) details.Add("No dependency, overlap, or local version issues detected.");

        return new ModCompatibilityResult
        {
            Package = mod.Package,
            Name = mod.Name,
            Dependencies = dependencies,
            MissingDependencies = missing,
            SatisfiedDependencies = satisfied,
            RedundantDependencies = redundant,
            Conflicts = conflictMessages,
            SharedFiles = sharedFiles,
            DependencyStatus = dependencyStatus,
            ConflictStatus = conflictStatus,
            VersionStatus = versionStatus,
            AvailableVersion = availableVersion,
            UpdateAvailable = updateAvailable,
            OverallState = overall,
            OverallStatus = overall switch
            {
                ModCompatibilityState.Compatible => "Compatible",
                ModCompatibilityState.Attention => "Attention",
                ModCompatibilityState.Conflict => "Conflict",
                ModCompatibilityState.Failed => "Failed",
                _ => "Unknown"
            },
            Details = string.Join(" ", details),
            CheckedAt = DateTime.Now
        };
    }

    private IReadOnlyList<string> ReadDependencies(ModRow mod)
    {
        var dependencies = new List<string>();

        var manifestPath = Path.Combine(settings.ManagedModsRoot, CleanName(mod.Package), "InstallManifest.json");
        if (File.Exists(manifestPath))
            TryReadDependenciesFromJson(manifestPath, dependencies);

        var workshopFolder = ResolveWorkshopFolder(mod);
        if (!string.IsNullOrWhiteSpace(workshopFolder) && Directory.Exists(workshopFolder))
        {
            foreach (var metadataName in new[] { "Info.json", "manifest.json", "mod.json", "metadata.json" })
            {
                var metadataPath = Directory.EnumerateFiles(workshopFolder, metadataName, SearchOption.AllDirectories).FirstOrDefault();
                if (metadataPath is not null) TryReadDependenciesFromJson(metadataPath, dependencies);
            }
        }

        return dependencies;
    }

    private static void TryReadDependenciesFromJson(string path, List<string> dependencies)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var propertyName in DependencyPropertyNames)
            {
                if (!TryGetProperty(document.RootElement, propertyName, out var property)) continue;
                ReadDependencyValue(property, dependencies);
            }
        }
        catch
        {
            // Third-party metadata is optional. Invalid JSON should not abort a scan.
        }
    }

    private static void ReadDependencyValue(JsonElement value, List<string> dependencies)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text)) dependencies.Add(text.Trim());
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray()) ReadDependencyValue(item, dependencies);
                break;
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    if (property.Name.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("Package", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("PackageName", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                        ReadDependencyValue(property.Value, dependencies);
                }
                break;
        }
    }

    private bool IsFrameworkDependencyAlreadyProvided(string dependency)
    {
        var key = Normalize(dependency);
        if (!key.Contains("ue4ss", StringComparison.OrdinalIgnoreCase))
            return false;

        var win64 = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64");
        return File.Exists(Path.Combine(win64, "UE4SS.dll")) ||
               File.Exists(Path.Combine(win64, "UE4SS-settings.ini")) ||
               Directory.Exists(Path.Combine(win64, "UE4SS")) ||
               Directory.Exists(Path.Combine(win64, "Mods"));
    }

    private bool DependencySatisfied(string dependency, IReadOnlyList<ModRow> installed)
    {
        var key = Normalize(dependency);
        if (key.Length == 0) return true;

        if (key.Contains("ue4ss", StringComparison.OrdinalIgnoreCase))
        {
            var win64 = Path.Combine(settings.ServerRoot, "Pal", "Binaries", "Win64");
            return File.Exists(Path.Combine(win64, "UE4SS.dll")) ||
                   Directory.Exists(Path.Combine(win64, "UE4SS")) ||
                   Directory.Exists(Path.Combine(win64, "Mods"));
        }

        return installed.Any(item =>
        {
            var package = Normalize(item.Package);
            var name = Normalize(item.Name);
            return item.Deployed && (package == key || name == key || package.Contains(key) || key.Contains(package) || name.Contains(key) || key.Contains(name));
        });
    }

    private IReadOnlyDictionary<string, IReadOnlyList<string>> BuildFileOwners()
    {
        var owners = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(settings.ManagedModsRoot))
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var manifestPath in Directory.EnumerateFiles(settings.ManagedModsRoot, "InstallManifest.json", SearchOption.AllDirectories))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                if (!TryGetProperty(document.RootElement, "Package", out var packageElement)) continue;
                var package = packageElement.ToString();
                if (string.IsNullOrWhiteSpace(package)) continue;
                if (!TryGetProperty(document.RootElement, "Files", out var filesElement) || filesElement.ValueKind != JsonValueKind.Array) continue;

                foreach (var file in filesElement.EnumerateArray())
                {
                    var path = file.ToString();
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    var normalizedPath = Path.GetFullPath(path);
                    if (!owners.TryGetValue(normalizedPath, out var list)) owners[normalizedPath] = list = [];
                    if (!list.Contains(package, StringComparer.OrdinalIgnoreCase)) list.Add(package);
                }
            }
            catch
            {
                // Damaged legacy manifests are ignored; other mods can still be scanned.
            }
        }

        return owners.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyDictionary<string, IReadOnlyList<string>> ReadKnownConflictRules()
    {
        var path = Path.Combine(settings.ManagedModsRoot, "ModCompatibilityRules.json");
        if (!File.Exists(path))
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return result;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array) continue;
                result[Normalize(property.Name)] = property.Value.EnumerateArray()
                    .Select(item => Normalize(item.ToString()))
                    .Where(item => item.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            return result;
        }
        catch
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private (string AvailableVersion, bool UpdateAvailable, string Status) DetermineVersionStatus(ModRow mod)
    {
        if (!mod.Source.StartsWith("Steam Workshop ", StringComparison.OrdinalIgnoreCase))
            return ("", false, string.IsNullOrWhiteSpace(mod.Version) ? "Unknown" : "Installed " + mod.Version);

        var folder = ResolveWorkshopFolder(mod);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return ("", false, "Workshop source unavailable");

        var available = ReadVersionFromFolder(folder);
        if (string.IsNullOrWhiteSpace(available) || string.IsNullOrWhiteSpace(mod.Version))
            return (available, false, "Version unknown");

        var update = CompareVersions(available, mod.Version) > 0;
        return (available, update, update ? $"Update {available}" : "Current");
    }

    private string? ResolveWorkshopFolder(ModRow mod)
    {
        if (!Directory.Exists(settings.WorkshopRoot)) return null;
        var sourceId = mod.Source.StartsWith("Steam Workshop ", StringComparison.OrdinalIgnoreCase)
            ? mod.Source["Steam Workshop ".Length..].Trim()
            : "";

        var directCandidates = new[]
        {
            Path.Combine(settings.WorkshopRoot, sourceId),
            Path.Combine(settings.WorkshopRoot, CleanName(mod.Package))
        };
        var direct = directCandidates.FirstOrDefault(Directory.Exists);
        if (direct is not null) return direct;

        foreach (var folder in Directory.EnumerateDirectories(settings.WorkshopRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var info = Directory.EnumerateFiles(folder, "Info.json", SearchOption.AllDirectories).FirstOrDefault();
            if (info is null) continue;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(info));
                if (TryGetProperty(document.RootElement, "PackageName", out var package) &&
                    package.ToString().Equals(mod.Package, StringComparison.OrdinalIgnoreCase))
                    return folder;
            }
            catch { }
        }
        return null;
    }

    private static string ReadVersionFromFolder(string folder)
    {
        foreach (var jsonName in new[] { "Info.json", "manifest.json", "mod.json", "metadata.json" })
        {
            var path = Directory.EnumerateFiles(folder, jsonName, SearchOption.AllDirectories).FirstOrDefault();
            if (path is null) continue;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                foreach (var key in new[] { "Version", "version", "ModVersion", "mod_version" })
                    if (TryGetProperty(document.RootElement, key, out var value) && !string.IsNullOrWhiteSpace(value.ToString()))
                        return value.ToString();
            }
            catch { }
        }
        return "";
    }

    private static int CompareVersions(string left, string right)
    {
        if (Version.TryParse(CleanVersion(left), out var a) && Version.TryParse(CleanVersion(right), out var b))
            return a.CompareTo(b);
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase) ? 0 : 0;
    }

    private static string CleanVersion(string value)
    {
        var match = Regex.Match(value ?? "", @"\d+(?:\.\d+){0,3}");
        return match.Success ? match.Value : "0";
    }

    private static string? FindSharedFeatureFamily(ModRow first, ModRow second)
    {
        var a = Normalize(first.Package + first.Name);
        var b = Normalize(second.Package + second.Name);
        foreach (var (family, tokens) in FeatureFamilies)
            if (tokens.Any(token => a.Contains(token, StringComparison.OrdinalIgnoreCase)) &&
                tokens.Any(token => b.Contains(token, StringComparison.OrdinalIgnoreCase)))
                return family;
        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static string Normalize(string value) => Regex.Replace(value ?? "", "[^a-zA-Z0-9]", "").ToLowerInvariant();

    private static string CleanName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string((value ?? "").Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
    }
}
