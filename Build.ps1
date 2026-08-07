[CmdletBinding()]
param(
    [ValidateSet('Build','Package','Installer','InstallerTools','Checksums','Release','All','Clean','Version','Validate')]
    [string]$Action = 'All',
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',
    [string]$ISCC,
    [switch]$SkipInstaller,
    [switch]$StrictValidation
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$scripts = Join-Path $root 'scripts'
$artifacts = Join-Path $root 'artifacts'

function Invoke-Script {
    param([string]$Name, [hashtable]$Parameters = @{})
    $path = Join-Path $scripts $Name
    if (-not (Test-Path $path -PathType Leaf)) { throw "Required build script was not found: $path" }
    & $path @Parameters
}

switch ($Action) {
    'Version' { Invoke-Script 'Get-ProjectVersion.ps1' }
    'Validate' { Invoke-Script 'Validate-Release.ps1' @{ Strict = $StrictValidation } }
    'InstallerTools' { Invoke-Script 'Install-InnoSetup.ps1' }
    'Clean' {
        Write-Host '==> Cleaning build artifacts...' -ForegroundColor Cyan
        Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue
        Get-ChildItem (Join-Path $root 'src') -Directory -Recurse -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -in @('bin','obj') } |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host 'Clean complete.' -ForegroundColor Green
    }
    'Build' { Invoke-Script 'Build.ps1' @{ Configuration = $Configuration } }
    'Package' {
        Invoke-Script 'Build.ps1' @{ Configuration = $Configuration }
        Invoke-Script 'Package-Portable.ps1'
        Invoke-Script 'Build-Checksums.ps1'
    }
    'Installer' {
        Invoke-Script 'Validate-Release.ps1' @{ Strict = $StrictValidation }
        Invoke-Script 'Build.ps1' @{ Configuration = $Configuration }
        Invoke-Script 'Build-Installer.ps1' @{ ISCC = $ISCC }
        Invoke-Script 'Build-Checksums.ps1'
    }
    'Checksums' { Invoke-Script 'Build-Checksums.ps1' }
    { $_ -in @('Release','All') } {
        Invoke-Script 'Build-Release.ps1' @{
            Configuration = $Configuration
            ISCC = $ISCC
            SkipInstaller = $SkipInstaller
            StrictValidation = $StrictValidation
        }
    }
}
