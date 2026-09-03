# Apply Instructions — v0.3.1.8

Extract ChangedFiles over `C:\GameServers\MystTiqPalLinux`, then:

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\scripts\Apply-v0.3.1.8-Cleanup.ps1
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.3.1.8-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```
