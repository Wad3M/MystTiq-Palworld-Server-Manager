# Apply Instructions — v0.3.1.7

Extract ChangedFiles over `C:\GameServers\MystTiqPalLinux`, then:

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\scripts\Apply-v0.3.1.7-Cleanup.ps1
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.3.1.7-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```
