[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory)]
    [string]$Goal,

    [string]$ProjectRoot = '.',

    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
$templatePath = Join-Path $root 'scripts\Testing\Test-Version.Template.ps1'
if (-not (Test-Path $templatePath -PathType Leaf)) {
    throw "Template not found: $templatePath"
}

$target = Join-Path $root "scripts\Test-v$Version-Logic.ps1"
if ((Test-Path $target) -and -not $Force) {
    throw "Target already exists: $target (use -Force to replace)"
}

$text = Get-Content $templatePath -Raw
$text = $text.Replace('__VERSION__', $Version)
$text = $text.Replace('__GOAL__', $Goal)
Set-Content $target $text -Encoding utf8

Write-Host "Created $target" -ForegroundColor Green
