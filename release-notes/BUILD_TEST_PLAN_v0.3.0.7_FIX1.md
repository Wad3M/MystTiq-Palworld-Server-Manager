# Build / Test Plan — v0.3.0.7 FIX1

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\scripts\Apply-v0.3.0.7-Cleanup.ps1

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.7-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson

.\Build.ps1 LinuxHeadless
```

Expected:
- validation: 0 errors / 0 warnings
- HeadlessHost compiles
- logic harness: 0 failures
- LinuxHeadless archive builds and includes all v0.3.0.7 Linux operational scripts

Only then run `Deploy-Test-MystTiqLinux.ps1 -Extended`.
