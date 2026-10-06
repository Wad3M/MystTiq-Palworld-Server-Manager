# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [int]$Port = 18600
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$root = (Resolve-Path $ProjectRoot).Path

# v0.9.9.0: the downloads themselves, as a user gets them (the roadmap's Distribution item). Run after `Build.ps1 Package`:
#   1. SHA256SUMS.txt lists both ZIPs with the hashes they really have.
#   2. The Windows ZIP holds the desktop, its headless service and the native console proxy; the Linux ZIP holds the
#      desktop and the headless service.
#   3. Extracted into an empty folder, the Windows binaries carry this version, and the packaged service starts there on
#      its own isolated configuration and reports that version.
# Nothing is installed and no real server or configuration is touched.
$version = & (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1')
$artifacts = Join-Path $root 'artifacts'
$windowsZip = Join-Path $artifacts "MystTiqPalworldServer_v${version}_Windows-x64.zip"
$linuxZip = Join-Path $artifacts "MystTiqPalworldServer_v${version}_Linux-x64.zip"
$sums = Join-Path $artifacts 'SHA256SUMS.txt'
if (-not (Test-Path $windowsZip) -or -not (Test-Path $linuxZip) -or -not (Test-Path $sums)) {
    Write-Host "[SKIP] The v$version packages are not built (run .\Build.ps1 Package first)." -ForegroundColor Yellow
    return
}

$failures = @()
function Test-Step([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "[PASS] Distribution :: $Name" -ForegroundColor Green }
    catch { Write-Host "[FAIL] Distribution :: $Name -- $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $Name }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$temp = Join-Path $artifacts ("runtime-smoke\v0.9.9.0-distribution-" + [guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory -Force | Out-Null
$svc = $null

try {
    Test-Step 'SHA256SUMS.txt lists both downloads with the hashes they really have' {
        $listed = @{}
        foreach ($line in Get-Content $sums) { if ($line -match '^([0-9A-Fa-f]{64})\s+\*?(.+)$') { $listed[(Split-Path $Matches[2].Trim() -Leaf)] = $Matches[1].ToUpperInvariant() } }
        foreach ($zip in $windowsZip, $linuxZip) {
            $name = Split-Path $zip -Leaf
            if (-not $listed.ContainsKey($name)) { throw "$name is not listed" }
            $actual = (Get-FileHash $zip -Algorithm SHA256).Hash
            if ($listed[$name] -ne $actual) { throw "$name is listed as $($listed[$name]) but is $actual" }
        }
    }

    Test-Step 'the Windows download holds the desktop, its headless service and the native console proxy; the Linux download the desktop and service' {
        $win = [IO.Compression.ZipFile]::OpenRead($windowsZip)
        try { $winNames = @($win.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }) } finally { $win.Dispose() }
        foreach ($need in 'MystTiq.Desktop.exe', 'headless/mysttiq-server.exe', 'MystTiqConsoleProxy.dll') {
            if (-not ($winNames | Where-Object { $_ -like "*/$need" -or $_ -eq $need })) { throw "the Windows download has no $need" }
        }
        $lin = [IO.Compression.ZipFile]::OpenRead($linuxZip)
        try { $linNames = @($lin.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }) } finally { $lin.Dispose() }
        foreach ($need in 'MystTiq.Desktop', 'headless/mysttiq-server') {
            if (-not ($linNames | Where-Object { $_ -like "*/$need" -or $_ -eq $need })) { throw "the Linux download has no $need" }
        }
        if ($winNames | Where-Object { $_ -match '(^|/)(mysttiq\.json|.*\.token|language\.json)$' }) { throw 'the Windows download carries a configuration or token file' }
    }

    $extract = Join-Path $temp 'app'
    [IO.Compression.ZipFile]::ExtractToDirectory($windowsZip, $extract)
    $desktop = Get-ChildItem $extract -Recurse -Filter 'MystTiq.Desktop.exe' | Select-Object -First 1
    $server = Get-ChildItem $extract -Recurse -Filter 'mysttiq-server.exe' | Select-Object -First 1

    Test-Step "extracted into an empty folder, the desktop and the service both carry version $version" {
        foreach ($exe in $desktop, $server) {
            $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe.FullName)
            $product = ($info.ProductVersion -split '\+')[0]
            if ($product -ne $version -and $info.FileVersion -ne $version) { throw "$($exe.Name) is stamped $($info.ProductVersion) / $($info.FileVersion)" }
        }
        if ($server.Directory.Name -ne 'headless' -or $server.Directory.Parent.FullName -ne $desktop.Directory.FullName) { throw 'the service is not in a headless folder beside the desktop' }
    }

    Test-Step 'the packaged service starts from that folder on its own configuration and reports the same version' {
        $config = Join-Path $temp 'mysttiq.json'; $fleet = Join-Path $temp 'fleet'; $serverRoot = Join-Path $temp 'server'
        New-Item $fleet, $serverRoot, (Join-Path $temp 'runtime'), (Join-Path $temp 'backups') -ItemType Directory -Force | Out-Null
        & $server.FullName config-write-default --config $config --overwrite | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'the packaged service could not write a configuration' }
        $cfg = Get-Content $config -Raw | ConvertFrom-Json
        $cfg.FleetRoot = $fleet; $cfg.api.Port = $Port
        $cfg | ConvertTo-Json -Depth 20 | Set-Content $config
        $script:svc = Start-Process -FilePath $server.FullName -ArgumentList @('api-run', '--config', $config, '--server-root', $serverRoot, '--steamcmd', (Join-Path $temp 'none.exe'), '--backup-root', (Join-Path $temp 'backups'), '--runtime-root', (Join-Path $temp 'runtime')) -WorkingDirectory $server.Directory.FullName -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $temp 'svc.log') -RedirectStandardError (Join-Path $temp 'svc.err.log')
        $health = $null
        for ($i = 0; $i -lt 80 -and -not $health; $i++) {
            Start-Sleep -Milliseconds 250
            if ($script:svc.HasExited) { break }
            try { $health = Invoke-RestMethod "http://127.0.0.1:$Port/healthz" -TimeoutSec 2 } catch {}
        }
        if (-not $health) { throw "the packaged service did not start. $(Get-Content (Join-Path $temp 'svc.err.log') -Raw -ErrorAction SilentlyContinue)" }
        if ($health.version -ne $version -or $health.component -ne 'mysttiq-headless') { throw "healthz reports $($health.component) $($health.version)" }
    }
}
finally {
    if ($svc -and -not $svc.HasExited) { Stop-Process -Id $svc.Id -Force; $svc.WaitForExit(10000) | Out-Null }
}

if ($failures.Count -gt 0) { throw "MystTiq v0.9.9.0 distribution check failed: $($failures.Count) check(s)." }
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "MystTiq v$version distribution check passed." -ForegroundColor Green
