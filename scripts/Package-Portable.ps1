[CmdletBinding()]
param([string]$Version)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if([string]::IsNullOrWhiteSpace($Version)){ $Version = & (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1') }
$project=Join-Path $root 'src\PalworldManager\PalworldManager.csproj'
$artifacts=Join-Path $root 'artifacts'
$publish=Join-Path $artifacts "publish\win-x64"
$stage=Join-Path $artifacts "MystTiqPalworldServer-v$Version-win-x64-portable"
$zip="$stage.zip"
$releaseNotes=Join-Path $root "release-notes\v$Version.md"
if(-not (Test-Path $releaseNotes)){ throw "Release notes not found for v${Version}: $releaseNotes" }
Remove-Item $publish,$stage,$zip -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publish,$stage -Force | Out-Null
& dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
Copy-Item "$publish\*" $stage -Recurse -Force
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $stage 'LICENSE.txt')
Copy-Item $releaseNotes (Join-Path $stage 'README.txt')
New-Item -ItemType File -Path (Join-Path $stage 'portable.mode') -Force | Out-Null
$dataRoot = Join-Path $stage 'Data'
$workspaceRoot = Join-Path $stage 'Workspace'
$workspaceFolders = @(
    (Join-Path $workspaceRoot 'Servers'),
    (Join-Path $workspaceRoot 'Servers\Palworld'),
    (Join-Path $workspaceRoot 'SteamCMD'),
    (Join-Path $workspaceRoot 'Backups'),
    (Join-Path $workspaceRoot 'Downloads'),
    (Join-Path $workspaceRoot 'Exports')
)
New-Item -ItemType Directory -Path $dataRoot,$workspaceRoot -Force | Out-Null
New-Item -ItemType Directory -Path $workspaceFolders -Force | Out-Null
foreach($folder in $workspaceFolders){ New-Item -ItemType File -Path (Join-Path $folder '.keep') -Force | Out-Null }
@"
MystTiq Portable Workspace
==========================

Place or install the Palworld dedicated server anywhere under:
  Workspace\Servers

Place SteamCMD anywhere under:
  Workspace\SteamCMD

On first launch, MystTiq searches those folders for PalServer.exe and steamcmd.exe.
Detected paths are stored in Data\Settings and remain local to this portable copy.

Backups default to Workspace\Backups. Downloads and exports remain inside this workspace.
You may still select external server, SteamCMD, or backup folders in Manager Settings.
"@ | Set-Content (Join-Path $workspaceRoot 'README.txt') -Encoding utf8
@"
This folder contains MystTiq portable settings, logs, cache, notifications, and diagnostics.
It is created and maintained by the application.
"@ | Set-Content (Join-Path $dataRoot 'README.txt') -Encoding utf8
@'
@echo off
cd /d "%~dp0"
start "" "MystTiqPalworldServer.exe"
'@ | Set-Content (Join-Path $stage 'Start-MystTiq.cmd') -Encoding ascii
Compress-Archive -Path "$stage\*" -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Version: $Version"
Write-Host "Portable package: $zip"
