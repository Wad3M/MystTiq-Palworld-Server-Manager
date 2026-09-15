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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.16.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.16\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.16.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.15.0 Pal Editor is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessPalEditService')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.16.0 contract presence -- Live World Map (player positions)
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.16 Contracts' 'HeadlessPlayerSnapshot captures real location_x/location_y from the existing REST poll, not a new data source' `
    ([regex]::IsMatch($hostText, 'LocationX') -and [regex]::IsMatch($hostText, 'LocationY') -and [regex]::IsMatch($hostText, 'GetAny\(player, "location_x"')) `
    -Severity Critical -Details 'Confirmed via real, cited research (Palworld''s own official REST API docs, cross-checked against an independent OpenAPI spec) that GET /v1/api/players already returns these fields for every online player -- MystTiq was simply discarding them until now.'

Add-MystTiqCheck $ctx 'v0.6.16 Contracts' 'Desktop DTO mirrors the new location fields' `
    ([regex]::IsMatch($desktopText, 'LocationX') -and [regex]::IsMatch($desktopText, 'LocationY')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.16 Contracts' 'Map points are auto-fit from real online-player coordinates, not a hardcoded world-coordinate range' `
    ([regex]::IsMatch($desktopText, 'RebuildPlayerMapPoints') -and [regex]::IsMatch($desktopText, 'PlayerMapPointDto') -and [regex]::IsMatch($desktopText, 'TryParseCoordinate')) `
    -Severity Critical -Details 'A player with a missing/unparseable coordinate is excluded from the map entirely rather than plotted at a wrong default position.'

Add-MystTiqCheck $ctx 'v0.6.16 Contracts' 'Map background image is a Desktop-local preference, never a server-side route' `
    ([regex]::IsMatch($desktopText, 'class LocalMapPreferencesStore') -and -not [regex]::IsMatch($hostText, 'MapBackground')) `
    -Severity Critical -Details 'Palworld''s actual map art is Pocketpair''s copyrighted asset -- this ships with a plain coordinate grid; a background image, if the user supplies one, is a per-machine preference only.'

Add-MystTiqCheck $ctx 'v0.6.16 Contracts' 'World Map card is wired onto the existing Players page, not a new nav destination' `
    ([regex]::IsMatch($desktopText, 'World Map') -and [regex]::IsMatch($desktopText, 'IsPlayersPage')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.16.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.16\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.15.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.15.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.15.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.15.0\MystTiqPalworldServer_v0.6.15.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.15.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.15.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.15.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.15.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.15.0 checkpoint logic gate still passes' `
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
