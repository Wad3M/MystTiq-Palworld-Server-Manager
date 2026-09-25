[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [switch]$RunBuild,
    # v0.8.1.0: where the previous checkpoint's FullSource ZIP is. Defaults to the release machine's backup
    # folder; when it is not there, the frozen-baseline check is reported as SKIP instead of crashing the gate.
    [string]$FrozenBaselineZip = '',
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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.8.2.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.8\.2\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.8.2.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.8\.2\.0"' `
    'Versioning' 'app.manifest reports v0.8.2.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.8.1.0-Logic.ps1' 'Regression' 'v0.8.1.0 logic gate remains available' -Severity High

$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'Regression' 'v0.8.0.0 artwork remains: themed page art, header art filling the card, 50px nav icons, v0.7.115.0 map label layer' `
    ($xaml -match 'Source="\{Binding PageArtwork\}"' -and $xaml -match 'x:Name="PageArtHeader"[^>]*Padding="0"' -and
     ([regex]::Matches($xaml, '\{services:IconArt [^}]+\}" Width="50" Height="50"')).Count -eq 26 -and
     $xaml -match 'Margin="\{Binding LabelPosition\}"') `
    -Severity Critical

$icons = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\RibbonIcons.cs'
$package = Get-Content (Join-Path $root 'docs\ribbon-icons\icons.json') -Raw | ConvertFrom-Json
$mismatched = @($package | Where-Object { $icons -notmatch ('\["' + [regex]::Escape($_.key) + '"\] = "' + [regex]::Escape($_.path) + '"') } | ForEach-Object { $_.key })
Add-MystTiqCheck $ctx 'Regression' 'v0.8.1.0 Ribbon icons remain: all 10 use the supplied path data and the Ribbon draws them' `
    (@($package).Count -eq 10 -and $mismatched.Count -eq 0 -and
     $xaml -match '<Path Classes="ribbonIcon" Data="\{Binding VectorIcon\}" IsVisible="\{Binding HasVectorIcon\}"') `
    -Severity Critical -Details "mismatched: $($mismatched -join ', ')"

# ---------------------------------------------------------------------------
# 3. v0.8.2.0 Contracts -- service mode (service-run) fixes
# ---------------------------------------------------------------------------
$program = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\Program.cs'
$apiHost = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
$supervisor = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\HeadlessSupervisor.cs'
$fleetRecovery = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessFleetCrashRecoveryService.cs'
$crashAlerts = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\CrashAlerts.cs'

Add-MystTiqCheck $ctx 'v0.8.2.0 Contracts' 'service-run builds its lifecycle with the per-profile factory (configured game port), on Windows and Linux' `
    ($program -match 'var lifecycle = WindowsServiceRunLifecycleFactory\(effectiveDefaultServerProfile, paths\);' -and
     $program -match 'var lifecycle = ServiceRunLifecycleFactory\(effectiveDefaultServerProfile, paths\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.8.2.0 Contracts' 'the embedded API host hands the service profile''s crash recovery to the service supervisor instead of running a second one' `
    ($apiHost -match 'public \(ISupervisorObserver Observer, SupervisorRecoveryStateStore State\) TakeOverCrashRecovery\(string profileId\)' -and
     $apiHost -match 'if \(!externallySupervised\.Contains\(p\.Id\)\) await p\.CrashRecovery\.StartAsync' -and
     ([regex]::Matches($program, 'apiHost\?\.TakeOverCrashRecovery\(effectiveDefaultServerProfile\.Id\)')).Count -eq 2) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.8.2.0 Contracts' 'the supervisor records its own give-up and, on a service start after one, announces the server back up once it is ready' `
    ($supervisor -match 'stateStore\?\.Update\(s => s with \{ GaveUpAtUtc = DateTimeOffset\.UtcNow \}\);' -and
     $supervisor -match 'stateStore\?\.Read\(\)\.GaveUpAtUtc is not null && \(ready \|\| await WaitUntilReadyAsync' -and
     $supervisor -match 'SupervisorEventKind\.ManualRecovery, 0, "PalServer is running again after the MystTiq service restarted\."' -and
     $fleetRecovery -notmatch 'GaveUpAtUtc = DateTimeOffset\.UtcNow') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.8.2.0 Contracts' 'a new DOWN notice unpins the previous one, so repeated give-ups never leave several pinned' `
    ($crashAlerts -match 'if \(PinnedGiveUpNotificationId is \{ \} previous\)' -and $crashAlerts -match 'notifications\.SetPinned\(previous, false\);') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.8.2.0 Contracts' 'the logic harness covers the service-mode give-up/recovery cycle and the replaced DOWN notice' `
    ($harness -match 'Service mode: the supervisor records its own give-up' -and $harness -match 'Service mode: a second give-up replaces the pinned DOWN notice') `
    -Severity Critical

$smoke = Get-MystTiqText $ctx 'scripts\Test-v0.8.2.0-RouteSmoke.ps1'
Add-MystTiqCheck $ctx 'v0.8.2.0 Contracts' 'the service-mode smoke uses its own FleetRoot, a non-8211 game port and the stand-in PalServer' `
    ($smoke -match '\$cfg\.FleetRoot = \$fleetRoot' -and $smoke -match '\[int\]\$GamePort = 18482' -and $smoke -match 'scripts\\Testing\\FakePalServer') `
    -Severity Critical
Test-MystTiqFile $ctx 'scripts\Testing\FakePalServer\FakePalServer.csproj' 'v0.8.2.0 Contracts' 'the stand-in PalServer used by the service-mode smoke is kept with the source' -Severity High
Test-MystTiqFile $ctx 'docs\architecture\v0.8.2.0-service-mode-fixes.md' 'v0.8.2.0 Contracts' 'v0.8.2.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.8.2.0 is documented' ([regex]::IsMatch($docText, 'v0\.8\.2\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.8.2.0.md' 'Documentation' 'v0.8.2.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.8.2.0 entry' ($changelogText -match '## v0\.8\.2\.0') -Severity High
$readmeText = Get-MystTiqText $ctx 'README.md'
Add-MystTiqCheck $ctx 'Documentation' 'README records the accepted baseline v0.8.1.0' ($readmeText -match 'Accepted baseline:\*\* v0\.8\.1\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = if ($FrozenBaselineZip) { $FrozenBaselineZip } else { Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.8.1.0\MystTiqPalworldServer_v0.8.1.0_FullSource.zip' }
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        # v0.8.1.0: reported, not fatal: the rest of the gate is still worth running on a machine without it.
        Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.8.1.0 checkpoint logic gate still passes' $false -Skipped -Severity Critical `
            -Details "Baseline archive not found at $frozenZip. Run on the release machine, or pass -FrozenBaselineZip <path>."
    }
    else {
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.8.1.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.8.1.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.8.1.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.8.1.0 checkpoint logic gate still passes' `
        ($unexpectedFrozenFailures.Count -eq 0) `
        -Severity Critical `
        -Details ($(if ($unexpectedFrozenFailures.Count -gt 0) { "Unexpected failures: $($unexpectedFrozenFailures -join ', ')" } else { '' }))
    }

    # v0.8.1.0: -AllowBuildOutputs, because this gate has already built and published by now; repository
    # hygiene (no bin/obj/artifacts) is checked by the release pipeline after Clean instead. This check used to
    # fail in every gate for exactly that reason.
    Test-MystTiqCommand $ctx 'Build' 'Strict validation passes' {
        & (Join-Path $root 'scripts\Validate-Release.ps1') -Strict -AllowBuildOutputs
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

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.112.0 give item route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.112.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.113.0 teleport points route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.113.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.114.0 multi-user login route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.114.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.115.0 persistence and readiness route smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.115.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    # v0.8.2.0: service mode end to end on the published exe (custom game port, one supervisor, give-up, recovery on restart).
    Test-MystTiqCommand $ctx 'v0.8.2.0 Runtime' 'v0.8.2.0 service mode route smoke passes' {
        & (Join-Path $root 'scripts\Test-v0.8.2.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    # v0.8.1.0: the artwork is Desktop-only, so its runtime proof is the offline headless rendering harness
    # (every page's day/night art, visible bindings, header/ribbon/category layout at 950x650, all 26 icons).
    Test-MystTiqCommand $ctx 'Regression Runtime' 'MystTiq.ArtworkHarness passes (offline headless rendering, incl. the Ribbon icons)' {
        $renderOut = Join-Path ([System.IO.Path]::GetTempPath()) ('mysttiq-artwork-render-' + [guid]::NewGuid().ToString('N'))
        Push-Location (Join-Path $root 'scripts\Testing\MystTiq.ArtworkHarness')
        try { & dotnet run -c Release -- $renderOut; if ($LASTEXITCODE -ne 0) { throw "artwork harness exited $LASTEXITCODE" } }
        finally {
            Pop-Location
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.ArtworkHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.ArtworkHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item $renderOut -Recurse -Force -ErrorAction SilentlyContinue
        }
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
