# Build / Test Plan — v0.3.1.6

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\scripts\Apply-v0.3.1.6-Cleanup.ps1

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.6-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected:

- validation 0 errors / 0 warnings
- Windows Avalonia publish PASS
- Linux Avalonia publish PASS
- Linux headless publish PASS
- logic failures 0

Deploy:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

Live GUI acceptance:

1. Setup & Update → Refresh shows Linux, SteamCMD installed, Palworld server installed.
2. Preview SteamCMD Plan shows the Linux platform override and configured ServerRoot.
3. While PalServer is running, Update Palworld Server must be rejected.
4. Stop PalServer.
5. Run Update Palworld Server with Validate enabled.
6. Confirm SteamCMD completes and PalServer executable remains present.
7. Start PalServer and confirm lifecycle readiness returns.
8. Confirm Doctor, Players, Monitoring, Backups and Configuration regressions remain healthy.

The existing secured-listener extended lifecycle skip may still appear and is not a failure.
