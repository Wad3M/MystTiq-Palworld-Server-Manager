[CmdletBinding()]
param(
    [string]$DownloadsPath = (Join-Path $HOME 'Downloads'),
    [string]$TargetPath = 'C:\GameServers\MystTiqPalLinux',
    [switch]$SkipBuild,
    [switch]$SkipValidate
)
$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot 'scripts\Install-LatestFullSourceFromDownloads.ps1'
if (-not (Test-Path $helper -PathType Leaf)) { throw "Updater helper not found: $helper" }

# Copy the destructive helper outside the source tree so replacing the source cannot invalidate the running script.
$tempHelper = Join-Path ([IO.Path]::GetTempPath()) ("MystTiq-FullSource-Updater-" + [Guid]::NewGuid().ToString('N') + '.ps1')
Copy-Item -LiteralPath $helper -Destination $tempHelper -Force
try {
    Set-Location ([IO.Path]::GetTempPath())
    & $tempHelper -DownloadsPath $DownloadsPath -TargetPath $TargetPath -SkipBuild:$SkipBuild -SkipValidate:$SkipValidate
}
finally {
    Remove-Item -LiteralPath $tempHelper -Force -ErrorAction SilentlyContinue
}
