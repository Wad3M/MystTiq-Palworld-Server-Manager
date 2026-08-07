[CmdletBinding()]
param(
    [string]$ArtifactsPath,
    [string[]]$Include = @('*.zip','*.exe'),
    [string]$OutputFile = 'SHA256SUMS.txt',
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ArtifactsPath)) { $ArtifactsPath = Join-Path $root 'artifacts' }
if (-not (Test-Path $ArtifactsPath -PathType Container)) { throw "Artifacts directory was not found: $ArtifactsPath" }

$outputPath = Join-Path $ArtifactsPath $OutputFile
$files = @(
    foreach ($pattern in $Include) {
        Get-ChildItem -Path $ArtifactsPath -File -Filter $pattern -ErrorAction SilentlyContinue
    }
) | Sort-Object FullName -Unique

if ($files.Count -eq 0) { throw "No release assets were found in $ArtifactsPath for: $($Include -join ', ')" }

if ($Verify) {
    if (-not (Test-Path $outputPath -PathType Leaf)) { throw "Checksum manifest was not found: $outputPath" }
    $expected = @{}
    foreach ($line in Get-Content $outputPath) {
        if ($line -match '^([A-Fa-f0-9]{64})\s+\*?(.+)$') { $expected[$matches[2].Trim()] = $matches[1].ToLowerInvariant() }
    }
    $failures = @()
    foreach ($file in $files) {
        $name = $file.Name
        if (-not $expected.ContainsKey($name)) { $failures += "Missing checksum entry: $name"; continue }
        $actual = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $expected[$name]) { $failures += "Checksum mismatch: $name" }
    }
    if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
    Write-Host "Verified $($files.Count) release asset checksum(s)." -ForegroundColor Green
    return
}

$lines = foreach ($file in $files) {
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($file.Name)"
}
$lines | Set-Content $outputPath -Encoding ascii
Write-Host "Checksums: $outputPath" -ForegroundColor Green
