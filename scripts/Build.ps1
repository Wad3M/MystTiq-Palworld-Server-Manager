[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Release')

$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$windowsProject=Join-Path $root 'src\PalworldManager\PalworldManager.csproj'
$coreProject=Join-Path $root 'src\MystTiq.Core\MystTiq.Core.csproj'
$headlessProject=Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj'

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Invoke-DotNet @('restore', $windowsProject)
Invoke-DotNet @('restore', $coreProject)
Invoke-DotNet @('restore', $headlessProject)

Invoke-DotNet @('build', $windowsProject, '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true')
Invoke-DotNet @('build', $coreProject, '-c', $Configuration, '--no-restore')
Invoke-DotNet @('build', $headlessProject, '-c', $Configuration, '--no-restore')
