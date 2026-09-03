# Build / Test Plan — v0.3.1.0 FIX5

Apply FIX5 over FIX4.

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
- all FIX5 Build Gate checks PASS
- Release validation PASS
- Windows Avalonia publish PASS
- Linux Avalonia publish PASS
- Failed : 0

No separate desktop publish commands are required because `-RunBuild` performs both.
