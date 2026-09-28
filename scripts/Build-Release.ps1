# MystTiq v0.8.26.0: file reviewed for this release (2026-09-27).
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$StrictValidation,
    # The full release gate (-RunBuild) takes about 50 minutes; skip it only when it has just passed.
    [switch]$SkipGate
)

# v0.8.26.0: the local equivalent of the GitHub release workflow. Validate, build, run the release gate, then package
# the self-contained Windows and Linux downloads (scripts/Package-GitHubRelease.ps1: the Avalonia desktop with the
# headless service beside it) and write SHA256SUMS.txt. The legacy WPF build, portable package and installer are gone.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = & (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1')
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)
    Write-Host "`n==> $Name" -ForegroundColor Cyan
    & $Action
}

try {
    Invoke-Step "Validating v$version release candidate" {
        & (Join-Path $PSScriptRoot 'Validate-Release.ps1') -Strict:$StrictValidation
    }
    Invoke-Step "Building $Configuration" {
        & (Join-Path $PSScriptRoot 'Build.ps1') -Configuration $Configuration
    }
    if ($SkipGate) {
        Write-Warning 'The release gate was explicitly skipped.'
    } else {
        Invoke-Step "Running the v$version release gate" {
            & (Join-Path $PSScriptRoot 'Build-AvaloniaDesktop.ps1') -Configuration $Configuration -Runtime 'win-x64' -Publish -NoLaunch
            & (Join-Path $PSScriptRoot "Test-v$version-Logic.ps1") -ProjectRoot $root -RunBuild
        }
    }
    foreach ($runtime in 'win-x64', 'linux-x64') {
        Invoke-Step "Packaging $runtime" {
            & (Join-Path $PSScriptRoot 'Package-GitHubRelease.ps1') -Runtime $runtime
        }
    }
    Invoke-Step 'Generating SHA256 checksums' {
        & (Join-Path $PSScriptRoot 'Build-Checksums.ps1') -Include '*.zip'
        & (Join-Path $PSScriptRoot 'Build-Checksums.ps1') -Include '*.zip' -Verify
    }
    $stopwatch.Stop()
    Write-Host "`nRelease v$version completed in $([math]::Round($stopwatch.Elapsed.TotalSeconds, 1)) seconds." -ForegroundColor Green
    Write-Host "Artifacts: $(Join-Path $root 'artifacts')" -ForegroundColor Green
} catch {
    $stopwatch.Stop()
    Write-Error "Release build failed after $([math]::Round($stopwatch.Elapsed.TotalSeconds, 1)) seconds: $($_.Exception.Message)"
    exit 1
}
