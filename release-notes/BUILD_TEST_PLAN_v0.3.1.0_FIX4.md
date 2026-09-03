# Build / Test Plan — v0.3.1.0 FIX4

Apply FIX4 over FIX3.

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
- FIX4 RunBuild boundary check: PASS
- FIX4 LASTEXITCODE exclusion check: PASS
- Windows desktop publish: PASS
- Linux desktop publish: PASS
- total failures: 0
