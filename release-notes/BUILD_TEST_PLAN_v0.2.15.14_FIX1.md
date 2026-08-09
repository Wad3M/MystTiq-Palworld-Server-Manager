# Build / Test Plan — v0.2.15.14 FIX1

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.15.14-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Expected:
- validation: 0 errors / 0 warnings
- CS0246 for `AppSettings` is gone
- full build/package succeeds
- v0.2.15.14 architecture harness passes
