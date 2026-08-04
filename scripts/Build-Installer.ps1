[CmdletBinding()]
param([string]$Version='0.2.12',[string]$ISCC='C:\Program Files (x86)\Inno Setup 6\ISCC.exe')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Package-Portable.ps1') -Version $Version
if(-not (Test-Path $ISCC)){ throw "Inno Setup compiler not found: $ISCC" }
& $ISCC "/DMyAppVersion=$Version" (Join-Path $root 'installer\MystTiqPalworldServer.iss')
