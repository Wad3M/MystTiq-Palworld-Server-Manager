[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [switch]$ExportJson = $true
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
$build = Join-Path $root 'Build.ps1'
$getVersion = Join-Path $root 'scripts\Get-ProjectVersion.ps1'

if (-not (Test-Path $build -PathType Leaf)) { throw "Build.ps1 not found: $build" }
if (-not (Test-Path $getVersion -PathType Leaf)) { throw "Version helper not found: $getVersion" }

Write-Host '==> Unblocking PowerShell scripts...' -ForegroundColor Cyan
Get-ChildItem $root -Recurse -Filter *.ps1 -File | Unblock-File

$version = & $getVersion
$logicTest = Join-Path $root "scripts\Test-v$version-Logic.ps1"
if (-not (Test-Path $logicTest -PathType Leaf)) {
    throw "Current version logic test is missing: $logicTest"
}

Write-Host "==> MystTiq v$version complete local test gate" -ForegroundColor Cyan
& $build Clean

& $build Validate -StrictValidation

$logicParameters = @{ ProjectRoot = $root; RunBuild = $true }
if ($ExportJson) { $logicParameters.ExportJson = $true }
& $logicTest @logicParameters

Write-Host "MystTiq v$version complete local test gate passed." -ForegroundColor Green
