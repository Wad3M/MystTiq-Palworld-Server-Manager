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

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.6.1.0' -Suite 'Logic'

# ===========================================================================
# MILESTONE: Guardian & Transactional Server Safety
#
# IMPORTANT:
# This gate is intentionally FAIL-CLOSED until the milestone-specific executable
# assertions are implemented. Do not replace these with source-presence checks
# alone. Use unit/integration/simulation/runtime tests to prove behavior.
# ===========================================================================

$implementationMarker = Join-Path $root 'scripts\Testing\Implemented\0.6.1.0.logic-ready'
Add-MystTiqCheck $ctx 'Milestone Gate' `
    'Milestone-specific executable logic assertions have been implemented' `
    (Test-Path -LiteralPath $implementationMarker -PathType Leaf) `
    -Severity Critical `
    -Details "Implement the behavioral tests for Guardian & Transactional Server Safety, then create: $implementationMarker"

# Permanent syntax safety.
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

if ($RunBuild) {
    Test-MystTiqCommand $ctx 'Build' 'Strict validation passes' {
        & (Join-Path $root 'Build.ps1') Validate -StrictValidation
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'Release build passes' {
        & (Join-Path $root 'Build.ps1') Build -Configuration Release
    } -Severity Critical | Out-Null

    # Add milestone-specific unit/integration/simulation/runtime commands here.
    # Also run the previous completed milestone gate/runtime smoke as regression.
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit
