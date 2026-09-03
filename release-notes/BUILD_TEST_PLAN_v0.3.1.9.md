# Build / Test Plan — v0.3.1.9

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\scripts\Apply-v0.3.1.9-Cleanup.ps1
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.3.1.9-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Then deploy with `.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended`. Expect 0 validation warnings, 0 failures, and the secured lifecycle test as SKIP.
