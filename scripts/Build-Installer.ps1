[CmdletBinding()]
param(
    [string]$Version,
    [string]$ISCC,
    [switch]$SkipPackage
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$iss = Join-Path $root 'installer\MystTiqPalworldServer.iss'
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = & (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1') }

function Add-Candidate {
    param([System.Collections.Generic.List[string]]$List, [string]$Path)
    if (-not [string]::IsNullOrWhiteSpace($Path) -and -not $List.Contains($Path)) { $List.Add($Path) }
}

function Resolve-Iscc {
    param([string]$RequestedPath)
    $candidates = [System.Collections.Generic.List[string]]::new()

    Add-Candidate $candidates $RequestedPath
    if ($env:INNO_SETUP_HOME) { Add-Candidate $candidates (Join-Path $env:INNO_SETUP_HOME 'ISCC.exe') }

    $pathCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($pathCommand) { Add-Candidate $candidates $pathCommand.Source }

    foreach ($versionNumber in @('7','6')) {
        Add-Candidate $candidates "C:\Program Files (x86)\Inno Setup $versionNumber\ISCC.exe"
        Add-Candidate $candidates "C:\Program Files\Inno Setup $versionNumber\ISCC.exe"
        if ($env:LOCALAPPDATA) { Add-Candidate $candidates (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup $versionNumber\ISCC.exe") }
    }

    $registryPaths = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 7_is1',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 7_is1',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 7_is1',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1'
    )
    foreach ($key in $registryPaths) {
        try {
            $entry = Get-ItemProperty -Path $key -ErrorAction Stop
            if ($entry.InstallLocation) { Add-Candidate $candidates (Join-Path $entry.InstallLocation 'ISCC.exe') }
        } catch { }
    }

    foreach ($appPath in @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\ISCC.exe',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\ISCC.exe'
    )) {
        try {
            $entry = Get-ItemProperty -Path $appPath -ErrorAction Stop
            Add-Candidate $candidates $entry.'(default)'
        } catch { }
    }

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate -PathType Leaf) { return (Resolve-Path $candidate).Path }
    }
    return $null
}

if (-not (Test-Path $iss -PathType Leaf)) { throw "Installer definition was not found: $iss" }
$isccPath = Resolve-Iscc $ISCC
if (-not $isccPath) {
    throw "Inno Setup 6 or 7 was not found by explicit path, INNO_SETUP_HOME, PATH, standard install folders, or registry. Run '.\Build.ps1 InstallerTools' or pass -ISCC <path>."
}
Write-Host "Using Inno Setup compiler: $isccPath" -ForegroundColor DarkCyan

if (-not $SkipPackage) { & (Join-Path $PSScriptRoot 'Package-Portable.ps1') -Version $Version }
$publishPath = Join-Path $artifacts 'publish\win-x64'
if (-not (Test-Path (Join-Path $publishPath 'MystTiqPalworldServer.exe') -PathType Leaf)) {
    throw "Installer source publish output is missing: $publishPath"
}

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
& $isccPath "/DMyAppVersion=$Version" "/O$artifacts" $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed with exit code $LASTEXITCODE." }
$installer = Join-Path $artifacts "MystTiqPalworldServer-v$Version-win-x64-setup.exe"
if (-not (Test-Path $installer -PathType Leaf)) { throw "Expected installer was not produced: $installer" }
Write-Host "Installer: $installer" -ForegroundColor Green
