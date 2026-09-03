[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [string]$DownloadsPath = (Join-Path $HOME 'Downloads'),
    [string]$TargetPath = 'C:\GameServers\MystTiqPalLinux',
    [switch]$SkipBuild,
    [switch]$SkipValidate
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Resolve-LatestFullSourceZip {
    param([string]$Path)
    if (-not (Test-Path $Path -PathType Container)) { throw "Downloads folder was not found: $Path" }
    $packages = @(Get-ChildItem -Path $Path -File -Filter 'MystTiqPalworldServer_v*_FullSource*.zip' |
        Sort-Object LastWriteTimeUtc -Descending)
    if ($packages.Count -eq 0) { throw "No MystTiq FullSource ZIP was found in $Path" }
    return $packages[0]
}

function Assert-SafeArchiveEntries {
    param([string]$ZipPath)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('\','/')
            if ($name.StartsWith('/') -or $name -match '(^|/)\.\.(/|$)' -or $name -match '^[A-Za-z]:') {
                throw "Unsafe archive entry rejected: $($entry.FullName)"
            }
        }
    }
    finally { $archive.Dispose() }
}

function Stop-TargetWorkspaceProcesses {
    param([Parameter(Mandatory)][string]$TargetRoot)
    if (-not (Test-Path -LiteralPath $TargetRoot -PathType Container)) { return }
    $targetPrefix = [IO.Path]::GetFullPath($TargetRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    for ($sweep = 1; $sweep -le 3; $sweep++) {
        $stoppedAny = $false
        foreach ($process in Get-Process -ErrorAction SilentlyContinue) {
            if ($process.ProcessName -notin @('MystTiq.Desktop','mysttiq-server')) { continue }
            try {
                $processPath = $process.Path
                if ([string]::IsNullOrWhiteSpace($processPath)) { continue }
                $processPath = [IO.Path]::GetFullPath($processPath)
                if (-not $processPath.StartsWith($targetPrefix, [StringComparison]::OrdinalIgnoreCase)) { continue }
                Write-Host "==> Closing installed workspace process $($process.ProcessName) (PID $($process.Id)) before update..." -ForegroundColor DarkYellow
                Stop-Process -Id $process.Id -Force -ErrorAction Stop
                try { $process.WaitForExit(5000) | Out-Null } catch { }
                $stoppedAny = $true
            }
            catch { Write-Warning "Could not inspect/stop installed workspace process PID $($process.Id): $($_.Exception.Message)" }
        }
        if (-not $stoppedAny) { break }
        Start-Sleep -Milliseconds 500
    }
}

$package = Resolve-LatestFullSourceZip -Path $DownloadsPath
Write-Host "==> Latest FullSource package: $($package.FullName)" -ForegroundColor Cyan
Assert-SafeArchiveEntries -ZipPath $package.FullName

$stage = Join-Path ([IO.Path]::GetTempPath()) ("MystTiqFullSource_" + [Guid]::NewGuid().ToString('N'))
New-Item -Path $stage -ItemType Directory -Force | Out-Null
try {
    Expand-Archive -LiteralPath $package.FullName -DestinationPath $stage -Force

    $sourceRoot = $stage
    if (-not (Test-Path (Join-Path $sourceRoot 'Build.ps1'))) {
        $children = @(Get-ChildItem -Path $stage -Directory -Force)
        if ($children.Count -eq 1 -and (Test-Path (Join-Path $children[0].FullName 'Build.ps1'))) { $sourceRoot = $children[0].FullName }
    }

    foreach ($required in @('Build.ps1','Directory.Build.props','src','scripts')) {
        if (-not (Test-Path (Join-Path $sourceRoot $required))) { throw "Selected package is not a valid MystTiq FullSource archive; missing $required" }
    }

    # Validate the staged version identity before touching the installed tree.
    $stagedVersion = & (Join-Path $sourceRoot 'scripts\Get-ProjectVersion.ps1')
    $stagedLogic = Join-Path $sourceRoot "scripts\Test-v$stagedVersion-Logic.ps1"
    if (-not (Test-Path $stagedLogic -PathType Leaf)) { throw "Staged package is missing its version logic test: $stagedLogic" }
    Write-Host "==> Staged MystTiq source v$stagedVersion passed package identity checks." -ForegroundColor Green

    if (Test-Path $TargetPath -PathType Container) {
        Stop-TargetWorkspaceProcesses -TargetRoot $TargetPath
        $cleanScript = Join-Path $TargetPath 'Build.ps1'
        if (Test-Path $cleanScript -PathType Leaf) {
            Write-Host '==> Running current Build.ps1 Clean to stop artifact-hosted GUI/backend processes...' -ForegroundColor Cyan
            & $cleanScript Clean
        }
    } else { New-Item -Path $TargetPath -ItemType Directory -Force | Out-Null }

    Set-Location ([IO.Path]::GetTempPath())
    Write-Host "==> Clearing target source tree: $TargetPath" -ForegroundColor Cyan
    Get-ChildItem -LiteralPath $TargetPath -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction Stop
    if (@(Get-ChildItem -LiteralPath $TargetPath -Force -ErrorAction SilentlyContinue).Count -ne 0) { throw "Target directory was not fully cleared: $TargetPath" }

    Write-Host "==> Installing $($package.Name)..." -ForegroundColor Cyan
    Get-ChildItem -LiteralPath $sourceRoot -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $TargetPath -Recurse -Force }
    Get-ChildItem -LiteralPath $TargetPath -Recurse -Filter '*.ps1' -ErrorAction SilentlyContinue | Unblock-File

    $version = & (Join-Path $TargetPath 'scripts\Get-ProjectVersion.ps1')
    if ($version -ne $stagedVersion) { throw "Installed version mismatch. Staged v$stagedVersion but installed v$version." }
    Write-Host "==> Installed MystTiq source v$version" -ForegroundColor Green

    Set-Location $TargetPath
    if (-not $SkipBuild) {
        Write-Host '==> Running the complete current-release gate (clean, strict validation, logic tests, Windows/Linux builds, runtime smoke)...' -ForegroundColor Cyan
        & (Join-Path $TargetPath 'scripts\Test-CurrentRelease.ps1') -ProjectRoot $TargetPath -ExportJson
    }
    elseif (-not $SkipValidate) {
        Write-Host '==> Running strict release validation...' -ForegroundColor Cyan
        & (Join-Path $TargetPath 'Build.ps1') Validate -StrictValidation
    }

    Write-Host "Update complete: $TargetPath (v$version)" -ForegroundColor Green
}
finally {
    try { Set-Location ([IO.Path]::GetTempPath()) } catch { }
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue }
}
