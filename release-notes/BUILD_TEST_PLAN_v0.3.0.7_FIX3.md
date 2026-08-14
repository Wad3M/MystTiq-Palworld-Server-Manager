# Build / Test Plan — v0.3.0.7 FIX3

Run the normal Windows gate:

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.7-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson

.\Build.ps1 LinuxHeadless
```

Then:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

Expected production-readiness section:

```text
[PASS] MystTiq executable
[PASS] Production Doctor
[PASS] systemd enabled
[PASS] systemd active
[PASS] Current-boot fatal journal events
[PASS] PalServer ready
[PASS] Disk reserve
```

There must be no contradictory duplicate PASS/FAIL results.

Expected integration result:

```text
[PASS] Production readiness integration
```

The final extended Linux acceptance must contain zero failures.
