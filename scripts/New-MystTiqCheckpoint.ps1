# MystTiq v1.1.2.0: file reviewed for this release (2026-10-19).
# v1.1.1.0: the release checkpoint, kept with the project (it lived outside it before). For version X it writes
# <BackupRoot>\vX\:
#   MystTiqPalworldServer_vX_FullSource.zip   the source tree (no artifacts, bin, obj, .git, .vs, __pycache__, scratch)
#   SHA256_MANIFEST.txt                        a SHA-256 for every file in the ZIP; the ZIP is expanded again and checked
#   publish\                                   the Windows desktop publish (artifacts\publish\desktop-win-x64)
#   and the -Extras files (release ZIPs, the Docker image archive, SHA256SUMS, CHECKPOINT_NOTES.md).
# Prints one line: files=<n> mismatches=<n> zip=<size>. Any mismatch fails the script.
param(
    [Parameter(Mandatory)][string]$Version,
    [string[]]$Extras = @(),
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$BackupRoot = 'C:\GameServers\_Backups\MystTiqPalworldServer'
)
$ErrorActionPreference = 'Stop'
$src = (Resolve-Path $ProjectRoot).Path
$dest = Join-Path $BackupRoot "v$Version"
$stage = Join-Path ([IO.Path]::GetTempPath()) "mysttiq-stage-$Version"
$verify = Join-Path ([IO.Path]::GetTempPath()) "mysttiq-verify-$Version"
foreach ($folder in $stage, $verify) { if (Test-Path $folder) { [IO.Directory]::Delete($folder, $true) } }
New-Item $dest -ItemType Directory -Force | Out-Null
try {
    robocopy $src $stage /E /XD artifacts bin obj .git .vs __pycache__ scratch /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
    $files = Get-ChildItem $stage -Recurse -File | Sort-Object FullName
    $lines = foreach ($f in $files) { $rel = $f.FullName.Substring($stage.Length + 1).Replace('\', '/'); "$((Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash)  $rel" }
    Set-Content (Join-Path $dest 'SHA256_MANIFEST.txt') $lines
    $zip = Join-Path $dest "MystTiqPalworldServer_v${Version}_FullSource.zip"
    if (Test-Path $zip) { [IO.File]::Delete($zip) }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
    Expand-Archive $zip -DestinationPath $verify
    $mismatch = 0
    foreach ($l in $lines) {
        $hash, $rel = $l -split '  ', 2
        $p = Join-Path $verify $rel.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $p) -or (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash -ne $hash) { $mismatch++ }
    }
    $publish = Join-Path $src 'artifacts\publish\desktop-win-x64'
    if (Test-Path $publish) {
        robocopy $publish (Join-Path $dest 'publish') /E /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy of the desktop publish failed ($LASTEXITCODE)" }
    }
    # The window-capture helper the UI checks use, kept beside each checkpoint since v0.7.110.0.
    $helper = Join-Path $BackupRoot 'v0.7.110.0\ui-capture-helper.ps1'
    if (Test-Path $helper) { Copy-Item $helper $dest -Force }
    foreach ($e in $Extras) { Copy-Item $e $dest -Force }
    "files=$($files.Count) mismatches=$mismatch zip=$([math]::Round((Get-Item $zip).Length / 1MB, 1))MB"
    if ($mismatch -gt 0) { throw "$mismatch file(s) in the ZIP do not match the manifest" }
}
finally {
    foreach ($folder in $stage, $verify) { if (Test-Path $folder) { [IO.Directory]::Delete($folder, $true) } }
}
