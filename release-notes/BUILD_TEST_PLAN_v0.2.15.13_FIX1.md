# Build / Test Plan — v0.2.15.13 FIX1

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.15.13-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime acceptance
1. Enable the normal eight-MOD set and verify all MODs.
2. Confirm eight Healthy/100% MODs with `No known conflict` produce zero confirmed MOD issues.
3. Confirm Overall Health receives no MOD deduction.
4. Disable all MODs and confirm disabled MODs remain neutral.
5. Confirm a genuinely `Confirmed conflict` MOD does produce a health issue.
6. Confirm missing dependency/runtime error/failed MOD conditions still produce health issues.
