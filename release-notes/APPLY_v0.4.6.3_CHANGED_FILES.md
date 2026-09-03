# Apply v0.4.6.3 Changed Files

Apply the Changed Files package over a clean v0.4.5.1 baseline. Preserve relative paths and overwrite files when prompted. Do not copy `artifacts`, `bin`, or `obj` directories from an older build.

After applying:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.6.3-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Any failure remains a v0.4.6.x fix until the complete promotion gate passes.
