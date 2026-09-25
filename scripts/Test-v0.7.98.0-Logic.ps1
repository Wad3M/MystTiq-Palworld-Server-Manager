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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.98.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.98\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.98.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.98\.0"' `
    'Versioning' 'app.manifest reports v0.7.98.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.97.0-Logic.ps1' 'Regression' 'v0.7.97.0 logic gate remains available' -Severity High

$svc = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessDiagnosticsService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.91.0 crash-risk config check and the Identity health exemption remain present' `
    ($svc -match 'findings\.AddRange\(BuildCrashRiskFindings\(\)\);' -and $svc -match 'f => f\.Category != "Identity"') `
    -Severity Critical

$crash = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessCrashAndSaveToolsService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.97.0 crash analyzer catalog use remains present' `
    ($crash -match 'CrashAnalysisBuilder\.Build\(evidence\.Lines, installedModNames, previousKeys\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.98.0 Contracts -- Doctor: disk, backups, admin access, memory, recent crashes
# ---------------------------------------------------------------------------
$rules = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\DoctorHealthRules.cs'
Add-MystTiqCheck $ctx 'v0.7.98.0 Contracts' 'disk thresholds match the earlier Linux check (fail under 2 GiB, warn under 5 GiB) and the next backup must fit' `
    ($rules -match 'free < 2 \? DiagnosticState\.Fail : free < 5 \? DiagnosticState\.Warning' -and $rules -match 'freeBytes < 2 \* largestBackupBytes') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.98.0 Contracts' 'backup freshness is judged against the world''s last change, not the clock, and a world with no backup is a warning not a fail' `
    ($rules -match 'var exposure = worldLastWriteAt\.Value - latestBackupAt\.Value;' -and $rules -match 'exposure < TimeSpan\.FromDays\(14\) \? DiagnosticState\.Warning : DiagnosticState\.Fail' -and
     $rules -match 'new\(DiagnosticState\.Warning, "The world has save data but no backup exists\."') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.98.0 Contracts' 'the admin password is never interpolated into any finding text' `
    ($rules -notmatch '\{password\}' -and $rules -notmatch '\{adminPassword\}' -and $rules -match 'CommonPasswords\.Contains\(password\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.98.0 Contracts' 'only new critical crash findings warn, and an older-format report is skipped rather than guessed at' `
    ($rules -match 'f\.IsNew && f\.Severity == "Critical"' -and $rules -match 'string\.IsNullOrEmpty\(f\.Key\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.98.0 Contracts' 'the diagnostics service adds the four finding groups to the unified report' `
    ($svc -match 'findings\.AddRange\(BuildResourceFindings\(\)\);' -and $svc -match 'findings\.AddRange\(BuildBackupFindings\(\)\);' -and
     $svc -match 'findings\.AddRange\(BuildSecurityFindings\(\)\);' -and $svc -match 'findings\.AddRange\(BuildRecentCrashFindings\(\)\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.98.0 Contracts' 'drives are resolved by longest mount prefix, Palworld''s own rolling backup folders are ignored, and the Linux server-drive check is not duplicated' `
    ($svc -match 'root\.Length > best\.RootDirectory\.FullName\.Length' -and $svc -match 's\.Equals\("backup", StringComparison\.OrdinalIgnoreCase\)' -and $svc -match '!OperatingSystem\.IsLinux\(\)') `
    -Severity Critical

$api = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'v0.7.98.0 Contracts' 'the backup and crash services are passed into the diagnostics service at composition' `
    ($api -match 'playerRegistry, playerGuildExplorer, backups, crashAndSaveTools\);') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.98.0 Contracts' 'the logic harness covers disk, backup freshness, admin access, memory and recent crash rules' `
    ((([regex]::Matches($harness, 'RunScenario\("Doctor ')).Count) -ge 5) `
    -Severity Critical

Test-MystTiqFile $ctx 'scripts\Test-v0.7.98.0-RouteSmoke.ps1' 'v0.7.98.0 Contracts' 'the doctor route smoke exists' -Severity Critical
Test-MystTiqFile $ctx 'docs\architecture\v0.7.98.0-doctor-disk-backups-security-memory-crashes.md' 'v0.7.98.0 Contracts' 'v0.7.98.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.98.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.98\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.98.0.md' 'Documentation' 'v0.7.98.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.98.0 entry' ($changelogText -match '## v0\.7\.98\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.97.0\MystTiqPalworldServer_v0.7.97.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.97.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.97.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.97.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.97.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.97.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'v0.7.98.0 Runtime' 'v0.7.98.0 doctor route smoke passes' {
        & (Join-Path $root 'scripts\Test-v0.7.98.0-RouteSmoke.ps1') -ProjectRoot $root
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
