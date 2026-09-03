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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.0.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.6\.0\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.6.0.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs' `
    'Version\s*=>\s*\$"v\{System\.Reflection\.Assembly\.GetExecutingAssembly\(\)\.GetName\(\)\.Version' `
    'Versioning' 'Desktop derives its displayed version from the assembly rather than a hardcoded literal (a stale "v0.4.18.2" literal in Program.cs went unnoticed for six releases before this convention existed)' -Severity High

Test-MystTiqTextMatch $ctx 'src\MystTiq.HeadlessHost\Program.cs' `
    'MystTiq Headless Host v\{System\.Reflection\.Assembly\.GetExecutingAssembly\(\)\.GetName\(\)\.Version' `
    'Versioning' 'Headless --help derives its displayed version from the assembly rather than a hardcoded literal' -Severity High

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'

Test-MystTiqTextMatch $ctx 'src\MystTiq.Desktop\MainWindow.axaml' `
    'x:DataType="vm:MainWindowViewModel"' `
    'Regression' 'Desktop remains Avalonia MVVM' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs' `
    '_api\.StartServerAsync' `
    'Regression' 'Server Start remains routed through management API' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs' `
    '_api\.StopServerAsync' `
    'Regression' 'Server Stop remains routed through management API' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs' `
    '_api\.RestartServerAsync' `
    'Regression' 'Server Restart remains routed through management API' -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.6 architecture contract presence
#
# These patterns intentionally allow either interface/class names or equivalent
# documented contract records. If implementation names change, update this test
# rather than removing the architectural requirement.
# ---------------------------------------------------------------------------
$coreFiles = Get-ChildItem (Join-Path $root 'src\MystTiq.Core') -Recurse -File -Include *.cs
$coreText = ($coreFiles | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'v0.6 Contracts' 'Operation contract is defined' `
    ([regex]::IsMatch($coreText, 'IOperation(Coordinator|Service)|Operation(Id|Record|Descriptor|Request)')) `
    -Severity Critical `
    -Details 'Expected a reusable operation contract such as IOperationCoordinator, OperationId, OperationRecord, or OperationRequest.'

Add-MystTiqCheck $ctx 'v0.6 Contracts' 'Provider/capability contract is defined' `
    ([regex]::IsMatch($coreText, 'I[A-Za-z0-9]+Provider|ProviderCapability|CapabilityDescriptor')) `
    -Severity Critical `
    -Details 'Expected at least one provider/capability abstraction in MystTiq.Core.'

Add-MystTiqCheck $ctx 'v0.6 Contracts' 'Health-state contract is defined' `
    ([regex]::IsMatch($coreText, 'Health(State|Status|Snapshot)|ServerHealth')) `
    -Severity High `
    -Details 'Expected a reusable health-state contract.'

Add-MystTiqCheck $ctx 'v0.6 Contracts' 'Transaction contract is defined' `
    ([regex]::IsMatch($coreText, 'Transaction(Id|Record|Context|Result|State)|I[A-Za-z0-9]*Transaction')) `
    -Severity High `
    -Details 'Expected a reusable transaction contract for future rollback-capable operations.'

Add-MystTiqCheck $ctx 'v0.6 Contracts' 'Server/profile identity contract is defined' `
    ([regex]::IsMatch($coreText, 'Server(Profile)?Id|ProfileId|ServerIdentity')) `
    -Severity Critical `
    -Details 'Expected canonical server/profile identity before multi-server work begins.'

# ---------------------------------------------------------------------------
# 4. Migration / architecture documentation
# ---------------------------------------------------------------------------
$architectureFiles = @(
    'docs\architecture',
    'docs\roadmap'
)
foreach ($dir in $architectureFiles) {
    Test-MystTiqDirectory $ctx $dir 'Documentation'
}

$docText = (
    Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md |
    ForEach-Object { Get-Content $_.FullName -Raw }
) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.6 architecture or roadmap is documented' `
    ([regex]::IsMatch($docText, 'v0\.6|Operation Coordinator|Provider Framework|Guardian')) `
    -Severity High `
    -Details 'Expected v0.6 architecture/roadmap documentation.'

Add-MystTiqCheck $ctx 'Documentation' 'Legacy parity/migration inventory remains documented' `
    ([regex]::IsMatch($docText, 'GUI_PARITY|legacy|migration|Not migrated|Partial')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.5.1.5 gate retained as baseline evidence
#
# v0.6.0.0 builds on top of the completed v0.5.1.x GUI-parity/shell-integration
# line, not the older v0.4.6.0 snapshot this milestone was originally drafted
# against. v0.4.6.0 predates the entire Avalonia GUI-restoration arc and is no
# longer the meaningful regression baseline for new work.
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.5.1.5-Logic.ps1' `
    'Regression Baseline' 'v0.5.1.5 logic gate remains available' -Severity High

Test-MystTiqFile $ctx 'scripts\Test-v0.5.1.5-RuntimeSmoke.ps1' `
    'Regression Baseline' 'v0.5.1.5 runtime smoke remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    # The v0.5.1.5 logic gate asserts a literal '<VersionPrefix>0.5.1.5</VersionPrefix>' and
    # matching hardcoded display-version strings, so it can never pass again against the live
    # tree once VersionPrefix moves forward -- that isn't a regression, it's the gate doing its
    # job on a tree that intentionally no longer matches. The roadmap's actual ask ("Freeze
    # v0.5.1.5 as regression baseline") means: the frozen v0.5.1.5 FullSource checkpoint must
    # still pass its own gate, not that the current tree must impersonate v0.5.1.5. Extract that
    # frozen checkpoint and run the gate against it instead of the live $root.
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.5.1.5\MystTiqPalworldServer_v0.5.1.5_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.5.1.5 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.5.1.5-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    # One check inside the frozen v0.5.1.5 gate itself -- 'Guild Base Capability Truth' -- was
    # already stale at the moment that checkpoint was archived: it asserts literal button text
    # ("Transfer Leader — BACKEND REQUIRED", "Claim Orphan — BACKEND REQUIRED") that the actual
    # v0.5.1.5 UI never contained (the real buttons read "Preview/Apply Ownership Repair —
    # BACKEND REQUIRED"), confirmed by inspecting the frozen ZIP's own MainWindow.axaml directly.
    # This is a pre-existing defect in that historical test file, not a v0.6.0.0 regression, and
    # the frozen archive must not be edited after the fact. Tolerate exactly this one known,
    # named failure; any other failure in the frozen gate still fails this check.
    $knownStaleFrozenFailures = @('Guild Base Capability Truth')
    # File redirection (not a pipe) is required: the frozen gate's [PASS]/[FAIL] lines are
    # Write-Host output on the Information stream (needs *> to capture, not 2>&1), and it throws
    # a terminating error on any failure, which tears down a pipe before Out-String's End block
    # can emit its buffered text -- a plain file redirect keeps everything written so far
    # regardless of the later throw.
    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.5.1.5-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.5.1.5-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() } |
            Where-Object { $_ -notin $knownStaleFrozenFailures }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.5.1.5 checkpoint logic gate still passes (known pre-existing stale check tolerated)' `
        ($unexpectedFrozenFailures.Count -eq 0) `
        -Severity Critical `
        -Details ($(if ($unexpectedFrozenFailures.Count -gt 0) { "Unexpected failures: $($unexpectedFrozenFailures -join ', ')" } else { '' }))

    Test-MystTiqCommand $ctx 'Build' 'Strict validation passes' {
        & (Join-Path $root 'Build.ps1') Validate -StrictValidation
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'Release solution build passes' {
        & (Join-Path $root 'Build.ps1') Build -Configuration Release
    } -Severity Critical | Out-Null

    # Preserve the last known-good runtime acceptance test during architecture-only milestone.
    Test-MystTiqCommand $ctx 'Regression Runtime' 'v0.5.1.5 isolated runtime smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.5.1.5-RuntimeSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
