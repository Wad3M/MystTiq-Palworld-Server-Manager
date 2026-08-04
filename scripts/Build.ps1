[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Release')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$project=Join-Path $root 'src\PalworldManager\PalworldManager.csproj'
dotnet restore $project
dotnet build $project -c $Configuration -r win-x64 --self-contained true
