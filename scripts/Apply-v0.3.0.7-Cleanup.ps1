[CmdletBinding()]
param([string]$ProjectRoot=".")
$ErrorActionPreference="Stop"
$root=(Resolve-Path $ProjectRoot).Path

$previous = '0.3.0.' + '6'
$obsolete = @(
    ('scripts\Test-v{0}-Logic.ps1' -f $previous),
    ('scripts\Test-v{0}-LinuxAcceptance.sh' -f $previous)
)

foreach($relative in $obsolete){
    $path=Join-Path $root $relative
    if(Test-Path $path){
        Remove-Item $path -Force
        Write-Host "Removed superseded active script: $relative" -ForegroundColor Yellow
    }
}
Write-Host "v0.3.0.7 cleanup complete." -ForegroundColor Green
