# MystTiq v1.0.0.0: file reviewed for this release (2026-09-30).
#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('win-x64','linux-x64')][string]$Runtime = 'win-x64',
    [switch]$RequireNativeProxy,
    [string]$RestoreSource,
    # v1.0.0.0, code signing: -StageOnly publishes the app folder and prints its path without making the ZIP (the release
    # workflow sends that folder to SignPath); -FromFolder makes the ZIP from an existing app folder (the signed one).
    [switch]$StageOnly,
    [string]$FromFolder
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = & (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1')
$platform = if ($Runtime -eq 'win-x64') { 'Windows-x64' } else { 'Linux-x64' }
$name = "MystTiqPalworldServer_v${version}_$platform"
$artifacts = Join-Path $root 'artifacts'
$zip = Join-Path $artifacts "$name.zip"
# Every invocation gets its own staging directory; never delete or stop a running app.
if (Test-Path -LiteralPath $zip) { throw "Package already exists: $zip. Preserve it or choose a fresh checkout." }
if ($FromFolder) {
    $source = (Resolve-Path -LiteralPath $FromFolder).Path
    $suffix = if ($Runtime -eq 'win-x64') { '.exe' } else { '' }
    foreach ($exe in @((Join-Path $source "MystTiq.Desktop$suffix"), (Join-Path $source "headless/mysttiq-server$suffix"))) {
        if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing executable in ${FromFolder}: $exe" }
    }
    # The ZIP holds one top-level folder named after the release, as an unsigned package does.
    $stage = Join-Path $artifacts ("package-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination (Join-Path $stage $name) -Recurse
    Compress-Archive -LiteralPath (Join-Path $stage $name) -DestinationPath $zip -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath (Join-Path $artifacts "SHA256SUMS-$Runtime.txt") -Value "$hash  $name.zip" -Encoding utf8
    Write-Host "Package: $zip"
    return
}
$stage = Join-Path $artifacts ("package-" + [guid]::NewGuid().ToString('N'))
$app = Join-Path $stage $name
$sidecar = Join-Path $app 'headless'
New-Item -ItemType Directory -Path $sidecar -Force | Out-Null
foreach ($entry in @(
    @{ Project='src/MystTiq.Desktop/MystTiq.Desktop.csproj'; Output=$app },
    @{ Project='src/MystTiq.HeadlessHost/MystTiq.HeadlessHost.csproj'; Output=$sidecar }
)) {
    $restoreArgs = if ($RestoreSource) { @('--source', $RestoreSource, '-p:NuGetAudit=false') } else { @() }
    & dotnet publish (Join-Path $root $entry.Project) -c Release -r $Runtime --self-contained true -p:UsedAvaloniaProducts= -o $entry.Output @restoreArgs
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($entry.Project)" }
}
$suffix = if ($Runtime -eq 'win-x64') { '.exe' } else { '' }
foreach ($exe in @((Join-Path $app "MystTiq.Desktop$suffix"), (Join-Path $sidecar "mysttiq-server$suffix"))) {
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing executable: $exe" }
}
if ($Runtime -eq 'win-x64') {
    $native = Join-Path $artifacts 'native/MystTiqConsoleProxy.dll'
    if (Test-Path -LiteralPath $native -PathType Leaf) {
        New-Item -ItemType Directory -Path (Join-Path $sidecar 'native') -Force | Out-Null
        Copy-Item -LiteralPath $native -Destination (Join-Path $sidecar 'native/MystTiqConsoleProxy.dll')
    } elseif ($RequireNativeProxy) { throw 'Build the native console proxy before packaging a public Windows release.' }
    else { Write-Warning 'Native console-capture helper omitted. Build-ConsoleProxy.ps1 supplies it.' }
}
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $app
Copy-Item -LiteralPath (Join-Path $root "release-notes/v$version.md") -Destination (Join-Path $app 'RELEASE_NOTES.md')
$instructions = @"
MystTiq v$version ($platform)

Extract the entire folder. Run MystTiq.Desktop$suffix and keep headless beside it.
On Linux: chmod +x MystTiq.Desktop headless/mysttiq-server
The .NET runtime is included. Keep server data and backups outside this folder.
Closing the desktop does not stop a managed server.
See RELEASE_NOTES.md for what is new and the project README for setup.
https://github.com/Wad3M/MystTiq-Palworld-Server-Manager
"@
Set-Content -LiteralPath (Join-Path $app 'START_HERE.txt') -Value $instructions -Encoding utf8
if ($StageOnly) {
    Write-Host "Staged: $app"
    if ($env:GITHUB_OUTPUT) { Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "app-folder=$app" }
    return
}
Compress-Archive -LiteralPath $app -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $artifacts "SHA256SUMS-$Runtime.txt") -Value "$hash  $name.zip" -Encoding utf8
Write-Host "Package: $zip"
