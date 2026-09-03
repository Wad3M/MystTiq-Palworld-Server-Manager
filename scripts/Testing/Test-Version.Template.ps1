[CmdletBinding()]
param(
    [string]$ProjectRoot = '.',
    [switch]$RunBuild,
    [bool]$ExportJson = $true,
    [bool]$ExportJUnit = $true
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$root = (Resolve-Path $ProjectRoot).Path
. (Join-Path $root 'scripts\Testing\MystTiq.TestFramework.ps1')

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '__VERSION__' -Suite 'Logic'

# ===========================================================================
# GOAL
# __GOAL__
# ===========================================================================

# --- Static contract checks -------------------------------------------------

# Example:
# Test-MystTiqTextMatch $ctx 'src\MystTiq.Core\Services\Example.cs' `
#     'ExpectedPattern' `
#     'Architecture' 'Expected behavior is represented in Core' -Severity Critical

# --- Behavioral/in-memory checks -------------------------------------------

# Example:
# $sample = [pscustomobject]@{ State = 'Running'; Ready = $true }
# Add-MystTiqCheck $ctx 'Behavior' 'Healthy state is represented correctly' `
#     ($sample.State -eq 'Running' -and $sample.Ready) -Severity High

# --- Build / runtime checks -------------------------------------------------

if ($RunBuild) {
    Test-MystTiqCommand $ctx 'Build' 'Strict validation passes' {
        & (Join-Path $root 'Build.ps1') Validate -StrictValidation
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'Release build passes' {
        & (Join-Path $root 'Build.ps1') Build -Configuration Release
    } -Severity Critical | Out-Null

    # Add milestone-specific runtime smoke here.
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
