[CmdletBinding()]
param([string]$ProjectRoot='.')
$ErrorActionPreference='Stop'
$root=(Resolve-Path $ProjectRoot).Path
$previousPatch='0.3.1.'+'8'
$obsolete=@(
 ('scripts\Test-v{0}-Logic.ps1' -f $previousPatch),
 ('scripts\Apply-v{0}-Cleanup.ps1' -f $previousPatch),
 ('scripts\Test-v{0}-LinuxAcceptance.sh' -f $previousPatch),
 ('scripts\Test-v{0}-ProductionReadiness.sh' -f $previousPatch)
)
foreach($relative in $obsolete){
 $path=Join-Path $root $relative
 if(Test-Path $path){Remove-Item $path -Force;Write-Host "Removed superseded file: $relative" -ForegroundColor Yellow}
}
Write-Host 'v0.3.1.9 cleanup complete.' -ForegroundColor Green
