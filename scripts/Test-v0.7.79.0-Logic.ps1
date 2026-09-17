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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.79.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.79\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.79.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.79\.0"' `
    'Versioning' 'app.manifest reports v0.7.79.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.78.0-Logic.ps1' 'Regression' 'v0.7.78.0 logic gate remains available' -Severity High

$modText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessModManagementService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.78.0-era UE4SS runtime verification remains present, untouched by this version' `
    ($modText -match 'internal sealed record Ue4ssRuntimeVerification\(DateTimeOffset VerifiedAtUtc\);') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.79.0 Contracts -- Server Doctor compact, color-coded checks
# ---------------------------------------------------------------------------
$dtoText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Models\DiagnosticFindingDtos.cs'
Add-MystTiqCheck $ctx 'v0.7.79.0 Contracts' 'DiagnosticFindingDto gained IsPass/IsWarning/IsFail/IsOtherState computed properties' `
    ($dtoText -match 'public bool IsPass => State == 0;' -and
     $dtoText -match 'public bool IsWarning => State == 1;' -and
     $dtoText -match 'public bool IsFail => State == 2;' -and
     $dtoText -match 'public bool IsOtherState => State is not \(0 or 1 or 2\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.79.0 Contracts' 'ShowRecommendation hides a Recommendation that duplicates Evidence' `
    ($dtoText -match 'public bool ShowRecommendation =>' -and
     $dtoText -match 'StringComparison\.Ordinal') `
    -Severity Critical

$mainWindowXaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.79.0 Contracts' 'Server Doctor finding row has 4 color-coded state badges (green/amber/red/neutral)' `
    ($mainWindowXaml -match 'Border Classes="badge" Background="\{DynamicResource GreenBrush\}" IsVisible="\{Binding IsPass\}"' -and
     $mainWindowXaml -match 'Border Classes="badge" Background="\{DynamicResource AmberBrush\}" IsVisible="\{Binding IsWarning\}"' -and
     $mainWindowXaml -match 'Border Classes="badge" Background="\{DynamicResource RedBrush\}" IsVisible="\{Binding IsFail\}"' -and
     $mainWindowXaml -match 'Border Classes="badge" IsVisible="\{Binding IsOtherState\}"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.79.0 Contracts' 'state badge labels use the new badgeText drop-shadow style' `
    (([regex]::Matches($mainWindowXaml, 'TextBlock Classes="badgeText" Text="\{Binding StateText\}"')).Count -eq 3) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.79.0 Contracts' 'Recommendation line is gated on ShowRecommendation' `
    ($mainWindowXaml -match 'Text="\{Binding Recommendation\}" TextWrapping="Wrap" Foreground="\{DynamicResource MutedBrush\}" FontSize="11" IsVisible="\{Binding ShowRecommendation\}"') `
    -Severity Critical

$designSystemText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Styles\DesignSystem.axaml'
Add-MystTiqCheck $ctx 'v0.7.79.0 Contracts' 'TextBlock.badgeText style exists with a DropShadowEffect' `
    ($designSystemText -match 'Selector="TextBlock\.badgeText">' -and
     $designSystemText -match 'DropShadowEffect') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.79.0-server-doctor-compact-checks.md' `
    'v0.7.79.0 Contracts' 'v0.7.79.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.79.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.79\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.79.0.md' 'Documentation' 'v0.7.79.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.79.0 entry' `
    ($changelogText -match '## v0\.7\.79\.0') `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.78.0\MystTiqPalworldServer_v0.7.78.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.78.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.78.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.78.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.78.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.78.0 checkpoint logic gate still passes' `
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
