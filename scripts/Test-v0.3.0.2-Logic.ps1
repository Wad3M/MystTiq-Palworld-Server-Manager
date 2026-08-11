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

Write-Host "`nMystTiq v0.3.0.2 Linux Service & Automatic Recovery Foundation Harness"
Write-Host "Repository: $root`n"

$serviceModels=Get-Content (Join-Path $root 'src\MystTiq.Core\Models\LinuxServiceModels.cs') -Raw
$serviceManager=Get-Content (Join-Path $root 'src\MystTiq.Core\Services\LinuxSystemdServiceManager.cs') -Raw
$supervisor=Get-Content (Join-Path $root 'src\MystTiq.Core\Services\LinuxHeadlessSupervisor.cs') -Raw
$lifecycle=Get-Content (Join-Path $root 'src\MystTiq.Core\Services\LinuxServerLifecycleService.cs') -Raw
$headlessHostSource=Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\Program.cs') -Raw
$coreProject=Get-Content (Join-Path $root 'src\MystTiq.Core\MystTiq.Core.csproj') -Raw
$hostProject=Get-Content (Join-Path $root 'src\MystTiq.HeadlessHost\MystTiq.HeadlessHost.csproj') -Raw
$windowsProject=Get-Content (Join-Path $root 'src\PalworldManager\PalworldManager.csproj') -Raw
$distribution=Get-Content (Join-Path $root 'src\MystTiq.Core\Services\LinuxServerDistributionPlatformService.cs') -Raw
$props=Get-Content (Join-Path $root 'Directory.Build.props') -Raw
$readme=Get-Content (Join-Path $root 'README.md') -Raw
$tested=Get-Content (Join-Path $root 'docs\linux\TESTED_ENVIRONMENT.md') -Raw
$backports=Get-Content (Join-Path $root 'docs\roadmap\WINDOWS_BACKPORT_REGISTRY.md') -Raw
$architecture=Get-Content (Join-Path $root 'docs\architecture\linux-service-v0.3.0.2.md') -Raw

Check-Literal 'Service Models' 'Linux service state enum exists' $serviceModels 'public enum LinuxServiceState'
Check-Literal 'Service Models' 'Active state exists' $serviceModels 'Active = 4'
Check-Literal 'Service Models' 'Failed state exists' $serviceModels 'Failed = 5'
Check-Literal 'Service Models' 'Supervisor options exist' $serviceModels 'LinuxServiceSupervisorOptions'
Check-Literal 'Service Models' 'Recovery attempt budget exists' $serviceModels 'MaximumRestartAttempts'
Check-Literal 'Service Models' 'Recovery window exists' $serviceModels 'RestartWindow'

Check-Literal 'systemd Manager' 'Linux service manager contract exists' $serviceManager 'public interface ILinuxServiceManager'
Check-Literal 'systemd Manager' 'systemd implementation is Linux annotated' $serviceManager '[SupportedOSPlatform("linux")]'
Check-Literal 'systemd Manager' 'Unit name is stable' $serviceManager 'mysttiq-palworld.service'
Check-Literal 'systemd Manager' 'Stable install directory is /opt/mysttiq/bin' $serviceManager '"/opt/mysttiq/bin"'
Check-Literal 'systemd Manager' 'Stable executable path is defined' $serviceManager '"/mysttiq-server"'
Check-Literal 'systemd Manager' 'Install requires root' $serviceManager 'EnsureRoot()'
Check-Literal 'systemd Manager' 'Current binary is copied into stable service location' $serviceManager 'File.Copy(sourceExecutable, InstalledExecutable, overwrite: true)'
Check-Literal 'systemd Manager' 'In-place reinstall is guarded' $serviceManager 'StringComparison.Ordinal'
Check-Literal 'systemd Manager' 'Installed binary gets Unix executable permissions' $serviceManager 'File.SetUnixFileMode'
Check-Literal 'systemd Manager' 'Runtime root ownership is assigned to service user' $serviceManager '"/usr/bin/chown"'
Check-Literal 'systemd Manager' 'Unit is written under /etc/systemd/system' $serviceManager '"/etc/systemd/system/"'
Check-Literal 'systemd Manager' 'daemon-reload occurs during install' $serviceManager '"daemon-reload"'
Check-Literal 'systemd Manager' 'Unit is enabled' $serviceManager '["enable", UnitName]'
Check-Literal 'systemd Manager' 'Start-now is explicit' $serviceManager 'if (startNow)'
Check-Literal 'systemd Manager' 'Uninstall disables and stops unit' $serviceManager '["disable", "--now", UnitName]'
Check-Literal 'systemd Manager' 'Uninstall removes unit file' $serviceManager 'File.Delete(UnitPath)'
$rawStringBuildUnit = $serviceManager.Contains('$"""') -or $serviceManager.Contains('$$"""')
Add-TestResult 'systemd Unit' 'BuildUnit avoids fragile raw-string literal' (-not $rawStringBuildUnit) $(if($rawStringBuildUnit){'BuildUnit still contains a raw-string literal.'}else{'BuildUnit uses compile-safe string construction.'})
Check-Literal 'systemd Unit' 'Unit section is emitted' $serviceManager '"[Unit]\\n"'
Check-Literal 'systemd Unit' 'Service section is emitted' $serviceManager '"[Service]\\n"'
Check-Literal 'systemd Unit' 'Install section is emitted' $serviceManager '"[Install]\\n"'
Check-Literal 'systemd Unit' 'Unit waits for network-online' $serviceManager 'After=network-online.target'
Check-Literal 'systemd Unit' 'Unit runs service-run' $serviceManager 'service-run'
Check-Literal 'systemd Unit' 'Unit runs selected non-root user' $serviceManager 'User={serviceUser}'
Check-Literal 'systemd Unit' 'Restart policy is on-failure' $serviceManager 'Restart=on-failure'
Check-Literal 'systemd Unit' 'Restart delay is defined' $serviceManager 'RestartSec=10'
Check-Literal 'systemd Unit' 'systemd recovery burst is bounded' $serviceManager 'StartLimitBurst=5'
Check-Literal 'systemd Unit' 'Stop timeout is defined' $serviceManager 'TimeoutStopSec=60'
Check-Literal 'systemd Unit' 'KillMode only targets supervisor' $serviceManager 'KillMode=process'
Check-Literal 'systemd Unit' 'NoNewPrivileges is enabled' $serviceManager 'NoNewPrivileges=true'
Check-Literal 'systemd Unit' 'Boot target is multi-user' $serviceManager 'WantedBy=multi-user.target'

Check-Literal 'Supervisor' 'Linux supervisor exists' $supervisor 'public sealed class LinuxHeadlessSupervisor'
Check-Literal 'Supervisor' 'Supervisor starts missing PalServer' $supervisor 'lifecycle.StartAsync'
Check-Literal 'Supervisor' 'Supervisor adopts existing PalServer' $supervisor 'Adopted existing PalServer PID'
Check-Literal 'Supervisor' 'Supervisor polls lifecycle state' $supervisor 'lifecycle.GetStatusAsync'
Check-Literal 'Supervisor' 'Intentional stopped state is respected' $supervisor 'supervisor will not auto-restart it'
Check-Literal 'Supervisor' 'Recovery budget is enforced' $supervisor 'CanRestart'
Check-Literal 'Supervisor' 'Recovery backoff is applied' $supervisor 'options.RestartBackoff'
Check-Literal 'Supervisor' 'Recovery success is reported' $supervisor 'PalServer recovery succeeded'
Check-Literal 'Supervisor' 'Service stop calls graceful lifecycle stop' $supervisor 'lifecycle.StopAsync'
Check-Literal 'Supervisor' 'Restart history is windowed' $supervisor 'restartHistory.Dequeue'

Check-Literal 'Headless Host' 'service-status command is registered' $headlessHostSource '"service-status"'
Check-Literal 'Headless Host' 'service-install command is registered' $headlessHostSource '"service-install"'
Check-Literal 'Headless Host' 'service-uninstall command is registered' $headlessHostSource '"service-uninstall"'
Check-Literal 'Headless Host' 'service-run command is registered' $headlessHostSource '"service-run"'
Check-Literal 'Headless Host' 'Service user option exists' $headlessHostSource '--service-user'
Check-Literal 'Headless Host' 'Start-now option exists' $headlessHostSource '--start-now'
Check-Literal 'Headless Host' 'Service poll interval is configurable' $headlessHostSource '--service-poll-seconds'
Check-Literal 'Headless Host' 'Recovery backoff is configurable' $headlessHostSource '--recovery-backoff-seconds'
Check-Literal 'Headless Host' 'Recovery attempt limit is configurable' $headlessHostSource '--max-recovery-attempts'
Check-Literal 'Headless Host' 'Recovery window is configurable' $headlessHostSource '--recovery-window-seconds'
Check-Literal 'Headless Host' 'SIGTERM registration exists' $headlessHostSource 'PosixSignal.SIGTERM'
Check-Literal 'Headless Host' 'SIGINT registration exists' $headlessHostSource 'PosixSignal.SIGINT'
Check-Literal 'Headless Host' 'Service cancellation stops managed server' $headlessHostSource 'StopManagedServerAsync'

Check-Literal 'Lifecycle Regression' 'SIGTERM-first lifecycle remains present' $lifecycle 'signals.TryTerminate'
Check-Literal 'Lifecycle Regression' 'SIGKILL escalation remains present' $lifecycle 'signals.TryKill'
Check-Literal 'Lifecycle Regression' 'UDP 8211 readiness remains present' $lifecycle 'ports.Contains(8211)'
Check-Literal 'Linux Distribution' 'SteamCMD Linux override remains present' $distribution '+@sSteamCmdForcePlatformType'

Check-Literal 'Core' 'Core remains platform-neutral net10.0' $coreProject '<TargetFramework>net10.0</TargetFramework>'
$coreHasWpf=$coreProject -match '<UseWPF>\s*true\s*</UseWPF>|net10\.0-windows'
Add-TestResult 'Core' 'Core remains free of WPF target dependency' (-not $coreHasWpf) $(if($coreHasWpf){'MystTiq.Core contains WPF/Windows targeting.'}else{'Core remains platform neutral.'})
Check-Literal 'Headless Host' 'Headless host remains net10.0' $hostProject '<TargetFramework>net10.0</TargetFramework>'
Check-Literal 'Windows Regression' 'Windows app remains net10.0-windows' $windowsProject '<TargetFramework>net10.0-windows</TargetFramework>'
Check-Literal 'Windows Regression' 'Windows app remains WPF' $windowsProject '<UseWPF>true</UseWPF>'

Check-Literal 'Documentation' 'README documents systemd service phase' $readme '## Linux systemd Service & Automatic Recovery'
Check-Literal 'Documentation' 'Tested distro remains Ubuntu Server 24.04.4 LTS' $tested 'Ubuntu Server 24.04.4 LTS'
Check-Literal 'Documentation' 'systemd is in the validation target' $tested 'systemd available as the init/service manager'
Check-Literal 'Documentation' 'Architecture documents systemd ownership model' $architecture 'systemd supervises the long-running MystTiq headless process'
Check-Literal 'Backport Registry' 'Windows Service backport is recorded' $backports 'Windows Service host'
Check-Literal 'Backport Registry' 'Windows recovery budget backport is recorded' $backports 'automatic-recovery budget'
Check-Literal 'Backport Registry' 'Windows background logging backport is recorded' $backports 'Windows Event Log'

Check-Literal 'Versioning' 'Version is v0.3.0.2' $props '<VersionPrefix>0.3.0.2</VersionPrefix>'
Add-TestResult 'Documentation' 'Release notes present' (Test-Path (Join-Path $root 'release-notes\v0.3.0.2.md')) 'release notes'
Add-TestResult 'Documentation' 'Build plan present' (Test-Path (Join-Path $root 'release-notes\BUILD_TEST_PLAN_v0.3.0.2.md')) 'build plan'
Add-TestResult 'Documentation' 'Apply instructions present' (Test-Path (Join-Path $root 'release-notes\APPLY_v0.3.0.2_CHANGED_FILES.md')) 'apply instructions'

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
Write-Host "`n================ MystTiq v0.3.0.2 Summary ================"
Write-Host "Passed : $pass"
Write-Host "Failed : $fail"

if($ExportJson){
    $dir=Join-Path $root 'artifacts\logic-tests'
    New-Item -ItemType Directory -Force $dir | Out-Null
    $report=Join-Path $dir ("MystTiq_v0.3.0.2_{0}.json" -f (Get-Date -Format 'yyyyMMdd_HHmmss'))
    $results | ConvertTo-Json -Depth 5 | Set-Content $report -Encoding UTF8
    Write-Host "JSON report: $report"
}
if($fail){ exit 1 }
