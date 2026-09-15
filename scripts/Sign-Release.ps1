[CmdletBinding()]
param(
    # Path to a single file, or a directory to sign every .exe/.dll in (non-recursive by default).
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [switch]$Recurse,

    # Traditional PFX-file signing. Leave both unset to use a certificate already installed in the
    # local machine/user cert store instead (selected via -CertSubject or -CertThumbprint), which is
    # how most hardware-token/HSM-backed code signing certificates are actually used on Windows --
    # the CA's own client software (e.g. SSL.com's eSigner, a USB token's CSP) installs the cert into
    # the store and signtool talks to it there; you never handle a raw private-key file for those.
    [string]$PfxPath,
    [string]$PfxPasswordFile,

    [string]$CertSubject,
    [string]$CertThumbprint,

    # A real RFC3161 timestamp server is required -- without one, the signature becomes invalid the
    # moment the certificate itself expires, even for files signed while it was still valid. DigiCert's
    # is free and widely trusted; swap to your CA's own if they provide one.
    [string]$TimestampServer = "http://timestamp.digicert.com"
)

$ErrorActionPreference = 'Stop'

function Find-SignTool {
    $candidates = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object FullName -Descending
    if ($candidates.Count -eq 0) {
        throw "signtool.exe not found under the Windows 10/11 SDK. Install the 'Windows SDK Signing Tools' component via Visual Studio Installer or the standalone SDK, then retry."
    }
    return $candidates[0].FullName
}

$signtool = Find-SignTool
Write-Host "==> Using signtool: $signtool"

$targets = if (Test-Path $Path -PathType Container) {
    Get-ChildItem $Path -Include *.exe, *.dll -File -Recurse:$Recurse
} else {
    Get-Item $Path
}

if ($targets.Count -eq 0) {
    Write-Host "No .exe/.dll files found under $Path -- nothing to sign."
    exit 0
}

$signArgs = @('sign', '/fd', 'sha256', '/td', 'sha256', '/tr', $TimestampServer)

if ($PfxPath) {
    if (-not (Test-Path $PfxPath)) { throw "PFX file not found: $PfxPath" }
    $signArgs += @('/f', $PfxPath)
    if ($PfxPasswordFile) {
        if (-not (Test-Path $PfxPasswordFile)) { throw "PFX password file not found: $PfxPasswordFile" }
        $password = (Get-Content $PfxPasswordFile -Raw).Trim()
        $signArgs += @('/p', $password)
    }
} elseif ($CertThumbprint) {
    $signArgs += @('/sha1', $CertThumbprint)
} elseif ($CertSubject) {
    $signArgs += @('/n', $CertSubject)
} else {
    throw "No certificate specified. Pass -PfxPath (+ optionally -PfxPasswordFile), or -CertThumbprint / -CertSubject to select a certificate already installed in the Windows certificate store (the usual path for hardware-token/HSM-backed certs, since the CA/BF baseline requirements have required hardware-protected private keys for all code signing certs since June 2023)."
}

$failures = @()
foreach ($file in $targets) {
    Write-Host "==> Signing $($file.FullName)"
    & $signtool @signArgs $file.FullName
    if ($LASTEXITCODE -ne 0) { $failures += $file.FullName }
}

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "FAILED to sign $($failures.Count) file(s):"
    $failures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host ""
Write-Host "==> Verifying signatures..."
foreach ($file in $targets) {
    & $signtool verify /pa /v $file.FullName
}

Write-Host ""
Write-Host "Signed and verified $($targets.Count) file(s)."
