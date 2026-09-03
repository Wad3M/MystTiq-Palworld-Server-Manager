# Build / Test Plan — v0.3.1.0 FIX6

Apply FIX6 over FIX5.

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.0-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected:

- validation: 0 errors / 0 warnings
- all FIX6 Build Gate checks PASS
- release validation PASS
- Windows Avalonia publish PASS
- Linux Avalonia publish PASS
- Failed : 0
