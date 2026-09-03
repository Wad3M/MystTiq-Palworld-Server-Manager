# Apply Instructions — v0.3.1.2

Extract the ChangedFiles ZIP over:

`C:\GameServers\MystTiqPalLinux`

Then:

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\scripts\Apply-v0.3.1.2-Cleanup.ps1

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.2-Logic.ps1 `
  -ProjectRoot . `
  -RunBuild `
  -ExportJson
```
