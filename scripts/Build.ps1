# MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Release')

# v0.8.26.0: builds the product -- the shared core, the headless service and the Avalonia desktop (the solution file
# holds exactly these three). The legacy WPF app this script used to build was removed.
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$solution=Join-Path $root 'PalworldServerManager.slnx'

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Invoke-DotNet @('restore', $solution)
Invoke-DotNet @('build', $solution, '-c', $Configuration, '--no-restore')
