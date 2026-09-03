# Build & Test Plan — v0.4.5.0

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.5.0-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

After the GUI launches, verify the established MystTiq logo/window icon, equal navigation row heights, modest child indentation, local connection state, lifecycle command availability, and representative navigation pages.
