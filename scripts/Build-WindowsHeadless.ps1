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

# v0.7.72.0: see the matching step in Build-AvaloniaDesktop.ps1 for the full rationale -- best-effort,
# never fails this build if the native proxy hasn't been built yet.
$nativeProxySource=Join-Path $root 'artifacts\native\MystTiqConsoleProxy.dll'
if(Test-Path $nativeProxySource -PathType Leaf){
    $nativeProxyDestDir=Join-Path $out 'native'
    New-Item -ItemType Directory -Force -Path $nativeProxyDestDir | Out-Null
    Copy-Item $nativeProxySource (Join-Path $nativeProxyDestDir 'MystTiqConsoleProxy.dll') -Force
    Write-Host "Native console proxy staged: $nativeProxyDestDir\MystTiqConsoleProxy.dll" -ForegroundColor Green
} else {
    Write-Host "Native console proxy not found at $nativeProxySource -- run scripts\Build-ConsoleProxy.ps1 first if you want console-capture install available. Skipping (non-fatal)." -ForegroundColor Yellow
}
