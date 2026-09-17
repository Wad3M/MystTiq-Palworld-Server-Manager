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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.75.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.75\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.75.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.75\.0"' `
    'Versioning' 'app.manifest reports v0.7.75.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.74.0-Logic.ps1' 'Regression' 'v0.7.74.0 logic gate remains available' -Severity High

$appText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\App.axaml.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.74.0 exit-shortcut methods remain present' `
    ($appText -match 'public async Task SafeExitAsync\(\)' -and $appText -match 'public async Task ForceExitAsync\(\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.75.0 Contracts -- Delete Player Completely
# ---------------------------------------------------------------------------
$deletionText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessPlayerDeletionService.cs'
Add-MystTiqCheck $ctx 'v0.7.75.0 Contracts' 'HeadlessPlayerDeletionService deletes the player save file' `
    ($deletionText -match 'File\.Delete\(op\.SavePath\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.75.0 Contracts' 'PreviewAsync requires the player offline and the save present' `
    ($deletionText -match 'player is \{ SaveExists: true, Online: false \}') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.75.0 Contracts' 'guild cleanup follow-up disposes the operation handle before running, fixing the real lock-ordering bug found live' `
    ($deletionText -match 'operation\.Dispose\(\);' -and $deletionText -match 'guildOwnership\.PreviewAsync' -and
     $deletionText.IndexOf('operation.Dispose();') -lt $deletionText.IndexOf('guildOwnership.PreviewAsync')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.75.0 Contracts' 'registry history is cleared via the new Forget method' `
    ($deletionText -match 'registry\.Forget\(op\.PlayerId\)') `
    -Severity Critical

$registryText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessPlayerRegistryService.cs'
Add-MystTiqCheck $ctx 'v0.7.75.0 Contracts' 'HeadlessPlayerRegistryService gained a Forget method' `
    ($registryText -match 'public void Forget\(string playerId\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. v0.7.75.0 Contracts -- Copy Player
# ---------------------------------------------------------------------------
$copyText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessPlayerCopyService.cs'
Add-MystTiqCheck $ctx 'v0.7.75.0 Contracts' 'CopiedFields matches the real, investigated field split (progress fields, not identity)' `
    ($copyText -match '"PlayerCharacterMakeData", "InventoryInfo", "UnlockedRecipeTechnologyNames"' -and
     $copyText -match '"RecordData", "SkinInventoryInfo", "OrderedQuestArray_FullRelease"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.75.0 Contracts' 'identity/container/position fields are explicitly documented as preserved, not copied' `
    ($copyText -match 'deliberately NOT overwritten: PlayerUId, IndividualId' -and $copyText -match 'OtomoCharacterContainerId/PalStorageContainerId') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.75.0 Contracts' 'each copied field is independently verified against the source after re-decoding the staged save' `
    ($copyText -match 'verifySaveData\[field\]\?\.ToJsonString\(\) != sourceSaveData\[field\]!\.ToJsonString\(\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 5. v0.7.75.0 Contracts -- routes, Desktop wiring, disabled-menu styling
# ---------------------------------------------------------------------------
$routesText = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs'
Add-MystTiqCheck $ctx 'v0.7.75.0 Contracts' 'player deletion/copy routes are mapped' `
    ($routesText -match 'MapPost\("/players/\{playerId\}/delete/preview"' -and
     $routesText -match 'MapPost\("/players/delete/apply"' -and
     $routesText -match 'MapPost\("/players/copy/preview"' -and
     $routesText -match 'MapPost\("/players/copy/apply"') `
    -Severity Critical

Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Views\ConfirmDeletePlayerDialog.axaml' 'v0.7.75.0 Contracts' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Views\ConfirmCopyPlayerDialog.axaml' 'v0.7.75.0 Contracts' -Severity Critical
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\Views\SelectPlayerDialog.axaml' 'v0.7.75.0 Contracts' -Severity Critical

$mainWindowXaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.75.0 Contracts' 'Players context menu gains Copy Player Data From and Delete Player Completely' `
    ($mainWindowXaml -match 'Copy Player Data From' -and $mainWindowXaml -match 'Delete Player Completely') `
    -Severity Critical

$designSystemText = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Styles\DesignSystem.axaml'
Add-MystTiqCheck $ctx 'v0.7.75.0 Contracts' 'MenuItem:disabled styling now exists (root cause of the "right-click looks broken" report)' `
    ($designSystemText -match 'Style Selector="MenuItem:disabled"') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.75.0-delete-and-copy-player.md' `
    'v0.7.75.0 Contracts' 'v0.7.75.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 6. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.75.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.75\.0')) `
    -Severity High

Test-MystTiqFile $ctx 'release-notes\v0.7.75.0.md' 'Documentation' 'v0.7.75.0 release notes exist' -Severity High

$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.75.0 entry' `
    ($changelogText -match '## v0\.7\.75\.0') `
    -Severity High

# ---------------------------------------------------------------------------
# 7. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 8. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.74.0\MystTiqPalworldServer_v0.7.74.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.74.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.74.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.74.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.74.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.74.0 checkpoint logic gate still passes' `
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
