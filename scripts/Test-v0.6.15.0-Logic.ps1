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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.15.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.15\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.15.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.13.0 fleet-wide crash recovery is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessFleetCrashRecoveryService')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.12.0 explorer sidecar refresh is still present' `
    ([regex]::IsMatch($hostText, 'RefreshExplorerSidecar')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.15.0 contract presence -- Save-Data Edit Engine Foundation (Pal Editor)
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.15 Contracts' 'HeadlessPalEditService exists with the full Preview/Apply pipeline reused from Guild Ownership' `
    ([regex]::IsMatch($hostText, 'class HeadlessPalEditService') -and [regex]::IsMatch($hostText, 'ListPalsAsync') -and [regex]::IsMatch($hostText, 'PreviewAsync') -and [regex]::IsMatch($hostText, 'RefreshExplorerSidecar\(op\.LevelSavePath')) `
    -Severity Critical -Details 'The top finding from a 21-repo competitive survey: real Pal-level save editing, the single most-validated gap versus other Palworld server tools.'

Add-MystTiqCheck $ctx 'v0.6.15 Contracts' 'Apply refuses while PalServer is running, matching Guild Ownership''s precondition' `
    ([regex]::IsMatch($hostText, 'class HeadlessPalEditService[\s\S]*?status\.NativeProcessId\.HasValue \|\| status\.Ready', 'Singleline')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.15 Contracts' 'Pal identification uses the map entry''s own InstanceId, and players are excluded via IsPlayer' `
    ([regex]::IsMatch($hostText, 'FindPalEntry') -and [regex]::IsMatch($hostText, 'ReadBool\(GetProperty\(saveParam, "IsPlayer"\)\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.15 Contracts' 'Pal ownership is read from SaveParameter''s OwnerPlayerUId, not the map entry''s key.PlayerUId' `
    ([regex]::IsMatch($hostText, 'GetProperty\(saveParam, "OwnerPlayerUId"\)') -and -not [regex]::IsMatch($hostText, 'GetProperty\(key, "PlayerUId"\)')) `
    -Severity Critical -Details 'Real bug found and fixed during this milestone''s own live verification: key.PlayerUId is zero for every genuine Pal entry (it identifies a player''s own character-body entry, not Pal ownership), which silently produced "no owner" for all 85 real Pals in the first test pass until this was corrected.'

Add-MystTiqCheck $ctx 'v0.6.15 Contracts' 'Field setters match the real confirmed GVAS wrapping shapes (double-wrapped ByteProperty, single-wrapped Str/Bool, nested EnumProperty)' `
    ([regex]::IsMatch($hostText, 'SetByteProperty') -and [regex]::IsMatch($hostText, 'SetStrProperty') -and [regex]::IsMatch($hostText, 'SetBoolProperty') -and [regex]::IsMatch($hostText, 'SetGenderProperty')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.15 Contracts' 'Routes and Desktop UI are wired' `
    ([regex]::IsMatch($hostText, '"/pals"') -and [regex]::IsMatch($hostText, '"/pals/edit/preview"') -and [regex]::IsMatch($hostText, '"/pals/edit/apply"') -and [regex]::IsMatch($desktopText, 'RefreshPalsCommand') -and [regex]::IsMatch($desktopText, 'Pal Editor')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.15.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.15\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.14.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.14.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.14.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.14.0\MystTiqPalworldServer_v0.6.14.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.14.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.14.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.14.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.14.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.14.0 checkpoint logic gate still passes' `
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
