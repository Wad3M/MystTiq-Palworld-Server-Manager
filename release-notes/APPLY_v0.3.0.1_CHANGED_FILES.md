# Apply Instructions — v0.3.0.1

Apply over official Linux/headless baseline **v0.3.0.0 FIX1** while preserving repository-relative paths, or use the complete-source package.

The Changed Files package includes a deletion notice for the obsolete active v0.3.0.0 harness.

After applying:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.3.0.1-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
.\Build.ps1 LinuxHeadless
```

Linux lifecycle commands are experimental and should be tested against the disposable Ubuntu/Hyper-V reference environment before promotion.
