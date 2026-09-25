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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.95.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.95\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.95.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.95\.0"' `
    'Versioning' 'app.manifest reports v0.7.95.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.94.0-Logic.ps1' 'Regression' 'v0.7.94.0 logic gate remains available' -Severity High

$vm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.94.0-era Starter Kits card remains present, untouched by this version' `
    ($vm -match 'private async Task GiveSelectedKitAsync\(\)') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.95.0 Contracts -- Discord bot live features
# ---------------------------------------------------------------------------
$bot = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\HeadlessDiscordBotService.cs'
$fmt = Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\DiscordBotFormatting.cs'
Add-MystTiqCheck $ctx 'v0.7.95.0 Contracts' 'the bot runs a live loop after Ready and cancels it on disconnect' `
    ($bot -match 'StartLiveLoop\(socket\);' -and $bot -match 'liveLoop\?\.Cancel\(\);' -and $bot -match 'private async Task LiveTickAsync\(') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.95.0 Contracts' 'every message the bot sends disables mentions (a player name must never ping the guild)' `
    ($bot -match 'RespondAsync\(reply, allowedMentions: AllowedMentions\.None\)' -and
     $bot -match 'SendMessageAsync\(embed: embed, allowedMentions: AllowedMentions\.None\)' -and
     $bot -match 'SendMessageAsync\(text, allowedMentions: AllowedMentions\.None\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.95.0 Contracts' 'a stop or restart resets the join/leave baseline and a failed player read is never diffed' `
    ($bot -match 'if \(stateKey != "Online"\) state\.PreviousPlayers = null;' -and $bot -match 'else if \(players\.Available\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.95.0 Contracts' 'the status message id is persisted, kept across saves, and dropped when the channel changes' `
    ($bot -match 'PersistStatusMessageId\(cfg\.StatusChannelId' -and $bot -match 'string\.Equals\(statusChannel, config\.StatusChannelId, StringComparison\.Ordinal\) \? config\.StatusMessageId : null') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.95.0 Contracts' 'kick/ban autocomplete only serves callers whose role could run the command' `
    ($bot -match 'OnAutocompleteAsync' -and $bot -match 'granted < DiscordBotFormatting\.RequiredRole\(name\)' -and $bot -match 'WithAutocomplete\(true\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.95.0 Contracts' 'save, backup and unban commands exist and are role-gated in one shared place' `
    ($bot -match 'case "mysttiq-save":' -and $bot -match 'case "mysttiq-backup":' -and $bot -match 'case "mysttiq-unban":' -and
     $fmt -match '"mysttiq-save" or "mysttiq-backup"' -and $fmt -match '"mysttiq-kick" or "mysttiq-ban" or "mysttiq-unban" => MystTiqRole\.Admin') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.95.0 Contracts' 'channel ids are validated as snowflakes on the server and in the Desktop form' `
    ((Get-MystTiqText $ctx 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -match 'HeadlessDiscordBotService\.ValidateChannelIds\(request\)' -and
     $vm -match 'MystTiq\.Core\.Models\.DiscordSnowflake\.TryParse\(value, out _\)') `
    -Severity Critical

$models = Get-MystTiqText $ctx 'src\MystTiq.Core\Models\DiscordBotModels.cs'
Add-MystTiqCheck $ctx 'v0.7.95.0 Contracts' 'new config fields are optional with defaults so an older discord-bot.json still loads' `
    ($models -match 'string\? StatusChannelId = null' -and $models -match 'bool ShowPresence = true') `
    -Severity Critical

$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.95.0 Contracts' 'the Discord Bot card exposes the status channel, events channel and presence toggle' `
    ($xaml -match 'DiscordBotConfig\.StatusChannelId' -and $xaml -match 'DiscordBotConfig\.EventsChannelId' -and $xaml -match 'DiscordBotConfig\.ShowPresence') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.95.0 Contracts' 'the logic harness covers the Discord status, diff, autocomplete, roles and id validation logic' `
    (([regex]::Matches($harness, 'RunScenario\("Discord')).Count -ge 6) `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.95.0-discord-bot-live-features.md' 'v0.7.95.0 Contracts' 'v0.7.95.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.95.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.95\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.95.0.md' 'Documentation' 'v0.7.95.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.95.0 entry' ($changelogText -match '## v0\.7\.95\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.94.0\MystTiqPalworldServer_v0.7.94.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.94.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.94.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.94.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.94.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.94.0 checkpoint logic gate still passes' `
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





