Set-StrictMode -Version 3.0

function New-MystTiqTestContext {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ProjectRoot,
        [Parameter(Mandatory)][string]$Version,
        [string]$Suite = 'Logic'
    )

    $resolvedRoot = (Resolve-Path $ProjectRoot).Path

    [pscustomobject]@{
        ProjectRoot = $resolvedRoot
        Version     = $Version
        Suite       = $Suite
        StartedAt   = [DateTimeOffset]::UtcNow
        Checks      = [System.Collections.Generic.List[object]]::new()
        Data        = @{}
    }
}

function Add-MystTiqCheck {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$Area,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][bool]$Passed,
        [string]$Details = '',
        [ValidateSet('Critical','High','Medium','Low','Info')]
        [string]$Severity = 'High'
    )

    $result = [pscustomobject]@{
        Area     = $Area
        Check    = $Name
        Passed   = $Passed
        Severity = $Severity
        Details  = $Details
    }

    $Context.Checks.Add($result)

    $tag = if ($Passed) { 'PASS' } else { 'FAIL' }
    $color = if ($Passed) { 'Green' } else { 'Red' }
    Write-Host ("[{0}] [{1}] {2} :: {3}" -f $tag, $Severity, $Area, $Name) -ForegroundColor $color

    if (-not $Passed -and $Details) {
        Write-Host ("       {0}" -f $Details) -ForegroundColor DarkYellow
    }

    return $Passed
}

function Get-MystTiqText {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$RelativePath,
        [switch]$Optional
    )

    $path = Join-Path $Context.ProjectRoot $RelativePath
    if (-not (Test-Path $path -PathType Leaf)) {
        if ($Optional) { return $null }
        throw "Required file not found: $path"
    }

    Get-Content $path -Raw
}

function Test-MystTiqFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$Area,
        [string]$Name = '',
        [ValidateSet('Critical','High','Medium','Low','Info')]
        [string]$Severity = 'High'
    )

    if (-not $Name) { $Name = "Required file exists: $RelativePath" }
    $path = Join-Path $Context.ProjectRoot $RelativePath
    Add-MystTiqCheck -Context $Context -Area $Area -Name $Name `
        -Passed:(Test-Path $path -PathType Leaf) -Severity $Severity
}

function Test-MystTiqDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$Area,
        [string]$Name = '',
        [ValidateSet('Critical','High','Medium','Low','Info')]
        [string]$Severity = 'High'
    )

    if (-not $Name) { $Name = "Required directory exists: $RelativePath" }
    $path = Join-Path $Context.ProjectRoot $RelativePath
    Add-MystTiqCheck -Context $Context -Area $Area -Name $Name `
        -Passed:(Test-Path $path -PathType Container) -Severity $Severity
}

function Test-MystTiqTextMatch {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$Pattern,
        [Parameter(Mandatory)][string]$Area,
        [Parameter(Mandatory)][string]$Name,
        [switch]$Not,
        [ValidateSet('Critical','High','Medium','Low','Info')]
        [string]$Severity = 'High'
    )

    $text = Get-MystTiqText -Context $Context -RelativePath $RelativePath
    $matched = [regex]::IsMatch($text, $Pattern,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
        [System.Text.RegularExpressions.RegexOptions]::Multiline)

    if ($Not) { $matched = -not $matched }

    Add-MystTiqCheck -Context $Context -Area $Area -Name $Name `
        -Passed:$matched -Severity $Severity `
        -Details:$(if ($matched) { '' } else { "Pattern check failed in $RelativePath" })
}

function Test-MystTiqCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$Area,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$ScriptBlock,
        [ValidateSet('Critical','High','Medium','Low','Info')]
        [string]$Severity = 'High'
    )

    $output = ''
    $passed = $false
    $details = ''

    try {
        # $ErrorActionPreference='Stop' in the calling gate converts PowerShell
        # non-terminating errors into terminating errors. For native commands,
        # PowerShell sets $? to $false when the native exit code is non-zero.
        #
        # Keep the command being tested as the final statement in ScriptBlock,
        # or explicitly throw when a custom condition fails.
        $captured = & $ScriptBlock 2>&1
        $passed = [bool]$?
        $output = ($captured | Out-String).Trim()

        if (-not $passed) {
            $details = if ($output) {
                "Command reported failure.`n$output"
            } else {
                'Command reported failure without diagnostic output.'
            }
        }
    }
    catch {
        $passed = $false
        $output = ($output | Out-String).Trim()
        $details = $_.Exception.Message
        if ($output) { $details += "`n$output" }
    }

    [void](Add-MystTiqCheck -Context $Context -Area $Area -Name $Name `
        -Passed:$passed -Severity $Severity -Details $details)

    [pscustomobject]@{
        Passed = $passed
        Output = $output
    }
}

function Test-MystTiqPowerShellSyntax {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [string]$RelativePath = 'scripts',
        [Parameter(Mandatory)][string]$Area,
        [string]$Name = 'All PowerShell files parse without syntax errors'
    )

    $scanRoot = Join-Path $Context.ProjectRoot $RelativePath
    $errors = [System.Collections.Generic.List[string]]::new()

    if (-not (Test-Path -LiteralPath $scanRoot -PathType Container)) {
        [void](Add-MystTiqCheck -Context $Context -Area $Area -Name $Name `
            -Passed:$false -Severity Critical `
            -Details:"PowerShell scan root does not exist: $scanRoot")
        return
    }

    $files = Get-ChildItem -LiteralPath $scanRoot -Recurse -File |
        Where-Object { $_.Extension -in '.ps1', '.psm1', '.psd1' }

    foreach ($file in $files) {
        $tokens = $null
        $parseErrors = $null

        [void][System.Management.Automation.Language.Parser]::ParseFile(
            $file.FullName,
            [ref]$tokens,
            [ref]$parseErrors
        )

        foreach ($err in @($parseErrors)) {
            $errors.Add("$($file.FullName):$($err.Extent.StartLineNumber): $($err.Message)")
        }
    }

    [void](Add-MystTiqCheck -Context $Context -Area $Area -Name $Name `
        -Passed:($errors.Count -eq 0) -Severity Critical `
        -Details:($errors -join "`n"))
}

function Test-MystTiqJsonFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$Area,
        [string]$Name = ''
    )

    if (-not $Name) { $Name = "Valid JSON: $RelativePath" }
    $path = Join-Path $Context.ProjectRoot $RelativePath
    $passed = $true
    $details = ''

    try {
        Get-Content $path -Raw | ConvertFrom-Json | Out-Null
    }
    catch {
        $passed = $false
        $details = $_.Exception.Message
    }

    Add-MystTiqCheck -Context $Context -Area $Area -Name $Name `
        -Passed:$passed -Severity High -Details $details
}

function Complete-MystTiqTestContext {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [switch]$ExportJson,
        [switch]$ExportJUnit,
        [string]$ArtifactRoot = 'artifacts\logic-tests'
    )

    $finished = [DateTimeOffset]::UtcNow
    $failed = @($Context.Checks | Where-Object { -not $_.Passed })
    $passedCount = $Context.Checks.Count - $failed.Count
    $criticalFailures = @($failed | Where-Object Severity -eq 'Critical')

    Write-Host ''
    Write-Host ("================ MystTiq v{0} {1} Summary ================" -f `
        $Context.Version, $Context.Suite) -ForegroundColor Cyan
    Write-Host ("Passed: {0} / {1}" -f $passedCount, $Context.Checks.Count)
    Write-Host ("Failed: {0}" -f $failed.Count)
    Write-Host ("Critical failures: {0}" -f $criticalFailures.Count)

    $artifactDir = Join-Path $Context.ProjectRoot $ArtifactRoot
    if ($ExportJson -or $ExportJUnit) {
        New-Item $artifactDir -ItemType Directory -Force | Out-Null
    }

    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

    if ($ExportJson) {
        $jsonPath = Join-Path $artifactDir ("MystTiq_v{0}_{1}_{2}.json" -f `
            $Context.Version, $Context.Suite, $stamp)

        [pscustomobject]@{
            Version   = $Context.Version
            Suite     = $Context.Suite
            StartedAt = $Context.StartedAt
            FinishedAt = $finished
            Passed    = $failed.Count -eq 0
            Summary   = [pscustomobject]@{
                Total = $Context.Checks.Count
                Passed = $passedCount
                Failed = $failed.Count
                CriticalFailures = $criticalFailures.Count
            }
            Checks = $Context.Checks
        } | ConvertTo-Json -Depth 8 | Set-Content $jsonPath -Encoding utf8

        Write-Host "JSON report: $jsonPath"
    }

    if ($ExportJUnit) {
        $junitPath = Join-Path $artifactDir ("MystTiq_v{0}_{1}_{2}.xml" -f `
            $Context.Version, $Context.Suite, $stamp)

        $duration = ($finished - $Context.StartedAt).TotalSeconds
        $xml = [System.Xml.XmlDocument]::new()
        $suite = $xml.CreateElement('testsuite')
        $suite.SetAttribute('name', "MystTiq v$($Context.Version) $($Context.Suite)")
        $suite.SetAttribute('tests', [string]$Context.Checks.Count)
        $suite.SetAttribute('failures', [string]$failed.Count)
        $suite.SetAttribute('time', ('{0:F3}' -f $duration))
        [void]$xml.AppendChild($suite)

        foreach ($check in $Context.Checks) {
            $case = $xml.CreateElement('testcase')
            $case.SetAttribute('classname', $check.Area)
            $case.SetAttribute('name', $check.Check)

            if (-not $check.Passed) {
                $failure = $xml.CreateElement('failure')
                $failure.SetAttribute('message', "$($check.Severity) failure")
                $failure.InnerText = $check.Details
                [void]$case.AppendChild($failure)
            }

            [void]$suite.AppendChild($case)
        }

        $xml.Save($junitPath)
        Write-Host "JUnit report: $junitPath"
    }

    if ($failed.Count) {
        Write-Host ''
        $failed | Format-Table Area, Severity, Check -AutoSize
        throw "MystTiq v$($Context.Version) $($Context.Suite) gate failed: $($failed.Count) check(s)."
    }

    Write-Host ("MystTiq v{0} {1} gate passed." -f $Context.Version, $Context.Suite) `
        -ForegroundColor Green
}
