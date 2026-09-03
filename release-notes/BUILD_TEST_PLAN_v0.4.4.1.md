# Build & Test Plan — v0.4.4.1

From PowerShell 7 at the repository root:

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.4.4.1-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Then run the existing regression, Windows/Linux build, packaging, and acceptance gates required by `RELEASE_CHECKLIST.md`. Any failure remains a v0.4.4.x fix.

After `DesktopWindows` publishes successfully, the GUI should launch automatically. Use `-NoGuiLaunch` only for unattended/CI/release execution. Linux packaging must include both `Test-v0.4.4.1-LinuxAcceptance.sh` and `Test-v0.4.4.1-ProductionReadiness.sh`.
