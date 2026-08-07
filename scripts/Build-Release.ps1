[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$ISCC,
    [switch]$SkipInstaller,
    [switch]$StrictValidation
)

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
    Invoke-Step "Building $Configuration win-x64" {
        & (Join-Path $PSScriptRoot 'Build.ps1') -Configuration $Configuration
    }
    Invoke-Step "Creating portable package" {
        & (Join-Path $PSScriptRoot 'Package-Portable.ps1') -Version $version
    }
    if ($SkipInstaller) {
        Write-Warning 'Installer generation was explicitly skipped.'
    } else {
        Invoke-Step 'Creating Windows installer' {
            & (Join-Path $PSScriptRoot 'Build-Installer.ps1') -Version $version -ISCC $ISCC -SkipPackage
        }
    }
    Invoke-Step 'Generating SHA256 checksums' {
        & (Join-Path $PSScriptRoot 'Build-Checksums.ps1')
        & (Join-Path $PSScriptRoot 'Build-Checksums.ps1') -Verify
    }
    $stopwatch.Stop()
    Write-Host "`nRelease v$version completed in $([math]::Round($stopwatch.Elapsed.TotalSeconds, 1)) seconds." -ForegroundColor Green
    Write-Host "Artifacts: $(Join-Path $root 'artifacts')" -ForegroundColor Green
} catch {
    $stopwatch.Stop()
    Write-Error "Release build failed after $([math]::Round($stopwatch.Elapsed.TotalSeconds, 1)) seconds: $($_.Exception.Message)"
    exit 1
}
