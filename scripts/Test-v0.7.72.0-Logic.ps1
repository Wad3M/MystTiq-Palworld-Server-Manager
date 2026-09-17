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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.72.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.72\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.72.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.72\.0"' `
    'Versioning' 'app.manifest reports v0.7.72.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.71.0-Logic.ps1' 'Regression' 'v0.7.71.0 logic gate remains available' -Severity High

$monitoringText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessMonitoringService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.71.0 PalDefender console source remains present' `
    ($monitoringText -match 'Add\(result, "PalDefender log", latestPalDefender\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.72.0 Contracts -- the native hook itself
# ---------------------------------------------------------------------------
$dllMainText = Get-MystTiqText $ctx 'native\MystTiqConsoleProxy\dllmain.cpp'

Add-MystTiqCheck $ctx 'v0.7.72.0 Contracts' 'dllmain.cpp hooks WriteConsoleA and WriteConsoleW (not the disclosed-dead LogfImpl target)' `
    ($dllMainText -match 'HookedWriteConsoleA' -and $dllMainText -match 'HookedWriteConsoleW') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.72.0 Contracts' 'PatchKernel32Import walks the target module''s own Import Address Table' `
    ($dllMainText -match 'PatchKernel32Import' -and $dllMainText -match 'IMAGE_DIRECTORY_ENTRY_IMPORT') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.72.0 Contracts' 'the real function is resolved independently via GetProcAddress against kernel32, not read back from the IAT' `
    ($dllMainText -match 'GetProcAddress\(kernel32, "WriteConsoleA"\)' -and $dllMainText -match 'GetProcAddress\(kernel32, "WriteConsoleW"\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.72.0 Contracts' 'capture log files use _wfsopen with _SH_DENYWR (the concurrent-read fix), not plain _wfopen_s' `
    ($dllMainText -match '_wfsopen\(logPath\.c_str\(\), L"a", _SH_DENYWR\)' -and
     $dllMainText -match '_wfsopen\(capturePath\.c_str\(\), L"a", _SH_DENYWR\)' -and
     $dllMainText -notmatch '_wfopen_s\(&logFile') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.72.0 Contracts' 'DSOUND export forwarding (v0.7.57.0 foundation) remains unchanged' `
    ($dllMainText -match 'extern "C" __declspec\(dllexport\) HRESULT WINAPI DirectSoundCreate\(') `
    -Severity Critical

$serviceText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessConsoleCaptureProxyService.cs'
Add-MystTiqCheck $ctx 'v0.7.72.0 Contracts' 'HeadlessConsoleCaptureProxyService is Windows-only and never installs automatically' `
    ($serviceText -match 'paths\.PlatformId != "windows"' -and $serviceText -notmatch 'InstallAsync\(\)\s*;' ) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.72.0 Contracts' 'Install refuses to overwrite a foreign dsound.dll and refuses a locked file' `
    ($serviceText -match 'Refusing to overwrite it' -and $serviceText -match 'it may be locked by a running PalServer') `
    -Severity Critical

$routesText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'v0.7.72.0 Contracts' 'console-capture status/install/uninstall routes are mapped' `
    ($routesText -match 'MapGet\("/server/console-capture"' -and
     $routesText -match 'MapPost\("/server/console-capture/install"' -and
     $routesText -match 'MapPost\("/server/console-capture/uninstall"') `
    -Severity Critical

$profileHostText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\ServerProfileHost.cs'
Add-MystTiqCheck $ctx 'v0.7.72.0 Contracts' 'ServerProfileHost exposes ConsoleCaptureProxy' `
    ($profileHostText -match 'public required HeadlessConsoleCaptureProxyService ConsoleCaptureProxy') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.72.0 Contracts' 'ResolveConsoleSources reads the native capture log as a new source' `
    ($monitoringText -match 'MystTiqConsoleProxy-Capture\.log' -and $monitoringText -match 'PalServer console capture \(native hook\)') `
    -Severity Critical

$buildAvaloniaText = Get-MystTiqText $ctx 'scripts\Build-AvaloniaDesktop.ps1'
Add-MystTiqCheck $ctx 'v0.7.72.0 Contracts' 'Build-AvaloniaDesktop.ps1 stages the native proxy DLL best-effort (never fails the publish if absent)' `
    ($buildAvaloniaText -match 'MystTiqConsoleProxy\.dll' -and $buildAvaloniaText -match 'non-fatal') `
    -Severity High

Test-MystTiqFile $ctx 'docs\architecture\v0.7.72.0-console-write-hook.md' `
    'v0.7.72.0 Contracts' 'v0.7.72.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.72.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.72\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.72.0.md' 'Documentation' 'v0.7.72.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.72.0 entry' `
    ($changelogText -match '## v0\.7\.72\.0') `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.71.0\MystTiqPalworldServer_v0.7.71.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.71.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.71.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.71.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.71.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.71.0 checkpoint logic gate still passes' `
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
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
