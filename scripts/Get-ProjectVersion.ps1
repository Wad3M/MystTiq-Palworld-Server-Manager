# MystTiq v1.0.3.0: file reviewed for this release (2026-10-05).
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $root 'Directory.Build.props'
if (-not (Test-Path $propsPath)) { throw "Version file not found: $propsPath" }
[xml]$props = Get-Content $propsPath
$version = [string]$props.Project.PropertyGroup.VersionPrefix
if ([string]::IsNullOrWhiteSpace($version)) { throw 'VersionPrefix is missing from Directory.Build.props.' }
$version.Trim()
