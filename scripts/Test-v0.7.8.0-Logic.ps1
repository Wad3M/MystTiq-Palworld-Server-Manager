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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.8.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.8\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.8.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$rconProviderText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\RconPlayerModerationProvider.cs') -Raw
$apiHostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
$clientInterfaceText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\IMystTiqApiClient.cs') -Raw
$clientImplText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs') -Raw
$vmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$axamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.7.0 backup-restore coordinator lock is still present' `
    ([regex]::IsMatch((Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\HeadlessBackupService.cs') -Raw), 'coordinator\.BeginAsync\(profile, "backup-restore"')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.8.0 contract presence -- Unban + Ban List
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' 'RconPlayerModerationProvider now supports unban and maps it to UnBanPlayer' `
    ([regex]::IsMatch($rconProviderText, 'or "unban"') -and [regex]::IsMatch($rconProviderText, '"unban" => \$"UnBanPlayer \{playerId\}"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' '/players/{playerId}/action routes unban through the moderation coordinator, not the REST-only admin provider' `
    ([regex]::IsMatch($apiHostText, 'normalizedAction is "kick" or "ban" or "unban"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' 'GET /players/ban-list route exists' `
    ([regex]::IsMatch($apiHostText, 'MapGet\("/players/ban-list"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' 'Unban button in MainWindow.axaml is no longer a BACKEND REQUIRED stub' `
    ([regex]::IsMatch($axamlText, 'Content="Unban" Command="\{Binding UnbanSelectedPlayerCommand\}"')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.8.0 contract presence -- Teleport
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' 'teleport-to-me and teleport-to-player routes exist' `
    ([regex]::IsMatch($apiHostText, 'MapPost\("/players/\{playerId\}/teleport-to-me"') -and [regex]::IsMatch($apiHostText, 'MapPost\("/players/\{playerId\}/teleport-to-player"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' 'teleport buttons are wired in MainWindow.axaml' `
    ([regex]::IsMatch($axamlText, 'Command="\{Binding TeleportPlayerToMeCommand\}"') -and [regex]::IsMatch($axamlText, 'Command="\{Binding TeleportToPlayerCommand\}"')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 5. v0.7.8.0 contract presence -- Save World Now
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' 'POST /world/save-now route exists' `
    ([regex]::IsMatch($apiHostText, 'MapPost\("/world/save-now"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' 'Dashboard SAVE button is wired' `
    ([regex]::IsMatch($axamlText, 'Content="SAVE" Padding="10,3" Command="\{Binding SaveWorldNowCommand\}"')) `
    -Severity High

# ---------------------------------------------------------------------------
# 6. v0.7.8.0 contract presence -- API client + unban online-requirement fix
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' 'all 4 new API client methods declared and implemented' `
    ($(
        $expected = @('GetBanListAsync', 'TeleportToMeAsync', 'TeleportToPlayerAsync', 'SaveWorldNowAsync')
        -not ($expected | Where-Object { $clientInterfaceText -notmatch $_ -or $clientImplText -notmatch $_ })
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' 'RunSelectedPlayerAdminActionAsync supports a non-online-requiring path for unban' `
    ([regex]::IsMatch($vmText, 'requireOnline: false') -and [regex]::IsMatch($vmText, 'bool requireOnline = true')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' 'UnbanSelectedPlayerCommand does not gate on SelectedPlayerRecord.Online' `
    ([regex]::IsMatch($vmText, 'UnbanSelectedPlayerCommand = new AsyncCommand\([^;]*?SelectedPlayerRecord is not null\)')) `
    -Severity High

# ---------------------------------------------------------------------------
# 7. v0.7.8.0 contract presence -- SelectedPlayerRecord tab-staleness correction
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' 'ActiveTab setter now also clears SelectedPlayerRecord (the property Kick/Ban/Unban/Teleport actually use)' `
    ([regex]::IsMatch($vmText, '(?s)SelectedOnlinePlayer = null;\s*SelectedPlayerRecord = null;')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' 'ActiveTab setter re-runs RefreshPlayersPageAsync immediately when already on the Players page' `
    ([regex]::IsMatch($vmText, 'if \(SelectedPage == NavigationPage\.Players\) _ = RefreshPlayersPageAsync\(\);')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.8.0 Contracts' 'RefreshPlayersPageAsync discards a response for a tab that is no longer active' `
    ([regex]::IsMatch($vmText, '(?s)private async Task RefreshPlayersPageAsync\(\).*?var requestTab = ActiveTab;.*?if \(!ReferenceEquals\(requestTab, ActiveTab\)\) return;')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 8. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.8.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.8\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 9. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 10. Existing v0.7.7.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.7.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.7.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 11. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.7.0\MystTiqPalworldServer_v0.7.7.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.7.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.7.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.7.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.7.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.7.0 checkpoint logic gate still passes' `
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
