[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [string]$Runtime='linux-x64'
)

$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$project=Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj'
$publish=Join-Path $root "artifacts\publish\$Runtime"
$version=& (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1')

New-Item -ItemType Directory -Force $publish | Out-Null

Write-Host "==> Publishing MystTiq Headless Host v$version for $Runtime" -ForegroundColor Cyan
& dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:PublishTrimmed=false -o $publish
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

# Ship all Linux-side operational/test scripts required by this release.
$publishScripts = Join-Path $publish 'scripts'
New-Item -ItemType Directory -Force $publishScripts | Out-Null

$linuxScripts = @(
    "Test-v$version-LinuxAcceptance.sh",
    "Configure-MystTiqRemoteApi.sh",
    "Disable-MystTiqRemoteApi.sh",
    "Install-MystTiqLinux.sh",
    "Upgrade-MystTiqLinux.sh",
    "Test-v$version-ProductionReadiness.sh"
)

foreach ($scriptName in $linuxScripts) {
    $source = Join-Path $PSScriptRoot $scriptName

    if (-not (Test-Path $source -PathType Leaf)) {
        throw "Required Linux publish script was not found: $source"
    }

    Copy-Item $source (Join-Path $publishScripts $scriptName) -Force
    Write-Host "Included Linux script: $scriptName" -ForegroundColor Green
}

$tar=Get-Command tar -ErrorAction SilentlyContinue
if ($tar) {
    $archive=Join-Path $root "artifacts\MystTiqHeadless-v$version-$Runtime.tar.gz"
    if (Test-Path $archive) { Remove-Item $archive -Force }
    Push-Location $publish
    try {
        & $tar.Source -czf $archive .
        if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE." }
    }
    finally { Pop-Location }
    $hash=(Get-FileHash $archive -Algorithm SHA256).Hash
    $checksumPath="$archive.sha256.txt"
    "$hash  $(Split-Path -Leaf $archive)" | Set-Content $checksumPath -Encoding ascii
    Write-Host "Linux headless archive: $archive" -ForegroundColor Green
    Write-Host "Linux headless SHA256:  $hash" -ForegroundColor Green
    Write-Host "Checksum file:          $checksumPath" -ForegroundColor Green
} else {
    Write-Warning 'tar was not found; publish directory was created without a .tar.gz archive.'
}

Write-Host "Linux headless publish: $publish" -ForegroundColor Green
