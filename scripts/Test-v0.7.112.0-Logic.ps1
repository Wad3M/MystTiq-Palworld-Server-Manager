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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.112.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.112\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.112.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.112\.0"' `
    'Versioning' 'app.manifest reports v0.7.112.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.111.0-Logic.ps1' 'Regression' 'v0.7.111.0 logic gate remains available' -Severity High

$alertCenter = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessAlertCenterService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.111.0 fixes remain: alert mute and crash alert settings' `
    ($alertCenter -match 'public DateTimeOffset\? MutedUntilUtc \{ get; init; \}' -and
     $alertCenter -match 'if \(AlertMutePolicy\.IsMuted\(set, now\)\) return;') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.112.0 Contracts -- Give Item
# ---------------------------------------------------------------------------
$kits = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessKitService.cs'
$host_ = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
$vm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'

Add-MystTiqCheck $ctx 'v0.7.112.0 Contracts' 'Give Item goes through the same validated provider path as kits and records no kit claim' `
    ($kits -match 'public async Task<KitGiveResult> GiveEntriesAsync\(IReadOnlyList<KitEntry> entries, string playerId, HeadlessPlayersSnapshot players, CancellationToken cancellationToken\)' -and
     $kits -match 'var errors = Validate\(new KitConfig\(false, string\.Empty, null, \[adHoc\]\)\);' -and
     $kits -match 'private async Task<KitGiveResult> DeliverAsync\(string label, KitDefinition kit, HeadlessPlayerSnapshot player, CancellationToken cancellationToken\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.112.0 Contracts' 'the give route is Admin-only and logs refusals to Activity' `
    ($host_ -match 'routes\.MapPost\("/players/\{playerId\}/give"' -and
     $host_ -match 'p\.Activity\.Record\("Warning", "Players", "Give Item failed", result\.Message\);' -and
     $host_ -match 'public sealed record GiveItemsRequest\(IReadOnlyList<KitEntry>\? Entries\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.112.0 Contracts' 'the Players page Give Item is no longer hardcoded off: enabled for an online player with something typed' `
    ($vm -match 'GiveItemSelectedPlayerCommand = new AsyncCommand\(GiveItemsToSelectedPlayerAsync, \(\) => !IsBusy && SelectedPlayerRecord\?\.Online == true && !string\.IsNullOrWhiteSpace\(GiveItemsText\)\);' -and
     $xaml -notmatch 'Give Item — BACKEND REQUIRED' -and
     $xaml -match 'Text="\{Binding GiveItemsText\}"') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.112.0 Contracts' 'the logic harness covers commands sent, no claim, and every refusal sending nothing' `
    ($harness -match 'RunScenario\("Give Item sends PalDefender give commands' -and
     $harness -match 'none of the refused gives sent anything') `
    -Severity Critical

Test-MystTiqFile $ctx 'scripts\Test-v0.7.112.0-RouteSmoke.ps1' 'v0.7.112.0 Contracts' 'the give item route smoke exists' -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.7.112.0-give-item.md' 'v0.7.112.0 Contracts' 'v0.7.112.0 architecture doc is present' -Severity Critical
# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.112.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.112\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.112.0.md' 'Documentation' 'v0.7.112.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.112.0 entry' ($changelogText -match '## v0\.7\.112\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.111.0\MystTiqPalworldServer_v0.7.111.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.111.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.111.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.111.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.111.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.111.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.81.0 new-server wizard route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.81.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.97.0 crash analyzer route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.97.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.98.0 doctor route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.98.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.100.0 player locations route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.100.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.101.0 crash alerts route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.101.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.102.0 alert episodes route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.102.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.103.0 backup schedule route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.103.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.104.0 alert unpin route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.104.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.107.0 alert reminder route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.107.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.108.0 configured reminder route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.108.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.110.0 crash-recovery give-up/manual-recovery route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.110.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.111.0 alert mute route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.111.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'v0.7.112.0 Runtime' 'v0.7.112.0 give item route smoke passes' {
        & (Join-Path $root 'scripts\Test-v0.7.112.0-RouteSmoke.ps1') -ProjectRoot $root
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
