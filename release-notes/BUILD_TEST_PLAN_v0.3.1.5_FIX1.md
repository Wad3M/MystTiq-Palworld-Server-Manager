# Build / Test Plan — v0.3.1.5 FIX1

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.5-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected:

```text
Validation summary: 0 error(s), 0 warning(s)

[PASS] FIX1 Versioning :: PalworldManager manifest contains current version
[PASS] FIX1 Versioning :: Avalonia project metadata contains current version

Failed : 0
```

During `Deploy-Test-MystTiqLinux.ps1 -Extended`, this remains expected with the secured production listener:

```text
[WARN] Extended API lifecycle :: skipped: current API uses TLS/auth/non-default bind
```
