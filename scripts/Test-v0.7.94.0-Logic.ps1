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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.94.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.94\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.94.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.94\.0"' `
    'Versioning' 'app.manifest reports v0.7.94.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.93.0-Logic.ps1' 'Regression' 'v0.7.93.0 logic gate remains available' -Severity High

$nexusVm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.93.0-era Nexus Mods pipeline remains present, untouched by this version' `
    ($nexusVm -match 'private async Task NexusDownloadAndInstallAsync\(') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.94.0 Contracts -- Starter Kits
# ---------------------------------------------------------------------------
$kit = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessKitService.cs'
Add-MystTiqCheck $ctx 'v0.7.94.0 Contracts' 'ids and UserIds are validated with strict patterns before anything reaches an RCON command' `
    ($kit -match '\^\[A-Za-z0-9_\]\{1,64\}\$' -and $kit -match '\^\[A-Za-z0-9_\\\\-\]\{3,64\}\$' -and $kit -match 'if \(!UserIdPattern\(\)\.IsMatch\(userId') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.94.0 Contracts' 'delivery uses PalDefender giveitems/givepal behind the IKitCommandRunner seam' `
    ($kit -match 'interface IKitCommandRunner' -and $kit -match '\$"giveitems \{userId\} "' -and $kit -match '\$"givepal \{userId\} \{pal\.Id\} \{pal\.Amount\}"' -and $kit -match 'class RconKitCommandRunner') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.94.0 Contracts' 'auto-gift only applies to players first seen at or after it was enabled, once, with capped retries' `
    ($kit -match 'record\.FirstSeenUtc < enabledAt' -and $kit -match 'MaximumDeliveryAttempts = 3' -and $kit -match 'alreadyClaimed \|\| tried >= MaximumDeliveryAttempts') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.94.0 Contracts' 'a reply that reads as an error is a failed delivery and is not recorded as a claim' `
    ($kit -match 'LooksLikeFailure\(result\.Response\)' -and $kit -match 'RecordClaim\(new KitClaim\(') `
    -Severity Critical

$host1 = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'v0.7.94.0 Contracts' 'kit enforcement runs on the status poll and mutating kit routes are Admin-only' `
    ($host1 -match 'await p\.Kits\.EnforceAsync\(players, token\);' -and
     $host1 -match 'routes\.MapGet\("/players/kits"[\s\S]{0,80}RequireRole\(MystTiqRole\.Operator' -and
     $host1 -match 'routes\.MapPut\("/players/kits"[\s\S]{0,300}RequireRole\(MystTiqRole\.Admin' -and
     $host1 -match '"/players/kits/\{kitId\}/give"' -and $host1 -match '"/players/kits/test-provider"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.94.0 Contracts' 'the kit service is composed into every server profile host' `
    ((Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\ServerProfileHost.cs') -match 'required HeadlessKitService Kits' -and $host1 -match 'new HeadlessKitService\(paths, activity, playerRegistry, new RconKitCommandRunner\(paths, rcon\)\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.94.0 Contracts' 'the Desktop kit editor refuses to save text that does not parse' `
    ($nexusVm -match 'if \(!KitEntryText\.TryParse\(KitEntriesText, out _, out var parseError\)\) \{ KitStatusText = parseError; return; \}') `
    -Severity Critical

$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.94.0 Contracts' 'the Players page has the Starter Kits card with provider test, auto-gift and give' `
    ($xaml -match 'Text="Starter Kits"' -and $xaml -match 'Command="\{Binding TestKitProviderCommand\}"' -and
     $xaml -match 'Command="\{Binding GiveSelectedKitCommand\}"' -and $xaml -match 'Auto-gift the chosen kit to new players') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.94.0 Contracts' 'the logic harness covers kit validation, commands, auto-gift rules and the entry text format' `
    (([regex]::Matches($harness, 'RunScenario\("Kit')).Count -ge 7 -and ([regex]::Matches($harness, 'RunScenario\("KitEntryText')).Count -ge 2) `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.94.0-starter-kits.md' 'v0.7.94.0 Contracts' 'v0.7.94.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.94.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.94\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.94.0.md' 'Documentation' 'v0.7.94.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.94.0 entry' ($changelogText -match '## v0\.7\.94\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.93.0\MystTiqPalworldServer_v0.7.93.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.93.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.93.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.93.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.93.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.93.0 checkpoint logic gate still passes' `
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



