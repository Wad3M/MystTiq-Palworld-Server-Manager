# MystTiq v1.1.5.0: file reviewed for this release (2026-10-23).
#requires -Version 7.0
# v1.1.5.0 (roadmap W-1): builds the Windows setup (MystTiqPalworldServer_v<version>_Windows-x64.msi) from a packaged app
# folder (Package-GitHubRelease.ps1's, signed or not). WiX 5 comes from NuGet through the .NET SDK.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AppFolder,
    [string]$OutputFolder
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = & (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1')
$app = (Resolve-Path -LiteralPath $AppFolder).Path
if (-not (Test-Path -LiteralPath (Join-Path $app 'MystTiq.Desktop.exe') -PathType Leaf)) { throw "MystTiq.Desktop.exe is not in $app" }
if (-not (Test-Path -LiteralPath (Join-Path $app 'headless\mysttiq-server.exe') -PathType Leaf)) { throw "headless\mysttiq-server.exe is not in $app" }
if (@(Get-ChildItem -LiteralPath $app -Recurse -File | Where-Object { $_.BaseName -eq 'createdump' }).Count -gt 0) { throw "The app folder holds .NET's createdump helper." }
# A setup never ships the portable marker (a portable copy would ignore AppData and ProgramData).
if (Test-Path -LiteralPath (Join-Path $app 'portable.txt')) { throw 'The app folder holds portable.txt; the setup is built from the regular app folder.' }
$out = if ($OutputFolder) { $OutputFolder } else { Join-Path $root 'artifacts' }
New-Item -ItemType Directory -Path $out -Force | Out-Null
$msi = Join-Path $out "MystTiqPalworldServer_v${version}_Windows-x64.msi"
if (Test-Path -LiteralPath $msi) { throw "Setup already exists: $msi" }

$project = Join-Path $root 'installer\MystTiq.Installer.wixproj'
$build = Join-Path $root ("artifacts\installer-" + [guid]::NewGuid().ToString('N'))
& dotnet build $project -c Release -nologo -v q "-p:AppFolder=$app" "-p:ProductVersion=$version" "-p:OutputPath=$build\" "-p:IntermediateOutputPath=$build\obj\"
if ($LASTEXITCODE -ne 0) { throw 'The setup did not build.' }
$built = Get-ChildItem -LiteralPath $build -Recurse -Filter 'MystTiq.msi' | Select-Object -First 1
if (-not $built) { throw "MystTiq.msi was not produced under $build" }
Move-Item -LiteralPath $built.FullName -Destination $msi
Remove-Item -LiteralPath $build -Recurse -Force -ErrorAction SilentlyContinue
# NuGet's restore writes installer\obj beside the project; it is a build output, not part of the source.
foreach ($generated in 'obj', 'bin') { Remove-Item -LiteralPath (Join-Path $root "installer\$generated") -Recurse -Force -ErrorAction SilentlyContinue }
Write-Host "Setup: $msi"
