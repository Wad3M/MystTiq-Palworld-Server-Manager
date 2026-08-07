[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Release')

$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$project=Join-Path $root 'src\PalworldManager\PalworldManager.csproj'

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Invoke-DotNet @('restore', $project)
Invoke-DotNet @('build', $project, '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true')
