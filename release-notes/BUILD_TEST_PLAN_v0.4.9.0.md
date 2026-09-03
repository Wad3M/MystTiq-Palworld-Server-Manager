# v0.4.9.0 Build/Test Plan

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.9.0-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Runtime acceptance: verify console filters/pause/clear/export, card containment at narrow widths, RCON Doctor capability truth, Info/ShowPlayers/Save when RCON is enabled, and no AdminPassword appears in desktop/API payloads/logs.
