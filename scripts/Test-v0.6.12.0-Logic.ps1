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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.12.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.12\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.12.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.11.0 WAN reachability diagnostics are still present' `
    ([regex]::IsMatch($coreText, 'class WanReachabilityService') -and [regex]::IsMatch($hostText, '"/diagnostics/network/wan"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.11.0 stale-instance detection is still present' `
    ([regex]::IsMatch($desktopText, 'VersionMatches') -and [regex]::IsMatch($desktopText, 'StaleInstanceDetected')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.12.0 contract presence -- explorer sidecar staleness fix
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.12 Contracts' 'HeadlessSaveCodecService can refresh the explorer sidecar from an already-verified decode' `
    ([regex]::IsMatch($hostText, 'RefreshExplorerSidecar')) `
    -Severity Critical -Details 'Closes a real gap first disclosed in the v0.5.2.0 checkpoint and reconfirmed unfixed through v0.6.7.0: the Players/Guilds/World Explorer read views relied on a static Level.sav.json sidecar nothing ever regenerated after a mutation.'

$sidecarCallSites = [regex]::Matches($hostText, 'RefreshExplorerSidecar\(op\.LevelSavePath').Count
Add-MystTiqCheck $ctx 'v0.6.12 Contracts' 'Sidecar refresh is wired into all four mutation commit sites (Guild Ownership, Base Ownership x2, Character Migration)' `
    ($sidecarCallSites -ge 4) `
    -Severity Critical -Details "Found $sidecarCallSites call site(s); expected at least 4."

# ---------------------------------------------------------------------------
# 4. v0.6.12.0 contract presence -- config-write-default override fix
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.12 Contracts' 'HeadlessConfigurationService.WriteDefault accepts a pre-built configuration' `
    ([regex]::IsMatch($coreText, 'WriteDefault\(string\? path = null, bool overwrite = false, HeadlessConfiguration\? configuration = null\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.12 Contracts' 'config-write-default applies --server-root/--steamcmd/--backup-root/--runtime-root before writing' `
    ([regex]::IsMatch($hostText, 'config-write-default.*?WriteDefault\(configurationPath, overwrite, effectiveDefault\)', 'Singleline')) `
    -Severity Critical -Details 'Previously config-write-default returned before the CLI-override logic every other command uses ever ran, so the written file always contained the hardcoded built-in default path regardless of what was passed on the command line -- a real bug found by this session''s own bug-testing methodology.'

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.12.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.12\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Existing v0.6.11.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.11.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.11.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 8. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.11.0\MystTiqPalworldServer_v0.6.11.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.11.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.11.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.11.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.11.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.11.0 checkpoint logic gate still passes' `
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
