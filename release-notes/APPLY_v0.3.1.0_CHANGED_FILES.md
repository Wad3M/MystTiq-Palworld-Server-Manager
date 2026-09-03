# Apply v0.3.1.0

Extract the ChangedFiles ZIP over `C:\GameServers\MystTiqPalLinux`, allowing overwrite.

Then:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.3.1.0-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
.\Build.ps1 DesktopWindows
.\Build.ps1 DesktopLinux
```
