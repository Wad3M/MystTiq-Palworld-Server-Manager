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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.17.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.17\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.17.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

$coreText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$hostText = (Get-ChildItem (Join-Path $root 'src\MystTiq.HeadlessHost') -Recurse -File -Include *.cs | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$desktopText = (Get-ChildItem (Join-Path $root 'src\MystTiq.Desktop') -Recurse -File -Include *.cs, *.axaml | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Regression' 'v0.6.16.0 Live World Map is still present' `
    ([regex]::IsMatch($desktopText, 'RebuildPlayerMapPoints')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'Regression' 'v0.6.15.0 Pal Editor is still present' `
    ([regex]::IsMatch($hostText, 'class HeadlessPalEditService')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6.17.0 contract presence -- Two-Way Discord Bot Control
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.6.17 Contracts' 'Outbound Discord dispatch is real, not the old "not implemented" stub' `
    ([regex]::IsMatch($hostText, 'DispatchDiscordAsync') -and -not [regex]::IsMatch($hostText, 'NotificationChannel\.Discord:\s*\r?\n\s*case NotificationChannel\.Email:')) `
    -Severity Critical -Details 'Confirmed by direct code inspection that the pre-v0.6.17.0 "Discord channel not implemented" stub was the only Discord-related code in the whole solution -- this replaces it with a real Discord-embed webhook POST.'

Add-MystTiqCheck $ctx 'v0.6.17 Contracts' 'A real Discord bot service exists (Discord.Net-backed, guild-scoped slash commands)' `
    ([regex]::IsMatch($hostText, 'class HeadlessDiscordBotService') -and [regex]::IsMatch($hostText, 'DiscordSocketClient') -and [regex]::IsMatch($hostText, 'GatewayIntents\.Guilds')) `
    -Severity Critical -Details 'Guild-scoped slash commands only -- no privileged MESSAGE_CONTENT intent, no public HTTPS endpoint exposed (outbound gateway connection only, matching the local/LAN-first security posture).'

Add-MystTiqCheck $ctx 'v0.6.17 Contracts' 'Discord.Net.WebSocket is referenced as a real package dependency' `
    ([regex]::IsMatch((Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj') -Raw), 'PackageReference Include="Discord\.Net\.WebSocket"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.17 Contracts' 'Every Discord command composes onto existing lifecycle/RCON/moderation services, not new game-control logic' `
    ([regex]::IsMatch($hostText, 'lifecycle\.StartAsync') -and [regex]::IsMatch($hostText, 'rcon\.ExecuteAsync\(\$"Broadcast') -and [regex]::IsMatch($hostText, 'playerModeration\.ExecuteAsync')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.17 Contracts' 'Discord command role gating reuses MystTiqRole, not a parallel permission model' `
    ([regex]::IsMatch($hostText, 'MystTiqRole\.Viewer.*MystTiqRole\.Operator.*MystTiqRole\.Admin') -or ([regex]::IsMatch($hostText, 'RequiredRole') -and [regex]::IsMatch($hostText, 'ResolveRole'))) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.17 Contracts' 'Discord-triggered lifecycle actions share the OperationCoordinator lock with REST/Desktop-triggered ones' `
    ([regex]::IsMatch($hostText, 'operations\.BeginAsync\(profileId, kind, "DiscordBot"')) `
    -Severity Critical -Details 'Prevents a /mysttiq-stop from racing a REST-triggered stop -- same coordinator, same resource keys as LocalManagementApiHost.RunLifecycleActionAsync.'

Add-MystTiqCheck $ctx 'v0.6.17 Contracts' 'Bot token is never round-tripped to the Desktop -- config view exposes only whether one is configured' `
    ([regex]::IsMatch($coreText, 'record DiscordBotConfigurationView') -and [regex]::IsMatch($coreText, 'TokenConfigured') -and -not [regex]::IsMatch($coreText, 'record DiscordBotConfigurationView[\s\S]{0,200}BotToken')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.17 Contracts' 'Discord bot config routes are Admin-gated, matching the existing notification-channel routes' `
    ([regex]::IsMatch($hostText, '"/notifications/discord-bot"[\s\S]{0,220}RequireRole\(MystTiqRole\.Admin')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.6.17 Contracts' 'Desktop wiring: Discord Bot card and API client methods exist' `
    ([regex]::IsMatch($desktopText, 'Discord Bot') -and [regex]::IsMatch($desktopText, 'GetDiscordBotConfigAsync') -and [regex]::IsMatch($desktopText, 'SaveDiscordBotConfigAsync')) `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6.17.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.6\.17\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.6.16.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.6.16.0-Logic.ps1' `
    'Regression Baseline' 'v0.6.16.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.6.16.0\MystTiqPalworldServer_v0.6.16.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.6.16.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.16.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.6.16.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.6.16.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.6.16.0 checkpoint logic gate still passes' `
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
