#requires -Version 7.0
<#
.SYNOPSIS
    Builds MystTiqConsoleProxy.dll (v0.7.57.0 Native Console Capture) -- a DSOUND.dll proxy that
    forwards PalServer's real audio-init calls to the genuine system DLL and logs its own injection
    lifecycle. See native/MystTiqConsoleProxy/dllmain.cpp for the full design rationale.

    This is native C++, not part of the .NET solution -- it has its own build step, invoked
    separately from Build.ps1, using the MSVC toolchain confirmed present on this machine
    (Visual Studio Community 18, MSVC v143 x86/x64 build tools).
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path $ProjectRoot).Path
$nativeDir = Join-Path $root 'native\MystTiqConsoleProxy'
$outDir = Join-Path $root 'artifacts\native'

$vcvarsCandidates = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio' -Recurse -Filter 'vcvars64.bat' -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending
if (-not $vcvarsCandidates) {
    throw "vcvars64.bat not found under C:\Program Files\Microsoft Visual Studio -- no MSVC toolchain detected."
}
$vcvars = $vcvarsCandidates[0].FullName
Write-Host "Using MSVC environment: $vcvars"

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sourceFile = Join-Path $nativeDir 'dllmain.cpp'
$defFile = Join-Path $nativeDir 'MystTiqConsoleProxy.def'
$outputDll = Join-Path $outDir 'MystTiqConsoleProxy.dll'

$clFlags = if ($Configuration -eq 'Release') { '/O2 /DNDEBUG' } else { '/Od /Zi /DDEBUG' }

# Run the actual compile inside a cmd.exe invocation so vcvars64.bat's environment (INCLUDE/LIB/PATH
# for cl.exe and link.exe) is scoped to this one build, matching how Build-AvaloniaDesktop.ps1 and
# friends already shell out to native tooling from PowerShell in this project.
$cmd = "call `"$vcvars`" >nul && cl.exe /nologo /LD /EHsc /std:c++17 $clFlags `"$sourceFile`" /Fe:`"$outputDll`" /link /DEF:`"$defFile`""
Write-Host "==> Building MystTiqConsoleProxy.dll ($Configuration)"
cmd.exe /c $cmd
if ($LASTEXITCODE -ne 0) { throw "MystTiqConsoleProxy.dll build failed (exit $LASTEXITCODE)." }

if (-not (Test-Path $outputDll)) { throw "Build reported success but $outputDll was not produced." }
Write-Host "Built: $outputDll"

# Clean up intermediate .obj/.exp/.lib the compiler drops next to the source directory by default
# when no /Fo is given -- keep the native source tree free of build artifacts, matching this
# project's own .gitignore-equivalent hygiene convention for src/**/bin,obj.
Get-ChildItem $nativeDir -Include '*.obj','*.exp','*.lib' -File -ErrorAction SilentlyContinue | Remove-Item -Force
