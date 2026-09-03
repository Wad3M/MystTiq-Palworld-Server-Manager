[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Release')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$project=Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj'
$out=Join-Path $root 'artifacts\publish\headless-win-x64'
$version=& (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1')
Write-Host "==> Publishing MystTiq persistent host v$version for win-x64" -ForegroundColor Cyan
dotnet publish $project -c $Configuration -r win-x64 --self-contained false -o $out
if($LASTEXITCODE -ne 0){ throw 'Windows persistent host publish failed.' }
Write-Host "Windows persistent host publish: $out" -ForegroundColor Green
