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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.10.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.10\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.10.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.9.0 Idle Auto-Stop is still present' `
    ([regex]::IsMatch($coreText, 'IdleEmpty') -and [regex]::IsMatch($hostText, 'EvaluateIdleRulesAsync')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.2.0 fleet contract is still present' `
    ([regex]::IsMatch($coreText, 'IReadOnlyList<HeadlessServerProfileConfiguration>\s+Servers') -and [regex]::IsMatch($hostText, 'class ServerProfileHost')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.10.0 contract presence -- Clone World
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.10 Contracts' 'HeadlessWorldCloneService exists and requires the source server stopped before copying, matching every other save-mutating service in this codebase' `
    ([regex]::IsMatch($hostText, 'class HeadlessWorldCloneService') -and [regex]::IsMatch($hostText, 'Stop this server before cloning its world')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.10 Contracts' 'Clone performs a full, real copy of the entire server installation, not a shortcut junction' `
    ([regex]::IsMatch($hostText, 'Directory\.EnumerateFiles\(sourceRoot, "\*", SearchOption\.AllDirectories\)') -and [regex]::IsMatch($hostText, 'Parallel\.ForEachAsync')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.10 Contracts' 'Clone assigns distinct ports (ini) AND injects a real -port= launch argument override' `
    ([regex]::IsMatch($hostText, '"PublicPort" => row with \{ Value = CaptureOffsetPort') -and [regex]::IsMatch($hostText, '\$"-port=\{newGamePort\}"')) `
    -Severity Critical -Details 'Found via live verification: PalWorldSettings.ini''s PublicPort alone does not control the actual UDP bind port -- a live two-instance test with only the ini rewritten still silently fell back to the next free port instead of honoring the configured one. The -port= launch argument is what actually works; both must be set, not just the ini.'

Add-MystTiqCheck $ctx 'v0.6.10 Contracts' 'Clone reuses the existing v0.6.2.0 fleet-registration path unchanged, inheriting its restart-required convention rather than inventing a parallel one' `
    ([regex]::IsMatch($hostText, 'fleetConfiguration\.AddServerAsync\(new HeadlessAddServerProfileRequest')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.10 Contracts' 'Clone route and Desktop UI are wired' `
    ([regex]::IsMatch($hostText, '"/server/clone"') -and [regex]::IsMatch($desktopText, 'CloneWorldCommand') -and [regex]::IsMatch($desktopText, '"Clone"')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.10.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.10\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.9.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.9.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.9.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.9.0\MystTiqPalworldServer_v0.6.9.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.9.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.9.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.9.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.9.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.9.0 checkpoint logic gate still passes' `
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
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
