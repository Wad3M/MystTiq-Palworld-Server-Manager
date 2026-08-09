# Build / Test Plan — v0.2.15.9 FIX1

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.15.9-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Expected compile result:
- 0 errors
- 0 warnings
- `ConfirmedRunning` enum errors gone
- harness build steps pass when `Build.ps1` completes without throwing

After compile validation, resume the v0.2.15.9 runtime capability/functional-verification tests.
