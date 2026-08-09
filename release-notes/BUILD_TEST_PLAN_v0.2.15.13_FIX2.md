# Build / Test Plan — v0.2.15.13 FIX2

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.15.13-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Expected:
- PowerShell syntax validation passes.
- Release validation reports 0 errors / 0 warnings.
- Conflict-health regression checks pass.
- C# build and packaging complete successfully.
