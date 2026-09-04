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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.7.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.7\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.7.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.6.0 Player Registry is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessPlayerRegistryService') -and [regex]::IsMatch($hostText, 'record PlayerRegistryRecord')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.5.0 Provider Framework is still present' `
    ([regex]::IsMatch($coreText, 'interface IPlayerModerationProvider') -and [regex]::IsMatch($coreText, 'class PlayerModerationCoordinator')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.4.0 unified diagnostics platform is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessDiagnosticsService') -and [regex]::IsMatch($coreText, 'record DiagnosticFinding')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.7.0 contract presence -- Guild Membership Repair (legacy repair tool integration)
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.7 Contracts' 'RemoveBrokenMember is a real operation on the existing, already-proven guild ownership transactional service (composition, not a new engine)' `
    ([regex]::IsMatch($hostText, 'RemoveBrokenMember') -and [regex]::IsMatch($hostText, 'enum GuildOwnershipOperationType \{ ClaimOrphanedGuild, TransferLeadership, AddPlayerToGuild, RemoveBrokenMember \}')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.7 Contracts' 'RemoveBrokenMember participates in the full Preview -> Safety Backup -> Transaction -> Encode -> Verify -> Commit pipeline, not a shortcut path' `
    ([regex]::IsMatch($hostText, 'case GuildOwnershipOperationType\.RemoveBrokenMember:\s*\r?\n\s*RemoveMember\(rawData, playerId\);') -and [regex]::IsMatch($hostText, 'case GuildOwnershipOperationType\.RemoveBrokenMember:\s*\r?\n\s*if \(ContainsMember\(rawData, playerId\)\)')) `
    -Severity Critical -Details 'Both ApplyMutation and VerifyMutation must have a RemoveBrokenMember case, or the operation would either not mutate anything or not be independently re-verified after encoding.'

Add-MystTiqCheck $ctx 'v0.6.7 Contracts' 'Remove Broken Member is reachable from the Desktop guild ownership UI' `
    ([regex]::IsMatch($desktopText, '"Remove Broken Member"') -and [regex]::IsMatch($desktopText, '"remove-broken-member"')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. v0.6.7.0 contract presence -- Player Identity Mismatch Detection (read-only foundation)
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.7 Contracts' 'Identity mismatch detection cross-references the v0.6.6.0 Player Registry against real guild-explorer save evidence' `
    ([regex]::IsMatch($hostText, 'BuildIdentityFindingsAsync') -and [regex]::IsMatch($hostText, 'Category: "Identity"') -and [regex]::IsMatch($hostText, '"Steam ID Collision"') -and [regex]::IsMatch($hostText, '"Missing Save File"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.7 Contracts' 'Identity findings are excluded from the Overall Health rollup (advisory, not a server-operational problem)' `
    ([regex]::IsMatch($hostText, 'healthRelevant\s*=\s*findings\.Where\(f\s*=>\s*f\.Category\s*!=\s*"Identity"\)')) `
    -Severity Critical -Details 'Verified live: without this exclusion, a routine "player just joined, save not written yet" condition would flip the Dashboard Overall Health badge to Degraded for a completely normal, transient state.'

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.7.0 is documented in the roadmap' `
    ([regex]::IsMatch($docText, 'v0\.6\.7\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Existing v0.6.6.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.6.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.6.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 8. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.6.0\MystTiqPalworldServer_v0.6.6.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.6.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.6.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.6.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.6.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.6.0 checkpoint logic gate still passes' `
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
