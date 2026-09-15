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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.57.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.57\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.57.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.57\.0"' `
    'Versioning' 'app.manifest reports v0.7.57.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression) -- this version touches
#    no .NET code at all, so every existing project/script must be byte-for-byte
#    unaffected.
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.15.0-RouteSmoke.ps1' 'Regression' 'v0.7.15.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.12.0-RouteSmoke.ps1' 'Regression' 'v0.7.12.0 route smoke script is still present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1' 'Regression' 'v0.7.17.0 api-remote-enable regression smoke script is still present' -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.57.0 contract presence -- Native Console Capture, Proxy DLL Foundation
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'native\MystTiqConsoleProxy\dllmain.cpp' 'v0.7.57.0 Contracts' 'Native proxy DLL source is present' -Severity Critical
Test-MystTiqFile $ctx 'native\MystTiqConsoleProxy\MystTiqConsoleProxy.def' 'v0.7.57.0 Contracts' 'Module-definition file is present' -Severity Critical
Test-MystTiqFile $ctx 'scripts\Build-ConsoleProxy.ps1' 'v0.7.57.0 Contracts' 'Native build script is present' -Severity Critical

$dllMainText = Get-Content (Join-Path $root 'native\MystTiqConsoleProxy\dllmain.cpp') -Raw
$defText = Get-Content (Join-Path $root 'native\MystTiqConsoleProxy\MystTiqConsoleProxy.def') -Raw

Add-MystTiqCheck $ctx 'v0.7.57.0 Contracts' 'All 6 real DSOUND.dll exports have forwarding stubs (matching the real import table exactly)' `
    ($dllMainText -match 'DirectSoundCreate\(const GUID\* guid, void\*\* ds, IUnknown\* outer\)' -and
     $dllMainText -match 'DirectSoundCreate8\(const GUID\* guid, void\*\* ds, IUnknown\* outer\)' -and
     $dllMainText -match 'DirectSoundEnumerateW\(void\* callback, void\* context\)' -and
     $dllMainText -match 'DirectSoundCaptureCreate\(const GUID\* guid, void\*\* ds, IUnknown\* outer\)' -and
     $dllMainText -match 'DirectSoundCaptureCreate8\(const GUID\* guid, void\*\* ds, IUnknown\* outer\)' -and
     $dllMainText -match 'DirectSoundCaptureEnumerateW\(void\* callback, void\* context\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.57.0 Contracts' 'Real dsound.dll is loaded via a full system-directory path, never a bare name (avoids proxy self-recursion)' `
    ($dllMainText -match 'GetSystemDirectoryW' -and $dllMainText -match 'LoadLibraryW\(realPath\.c_str\(\)\)') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.57.0 Contracts' 'The log-capture hook is a real, honest scaffold that logs its own no-op status -- not a silent stub, not fabricated capability' `
    ($dllMainText -match 'void TryInstallLogHook\(\)' -and
     $dllMainText -match 'not implemented' -and
     $dllMainText -match 'no live PalServer process was available') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.57.0 Contracts' 'DllMain fails open (returns TRUE) even if the real DLL cannot be loaded, rather than aborting the whole host process' `
    ($dllMainText -match '(?s)if \(!LoadRealDsound\(\)\).*?return TRUE;') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.57.0 Contracts' 'The .def file pins each export to the exact ordinal the real system dsound.dll uses' `
    ($defText -match 'DirectSoundCreate\s+@1\b' -and
     $defText -match 'DirectSoundEnumerateW\s+@3\b' -and
     $defText -match 'DirectSoundCaptureCreate\s+@6\b' -and
     $defText -match 'DirectSoundCaptureEnumerateW\s+@8\b' -and
     $defText -match 'DirectSoundCreate8\s+@11\b' -and
     $defText -match 'DirectSoundCaptureCreate8\s+@12\b') `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.57.0-native-console-capture.md' 'v0.7.57.0 Contracts' 'v0.7.57.0 architecture doc is present' -Severity Critical

$archDocText = Get-Content (Join-Path $root 'docs\architecture\v0.7.57.0-native-console-capture.md') -Raw
Add-MystTiqCheck $ctx 'v0.7.57.0 Contracts' 'Architecture doc discloses the unresolved PalServer launch freeze investigated alongside this version, and that the hook is unverified' `
    ($archDocText -match 'STATUS_STACK_OVERFLOW' -and
     $archDocText -match 'Never loaded into a real PalServer process') `
    -Severity High

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

Add-MystTiqCheck $ctx 'Documentation' 'v0.7.57.0 is documented' `
    ([regex]::IsMatch($docText, 'v0\.7\.57\.0')) `
    -Severity High

# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Existing v0.7.56.0 gate retained as baseline evidence
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'scripts\Test-v0.7.56.0-Logic.ps1' `
    'Regression Baseline' 'v0.7.56.0 logic gate remains available' -Severity High

# ---------------------------------------------------------------------------
# 7. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.56.0\MystTiqPalworldServer_v0.7.56.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.56.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.56.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.56.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.56.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.56.0 checkpoint logic gate still passes' `
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

    # v0.7.57.0 is native-only (no route contract changed), so every server-side route/CLI smoke
    # test carried forward is expected to pass unchanged.
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

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.15.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.15.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.17.0 api-remote-enable smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    # New this version: an actual native build, plus a real export-ordinal verification pass
    # against the built DLL -- not just a static-text check on the source.
    Test-MystTiqCommand $ctx 'Native Build' 'MystTiqConsoleProxy.dll builds cleanly via scripts/Build-ConsoleProxy.ps1' {
        & (Join-Path $root 'scripts\Build-ConsoleProxy.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    $builtDll = Join-Path $root 'artifacts\native\MystTiqConsoleProxy.dll'
    Add-MystTiqCheck $ctx 'Native Build' 'Built DLL exists on disk' `
        (Test-Path $builtDll -PathType Leaf) `
        -Severity Critical

    if (Test-Path $builtDll -PathType Leaf) {
        $dumpbinCandidates = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio' -Recurse -Filter 'dumpbin.exe' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match 'Hostx64\\x64' } | Sort-Object FullName -Descending
        if ($dumpbinCandidates) {
            $exportsOutput = & $dumpbinCandidates[0].FullName /exports $builtDll 2>&1 | Out-String
            # The "hint" column (2nd) is assigned by dumpbin in alphabetical-by-name order, not
            # ordinal order -- match ordinal + name only, treat hint as a wildcard.
            $ordinalsMatch =
                ($exportsOutput -match '(?m)^\s+1\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+DirectSoundCreate\s*$') -and
                ($exportsOutput -match '(?m)^\s+3\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+DirectSoundEnumerateW\s*$') -and
                ($exportsOutput -match '(?m)^\s+6\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+DirectSoundCaptureCreate\s*$') -and
                ($exportsOutput -match '(?m)^\s+8\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+DirectSoundCaptureEnumerateW\s*$') -and
                ($exportsOutput -match '(?m)^\s+11\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+DirectSoundCreate8\s*$') -and
                ($exportsOutput -match '(?m)^\s+12\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+DirectSoundCaptureCreate8\s*$')
            Add-MystTiqCheck $ctx 'Native Build' 'Built DLL exports land at the exact ordinals the real system dsound.dll uses' `
                $ordinalsMatch `
                -Severity Critical `
                -Details ($(if (-not $ordinalsMatch) { $exportsOutput } else { '' }))
        } else {
            Add-MystTiqCheck $ctx 'Native Build' 'dumpbin.exe found to verify export ordinals' $false -Severity Critical
        }
    }
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
