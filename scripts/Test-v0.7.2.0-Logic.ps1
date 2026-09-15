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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.2.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.2\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.2.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Services\ThemeCatalog.cs' 'Regression' 'v0.7.0.0 ThemeCatalog.cs is still present' -Severity Critical

$vmText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs') -Raw
$axamlText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.1.0 Simple Settings gap indicator is still present' `
    ([regex]::IsMatch($vmText, 'public string SimpleSettingsGapText')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.2.0 contract presence -- Port-conflict validation
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\Services\PortAvailabilityService.cs' 'v0.7.2.0 Contracts' 'PortAvailabilityService.cs exists' -Severity Critical

$portServiceText = Get-Content (Join-Path $root 'src\MystTiq.Core\Services\PortAvailabilityService.cs') -Raw
$modelsText = Get-Content (Join-Path $root 'src\MystTiq.Core\Models\NetworkDiagnosticModels.cs') -Raw
$hostText = Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
$clientIfaceText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\IMystTiqApiClient.cs') -Raw
$clientImplText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\MystTiqApiClient.cs') -Raw
$dtoText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Models\NetworkDiagnosticDtos.cs') -Raw

Add-MystTiqCheck $ctx 'v0.7.2.0 Contracts' 'Core: PortCheckResult model exists and PortAvailabilityService reuses the existing platform endpoint listing' `
    ([regex]::IsMatch($modelsText, 'public sealed record PortCheckResult\(') -and
     [regex]::IsMatch($portServiceText, 'GetUdpEndpointsAsync|GetTcpListenersAsync') -and
     [regex]::IsMatch($portServiceText, 'FirstOrDefault\(e\s*=>\s*e\.LocalPort\s*==\s*port\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.2.0 Contracts' 'HeadlessHost exposes a standalone, profile-independent port-check endpoint' `
    ([regex]::IsMatch($hostText, 'MapGet\("/api/v1/diagnostics/port-check"') -and
     [regex]::IsMatch($hostText, 'new PortAvailabilityService\(NetworkDiagnosticsPlatformService\.ForCurrentPlatform\(\)\)')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.2.0 Contracts' 'Desktop client exposes CheckPortAsync (interface + implementation) and the matching DTO' `
    ([regex]::IsMatch($clientIfaceText, 'Task<PortCheckResultDto>\s*CheckPortAsync') -and
     [regex]::IsMatch($clientImplText, 'public async Task<PortCheckResultDto> CheckPortAsync') -and
     [regex]::IsMatch($clientImplText, 'diagnostics/port-check\?port=') -and
     [regex]::IsMatch($dtoText, 'public sealed record PortCheckResultDto\(')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.2.0 Contracts' 'SetupGamePort/SetupRestPort trigger a live best-effort port check that never throws out of the setter' `
    ([regex]::IsMatch($vmText, 'CheckSetupPortAsync\(_setupGamePort,\s*"UDP"') -and
     [regex]::IsMatch($vmText, 'CheckSetupPortAsync\(_setupRestPort,\s*"TCP"') -and
     [regex]::IsMatch($vmText, '(?s)private async void CheckSetupPortAsync.*?catch\s*\{')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.2.0 Contracts' 'the Setup card shows the port warnings without hard-blocking Create' `
    ([regex]::IsMatch($axamlText, 'IsVisible="\{Binding HasSetupGamePortWarning\}"') -and
     [regex]::IsMatch($axamlText, 'IsVisible="\{Binding HasSetupRestPortWarning\}"') -and
     [regex]::IsMatch($vmText, 'public bool HasSetupGamePortWarning') -and
     [regex]::IsMatch($vmText, 'public bool HasSetupRestPortWarning')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.2.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.2\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.1.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.1.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.1.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.1.0\MystTiqPalworldServer_v0.7.1.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.1.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.1.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.1.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.1.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.1.0 checkpoint logic gate still passes' `
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
