[CmdletBinding()]
param(
    [ValidateSet('Build','Package','Installer','All','Clean','Version')]
    [string]$Action = 'All',
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',
    [string]$ISCC = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$scripts = Join-Path $root 'scripts'
$artifacts = Join-Path $root 'artifacts'
$versionScript = Join-Path $scripts 'Get-ProjectVersion.ps1'
$buildScript = Join-Path $scripts 'Build.ps1'
$packageScript = Join-Path $scripts 'Package-Portable.ps1'
$installerScript = Join-Path $scripts 'Build-Installer.ps1'

function Get-MystTiqVersion {
    & $versionScript
}

function Invoke-Build {
    Write-Host "==> Building MystTiq ($Configuration)..." -ForegroundColor Cyan
    & $buildScript -Configuration $Configuration
}

function Invoke-Package {
    $version = Get-MystTiqVersion
    Write-Host "==> Packaging portable v$version..." -ForegroundColor Cyan
    & $packageScript -Version $version
}

function Invoke-Installer {
    $version = Get-MystTiqVersion
    Write-Host "==> Building installer v$version..." -ForegroundColor Cyan
    & $installerScript -Version $version -ISCC $ISCC
}

switch ($Action) {
    'Version' {
        Get-MystTiqVersion
    }
    'Clean' {
        Write-Host '==> Cleaning build artifacts...' -ForegroundColor Cyan
        Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue
        Get-ChildItem (Join-Path $root 'src') -Directory -Recurse |
            Where-Object { $_.Name -in @('bin','obj') } |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host 'Clean complete.' -ForegroundColor Green
    }
    'Build' {
        Invoke-Build
    }
    'Package' {
        Invoke-Build
        Invoke-Package
    }
    'Installer' {
        Invoke-Build
        Invoke-Installer
    }
    'All' {
        Invoke-Build
        Invoke-Package
        if (Test-Path $ISCC) {
            Invoke-Installer
        }
        else {
            Write-Warning "Inno Setup was not found at '$ISCC'. Portable package completed; installer was skipped."
        }
        Write-Host '==> Release preparation complete.' -ForegroundColor Green
    }
}
