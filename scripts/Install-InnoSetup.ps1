[CmdletBinding(SupportsShouldProcess)]
param()
$ErrorActionPreference = 'Stop'
$known = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path $_) }
if ($known.Count -gt 0) {
    Write-Host "Inno Setup is already installed: $($known[0])" -ForegroundColor Green
    exit 0
}
$winget = Get-Command winget.exe -ErrorAction SilentlyContinue
if (-not $winget) {
    throw "Inno Setup 6 was not found and winget is unavailable. Install package JRSoftware.InnoSetup, then rerun .\Build.ps1 All."
}
if ($PSCmdlet.ShouldProcess('JRSoftware.InnoSetup', 'Install Inno Setup 6 with winget')) {
    & $winget.Source install --id JRSoftware.InnoSetup --exact --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) { throw "winget failed with exit code $LASTEXITCODE." }
}
Write-Host 'Inno Setup installation completed. Open a new PowerShell window, then run .\Build.ps1 All.' -ForegroundColor Green
