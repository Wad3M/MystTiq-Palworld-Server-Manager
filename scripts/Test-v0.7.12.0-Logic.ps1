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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.12.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.12\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.12.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Views\ConfirmCloseTabDialog.axaml' 'Regression' 'v0.7.11.0 tab-close dialog is still present' -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.12.0 contract presence -- new test infrastructure files exist
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Testing\MystTiq.LogicHarness\MystTiq.LogicHarness.csproj' 'v0.7.12.0 Contracts' 'whitelist enforcement harness project exists' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs' 'v0.7.12.0 Contracts' 'whitelist enforcement harness Program.cs exists' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'v0.7.12.0 Contracts' 'route smoke script exists' -Severity Critical

$harnessText = Get-Content (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\Program.cs') -Raw
$routeSmokeText = Get-Content (Join-Path $root 'scripts\Test-v0.7.12.0-RouteSmoke.ps1') -Raw
$providerModelsText = Get-Content (Join-Path $root 'src\MystTiq.Core\Providers\ProviderModels.cs') -Raw

Add-MystTiqCheck $ctx 'v0.7.12.0 Contracts' 'the harness is deliberately NOT part of the main solution file' `
    (-not ((Get-Content (Join-Path $root 'PalworldServerManager.slnx') -Raw) -match 'MystTiq\.LogicHarness')) `
    -Severity High

Add-MystTiqCheck $ctx 'v0.7.12.0 Contracts' 'no leftover bin/obj from the harness project would trip Validate-Release.ps1''s strict hygiene check' `
    ((-not (Test-Path (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\bin'))) -and (-not (Test-Path (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\obj')))) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.12.0 Contracts' 'Validate-Release.ps1 recognizes the new RouteSmoke script category, avoiding a stale-version false positive on its own filename' `
    ((Get-Content (Join-Path $root 'scripts\Validate-Release.ps1') -Raw) -match [regex]::Escape("scripts\Test-v*-RouteSmoke.ps1")) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.12.0 Contracts' 'the harness covers all 6 documented whitelist enforcement scenarios' `
    ($(
        $expected = @(
            'disabled config takes no action',
            'enabled config kicks a non-whitelisted online player, leaves an allowed one alone',
            'the same still-online non-whitelisted player is not re-kicked on a second poll',
            'a player is re-kicked after leaving and rejoining',
            'saving a new config resets the dedup set',
            'an unavailable players snapshot is ignored entirely'
        )
        -not ($expected | Where-Object { $harnessText -notmatch [regex]::Escape($_) })
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.12.0 Contracts' 'the route smoke script covers ban-list, save-now, both teleport routes, unban routing, and the whitelist round-trip' `
    ($(
        $expected = @('/players/ban-list', '/world/save-now', '/teleport-to-me', '/teleport-to-player', "action = 'unban'", '/players/whitelist')
        -not ($expected | Where-Object { $routeSmokeText -notmatch [regex]::Escape($_) })
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.12.0 Contracts' 'the route smoke script reads failure bodies via ErrorDetails.Message, not the PowerShell 5.1-era GetResponseStream API' `
    ([regex]::IsMatch($routeSmokeText, '\$_\.ErrorDetails\.Message') -and -not [regex]::IsMatch($routeSmokeText, 'GetResponseStream')) `
    -Severity High

Add-MystTiqCheck $ctx 'v0.7.12.0 Contracts' 'the stale whitelist/teleport-deferred comment in ProviderModels.cs is fixed' `
    ([regex]::IsMatch($providerModelsText, 'whitelist \(v0\.7\.10\.0.*and teleport \(v0\.7\.8\.0')) `
    -Severity Medium

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.12.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.12\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.11.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.11.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.11.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.11.0\MystTiqPalworldServer_v0.7.11.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.11.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.11.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.11.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.11.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.11.0 checkpoint logic gate still passes' `
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

    # v0.7.12.0: the two new test tools this release adds. Both need Build.ps1 DesktopWindows's
    # sidecar output (the same known quirk every version's -RunBuild already works around for
    # Test-v0.5.1.5-RuntimeSmoke.ps1) -- if this is run right after the plain Build above without
    # the DesktopWindows workaround, the route-smoke check below will fail with the same
    # "sidecar not found" message and needs the identical manual workaround.
    #
    # The harness's own bin/obj MUST be cleaned immediately after every run: Validate-Release.ps1's
    # strict-hygiene check flags a bin/obj folder anywhere in the repo as an Error, and `dotnet run`
    # recreates them every time. Found the hard way -- the first run of this exact block left them
    # behind and caused a false "9 errors" strict-validation failure directly after.
    Test-MystTiqCommand $ctx 'v0.7.12.0 New Tests' 'Whitelist enforcement harness (6 scenarios) passes' {
        Push-Location (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness')
        try { & dotnet run -c Release }
        finally {
            Pop-Location
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
        }
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'v0.7.12.0 New Tests' 'Route smoke gate (v0.7.8.0-v0.7.10.0 routes) passes' {
        & (Join-Path $root 'scripts\Test-v0.7.12.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
