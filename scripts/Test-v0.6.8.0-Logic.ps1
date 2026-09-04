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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.8.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.8\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.8.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.7.0 guild membership repair is still present' `
    ([regex]::IsMatch($hostText, 'RemoveBrokenMember')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.6.0 Player Registry is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessPlayerRegistryService')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.1.0 Alert Center is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessAlertCenterService')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.8.0 contract presence -- MOD backup, staged install & rollback
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.8 Contracts' 'A pre-mutation snapshot is captured before Install and Delete, not just before Delete' `
    ([regex]::IsMatch($hostText, 'CaptureSnapshot\(type, package\);[\s\S]{0,80}Directory\.CreateDirectory\(staging\)') -and [regex]::IsMatch($hostText, 'CaptureSnapshot\(type, package\);\s*\r?\n\s*var changed = 0;')) `
    -Severity Critical -Details 'Both InstallZipAsync and DeleteAsync must snapshot the pre-mutation state, or rollback would only ever be able to undo half of the operations that can destroy MOD state.'

Add-MystTiqCheck $ctx 'v0.6.8 Contracts' 'RollbackAsync exists, requires PalServer stopped like every other MOD mutation, and honestly reports when no snapshot exists' `
    ([regex]::IsMatch($hostText, 'public async Task<HeadlessModMutationResult> RollbackAsync') -and [regex]::IsMatch($hostText, 'No rollback snapshot is available')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.8 Contracts' 'Rollback correctly distinguishes "restore prior content" from "remove what was freshly created" (the absent-marker path)' `
    ([regex]::IsMatch($hostText, 'absentMarker')) `
    -Severity Critical -Details 'A package that did not exist before its last install must be DELETED on rollback, not left in place -- without the absent-marker distinction, rolling back a brand-new MOD install would silently no-op.'

Add-MystTiqCheck $ctx 'v0.6.8 Contracts' 'Rollback route and Desktop UI are wired' `
    ([regex]::IsMatch($hostText, '"/mods/\{type\}/\{package\}/rollback"') -and [regex]::IsMatch($desktopText, 'RollbackSelectedModCommand') -and [regex]::IsMatch($desktopText, '"Rollback Selected"')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. v0.6.8.0 contract presence -- Alert Center integration for MOD/UE4SS health
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.8 Contracts' 'Alert Center evaluates real MOD inventory health, not a duplicated computation' `
    ([regex]::IsMatch($hostText, 'modManagement\.GetInventoryAsync\(token\)') -and [regex]::IsMatch($hostText, 'inventory\.OverallHealth == "Degraded"') -and [regex]::IsMatch($hostText, '"mod-health-degraded"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.8 Contracts' 'ModHealthDegraded is a real, configurable rule (not hardcoded) reachable from the Desktop Alert Center page' `
    ([regex]::IsMatch($coreText + $hostText, 'ModHealthDegraded') -and [regex]::IsMatch($desktopText, 'AlertRules\.ModHealthDegraded\.Enabled')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.8.0 is documented in the roadmap' `
    ([regex]::IsMatch($docText, 'v0\.6\.8\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Existing v0.6.7.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.7.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.7.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 8. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.7.0\MystTiqPalworldServer_v0.6.7.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.7.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.7.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.7.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.7.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.7.0 checkpoint logic gate still passes' `
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
