[CmdletBinding()]
param([string]$Version='0.2.12')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$project=Join-Path $root 'src\PalworldManager\PalworldManager.csproj'
$artifacts=Join-Path $root 'artifacts'
$publish=Join-Path $artifacts "publish\win-x64"
$stage=Join-Path $artifacts "MystTiqPalworldServer-v$Version-win-x64-portable"
$zip="$stage.zip"
Remove-Item $publish,$stage,$zip -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publish,$stage -Force | Out-Null
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o $publish
Copy-Item "$publish\*" $stage -Recurse -Force
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $stage 'LICENSE.txt')
Copy-Item (Join-Path $root 'release-notes\v0.2.12.md') (Join-Path $stage 'README.txt')
@'
@echo off
cd /d "%~dp0"
start "" "MystTiqPalworldServer.exe"
'@ | Set-Content (Join-Path $stage 'Start-MystTiq.cmd') -Encoding ascii
Compress-Archive -Path "$stage\*" -DestinationPath $zip -CompressionLevel Optimal
$hash=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $zip -Leaf)" | Set-Content (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Portable package: $zip"
