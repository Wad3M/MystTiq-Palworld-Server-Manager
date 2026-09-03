# Build / Test Plan — v0.4.3.1

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.3.1-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected promotion gate: 0 validation errors, 0 validation warnings, 0 logic-test failures, and successful Windows/Linux headless + Avalonia publishes.
