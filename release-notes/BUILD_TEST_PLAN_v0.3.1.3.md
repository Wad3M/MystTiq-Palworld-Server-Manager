# Build / Test Plan — v0.3.1.3

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\scripts\Apply-v0.3.1.3-Cleanup.ps1

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.3-Logic.ps1 `
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

The v0.3.1.3 headless binary must then be deployed to the Ubuntu host before the new monitoring API can be functionally tested.

After deployment, use the GUI to verify Players and Monitoring. Lifecycle Start / Stop / Restart from v0.3.1.2 must continue to work.

For the Linux GUI:

```powershell
.\scripts\Deploy-Test-MystTiqDesktopLinux.ps1 -Launch
```
