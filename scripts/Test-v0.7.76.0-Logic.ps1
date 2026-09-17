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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.76.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.76\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.76.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.76\.0"' `
    'Versioning' 'app.manifest reports v0.7.76.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.75.0-Logic.ps1' 'Regression' 'v0.7.75.0 logic gate remains available' -Severity High

$playerDeletionText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessPlayerDeletionService.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.75.0 HeadlessPlayerDeletionService remains present, unchanged by this UI-only version' `
    ($playerDeletionText -match 'class HeadlessPlayerDeletionService') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.76.0 Contracts -- new Desktop dialogs
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Views\SelectGuildDialog.axaml' 'v0.7.76.0 Contracts' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Views\ConfirmOperationDialog.axaml' 'v0.7.76.0 Contracts' -Severity Critical

$vmText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'v0.7.76.0 Contracts' 'Base transfer/wipe methods call the existing, unchanged API client methods' `
    ($vmText -match 'PreviewTransferBaseAsync[\s\S]{0,300}_api\.PreviewBaseOwnershipTransferAsync' -and
     $vmText -match 'PreviewWipeBaseAsync[\s\S]{0,300}_api\.PreviewBaseRecoveryAsync') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.76.0 Contracts' 'Guild operation row methods call the existing, unchanged API client method' `
    ($vmText -match 'PreviewGuildOperationForRowAsync[\s\S]{0,300}_api\.PreviewGuildOwnershipAsync') `
    -Severity Critical

function Test-MethodPrecedesUsage([string]$text, [string]$methodName, [string]$usageText) {
    $methodIndex = $text.IndexOf("public async Task<bool> $methodName")
    $usageIndex = $text.IndexOf($usageText, $methodIndex)
    return ($methodIndex -ge 0 -and $usageIndex -gt $methodIndex -and ($usageIndex - $methodIndex) -lt 600)
}
Add-MystTiqCheck $ctx 'v0.7.76.0 Contracts' 'row-driven results write into the same status-text properties the in-page cards already display' `
    ((Test-MethodPrecedesUsage $vmText 'ApplyTransferBaseAsync' 'BaseTransferStatusText = result.Message') -and
     (Test-MethodPrecedesUsage $vmText 'ApplyWipeBaseAsync' 'BaseRecoveryStatusText = result.Message') -and
     (Test-MethodPrecedesUsage $vmText 'ApplyGuildOperationForRowAsync' 'GuildOperationStatusText = result.Message')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.76.0 Contracts -- context menus wired
# ---------------------------------------------------------------------------
$mainWindowXaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.76.0 Contracts' 'Bases context menu has Copy/Transfer/Wipe' `
    ($mainWindowXaml -match 'Copy Base Info' -and $mainWindowXaml -match 'Transfer to Guild' -and $mainWindowXaml -match 'Wipe Base Completely') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.76.0 Contracts' 'Guilds context menu has all four operation types' `
    ($mainWindowXaml -match 'Transfer Leadership To' -and $mainWindowXaml -match 'Add Player…' -and
     $mainWindowXaml -match 'Remove Broken Member…' -and $mainWindowXaml -match 'Claim Orphaned Guild…') `
    -Severity Critical

$mainWindowCodeText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml.cs'
Add-MystTiqCheck $ctx 'v0.7.76.0 Contracts' 'all four guild operation handlers route through the same RunGuildOperationAsync helper with the correct API operation names' `
    ($mainWindowCodeText -match 'RunGuildOperationAsync\("transfer-leadership"' -and
     $mainWindowCodeText -match 'RunGuildOperationAsync\("add-player"' -and
     $mainWindowCodeText -match 'RunGuildOperationAsync\("remove-broken-member"' -and
     $mainWindowCodeText -match 'RunGuildOperationAsync\("claim"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.76.0-base-guild-right-click-workflow.md' `
    'v0.7.76.0 Contracts' 'v0.7.76.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 5. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.76.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.76\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.76.0.md' 'Documentation' 'v0.7.76.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.76.0 entry' `
    ($changelogText -match '## v0\.7\.76\.0') `
    -Severity High

Test-MystTiqFile $ctx 'docs\roadmap\PRODUCT_ROADMAP.md' 'Documentation' 'PRODUCT_ROADMAP.md exists' -Severity High
Add-MystTiqCheck $ctx 'Documentation' 'PRODUCT_ROADMAP.md has a Live-Session Backlog section' `
    ((Get-MystTiqText $ctx 'docs\roadmap\PRODUCT_ROADMAP.md') -match '## Live-Session Backlog') `
    -Severity High

# ---------------------------------------------------------------------------
# 6. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.75.0\MystTiqPalworldServer_v0.7.75.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.75.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.75.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.75.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.75.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.75.0 checkpoint logic gate still passes' `
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

    Test-MystTiqCommand $ctx 'Build' 'MystTiq.Desktop builds clean (not part of PalworldServerManager.slnx)' {
        & dotnet build (Join-Path $root 'src\MystTiq.Desktop\MystTiq.Desktop.csproj') -c Release
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
