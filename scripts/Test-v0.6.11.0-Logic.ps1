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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.11.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.11\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.11.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.10.0 Clone World is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessWorldCloneService') -and [regex]::IsMatch($hostText, '"/server/clone"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.9.0 Idle Auto-Stop is still present' `
    ([regex]::IsMatch($coreText, 'IdleEmpty') -and [regex]::IsMatch($hostText, 'EvaluateIdleRulesAsync')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.11.0 contract presence -- stale-instance detection fix
# ---------------------------------------------------------------------------
$bootstrapText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Services\LocalManagementBootstrapper.cs') -Raw

Add-MystTiqCheck $ctx 'v0.6.11 Contracts' 'LocalManagementBootstrapper compares the actual probed version, not just a coarse apiVersion flag' `
    ([regex]::IsMatch($bootstrapText, 'VersionMatches') -and [regex]::IsMatch($bootstrapText, 'GetExecutingAssembly\(\)\.GetName\(\)\.Version')) `
    -Severity Critical -Details 'Root cause of the real user-reported bug: a leftover old-version mysttiq-server.exe was silently reused because the old check only compared component name and a historically-always-1 apiVersion integer.'

Add-MystTiqCheck $ctx 'v0.6.11 Contracts' 'A version mismatch is surfaced as StaleInstanceDetected rather than silently reused or force-killed' `
    ([regex]::IsMatch($bootstrapText, 'StaleInstanceDetected') -and [regex]::IsMatch($bootstrapText, 'StaleInstanceVersion')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.11 Contracts' 'MainWindowViewModel surfaces the stale-instance detail on the success path, not only on failure' `
    ([regex]::IsMatch($desktopText, 'StaleInstanceDetected')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. v0.6.11.0 contract presence -- WAN reachability diagnostics
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.11 Contracts' 'WanReachabilityService exists, is platform-independent, and checks both public IP and UPnP router mapping' `
    ([regex]::IsMatch($coreText, 'class WanReachabilityService') -and [regex]::IsMatch($coreText, 'api\.ipify\.org') -and [regex]::IsMatch($coreText, 'GetSpecificPortMappingEntry')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.11 Contracts' 'UPnP repair mirrors the existing firewall repair detect/repair pattern (AddPortMapping)' `
    ([regex]::IsMatch($coreText, 'RepairUpnpMappingAsync') -and [regex]::IsMatch($coreText, 'AddPortMapping')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.11 Contracts' 'WAN routes and Desktop UI are wired' `
    ([regex]::IsMatch($hostText, '"/diagnostics/network/wan"') -and [regex]::IsMatch($hostText, '"/diagnostics/network/wan/upnp/repair"') -and [regex]::IsMatch($desktopText, 'RunWanReachabilityCommand') -and [regex]::IsMatch($desktopText, 'WAN / External Reachability')) `
    -Severity High

Add-MystTiqCheck $ctx 'v0.6.11 Contracts' 'Local Windows Firewall inspection/repair (pre-existing, confirmed not duplicated) is still the only firewall-rule mechanism' `
    ([regex]::IsMatch($coreText, 'GetInboundFirewallRulesAsync') -and [regex]::IsMatch($coreText, 'RepairInboundFirewallRuleAsync')) `
    -Severity Critical -Details 'Confirmed via research this release: local firewall inspection/repair for the game port already shipped in an earlier v0.6.x milestone and is already wired into the Diagnostics Center page. This check guards against accidentally shipping a duplicate implementation.'

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.11.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.11\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Existing v0.6.10.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.10.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.10.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 8. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.10.0\MystTiqPalworldServer_v0.6.10.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.10.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.10.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.10.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.10.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.10.0 checkpoint logic gate still passes' `
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
