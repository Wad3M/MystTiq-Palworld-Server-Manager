[CmdletBinding()]
param(
    [ValidateSet('Build','Package','Installer','InstallerTools','Checksums','Release','All','Clean','Version','Validate','LinuxHeadless','WindowsHeadless','DesktopWindows','DesktopLinux','DeployDesktopLinux','LogicTests')]
    [string]$Action = 'All',
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',
    [string]$ISCC,
    [switch]$SkipInstaller,
    [switch]$StrictValidation,
    [switch]$NoGuiLaunch
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$scripts = Join-Path $root 'scripts'
$artifacts = Join-Path $root 'artifacts'


function Stop-ArtifactHostedProcesses {
    # Development publishes are auto-launched from artifacts. On Windows, a running
    # desktop/sidecar can lock its executable and prevent Clean from deleting the
    # publish tree. Stop only MystTiq processes whose executable is physically under
    # this repository's artifacts directory. Never stop PalServer or an installed
    # MystTiq service outside the development artifacts tree.
    if (-not (Test-Path $artifacts -PathType Container)) { return }

    $artifactPrefix = [System.IO.Path]::GetFullPath($artifacts).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    foreach ($process in Get-Process -ErrorAction SilentlyContinue) {
        if ($process.ProcessName -notin @('MystTiq.Desktop','mysttiq-server')) { continue }
        try {
            $processPath = $process.Path
            if ([string]::IsNullOrWhiteSpace($processPath)) { continue }
            $processPath = [System.IO.Path]::GetFullPath($processPath)
            if ($processPath.StartsWith($artifactPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                Write-Host "==> Stopping development artifact process $($process.ProcessName) (PID $($process.Id))..." -ForegroundColor DarkYellow
                Stop-Process -Id $process.Id -Force -ErrorAction Stop
                try { $process.WaitForExit(5000) | Out-Null } catch { }
            }
        }
        catch {
            Write-Warning "Could not inspect/stop development process PID $($process.Id): $($_.Exception.Message)"
        }
    }
}

function Remove-GeneratedDirectoryWithRetry {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return }
    $resolvedRoot = [System.IO.Path]::GetFullPath($root).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $leaf = Split-Path -Leaf $resolvedPath
    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase) -or $leaf -notin @('artifacts','bin','obj')) {
        throw "Refusing to remove an unrecognized generated directory: $resolvedPath"
    }
    for ($attempt = 1; $attempt -le 8; $attempt++) {
        try {
            Remove-Item -LiteralPath $resolvedPath -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq 8) { throw }
            Start-Sleep -Milliseconds (250 * $attempt)
        }
    }
}

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
        Stop-ArtifactHostedProcesses
        if (Test-Path $artifacts) {
            Remove-GeneratedDirectoryWithRetry -Path $artifacts
        }
        $generatedDirectories = @(Get-ChildItem (Join-Path $root 'src') -Directory -Recurse -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -in @('bin','obj') } |
            Sort-Object { $_.FullName.Length } -Descending)
        foreach ($directory in $generatedDirectories) {
            if (Test-Path -LiteralPath $directory.FullName -PathType Container) {
                Remove-GeneratedDirectoryWithRetry -Path $directory.FullName
            }
        }
        if (Test-Path $artifacts) {
            throw 'Build clean failed: artifacts directory still exists. Close any process using files under artifacts and retry.'
        }
        Write-Host 'Clean complete.' -ForegroundColor Green
    }
    'Build' { Invoke-Script 'Build.ps1' @{ Configuration = $Configuration } }
    'LinuxHeadless' { Invoke-Script 'Build-LinuxHeadless.ps1' @{ Configuration = $Configuration } }
    'WindowsHeadless' { Invoke-Script 'Build-WindowsHeadless.ps1' @{ Configuration = $Configuration } }
    'DesktopWindows' { Invoke-Script 'Build-AvaloniaDesktop.ps1' @{ Configuration = $Configuration; Runtime = 'win-x64'; Publish = $true; NoLaunch = $NoGuiLaunch } }
    'DesktopLinux' { Invoke-Script 'Build-AvaloniaDesktop.ps1' @{ Configuration = $Configuration; Runtime = 'linux-x64'; Publish = $true; NoLaunch = $true } }
    'DeployDesktopLinux' { Invoke-Script 'Deploy-Test-MystTiqDesktopLinux.ps1' }
    'LogicTests' { $v = & (Join-Path $scripts 'Get-ProjectVersion.ps1'); Invoke-Script "Test-v$v-Logic.ps1" @{ ProjectRoot = $root; ExportJson = $true } }
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
        $v = & (Join-Path $scripts 'Get-ProjectVersion.ps1')
        Invoke-Script "Test-v$v-Logic.ps1" @{ ProjectRoot = $root; ExportJson = $true }
    }
}
