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
$framework = Join-Path $root 'scripts\Testing\MystTiq.TestFramework.ps1'
if (-not (Test-Path $framework -PathType Leaf)) {
    throw "MystTiq test framework not found: $framework"
}
. $framework

$ctx = New-MystTiqTestContext -ProjectRoot $root -Version '0.7.93.0' -Suite 'Logic'

# ---------------------------------------------------------------------------
# 1. Version / build identity
# ---------------------------------------------------------------------------
Test-MystTiqTextMatch $ctx 'Directory.Build.props' `
    '<VersionPrefix>0\.7\.93\.0</VersionPrefix>' `
    'Versioning' 'Directory.Build.props reports v0.7.93.0' -Severity Critical

Test-MystTiqTextMatch $ctx 'src\PalworldManager\app.manifest' `
    'assemblyIdentity version="0\.7\.93\.0"' `
    'Versioning' 'app.manifest reports v0.7.93.0' -Severity Critical

# ---------------------------------------------------------------------------
# 2. Existing architecture remains present (regression)
# ---------------------------------------------------------------------------
Test-MystTiqFile $ctx 'src\MystTiq.Core\MystTiq.Core.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj' 'Regression'
Test-MystTiqFile $ctx 'src\MystTiq.Desktop\MystTiq.Desktop.csproj' 'Regression'
Test-MystTiqFile $ctx 'scripts\Test-v0.7.92.0-Logic.ps1' 'Regression' 'v0.7.92.0 logic gate remains available' -Severity High

$mapVm = Get-MystTiqText $ctx 'src\MystTiq.Desktop\ViewModels\MainWindowViewModel.cs'
Add-MystTiqCheck $ctx 'Regression' 'v0.7.92.0-era world map base markers remain present, untouched by this version' `
    ($mapVm -match 'public bool IsBaseMarkersActive => UseCalibratedWorldPositions && IsPalpagosMapActive;') `
    -Severity Critical

# ---------------------------------------------------------------------------
# 3. v0.7.93.0 Contracts -- Nexus Mods catalog
# ---------------------------------------------------------------------------
$links = Get-MystTiqText $ctx 'src\MystTiq.Core\Services\NexusModsLinks.cs'
Add-MystTiqCheck $ctx 'v0.7.93.0 Contracts' 'nxm parsing is Palworld-only and requires both key and expiry' `
    ($links -match 'uri\.Host\.Equals\(PalworldGameDomain' -and $links -match 'string\.IsNullOrWhiteSpace\(key\) \|\| !long\.TryParse\(expiresText') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.93.0 Contracts' 'downloads are only allowed from https Nexus-owned hosts' `
    ($links -match 'IsAllowedDownloadHost' -and $links -match 'EndsWith\("\.nexus-cdn\.com"' -and $links -match 'UriSchemeHttps') `
    -Severity Critical

$client = Get-MystTiqText $ctx 'src\MystTiq.Desktop\Services\NexusModsClient.cs'
Add-MystTiqCheck $ctx 'v0.7.93.0 Contracts' 'the client sends the required apikey / Application-Name / Application-Version headers' `
    ($client -match 'TryAddWithoutValidation\("apikey"' -and $client -match '"Application-Name"' -and $client -match '"Application-Version"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.93.0 Contracts' 'the client only talks to api.nexusmods.com and only uses the documented v1 list routes' `
    ($client -match 'ApiBase = "https://api\.nexusmods\.com"' -and $client -match 'trending" or "latest_added" or "latest_updated"') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.93.0 Contracts' 'downloads enforce a size cap and do not send the API key to the CDN' `
    ($client -match 'maxBytes' -and $client -match 'HttpRequestMessage\(HttpMethod\.Get, uri\)' -and $client -notmatch 'Get, uri\)[\s\S]{0,200}apikey') `
    -Severity Critical

$vmText = $mapVm
Add-MystTiqCheck $ctx 'v0.7.93.0 Contracts' 'the API key is kept client-side: DPAPI store only on an explicit Save Key, deleted by Forget Key' `
    ($vmText -match 'NexusKeyCredentialId = "nexusmods-api-key"' -and
     $vmText -match '_credentialStore\.Save\(NexusKeyCredentialId, key\);' -and
     $vmText -match '_credentialStore\.Delete\(NexusKeyCredentialId\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.93.0 Contracts' 'downloads are sniffed for ZIP and installed through the existing validated InstallModZipAsync, with temp cleanup' `
    ($vmText -match 'DescribeArchiveKind\(header\)' -and
     $vmText -match 'await InstallModZipAsync\(stream, fileName\);' -and
     $vmText -match 'if \(File\.Exists\(temp\)\) File\.Delete\(temp\);') `
    -Severity Critical

Add-MystTiqCheck $ctx 'v0.7.93.0 Contracts' 'the Premium-only direct download path and the nxm free-account path both exist' `
    ($vmText -match 'NexusDownloadAndInstallAsync\(mod\.ModId, file\.FileId' -and
     $vmText -match 'NexusDownloadAndInstallAsync\(link\.ModId, link\.FileId' -and
     $vmText -match 'link\.Key, link\.Expires') `
    -Severity Critical

$xaml = Get-MystTiqText $ctx 'src\MystTiq.Desktop\MainWindow.axaml'
Add-MystTiqCheck $ctx 'v0.7.93.0 Contracts' 'the MOD Library page has the Nexus card with attribution, lists, lookup, files and install' `
    ($xaml -match 'Expander Header="Nexus Mods Catalog"' -and
     $xaml -match 'CommandParameter="latest_updated"' -and
     $xaml -match 'Command="\{Binding NexusInstallFromNxmCommand\}"' -and
     $xaml -match 'not affiliated with Nexus Mods') `
    -Severity Critical

$harness = Get-MystTiqText $ctx 'scripts\Testing\MystTiq.LogicHarness\Program.cs'
Add-MystTiqCheck $ctx 'v0.7.93.0 Contracts' 'the logic harness covers the Nexus link helpers' `
    (([regex]::Matches($harness, 'RunScenario\("NexusModsLinks')).Count -ge 5) `
    -Severity Critical

Test-MystTiqFile $ctx 'docs\architecture\v0.7.93.0-nexus-mods-catalog.md' 'v0.7.93.0 Contracts' 'v0.7.93.0 architecture doc is present' -Severity Critical

# ---------------------------------------------------------------------------
# 4. Documentation
# ---------------------------------------------------------------------------
$docText = (Get-ChildItem (Join-Path $root 'docs') -Recurse -File -Include *.md | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
Add-MystTiqCheck $ctx 'Documentation' 'v0.7.93.0 is documented' ([regex]::IsMatch($docText, 'v0\.7\.93\.0')) -Severity High
Test-MystTiqFile $ctx 'release-notes\v0.7.93.0.md' 'Documentation' 'v0.7.93.0 release notes exist' -Severity High
$changelogText = Get-MystTiqText $ctx 'CHANGELOG.md'
Add-MystTiqCheck $ctx 'Documentation' 'CHANGELOG.md has a v0.7.93.0 entry' ($changelogText -match '## v0\.7\.93\.0') -Severity High
# ---------------------------------------------------------------------------
# 5. PowerShell syntax safety
# ---------------------------------------------------------------------------
Test-MystTiqPowerShellSyntax $ctx -RelativePath 'scripts' -Area 'PowerShell Safety'

# ---------------------------------------------------------------------------
# 6. Optional build/runtime execution
# ---------------------------------------------------------------------------
if ($RunBuild) {
    $frozenZip = Join-Path $root '..\_Backups\MystTiqPalworldServer\v0.7.92.0\MystTiqPalworldServer_v0.7.92.0_FullSource.zip'
    if (-not (Test-Path $frozenZip -PathType Leaf)) {
        throw "Frozen v0.7.92.0 regression baseline checkpoint not found: $frozenZip"
    }
    $frozenRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.92.0-frozen-baseline'
    if (-not (Test-Path $frozenRoot)) {
        Expand-Archive -Path $frozenZip -DestinationPath $frozenRoot -Force
    }
    $frozenProjectRoot = if (Test-Path (Join-Path $frozenRoot 'Directory.Build.props')) { $frozenRoot } else {
        (Get-ChildItem $frozenRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Directory.Build.props') } | Select-Object -First 1).FullName
    }

    $frozenGateLog = Join-Path ([System.IO.Path]::GetTempPath()) 'MystTiq-v0.7.92.0-frozen-gate-output.txt'
    try {
        & (Join-Path $frozenProjectRoot 'scripts\Test-v0.7.92.0-Logic.ps1') -ProjectRoot $frozenProjectRoot *> $frozenGateLog
    } catch { }
    $frozenGateOutput = Get-Content $frozenGateLog -Raw
    $frozenGateOutput -split "`n" | Where-Object { $_ -match '^\[FAIL\]' } | ForEach-Object { Write-Host $_ }
    $unexpectedFrozenFailures = @(
        [regex]::Matches($frozenGateOutput, '(?m)^\[FAIL\]\s+([^:]+?)\s*::') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    Add-MystTiqCheck $ctx 'Regression Logic' 'Frozen v0.7.92.0 checkpoint logic gate still passes' `
        ($unexpectedFrozenFailures.Count -eq 0) `
        -Severity Critical `
        -Details ($(if ($unexpectedFrozenFailures.Count -gt 0) { "Unexpected failures: $($unexpectedFrozenFailures -join ', ')" } else { '' }))

    Test-MystTiqCommand $ctx 'Build' 'Strict validation passes' {
        & (Join-Path $root 'Build.ps1') Validate -StrictValidation
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'Release solution build passes' {
        & (Join-Path $root 'Build.ps1') Build -Configuration Release
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression Runtime' 'v0.5.1.5 isolated runtime smoke still passes' {
        & (Join-Path $root 'scripts\Test-v0.5.1.5-RuntimeSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.12.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.12.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.15.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.15.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.17.0 api-remote-enable smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.17.0-RemoteEnableSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.64.0 route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.64.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'v0.7.81.0 new-server wizard route smoke gate still passes' {
        & (Join-Path $root 'scripts\Test-v0.7.81.0-RouteSmoke.ps1') -ProjectRoot $root
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Regression New Tests' 'MystTiq.LogicHarness passes' {
        Push-Location (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness')
        try { & dotnet run -c Release }
        finally {
            Pop-Location
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\bin') -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $root 'scripts\Testing\MystTiq.LogicHarness\obj') -Recurse -Force -ErrorAction SilentlyContinue
        }
    } -Severity Critical | Out-Null

    Test-MystTiqCommand $ctx 'Build' 'MystTiq.Desktop builds clean (not part of PalworldServerManager.slnx)' {
        & dotnet build (Join-Path $root 'src\MystTiq.Desktop\MystTiq.Desktop.csproj') -c Release
    } -Severity Critical | Out-Null
}

Complete-MystTiqTestContext $ctx -ExportJson:$ExportJson -ExportJUnit:$ExportJUnit


