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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.97.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.97\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.97.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.97\.0"' `
    'Versioning' 'app.manifest reports v0.7.97.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.96.0-Logic.ps1' 'Regression' 'v0.7.96.0 logic gate remains available' -Severity High

$vm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.96.0-era live map (calibrated, on by default) remains present, untouched by this version' `
    ($vm -match 'private bool _useCalibratedWorldPositions = true;' -and $vm -match 'private void ApplyBaseLocations\(PlayerGuildSnapshotDto snapshot\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.97.0 Contracts -- Crash Analyzer: known signatures, causes, fixes, new vs repeated
# ---------------------------------------------------------------------------
$catalog = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\CrashSignatureCatalog.cs'
Add-MystTiqCheck $ctx 'v0.7.97.0 Contracts' 'the signature catalog covers the known failure causes, each claimed most-specific-first' `
    (($catalog -match '"missing-runtime-dll"') -and ($catalog -match '"out-of-memory"') -and ($catalog -match '"stack-overflow"') -and ($catalog -match '"access-violation"') -and
     ($catalog -match '"disk-full"') -and ($catalog -match '"port-in-use"') -and ($catalog -match '"save-io"') -and ($catalog -match '"shutdown-failure"') -and
     ($catalog -match '"hang"') -and ($catalog -match '"ue4ss-error"') -and ($catalog -match '"steam-init"') -and ($catalog -match '"ue-fatal"') -and
     ($catalog.IndexOf('"ue-fatal"') -gt $catalog.IndexOf('"access-violation"'))) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.97.0 Contracts' 'matching is word-anchored: no bare substring needles that hit ordinary words, and every pattern has a timeout' `
    ($catalog -match [regex]::Escape('R(@"\bOOM\b")') -and $catalog -notmatch 'R\(@"oom"\)' -and $catalog -match 'MatchTimeout') `
    -Severity Critical

$svc = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessCrashAndSaveToolsService.cs'
Add-MystTiqCheck $ctx 'v0.7.97.0 Contracts' 'the analyzer uses the catalog through CrashAnalysisBuilder, not the old keyword needles' `
    ($svc -match 'CrashAnalysisBuilder\.Build\(evidence\.Lines, installedModNames, previousKeys\)' -and $svc -notmatch '"oom"' -and $svc -notmatch 'AddFinding\(') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.97.0 Contracts' 'finding fields are optional so a report saved before v0.7.97.0 still loads' `
    ($svc -match 'string SignatureId = ""' -and $svc -match 'bool IsNew = true, string Key = ""' -and $svc -match 'int NewFindings = 0, int RepeatedFindings = 0') `
    -Severity Critical

$api = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'v0.7.97.0 Contracts' 'the analyze route passes installed mod names in and never fails because the inventory could not be read' `
    ($api -match 'p\.CrashAndSaveTools\.Analyze\(modNames\)' -and $api -match 'p\.ModManagement\.GetInventoryAsync\(token\)' -and $api -match 'catch \(Exception ex\) when \(ex is not OperationCanceledException\) \{ \}') `
    -Severity Critical

$dto = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\CrashAndSaveToolsDtos.cs'
$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.97.0 Contracts' 'the Crash Analyzer page shows the selected finding''s cause, fixes, mods and evidence' `
    ($dto -match 'public List<string> Fixes' -and $dto -match 'public bool IsNew' -and $vm -match 'public CrashFindingDto\? SelectedCrashFinding' -and
     $xaml -match 'SelectedItem="\{Binding SelectedCrashFinding, Mode=TwoWay\}"' -and $xaml -match 'x:DataType="models:CrashFindingDto"' -and
     $xaml -match 'Text="\{Binding FixesText\}"' -and $xaml -match 'Text="\{Binding ModsText\}"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.97.0 Contracts' 'another server''s crash findings are cleared on a tab switch and reloaded if the Crash Analyzer page is open' `
    ($vm -match 'CrashFindings\.Clear\(\);\s*CrashIsolationPlan\.Clear\(\);\s*CrashHistory\.Clear\(\);\s*SelectedCrashFinding = null;' -and
     $vm -match 'if \(SelectedPage == NavigationPage\.CrashAnalyzer\) _ = RefreshCrashHistoryAsync\(\);') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.97.0 Contracts' 'the logic harness covers signatures, exit codes, timestamps, mod naming, new-versus-repeated and old-report loading' `
    ((([regex]::Matches($harness, 'RunScenario\("(Crash signatures|Crash analysis|A crash report)')).Count) -ge 7) `
    -Severity Critical

Test-MystTiqFile $ctx 'scripts\Test-v0.7.97.0-RouteSmoke.ps1' 'v0.7.97.0 Contracts' 'the crash analyzer route smoke exists' -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.7.97.0-crash-analyzer-known-causes.md' 'v0.7.97.0 Contracts' 'v0.7.97.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.97.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.97\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.97.0.md' 'Documentation' 'v0.7.97.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.97.0 entry' ($changelogText -match '## v0\.7\.97\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.96.0\MystTiqPalworldServer_v0.7.96.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.96.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.96.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.96.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.96.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.96.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.81.0 new-server wizard route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.81.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'v0.7.97.0 Runtime' 'v0.7.97.0 crash analyzer route smoke passes' {
        & (Join-Path $root 'scripts\Test-v0.7.97.0-RouteSmoke.ps1') -ProjectRoot $root
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
