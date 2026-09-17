[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [switch]$RunBuild,
    [bool]$ExportJson = $true,
    [bool]$ExportJUnit = $true
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$root = (Resolve-Path $ProjectRoot).Path
$framework = Join-Path $root 'scripts\Testing\MystTiq.TestFramework.ps1'
if (-not (Test-Path $framework -PathType Leaf)) {
    throw "MystTiq test framework not found: $framework"
}
. $framework

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.77.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.77\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.77.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.77\.0"' `
    'Versioning' 'app.manifest reports v0.7.77.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.76.0-Logic.ps1' 'Regression' 'v0.7.76.0 logic gate remains available' -Severity High

$modText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.76.0-era ImportWorkshopItemAsync remains present, reused by this version''s Repair flow' `
    ($modText -match 'public async Task<HeadlessModMutationResult> ImportWorkshopItemAsync') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.77.0 Contracts -- Workshop runtime detection fix
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.77.0 Contracts' 'DescribeWorkshopItem takes a ue4ssInstalled parameter and uses it for runtime-kind items' `
    ($modText -match 'DescribeWorkshopItem\(string itemDir, HashSet<string> installedPackages, bool ue4ssInstalled\)' -and
     $modText -match 'var installed = hasRuntime\s*\r?\n\s*\? ue4ssInstalled') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.77.0 Contracts' 'both call sites pass the real ue4ssInstalled flag' `
    (([regex]::Matches($modText, 'DescribeWorkshopItem\(itemDir, (installedPackages|matchSet), ue4ssInstalled\)')).Count -eq 2) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.77.0 Contracts -- UE4SS hash-based version detection
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.77.0 Contracts' 'DetectUe4ssVersion tries the hash-match helper before falling back to unavailable' `
    ($modText -match 'TryDetectVersionFromLocalWorkshopHash\(\)' -and $modText -match 'private string\? TryDetectVersionFromLocalWorkshopHash\(\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.77.0 Contracts' 'the hash-match result is clearly labeled, not presented as unconditional fact' `
    ($modText -match 'matched via local Workshop item \{workshopId\}, SHA-256 identical') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.77.0 Contracts' 'ResolveActiveUe4ssDllPath fixes the real dual-DLL bug by preferring the modern layout explicitly' `
    ($modText -match 'private string\? ResolveActiveUe4ssDllPath\(\)' -and
     $modText -match 'Directory\.Exists\(paths\.Ue4ssRoot\) && Directory\.Exists\(paths\.Ue4ssModsRoot\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 5. v0.7.77.0 Contracts -- per-MOD Repair
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.77.0 Contracts' 'RepairModAsync exists, finds a Workshop match, and fails honestly when none is known' `
    ($modText -match 'public async Task<HeadlessModMutationResult> RepairModAsync\(string type, string package, CancellationToken cancellationToken\)' -and
     $modText -match 'No local Steam Workshop source is known for') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.77.0 Contracts' 'RepairModAsync deletes the existing install before re-importing (the real fix for InstallUe4ssFiles refusing to overwrite)' `
    ($modText -match 'var deleteResult = await DeleteAsync\(type, package, cancellationToken\)' -and
     $modText -match 'var importResult = await ImportWorkshopItemAsync\(match\.WorkshopId, cancellationToken\)') `
    -Severity Critical

$routesText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'v0.7.77.0 Contracts' 'repair route is mapped' `
    ($routesText -match 'MapPost\("/mods/\{type\}/\{package\}/repair"') `
    -Severity Critical

$vmText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'v0.7.77.0 Contracts' 'RepairSelectedModCommand is wired to the real API call' `
    ($vmText -match 'RepairSelectedModCommand = new AsyncCommand\(RepairSelectedModAsync' -and
     $vmText -match '_api\.RepairModAsync\(profile, selected\.Type, selected\.Package, BearerToken\)') `
    -Severity Critical

$mainWindowXaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.77.0 Contracts' 'Repair / Re-install Selected button exists on the MOD Library page' `
    ($mainWindowXaml -match 'Repair / Re-install Selected') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.77.0-mod-workshop-fixes-and-repair.md' `
    'v0.7.77.0 Contracts' 'v0.7.77.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 6. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.77.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.77\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.77.0.md' 'Documentation' 'v0.7.77.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.77.0 entry' `
    ($changelogText -match '## v0\.7\.77\.0') `
    -Severity High

# ---------------------------------------------------------------------------
# 7. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 8. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.76.0\MystTiqPalworldServer_v0.7.76.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.76.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.76.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.76.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.76.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.76.0 checkpoint logic gate still passes' `
        ($unexpectedFrozenFailures.Count -eq 0) `
        -Severity Critical `
        -Details ($(if ($unexpectedFrozenFailures.Count -gt 0) { "Unexpected failures: $($unexpectedFrozenFailures -join ', ')" } else { '' }))

    Test-MystTiqCommand $ctx 'Build' 'Strict validation passes' {
        & (Join-Path $root 'Build.ps1') Validate -StrictValidation
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'Release solution build passes' {
        & (Join-Path $root 'Build.ps1') Build -Configuration Release
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression Runtime' 'v0.5.1.5 isolated runtime smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.5.1.5-RuntimeSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.12.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.12.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.15.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.15.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.17.0 api-remote-enable smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.64.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.64.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'MystTiq.LogicHarness passes' {
        Push-Location (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness')
        try { & dotnet run -c Release }
        finally {
            Pop-Location
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
        }
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'MystTiq.Desktop builds clean (not part of PalworldServerManager.slnx)' {
        & dotnet build (Join-Path $root 'src\MystTiq.Desktop\MystTiq.Desktop.csproj') -c Release
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
