# Apply v0.4.7.1 Changed Files

Preferred path: use the v0.4.7.1 FullSource ZIP with `Update-FromDownloads.ps1` so the installed tree is clean and the complete current-release gate runs automatically.

For a manual changed-files overlay, copy the package contents over the v0.4.7.0 tree, unblock PowerShell scripts, then run:

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.7.1-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```
