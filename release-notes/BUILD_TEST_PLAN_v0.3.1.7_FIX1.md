# Build / Test Plan — v0.3.1.7 FIX1

Apply FIX1 over v0.3.1.7, then run:

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.7-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected:

```text
Validation summary: 0 error(s), 0 warning(s)

[PASS] Warning Cleanup :: Doctor report version is assembly-derived
[PASS] Warning Cleanup :: Doctor report constructor uses assembly-derived reportVersion

Failed : 0
```

The secured production-listener lifecycle acceptance remains an intentional `SKIP`, not a warning.
