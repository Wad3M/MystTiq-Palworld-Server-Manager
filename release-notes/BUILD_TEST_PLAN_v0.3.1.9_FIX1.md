# Build / Test Plan — v0.3.1.9 FIX1

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.9-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Target:

```text
Validation summary: 0 error(s), 0 warning(s).
Release validation passed.

Failed : 0
```

Then:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```
