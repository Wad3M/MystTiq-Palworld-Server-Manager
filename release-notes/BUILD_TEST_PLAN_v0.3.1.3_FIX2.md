# Build / Test Plan — v0.3.1.3 FIX2

After extracting FIX2 ChangedFiles:

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
- Windows desktop publish PASS
- Linux desktop publish PASS
- Linux headless publish PASS
- Linux headless archive includes both current `.1.3` gate scripts
- logic failures 0

After that, deploy the new headless package to Ubuntu and run the Linux acceptance workflow.
