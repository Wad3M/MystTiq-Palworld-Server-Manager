# Build / Test Plan — v0.3.1.3 FIX4

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.3-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected:

```text
Validation summary: 0 error(s), 0 warning(s)
Failed : 0
```

Then:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

The deployment must build and locate the v0.3.1.3 headless archive rather than an obsolete v0.3.0.7 archive.
