[CmdletBinding()]
param([string]$Version,[string]$ISCC='C:\Program Files (x86)\Inno Setup 6\ISCC.exe')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if([string]::IsNullOrWhiteSpace($Version)){ $Version = & (Join-Path $PSScriptRoot 'Get-ProjectVersion.ps1') }
& (Join-Path $PSScriptRoot 'Package-Portable.ps1') -Version $Version
if(-not (Test-Path $ISCC)){ throw "Inno Setup compiler not found: $ISCC" }
& $ISCC "/DMyAppVersion=$Version" (Join-Path $root 'installer\MystTiqPalworldServer.iss')
