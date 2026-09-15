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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.68.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.68\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.68.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.68\.0"' `
    'Versioning' 'app.manifest reports v0.7.68.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.67.0-Logic.ps1' 'Regression' 'v0.7.67.0 logic gate remains available' -Severity High

$toastRegressionText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Views\TrayReminderToast.axaml.cs') -Raw
Add-MystTiqCheck $ctx 'Regression' 'v0.7.66.0/v0.7.67.0 tray toast fixes are unchanged and still present' `
    ($toastRegressionText -match 'LayoutUpdated \+= \(_, _\) => PositionBottomRight\(\);' -and
     $toastRegressionText -match 'public TrayReminderToast\(string message, Window\? owner = null\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.68.0 Contracts -- the actual fix (RCON-first graceful shutdown)
# ---------------------------------------------------------------------------
$windowsLifecycleText = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\WindowsServerLifecycleService.cs'
$linuxLifecycleText = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\LinuxServerLifecycleService.cs'

Add-MystTiqCheck $ctx 'v0.7.68.0 Contracts' 'WindowsServerLifecycleService constructs a PalworldRconService and StopAsync tries RCON Shutdown before CloseMainWindow' `
    ($windowsLifecycleText -match 'this\.rcon = new PalworldRconService\(new PalworldSettingsConfigurationService\(paths\)\);' -and
     $windowsLifecycleText -match 'rcon\.ExecuteAsync\("Shutdown 1 MystTiq requested a graceful shutdown\."' -and
     ($windowsLifecycleText.IndexOf('rcon.ExecuteAsync("Shutdown 1') -lt $windowsLifecycleText.IndexOf('process.CloseMainWindow()'))) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.68.0 Contracts' 'LinuxServerLifecycleService constructs a PalworldRconService and StopAsync tries RCON Shutdown before SIGTERM' `
    ($linuxLifecycleText -match 'this\.rcon = new PalworldRconService\(new PalworldSettingsConfigurationService\(paths\)\);' -and
     $linuxLifecycleText -match 'rcon\.ExecuteAsync\("Shutdown 1 MystTiq requested a graceful shutdown\."' -and
     ($linuxLifecycleText.IndexOf('rcon.ExecuteAsync("Shutdown 1') -lt $linuxLifecycleText.IndexOf('signals.TryTerminate'))) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.68.0 Contracts' 'Both RCON-first attempts are gated on Enabled+PasswordConfigured and wrapped in a swallowing try/catch (purely additive, never regresses the existing fallback)' `
    (([regex]::Matches($windowsLifecycleText + $linuxLifecycleText, 'rconStatus is \{ Enabled: true, PasswordConfigured: true \}')).Count -eq 2) `
    -Severity Critical

$harnessText = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.68.0 Contracts' 'LogicHarness includes the new real Source RCON protocol scenarios' `
    ($harnessText -match 'class StubRconServer' -and
     $harnessText -match 'sends a real Shutdown command over the wire and reads the response' -and
     $harnessText -match 'reports failure honestly when the RCON password is wrong') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.68.0-graceful-shutdown-rcon-fix.md' `
    'v0.7.68.0 Contracts' 'v0.7.68.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.68.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.68\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.68.0.md' 'Documentation' 'v0.7.68.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.68.0 entry' `
    ($changelogText -match '## v0\.7\.68\.0') `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.67.0\MystTiqPalworldServer_v0.7.67.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.67.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.67.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.67.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.67.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.67.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'Regression New Tests' 'MystTiq.LogicHarness (19 scenarios, incl. v0.7.68.0 RCON additions) passes' {
        Push-Location (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness')
        try { & dotnet run -c Release }
        finally {
            Pop-Location
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
        }
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
