param(
    [string]$ProjectRoot=".",
    [switch]$RunBuild,
    [switch]$ExportJson
)

$ErrorActionPreference="Stop"
$root=(Resolve-Path $ProjectRoot).Path
$results=@()

function Add-TestResult($area,$test,[bool]$passed,$details){
    $script:results += [pscustomobject]@{Area=$area;Test=$test;Passed=$passed;Details=$details}
    Write-Host ("[{0}] {1} :: {2}" -f $(if($passed){"PASS"}else{"FAIL"}),$area,$test)
    if(!$passed){ Write-Host "       $details" }
}

function Check-Literal($area,$test,$text,$literal){
    $ok=$text -match [regex]::Escape($literal)
    Add-TestResult $area $test $ok $(if($ok){"Found expected contract."}else{"Missing: $literal"})
}

Write-Host "`nMystTiq v0.2.16.4 SteamCMD Distribution Abstraction & Platform Audit Harness"
Write-Host "Repository: $root`n"

$contract=Get-Content (Join-Path $root "src\PalworldManager\Services\IServerDistributionPlatformService.cs") -Raw
$windows=Get-Content (Join-Path $root "src\PalworldManager\Services\WindowsServerDistributionPlatformService.cs") -Raw
$factory=Get-Content (Join-Path $root "src\PalworldManager\Services\ServerDistributionPlatformService.cs") -Raw
$installer=Get-Content (Join-Path $root "src\PalworldManager\Services\InstallerService.cs") -Raw
$update=Get-Content (Join-Path $root "src\PalworldManager\Services\SteamServerUpdateService.cs") -Raw
$server=Get-Content (Join-Path $root "src\PalworldManager\Services\ServerService.cs") -Raw
$composition=Get-Content (Join-Path $root "src\PalworldManager\Services\ApplicationServiceComposition.cs") -Raw
$appPaths=Get-Content (Join-Path $root "src\PalworldManager\Services\ApplicationPathService.cs") -Raw
$platform=Get-Content (Join-Path $root "src\PalworldManager\Services\ServerPlatformProfile.cs") -Raw
$diagnostics=Get-Content (Join-Path $root "src\PalworldManager\Services\DiagnosticsService.cs") -Raw
$readme=Get-Content (Join-Path $root "README.md") -Raw
$props=Get-Content (Join-Path $root "Directory.Build.props") -Raw

Check-Literal "Distribution" "Distribution contract exists" $contract "public interface IServerDistributionPlatformService"
Check-Literal "Distribution" "Package URI is abstracted" $contract "Uri SteamCmdPackageUri"
Check-Literal "Distribution" "Executable name is abstracted" $contract "string SteamCmdExecutableName"
Check-Literal "Distribution" "Self-update arguments are abstracted" $contract "BuildSteamCmdSelfUpdateArguments"
Check-Literal "Distribution" "Server install arguments are abstracted" $contract "BuildPalworldServerInstallArguments"
Check-Literal "Distribution" "SteamCMD process startup is abstracted" $contract "CreateSteamCmdStartInfo"
Check-Literal "Distribution" "Package extraction is abstracted" $contract "ExtractSteamCmdPackage"
Check-Literal "Distribution" "Default install recovery path is abstracted" $contract "GetDefaultPalworldInstallRoot"

Check-Literal "Windows Distribution" "Windows implementation exists" $windows "WindowsServerDistributionPlatformService : IServerDistributionPlatformService"
Check-Literal "Windows Distribution" "Valve SteamCMD package retained" $windows "steamcdn-a.akamaihd.net/client/installer/steamcmd.zip"
Check-Literal "Windows Distribution" "Palworld App ID retained" $windows 'PalworldDedicatedServerAppId = "2394010"'
Check-Literal "Windows Distribution" "Validated install remains supported" $windows 'arguments.Add("validate")'
Check-Literal "Windows Distribution" "force_install_dir retained" $windows '"+force_install_dir"'
Check-Literal "Windows Distribution" "Anonymous login retained" $windows '"anonymous"'
Check-Literal "Windows Distribution" "Hidden redirected process policy retained" $windows "RedirectStandardOutput = true"
Check-Literal "Windows Distribution" "Windows ZIP extraction retained" $windows "ZipFile.ExtractToDirectory"

Check-Literal "Factory" "Current-platform distribution factory exists" $factory "ForCurrentPlatform"
Check-Literal "Factory" "Windows selected on Windows" $factory "OperatingSystem.IsWindows()"
Check-Literal "Factory" "Non-Windows remains explicitly unsupported pre-v0.3" $factory "PlatformNotSupportedException"

Check-Literal "Installer" "Installer stores distribution dependency" $installer "private readonly IServerDistributionPlatformService distribution;"
Check-Literal "Installer" "Installer consumes package URI" $installer "distribution.SteamCmdPackageUri"
Check-Literal "Installer" "Installer delegates extraction" $installer "distribution.ExtractSteamCmdPackage"
Check-Literal "Installer" "Installer delegates self-update arguments" $installer "distribution.BuildSteamCmdSelfUpdateArguments()"
Check-Literal "Installer" "Installer delegates validated server arguments" $installer "validate: true"
Check-Literal "Installer" "Installer delegates retry server arguments" $installer "validate: false"
Check-Literal "Installer" "Installer delegates default install recovery" $installer "distribution.GetDefaultPalworldInstallRoot"

$installerOwnsAppUpdate=[regex]::IsMatch($installer,'"\+app_update"|"2394010"')
Add-TestResult "Installer" "Installer no longer owns SteamCMD app-update command" (-not $installerOwnsAppUpdate) $(if($installerOwnsAppUpdate){"InstallerService still embeds +app_update/App ID policy."}else{"SteamCMD app-update policy is distribution-owned."})

$installerOwnsPackage=[regex]::IsMatch($installer,'steamcmd\.zip', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
Add-TestResult "Installer" "Installer no longer owns SteamCMD package URL" (-not $installerOwnsPackage) $(if($installerOwnsPackage){"InstallerService still embeds the SteamCMD package URL."}else{"SteamCMD package source is distribution-owned."})

Check-Literal "Server Update" "Update service stores distribution dependency" $update "private readonly IServerDistributionPlatformService distribution;"
Check-Literal "Server Update" "Update delegates process startup" $update "distribution.CreateSteamCmdStartInfo"
Check-Literal "Server Update" "Update delegates validated arguments" $update "BuildPalworldServerInstallArguments(settings.ServerRoot, validate: true)"

$updateOwnsArgs=[regex]::IsMatch($update,'"\+force_install_dir"|"\+app_update"|"2394010"')
Add-TestResult "Server Update" "Update service no longer embeds SteamCMD command policy" (-not $updateOwnsArgs) $(if($updateOwnsArgs){"SteamServerUpdateService still embeds SteamCMD arguments/App ID."}else{"SteamCMD command policy is distribution-owned."})

Check-Literal "Composition" "Composition exposes shared distribution service" $composition "ServerDistribution"
Check-Literal "Composition" "Composition creates platform-selected distribution service" $composition "ServerDistributionPlatformService.ForCurrentPlatform()"
Check-Literal "Composition" "Server receives shared distribution service" $composition "distributionPlatform: serverDistribution"
Check-Literal "Composition" "Installer receives shared distribution service" $composition "new InstallerService(settings, serverPaths, serverDistribution)"

Check-Literal "Platform Profile" "Current-platform naming factory exists" $platform "ForCurrentPlatform()"
Check-Literal "Platform Profile" "Root server executable name is centralized" $platform "RootServerExecutableName"
Check-Literal "Workspace Paths" "SteamCMD discovery uses platform executable name" $appPaths "SteamCmdExecutableName"
$hardcodedAppPathSteam=[regex]::IsMatch($appPaths,'FindFirstFile\([^,\r\n]+,\s*"steamcmd\.exe"', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
Add-TestResult "Workspace Paths" "SteamCMD workspace discovery has no hard-coded executable literal" (-not $hardcodedAppPathSteam) $(if($hardcodedAppPathSteam){"ApplicationPathService still hard-codes steamcmd.exe discovery."}else{"Workspace discovery consumes the platform executable name."})

Check-Literal "Diagnostics" "Diagnostics consumes platform process names" $diagnostics "ServerPlatformProfile.ForCurrentPlatform().ProcessNames"

Check-Literal "Documentation" "README documents SteamCMD abstraction" $readme "## SteamCMD Distribution Abstraction"
Check-Literal "Documentation" "README documents final platform audit" $readme "## Final Windows Platform Audit"
Check-Literal "Versioning" "Version is v0.2.16.4" $props "<VersionPrefix>0.2.16.4</VersionPrefix>"
Add-TestResult "Documentation" "Release notes present" (Test-Path (Join-Path $root "release-notes\v0.2.16.4.md")) "release notes"
Add-TestResult "Documentation" "Build plan present" (Test-Path (Join-Path $root "release-notes\BUILD_TEST_PLAN_v0.2.16.4.md")) "build plan"
Add-TestResult "Documentation" "Platform audit note present" (Test-Path (Join-Path $root "docs\architecture\platform-completion-audit-v0.2.16.4.md")) "platform audit"

if($RunBuild){
    foreach($step in @("Clean","Validate","All")){
        try{
            $global:LASTEXITCODE=0
            & (Join-Path $root "Build.ps1") $step
            if($LASTEXITCODE -ne 0){ throw "Exit code: $LASTEXITCODE" }
            Add-TestResult "Build" "Build.ps1 $step" $true "Completed successfully."
        }catch{
            Add-TestResult "Build" "Build.ps1 $step" $false $_.Exception.Message
            break
        }
    }
}

$pass=@($results | Where-Object Passed).Count
$fail=@($results | Where-Object { -not $_.Passed }).Count
Write-Host "`n================ MystTiq v0.2.16.4 Summary ================"
Write-Host "Passed : $pass"
Write-Host "Failed : $fail"

if($ExportJson){
    $dir=Join-Path $root "artifacts\logic-tests"
    New-Item -ItemType Directory -Force $dir | Out-Null
    $report=Join-Path $dir ("MystTiq_v0.2.16.4_{0}.json" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
    $results | ConvertTo-Json -Depth 5 | Set-Content $report -Encoding UTF8
    Write-Host "JSON report: $report"
}
if($fail){ exit 1 }
