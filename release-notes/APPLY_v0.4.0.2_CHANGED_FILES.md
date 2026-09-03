# Apply v0.4.0.2 FIX2 Changed Files

Apply this corrective package over the existing v0.4.0.2 candidate at:

`C:\GameServers\MystTiqPalLinux`

Then run:

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.0.2-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Promotion requires 0 validation errors, 0 warnings, and 0 logic-test failures.

FIX2 also corrects the Console composition regression test and requires warning-free desktop compilation.
