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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.10.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.10\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.10.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Core\Models\WhitelistModels.cs' 'v0.7.10.0 Contracts' 'WhitelistModels.cs exists' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\HeadlessWhitelistService.cs' 'v0.7.10.0 Contracts' 'HeadlessWhitelistService.cs exists' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Models\WhitelistDtos.cs' 'v0.7.10.0 Contracts' 'WhitelistDtos.cs exists' -Severity Critical

$whitelistServiceText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessWhitelistService.cs') -Raw
$apiHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
$profileHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\ServerProfileHost.cs') -Raw
$clientInterfaceText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\IMystTiqApiClient.cs') -Raw
$clientImplText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs') -Raw
$vmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$axamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.9.0 server FPS metrics are still present' `
    ([regex]::IsMatch((Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessMonitoringService.cs') -Raw), 'GetGamePerformanceAsync')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.10.0 contract presence -- server-side
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.10.0 Contracts' 'HeadlessWhitelistService.EnforceAsync kicks via PlayerModerationCoordinator, not a separate moderation path' `
    ([regex]::IsMatch($whitelistServiceText, 'playerModeration\.ExecuteAsync\("kick", id,')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.10.0 Contracts' 'EnforceAsync skips enforcement entirely when the config is disabled' `
    ([regex]::IsMatch($whitelistServiceText, 'if \(!snapshot\.Enabled\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.10.0 Contracts' 'ServerProfileHost declares a required Whitelist property and the constructor wires it with the PlayerModerationCoordinator' `
    ([regex]::IsMatch($profileHostText, 'public required HeadlessWhitelistService Whitelist') -and
     [regex]::IsMatch($apiHostText, 'new HeadlessWhitelistService\(paths, activity, playerModeration\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.10.0 Contracts' 'GET and PUT /players/whitelist routes exist with role gating' `
    ([regex]::IsMatch($apiHostText, '(?s)MapGet\("/players/whitelist".*?RequireRole\(MystTiqRole\.Operator') -and
     [regex]::IsMatch($apiHostText, '(?s)MapPut\("/players/whitelist".*?RequireRole\(MystTiqRole\.Admin')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.10.0 Contracts' '/status/poll calls Whitelist.EnforceAsync alongside the existing PlayerRegistry.Observe call' `
    ([regex]::IsMatch($apiHostText, '(?s)p\.PlayerRegistry\.Observe\(players, DateTimeOffset\.UtcNow\);\s*//.*?await p\.Whitelist\.EnforceAsync\(players, token\);')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.10.0 contract presence -- Desktop
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.10.0 Contracts' 'GetWhitelistAsync/SaveWhitelistAsync declared and implemented' `
    ($(
        $expected = @('GetWhitelistAsync', 'SaveWhitelistAsync')
        -not ($expected | Where-Object { $clientInterfaceText -notmatch $_ -or $clientImplText -notmatch $_ })
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.10.0 Contracts' 'MainWindowViewModel wires the full add/remove/save whitelist command set' `
    ($(
        $expected = @('RefreshWhitelistCommand', 'SaveWhitelistCommand', 'AddWhitelistEntryCommand', 'RemoveWhitelistEntryCommand', 'WhitelistEntries', 'SelectedWhitelistEntry')
        -not ($expected | Where-Object { $vmText -notmatch $_ })
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.10.0 Contracts' 'Players page has a collapsible Whitelist card following the v0.7.5.0/v0.7.8.0 sectionToggle pattern' `
    ([regex]::IsMatch($axamlText, 'Command="\{Binding ToggleWhitelistCommand\}"') -and
     [regex]::IsMatch($axamlText, 'Text="Whitelist" FontSize="17"') -and
     [regex]::IsMatch($axamlText, 'Command="\{Binding SaveWhitelistCommand\}"')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.10.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.10\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Existing v0.7.9.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.9.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.9.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 8. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.9.0\MystTiqPalworldServer_v0.7.9.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.9.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.9.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.9.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.9.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.9.0 checkpoint logic gate still passes' `
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
