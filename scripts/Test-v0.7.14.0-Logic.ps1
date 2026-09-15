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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.14.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.14\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.14.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.14\.0"' `
    'Versioning' 'app.manifest reports v0.7.14.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs' 'Regression' 'v0.7.12.0 whitelist enforcement harness is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical

$mainWindowText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\MainWindow.axaml') -Raw
$designSystemText = Get-Content (Join-Path $root 'src\MystTiq.Desktop\Styles\DesignSystem.axaml') -Raw

Add-MystTiqCheck $ctx 'Regression' 'v0.7.13.0 dedicated wizard host is still present' `
    ([regex]::IsMatch($mainWindowText, 'Grid\s+Grid\.Row="3"[^>]*IsVisible="\{Binding IsCreatingNewProfile\}"')) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.14.0 contract presence -- accessibility & interaction feedback pass
# ---------------------------------------------------------------------------
Add-MystTiqCheck $ctx 'v0.7.14.0 Contracts' 'MainWindow.axaml has at least 16 explicit AutomationProperties.Name attributes (one per genuinely icon-only control)' `
    ([regex]::Matches($mainWindowText, 'AutomationProperties\.Name=').Count -ge 16) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.14.0 Contracts' 'window-chrome buttons (Minimize/Maximize/Close) have both ToolTip.Tip and AutomationProperties.Name' `
    ($(
        $expected = @(
            'Click="Minimize_OnClick" ToolTip.Tip="Minimize" AutomationProperties.Name="Minimize"',
            'Click="Maximize_OnClick" ToolTip.Tip="Maximize" AutomationProperties.Name="Maximize"',
            'Click="Close_OnClick" ToolTip.Tip="Close" AutomationProperties.Name="Close window"'
        )
        -not ($expected | Where-Object { $mainWindowText -notmatch [regex]::Escape($_) })
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.14.0 Contracts' 'the per-tab close button is 28x28 (was 20x20, below Fluent''s ~32-40px minimum pointer target)' `
    ([regex]::IsMatch($mainWindowText, 'Classes="ghost" Width="28" Height="28" Padding="0" Margin="6,0,0,0"\s*\r?\n\s*Click="CloseTabButton_OnClick"')) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.14.0 Contracts' 'all 7 ribbon action buttons have an explicit AutomationProperties.Name' `
    ($(
        $expected = @(
            'Command="{Binding ConnectCommand}" AutomationProperties.Name="Refresh"',
            'Command="{Binding StartCommand}" AutomationProperties.Name="Start server"',
            'Command="{Binding RestartCommand}" AutomationProperties.Name="Restart server"',
            'Command="{Binding StopCommand}" AutomationProperties.Name="Stop server"',
            'Command="{Binding CreateBackupCommand}" AutomationProperties.Name="Create backup"',
            'CommandParameter="Console" AutomationProperties.Name="Open Console"',
            'CommandParameter="Doctor" AutomationProperties.Name="Open Doctor"'
        )
        -not ($expected | Where-Object { $mainWindowText -notmatch [regex]::Escape($_) })
    )) `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.14.0 Contracts' 'DesignSystem.axaml restores the focus-visible ring for Button.ghost/.ribbon/.ribbonCompact/.ribbonMedium and ToggleButton.categoryTab' `
    ($(
        $expected = @(
            'Button.ghost:focus-visible',
            'Button.ribbon:focus-visible',
            'Button.ribbonCompact:focus-visible',
            'Button.ribbonMedium:focus-visible',
            'ToggleButton.categoryTab:focus-visible'
        )
        -not ($expected | Where-Object { $designSystemText -notmatch [regex]::Escape("Selector=`"$_`"") })
    )) `
    -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.14.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.14\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.13.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.13.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.13.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.13.0\MystTiqPalworldServer_v0.7.13.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.13.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.13.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.13.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.13.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.13.0 checkpoint logic gate still passes' `
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

    # v0.7.14.0 is Desktop-only (styles/markup), so the v0.7.12.0 route smoke and whitelist harness
    # (server-side) are still expected to pass unchanged -- carried forward as regression evidence.
    # Both need Build.ps1 DesktopWindows's sidecar output, the same known quirk every version's
    # -RunBuild already works around.
    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.12.0 whitelist enforcement harness (6 scenarios) still passes' {
        Push-Location (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness')
        try { & dotnet run -c Release }
        finally {
            Pop-Location
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
        }
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.12.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.12.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
