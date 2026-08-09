# Build / Test Plan — v0.2.15.10 FIX1

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.15.10-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Expected release-hygiene result:
- Validation: 0 errors, 0 warnings.
- No stale v0.2.15.9 harness warnings.
- docs/index.html and app.manifest contain v0.2.15.10.
- Harness completes without Invoke-History/alias errors.

After this passes, resume the v0.2.15.10 native-module runtime acceptance test.
