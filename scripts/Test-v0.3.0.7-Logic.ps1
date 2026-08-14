param(
    [string]$ProjectRoot='.',
    [switch]$RunBuild,
    [switch]$ExportJson
)

$ErrorActionPreference='Stop'
$root=(Resolve-Path $ProjectRoot).Path
$results=@()

function Add-TestResult {
    param([string]$Area,[string]$Test,[bool]$Passed,[string]$Details)
    $script:results += [pscustomobject]@{Area=$Area;Test=$Test;Passed=$Passed;Details=$Details}
    Write-Host ("[{0}] {1} :: {2}" -f $(if($Passed){'PASS'}else{'FAIL'}),$Area,$Test)
    if(-not $Passed){ Write-Host "       $Details" }
}
function Check-Literal {
    param([string]$Area,[string]$Test,[string]$Text,[string]$Literal)
    $ok=$Text -match [regex]::Escape($Literal)
    Add-TestResult $Area $Test $ok $(if($ok){'Found expected contract.'}else{"Missing: $Literal"})
}

Write-Host "`nMystTiq v0.3.0.7 Linux Integration & Production Readiness Harness"
Write-Host "Repository: $root`n"

$certificate=Get-Content (Join-Path $root 'src\MystTiq.Core\Services\HeadlessCertificateService.cs') -Raw
$enrollment=Get-Content (Join-Path $root 'src\MystTiq.Core\Services\HeadlessRemoteApiEnrollmentService.cs') -Raw
$program=Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\Program.cs') -Raw
$api=Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\LocalManagementApiHost.cs') -Raw
$configModels=Get-Content (Join-Path $root 'src\MystTiq.Core\Models\HeadlessConfiguration.cs') -Raw
$configService=Get-Content (Join-Path $root 'src\MystTiq.Core\Services\HeadlessConfigurationService.cs') -Raw
$secrets=Get-Content (Join-Path $root 'src\MystTiq.Core\Services\HeadlessSecretFileService.cs') -Raw
$configure=Get-Content (Join-Path $root 'scripts\Configure-MystTiqRemoteApi.sh') -Raw
$disable=Get-Content (Join-Path $root 'scripts\Disable-MystTiqRemoteApi.sh') -Raw
$remoteTest=Get-Content (Join-Path $root 'scripts\Test-MystTiqRemoteApi.ps1') -Raw
$deploy=Get-Content (Join-Path $root 'scripts\Deploy-Test-MystTiqLinux.ps1') -Raw
$productionDoctor=Get-Content (Join-Path $root ('scripts\Test-v{0}-ProductionReadiness.sh' -f '0.3.0.7')) -Raw
$firstRun=Get-Content (Join-Path $root 'scripts\Install-MystTiqLinux.sh') -Raw
$upgrade=Get-Content (Join-Path $root 'scripts\Upgrade-MystTiqLinux.sh') -Raw
$linuxAcceptance=Get-Content (Join-Path $root ('scripts\Test-v{0}-LinuxAcceptance.sh' -f '0.3.0.7')) -Raw
$buildLinux=Get-Content (Join-Path $root 'scripts\Build-LinuxHeadless.ps1') -Raw
$serviceManager=Get-Content (Join-Path $root 'src\MystTiq.Core\Services\LinuxSystemdServiceManager.cs') -Raw
$lifecycle=Get-Content (Join-Path $root 'src\MystTiq.Core\Services\LinuxServerLifecycleService.cs') -Raw
$coreProject=Get-Content (Join-Path $root 'src\MystTiq.Core\MystTiq.Core.csproj') -Raw
$windowsProject=Get-Content (Join-Path $root 'src\PalworldManager\PalworldManager.csproj') -Raw
$props=Get-Content (Join-Path $root 'Directory.Build.props') -Raw
$readme=Get-Content (Join-Path $root 'README.md') -Raw
$tested=Get-Content (Join-Path $root 'docs\linux\TESTED_ENVIRONMENT.md') -Raw
$architecture=Get-Content (Join-Path $root 'docs\architecture\production-readiness-v0.3.0.7.md') -Raw
$backports=Get-Content (Join-Path $root 'docs\roadmap\WINDOWS_BACKPORT_REGISTRY.md') -Raw
$security=Get-Content (Join-Path $root 'SECURITY.md') -Raw

Check-Literal 'TLS Provisioning' 'Certificate service exists' $certificate 'public sealed class HeadlessCertificateService'
Check-Literal 'TLS Provisioning' 'RSA 3072 is used' $certificate 'RSA.Create(3072)'
Check-Literal 'TLS Provisioning' 'SHA256 signature is used' $certificate 'HashAlgorithmName.SHA256'
Check-Literal 'TLS Provisioning' 'Server-auth EKU exists' $certificate '1.3.6.1.5.5.7.3.1'
Check-Literal 'TLS Provisioning' 'IP SAN is added' $certificate 'san.AddIpAddress(ipAddress)'
Check-Literal 'TLS Provisioning' 'localhost SAN is added' $certificate 'san.AddDnsName("localhost")'
Check-Literal 'TLS Provisioning' 'Optional DNS SAN is supported' $certificate 'san.AddDnsName(dnsName.Trim())'
Check-Literal 'TLS Provisioning' 'Validity is capped at 825 days' $certificate 'validityDays is < 1 or > 825'
Check-Literal 'TLS Provisioning' 'Certificate password uses secret generator' $certificate 'secretFiles.GenerateBearerToken(32)'
Check-Literal 'TLS Provisioning' 'PFX is exported' $certificate 'X509ContentType.Pfx'
Check-Literal 'TLS Provisioning' 'PFX is owner read/write on Linux' $certificate 'UnixFileMode.UserRead | UnixFileMode.UserWrite'

Check-Literal 'Remote Enrollment Core' 'Enrollment service exists' $enrollment 'public sealed class HeadlessRemoteApiEnrollmentService'
Check-Literal 'Remote Enrollment Core' 'Remote enable rejects non-IP bind' $enrollment 'Remote API bind address must be a literal IP address.'
Check-Literal 'Remote Enrollment Core' 'Remote enable rejects loopback bind' $enrollment 'Remote API enrollment requires a non-loopback bind address.'
Check-Literal 'Remote Enrollment Core' 'Authentication is enabled' $enrollment 'Authentication = configuration.Api.Authentication with'
Check-Literal 'Remote Enrollment Core' 'TLS is enabled' $enrollment 'Tls = configuration.Api.Tls with'
Check-Literal 'Remote Enrollment Core' 'Updated remote config is validated' $enrollment 'configurationService.Validate(updated)'
Check-Literal 'Remote Enrollment Core' 'Remote disable restores default bind' $enrollment 'BindAddress = defaults.Api.BindAddress'

Check-Literal 'CLI' 'api-tls-create command exists' $program '"api-tls-create"'
Check-Literal 'CLI' 'api-remote-enable command exists' $program '"api-remote-enable"'
Check-Literal 'CLI' 'api-remote-disable command exists' $program '"api-remote-disable"'
Check-Literal 'CLI' 'Certificate file option exists' $program '--certificate-file'
Check-Literal 'CLI' 'Certificate password option exists' $program '--certificate-password-file'
Check-Literal 'CLI' 'Bind-address option exists' $program '--bind-address'
Check-Literal 'CLI' 'API port option exists' $program '--api-port'
Check-Literal 'CLI' 'DNS SAN option exists' $program '--dns-name'
Check-Literal 'CLI' 'Headless help/version is current' $program 'v0.3.0.7'

Check-Literal 'Linux Enrollment' 'Enrollment script version is current' $configure '0.3.0.7'
Check-Literal 'Linux Enrollment' 'Bind must be explicit' $configure '--bind <LAN-IP>'
Check-Literal 'Linux Enrollment' 'Explicit confirmation is required' $configure 'Continue? (y/N)'
Check-Literal 'Linux Enrollment' 'One sudo validation is requested' $configure 'sudo -v'
Check-Literal 'Linux Enrollment' 'Token creation is automated' $configure 'api-token-create'
Check-Literal 'Linux Enrollment' 'TLS creation is automated' $configure 'api-tls-create'
Check-Literal 'Linux Enrollment' 'Secret ownership is assigned to service user' $configure 'sudo chown "${SERVICE_USER}:${SERVICE_USER}"'
Check-Literal 'Linux Enrollment' 'Protected files are mode 600' $configure 'sudo chmod 600'
Check-Literal 'Linux Enrollment' 'Remote enable is automated' $configure 'api-remote-enable'
Check-Literal 'Linux Enrollment' 'Current build is installed/restarted' $configure 'service-install'
Check-Literal 'Linux Enrollment' 'systemd verify is automated' $configure 'systemd-analyze verify'
Check-Literal 'Linux Enrollment' 'HTTPS health validation is automated' $configure 'https://${BIND}:${PORT}/healthz'
Check-Literal 'Linux Enrollment' 'Bearer status validation is automated' $configure 'Authorization: Bearer'
Check-Literal 'Linux Enrollment' 'Firewall is not silently changed' $configure 'MystTiq did not change firewall rules.'

$ufwMutation=$configure -match 'ufw\s+allow|firewall-cmd\s+--add'
Add-TestResult 'Linux Enrollment' 'No automatic firewall allow mutation exists' (-not $ufwMutation) $(if($ufwMutation){'Automatic firewall allow command found.'}else{'Firewall policy remains administrator-controlled.'})

Check-Literal 'Rollback' 'Remote-disable script exists' $disable 'api-remote-disable'
Check-Literal 'Rollback' 'Rollback restarts systemd service' $disable 'service-install'
Check-Literal 'Rollback' 'Rollback states safe loopback result' $disable 'safe loopback configuration'

Check-Literal 'Windows LAN Acceptance' 'Remote test defaults to current VM' $remoteTest '192.168.1.248'
Check-Literal 'Windows LAN Acceptance' 'Dedicated SSH key is used' $remoteTest 'mysttiq_linux_ed25519'
Check-Literal 'Windows LAN Acceptance' 'Token is retrieved over SSH' $remoteTest 'cat ''$TokenFile'''
Check-Literal 'Windows LAN Acceptance' 'Self-signed chain bypass is explicit to test' $remoteTest '-SkipCertificateCheck'
Check-Literal 'Windows LAN Acceptance' 'Unauthenticated 401 is required' $remoteTest 'expected 401'
Check-Literal 'Windows LAN Acceptance' 'Authorized 200 is required' $remoteTest 'expected 200'
Check-Literal 'Windows LAN Acceptance' 'Bearer header is used' $remoteTest 'Authorization = "Bearer $token"'
Check-Literal 'Windows LAN Acceptance' 'Token persistence is explicitly false' $remoteTest 'Token persisted:'
Check-Literal 'Windows LAN Acceptance' 'Token remains process-memory only' $remoteTest 'process memory only'

Check-Literal 'Linux Acceptance' 'Runner version is current' $linuxAcceptance 'VERSION="0.3.0.7"'
Check-Literal 'Linux Acceptance' 'Temporary TLS provisioning is tested' $linuxAcceptance 'TLS certificate provisioning'
Check-Literal 'Linux Acceptance' 'Temporary secured remote config is tested' $linuxAcceptance 'Explicit secured remote configuration'
Check-Literal 'Linux Acceptance' 'Temporary rollback is tested' $linuxAcceptance 'Remote configuration returns to loopback'

Check-Literal 'Deployment Regression' 'Default deployment version is current' $deploy '0.3.0.7'
Check-Literal 'Deployment Regression' 'Passwordless SSH key remains default' $deploy 'mysttiq_linux_ed25519'
Check-Literal 'Build Automation' 'Version-matched acceptance runner is packaged' $buildLinux 'Test-v$version-LinuxAcceptance.sh'

Check-Literal 'Security Regression' 'Configuration schema remains v2' $configModels 'CurrentSchemaVersion = 2'
Check-Literal 'Security Regression' 'Remote still requires authentication' $configService 'Non-loopback API binding requires api.authentication.enabled=true.'
Check-Literal 'Security Regression' 'Remote still requires TLS' $configService 'Non-loopback API binding requires api.tls.enabled=true.'
Check-Literal 'Security Regression' 'Fixed-time bearer compare remains present' $secrets 'CryptographicOperations.FixedTimeEquals'
Check-Literal 'API Regression' 'Runtime repeats auth/TLS remote gate' $api 'Non-loopback management API requires both authentication and TLS.'
Check-Literal 'systemd Regression' 'Restart policy remains on-failure' $serviceManager 'Restart=on-failure'
Check-Literal 'Lifecycle Regression' 'SIGTERM-first shutdown remains present' $lifecycle 'signals.TryTerminate'
Check-Literal 'Lifecycle Regression' 'UDP 8211 readiness remains present' $lifecycle 'ports.Contains(8211)'

Check-Literal 'Core' 'Core remains net10.0' $coreProject '<TargetFramework>net10.0</TargetFramework>'
$coreHasWpf=$coreProject -match '<UseWPF>\s*true\s*</UseWPF>|net10\.0-windows'
Add-TestResult 'Core' 'Core remains free of WPF target dependency' (-not $coreHasWpf) $(if($coreHasWpf){'MystTiq.Core contains WPF/Windows targeting.'}else{'Core remains platform neutral.'})
Check-Literal 'Windows Regression' 'Windows app remains WPF' $windowsProject '<UseWPF>true</UseWPF>'


Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment verifies remote commands before mutating' $configure 'Remote enrollment commands are present'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment backs up existing configuration' $configure 'Configuration backup created'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment validates config before remote write' $configure 'Configuration is valid'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment verifies token exists' $configure 'Bearer token file was not created'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment verifies token owner' $configure 'Bearer token owner is'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment verifies token mode' $configure 'expected 600'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment verifies TLS certificate exists' $configure 'TLS certificate was not created'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment verifies certificate password exists' $configure 'TLS certificate password file was not created'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment validates config after remote write' $configure 'Remote API configuration failed validation after write'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment verifies effective remote bind' $configure 'Effective bind is'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment verifies effective auth' $configure 'Effective authentication is not enabled'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment verifies effective TLS' $configure 'Effective TLS is not enabled'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment verifies exact LAN listener' $configure 'MystTiq never opened ${BIND}:${PORT}'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment verifies local unauthorized 401' $configure 'expected 401'
Check-Literal 'FIX1 Enrollment Reliability' 'Enrollment has pre-commit rollback' $configure 'Restoring previous MystTiq configuration'

Check-Literal 'FIX1 Windows Acceptance' 'Windows test verifies config before token read' $remoteTest 'Effective MystTiq configuration'
Check-Literal 'FIX1 Windows Acceptance' 'Windows test cleanly reports missing token' $remoteTest 'API token file is missing or empty'
Check-Literal 'FIX1 Windows Acceptance' 'Windows test verifies Linux listener first' $remoteTest 'Linux is not listening on'
Check-Literal 'FIX1 Windows Acceptance' 'Windows test catches HTTPS connection exceptions' $remoteTest 'Unable to connect to remote HTTPS health endpoint'
Check-Literal 'FIX1 Windows Acceptance' 'Windows test has explicit fail summary' $remoteTest '================ REMOTE API FAIL ================'

Check-Literal 'Documentation' 'README identifies current candidate' $readme 'v0.3.0.7'
Check-Literal 'Documentation' 'Reference Ubuntu remains documented' $tested 'Ubuntu Server 24.04.4 LTS'
Check-Literal 'Documentation' 'Remote enrollment architecture exists' $architecture 'Linux Integration & Production Readiness'
Check-Literal 'Documentation' 'Security doc covers remote enrollment' $security '## Remote API enrollment'
Check-Literal 'Documentation' 'Backport discoveries are recorded' $backports '## v0.3.0.7 discoveries'
Check-Literal 'Versioning' 'Version is v0.3.0.7' $props '<VersionPrefix>0.3.0.7</VersionPrefix>'

Add-TestResult 'Documentation' 'Release notes present' (Test-Path (Join-Path $root 'release-notes\v0.3.0.7.md')) 'release notes'
Add-TestResult 'Documentation' 'Build plan present' (Test-Path (Join-Path $root 'release-notes\BUILD_TEST_PLAN_v0.3.0.7.md')) 'build plan'
Add-TestResult 'Documentation' 'Apply instructions present' (Test-Path (Join-Path $root 'release-notes\APPLY_v0.3.0.7_CHANGED_FILES.md')) 'apply instructions'

if($RunBuild){
    foreach($step in @('Clean','Validate','All')){
        try{
            $global:LASTEXITCODE=0
            & (Join-Path $root 'Build.ps1') $step
            if($LASTEXITCODE -ne 0){ throw "Exit code: $LASTEXITCODE" }
            Add-TestResult 'Build' "Build.ps1 $step" $true 'Completed successfully.'
        }catch{
            Add-TestResult 'Build' "Build.ps1 $step" $false $_.Exception.Message
            break
        }
    }
}

$pass=@($results | Where-Object Passed).Count
$fail=@($results | Where-Object { -not $_.Passed }).Count
Write-Host "`n================ MystTiq v0.3.0.7 Summary ================"
Write-Host "Passed : $pass"
Write-Host "Failed : $fail"

if($ExportJson){
    $dir=Join-Path $root 'artifacts\logic-tests'
    New-Item -ItemType Directory -Force $dir | Out-Null
    $report=Join-Path $dir ("MystTiq_v0.3.0.7_{0}.json" -f (Get-Date -Format 'yyyyMMdd_HHmmss'))
    $results | ConvertTo-Json -Depth 5 | Set-Content $report -Encoding UTF8
    Write-Host "JSON report: $report"
}
if($fail){ exit 1 }
Check-Literal 'Production Readiness' 'Headless production-doctor command exists' $program 'production-doctor'
Check-Literal 'Production Readiness' 'Doctor reports recommendations' $program 'Recommendation'
Check-Literal 'Production Readiness' 'Doctor checks disk space' $program 'Disk space'
Check-Literal 'Production Readiness' 'Doctor checks management API security' $program 'Management API security'
Check-Literal 'Production Readiness' 'One-command readiness script exists' $productionDoctor 'PRODUCTION READINESS SUMMARY'
Check-Literal 'Production Readiness' 'Readiness captures current-boot journal' $productionDoctor 'journal-current-boot.txt'
Check-Literal 'FIX3 Readiness Accounting' 'record uses non-ambiguous counter assignment' $productionDoctor 'PASS=$((PASS + 1))'
Check-Literal 'FIX3 Readiness Accounting' 'record explicitly returns success' $productionDoctor 'return 0'
Check-Literal 'FIX3 Readiness Accounting' 'Executable result uses explicit if block' $productionDoctor 'if [[ -x "$APP" ]]; then'
Check-Literal 'FIX3 Readiness Accounting' 'Disk thresholds use explicit elif branch' $productionDoctor 'elif (( avail >= 2097152 )); then'
Check-Literal 'First Run' 'First-run setup preserves existing configuration' $firstRun 'Existing configuration preserved'
Check-Literal 'First Run' 'First-run setup validates configuration' $firstRun 'config-validate'
Check-Literal 'Upgrade' 'Upgrade creates config rollback copy' $upgrade 'pre-upgrade-${VERSION}'
Check-Literal 'Upgrade' 'Upgrade uses service-install' $upgrade 'service-install'
Check-Literal 'Upgrade' 'Upgrade preserves secrets and TLS' $upgrade 'secrets/TLS'
Check-Literal 'Automation' 'Extended Linux acceptance invokes production readiness' $linuxAcceptance 'Production-readiness integration gate'
Check-Literal 'Automation' 'Production readiness runner is resolved beside acceptance script' $linuxAcceptance 'Test-v${VERSION}-ProductionReadiness.sh'
Check-Literal 'Automation' 'Production readiness receives current MystTiq executable' $linuxAcceptance 'MYSTTIQ_APP="$APP"'
Check-Literal 'Automation' 'Production readiness receives current config path' $linuxAcceptance 'MYSTTIQ_CONFIG="$CONFIG"'
Check-Literal 'Automation' 'Production readiness failure counts as acceptance failure' $linuxAcceptance 'record FAIL "Production readiness integration"'
Check-Literal 'Packaging' 'First-run installer is packaged' $buildLinux 'Install-MystTiqLinux.sh'
Check-Literal 'Packaging' 'Upgrade helper is packaged' $buildLinux 'Upgrade-MystTiqLinux.sh'
Check-Literal 'Packaging' 'Production readiness runner is packaged' $buildLinux 'ProductionReadiness.sh'


