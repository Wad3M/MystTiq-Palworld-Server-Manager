[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [ValidateSet('win-x64','linux-x64')][string]$Runtime='win-x64',
    [switch]$Publish,
    [switch]$NoLaunch
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$project=Join-Path $root 'src\MystTiq.Desktop\MystTiq.Desktop.csproj'
$headlessProject=Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj'
$version=& (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1')

function Stop-ExistingArtifactDesktop {
    param([string]$ArtifactRoot)

    $guard = [IO.Path]::GetFullPath($ArtifactRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach($process in Get-Process -ErrorAction SilentlyContinue){
        if($process.ProcessName -notin @('MystTiq.Desktop','mysttiq-server')){ continue }
        try{
            $processPath=$process.Path
            if([string]::IsNullOrWhiteSpace($processPath)){ continue }
            $resolved=[IO.Path]::GetFullPath($processPath)
            if(-not $resolved.StartsWith($guard,[StringComparison]::OrdinalIgnoreCase)){ continue }
            Write-Host "==> Closing existing artifact-hosted $($process.ProcessName) (PID $($process.Id)) before relaunch..." -ForegroundColor DarkYellow
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
            try{ $process.WaitForExit(5000) | Out-Null }catch{}
        }catch{
            throw "Unable to close existing artifact-hosted MystTiq process PID $($process.Id): $($_.Exception.Message)"
        }
    }
}

Write-Host "==> Building MystTiq Desktop v$version ($Runtime)" -ForegroundColor Cyan
& dotnet restore $project -r $Runtime
if($LASTEXITCODE -ne 0){ throw 'Avalonia desktop restore failed.' }

if($Publish){
    $output=Join-Path $root "artifacts\publish\desktop-$Runtime"
    if($Runtime -eq 'win-x64'){
        Stop-ExistingArtifactDesktop -ArtifactRoot (Join-Path $root 'artifacts')
    }
    & dotnet publish $project -c $Configuration -r $Runtime --self-contained true --no-restore -o $output
    if($LASTEXITCODE -ne 0){ throw 'Avalonia desktop publish failed.' }

    # The Avalonia client is API-only. Ship the matching headless sidecar beside it so a
    # local Windows/Linux desktop can bootstrap its management API without embedding server logic.
    $sidecarOutput=Join-Path $output 'headless'
    & dotnet restore $headlessProject -r $Runtime
    if($LASTEXITCODE -ne 0){ throw 'Headless sidecar restore failed.' }
    & dotnet publish $headlessProject -c $Configuration -r $Runtime --self-contained true --no-restore -o $sidecarOutput
    if($LASTEXITCODE -ne 0){ throw 'Headless sidecar publish failed.' }
    $sidecarName=if($Runtime -eq 'win-x64'){'mysttiq-server.exe'}else{'mysttiq-server'}
    $sidecarExe=Join-Path $sidecarOutput $sidecarName
    if(-not (Test-Path $sidecarExe -PathType Leaf)){ throw "Desktop package is missing expected headless sidecar: $sidecarExe" }
    Write-Host "Desktop publish: $output" -ForegroundColor Green
    Write-Host "Headless sidecar: $sidecarExe" -ForegroundColor Green

    if($Runtime -eq 'win-x64' -and -not $NoLaunch){
        $desktopExe=Join-Path $output 'MystTiq.Desktop.exe'
        if(-not (Test-Path $desktopExe -PathType Leaf)){ throw "Successful publish did not produce expected GUI executable: $desktopExe" }
        Write-Host "==> Launching MystTiq Desktop..." -ForegroundColor Cyan
        Start-Process -FilePath $desktopExe -WorkingDirectory $output | Out-Null
    }
}else{
    & dotnet build $project -c $Configuration -r $Runtime --no-restore
    if($LASTEXITCODE -ne 0){ throw 'Avalonia desktop build failed.' }
}
