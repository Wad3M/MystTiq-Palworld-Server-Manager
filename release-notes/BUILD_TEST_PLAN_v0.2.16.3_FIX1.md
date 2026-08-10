# Build / Test Plan — v0.2.16.3 FIX1

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.16.3-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Expected:
- release validation: 0 errors / 0 warnings
- build/package/installer/checksum generation succeeds
- v0.2.16.3 harness: 34 PASS / 0 FAIL
- MystTiq GitHub update-awareness checks still pass
