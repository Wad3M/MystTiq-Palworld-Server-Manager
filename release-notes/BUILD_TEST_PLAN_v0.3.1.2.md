# Build / Test Plan — v0.3.1.2

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\scripts\Apply-v0.3.1.2-Cleanup.ps1

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.2-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected:

- validation: 0 errors / 0 warnings
- Windows Avalonia publish PASS
- Linux Avalonia publish PASS
- logic failures 0

## Functional lifecycle test

Connect to a MystTiq service profile and verify:

1. Refresh shows service/PalServer evidence.
2. Stop transitions PalServer to stopped/not-ready.
3. Start transitions PalServer to running/ready.
4. Restart changes the native PalServer PID and returns to running/ready.
5. Closing the GUI leaves MystTiq service and PalServer running.

For Linux desktop smoke deployment:

```powershell
.\scripts\Deploy-Test-MystTiqDesktopLinux.ps1 -Launch
```
