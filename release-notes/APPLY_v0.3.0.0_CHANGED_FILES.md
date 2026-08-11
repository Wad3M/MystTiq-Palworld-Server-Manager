# Apply Instructions — v0.3.0.0

Apply over the official v0.2.16.4 baseline while preserving repository-relative paths, or use the complete-source package.

The release adds new `MystTiq.Core` and `MystTiq.HeadlessHost` projects and updates build/validation/documentation files. Existing Windows WPF application files are intentionally kept as the regression reference.

After applying:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.3.0.0-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
.\Build.ps1 LinuxHeadless
```
