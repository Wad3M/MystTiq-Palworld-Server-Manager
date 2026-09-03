# Build / Test Plan — v0.3.1.0 FIX2

Apply FIX2 over v0.3.1.0 FIX1, then run:

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
- Windows Avalonia desktop publish: PASS
- Linux Avalonia desktop publish: PASS
- logic harness: 0 failures
