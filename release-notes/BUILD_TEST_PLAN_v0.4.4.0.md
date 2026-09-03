# Build / Test Plan — v0.4.4.0

```powershell
.\Build.ps1 -Action Clean
.\Build.ps1 -Action Validate -StrictValidation
.\Build.ps1 -Action Build -Configuration Release
.\scripts\Test-v0.4.4.0-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
.\Build.ps1 -Action WindowsHeadless -Configuration Release
.\Build.ps1 -Action LinuxHeadless -Configuration Release
.\Build.ps1 -Action DesktopWindows -Configuration Release
.\Build.ps1 -Action DesktopLinux -Configuration Release
.\Build.ps1 -Action Release -Configuration Release -StrictValidation
```

Runtime acceptance must confirm one periodic polling request, correct status-bar updates, busy animation during operations, Workspace path loading, lifecycle commands, and that closing the desktop does not stop the independently running PalServer/headless service.
