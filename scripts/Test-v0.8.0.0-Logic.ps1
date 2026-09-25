[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [switch]$RunBuild,
    # v0.8.0.0: where the previous checkpoint's FullSource ZIP is. Defaults to the release machine's backup
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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.8.0.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.8\.0\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.8.0.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.8\.0\.0"' `
    'Versioning' 'app.manifest reports v0.8.0.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.115.0-Logic.ps1' 'Regression' 'v0.7.115.0 logic gate remains available' -Severity High

$layout = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\MapLabelLayout.cs'
$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.115.0 fixes remain after the artwork merge: map labels as rectangles in their own layer, readiness, in-gate validation' `
    ($layout -match 'placed\.Any\(box\.Overlaps\)' -and $xaml -match 'Margin="\{Binding LabelPosition\}"' -and
     (Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessFleetCrashRecoveryService.cs') -match 'if \(status\.Processes\.Count > 0 && status\.Ready\) return true;' -and
     (Get-MystTiqText $ctx 'scripts\Validate-Release.ps1') -match '\[switch\]\$AllowBuildOutputs') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.8.0.0 Contracts -- artwork refresh (the user's v0.7.110.1 artwork, merged)
# ---------------------------------------------------------------------------
$catalog = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\ArtworkCatalog.cs'
$vm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
$assets = Join-Path $root 'src\MystTiq.Desktop\Assets'

Add-MystTiqCheck $ctx 'v0.8.0.0 Contracts' 'every navigation page maps to an artwork category, and artwork is decoded once at a bounded size' `
    ($catalog -match 'public static Bitmap Page\(NavigationPage page, bool light\)' -and $catalog -match 'Bitmap\.DecodeToWidth\(stream, width\)' -and
     $catalog -match 'Assign new pages an artwork category') `
    -Severity Critical

$artCount = @(Get-ChildItem (Join-Path $assets 'Artwork') -Filter 'page-art-*.png' -ErrorAction SilentlyContinue).Count
$iconCount = @(Get-ChildItem (Join-Path $assets 'Icons') -Filter 'icon-*.png' -ErrorAction SilentlyContinue).Count
Add-MystTiqCheck $ctx 'v0.8.0.0 Contracts' 'all 14 day/night category images and 26 navigation icons are present' `
    ($artCount -eq 14 -and $iconCount -eq 26) -Severity Critical -Details "artwork=$artCount icons=$iconCount"

$retired = @('dashboard-atmosphere-v3.png','page-art-home-dark.jpg','page-art-home-light.jpg','page-art-world-dark.jpg','page-art-world-light.jpg','server-card-art.png','world-card-art.png')
Add-MystTiqCheck $ctx 'v0.8.0.0 Contracts' 'the seven retired images are gone and nothing references them or the old per-category art properties' `
    (@($retired | Where-Object { Test-Path (Join-Path $assets $_) }).Count -eq 0 -and
     $xaml -notmatch 'dashboard-atmosphere|page-art-home|page-art-world|/Assets/Icons/' -and $vm -notmatch 'IsHomePageArtDark|IsWorldPageArtDark') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.8.0.0 Contracts' 'header, workspace and setup wizard bind the themed artwork; icons use the bounded IconArt loader; the header has its own layout column' `
    ($xaml -match 'Source="\{Binding PageArtwork\}"' -and $xaml -match 'Source="\{Binding SetupArtwork\}"' -and
     ([regex]::Matches($xaml, '\{services:IconArt ')).Count -ge 26 -and $xaml -match 'x:Name="PageArtHeader"' -and
     $vm -match 'public Bitmap PageArtwork => ArtworkCatalog\.Page\(SelectedPage, IsLightMode\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.8.0.0 Contracts' 'requested: the header art fills the whole card (no card padding, text layer inset) and nav icons are 10% smaller (50x50)' `
    ($xaml -match 'x:Name="PageArtHeader"[^>]*Padding="0"' -and $xaml -match '<Grid Margin="20,8,20,9">' -and
     ([regex]::Matches($xaml, '\{services:IconArt [^}]+\}" Width="50" Height="50"')).Count -eq 26 -and
     $xaml -notmatch '\{services:IconArt [^}]+\}" Width="56"') `
    -Severity Critical

Test-MystTiqFile $ctx 'scripts\Testing\MystTiq.ArtworkHarness\Program.cs' 'v0.8.0.0 Contracts' 'the offline artwork rendering harness is present' -Severity Critical
Test-MystTiqFile $ctx 'docs\artwork\PROMPTS.json' 'v0.8.0.0 Contracts' 'artwork provenance (generation prompts) is kept with the source' -Severity High
Test-MystTiqFile $ctx 'docs\architecture\v0.8.0.0-artwork-refresh.md' 'v0.8.0.0 Contracts' 'v0.8.0.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.8.0.0 is documented' ([regex]::IsMatch($docText, 'v0\.8\.0\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.8.0.0.md' 'Documentation' 'v0.8.0.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.8.0.0 entry' ($changelogText -match '## v0\.8\.0\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = if ($FrozenBaselineZip) { $FrozenBaselineZip } else { Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.115.0\MystTiqPalworldServer_v0.7.115.0_FullSource.zip' }
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        # v0.8.0.0: reported, not fatal: the rest of the gate is still worth running on a machine without it.
        Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.115.0 checkpoint logic gate still passes' $false -Skipped -Severity Critical `
            -Details "Baseline archive not found at $frozenZip. Run on the release machine, or pass -FrozenBaselineZip <path>."
    }
    else {
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.115.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.115.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.115.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.115.0 checkpoint logic gate still passes' `
        ($unexpectedFrozenFailures.Count -eq 0) `
        -Severity Critical `
        -Details ($(if ($unexpectedFrozenFailures.Count -gt 0) { "Unexpected failures: $($unexpectedFrozenFailures -join ', ')" } else { '' }))
    }

    # v0.8.0.0: -AllowBuildOutputs, because this gate has already built and published by now; repository
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

    # v0.8.0.0: the artwork is Desktop-only, so its runtime proof is the offline headless rendering harness
    # (every page's day/night art, visible bindings, header/ribbon/category layout at 950x650, all 26 icons).
    Test-MystTiqCommand $ctx 'v0.8.0.0 Runtime' 'MystTiq.ArtworkHarness passes (offline headless rendering)' {
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
