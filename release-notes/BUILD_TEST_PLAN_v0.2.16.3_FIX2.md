# Build / Test Plan — v0.2.16.3 FIX2

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.16.3-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Expected:
- validation: 0 errors / 0 warnings
- build/package/installer/checksum generation succeeds
- no legacy release-source status remains in active Update Center code
- GitHub failure fallback uses `UNABLE TO CHECK`
- all v0.2.16.3 harness tests pass
