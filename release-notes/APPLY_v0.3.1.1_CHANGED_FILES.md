# Apply Instructions — v0.3.1.1

Extract the ChangedFiles ZIP over:

`C:\GameServers\MystTiqPalLinux`

Allow overwrite of matching files.

Then run:

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\scripts\Apply-v0.3.1.1-Cleanup.ps1

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.1-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```
