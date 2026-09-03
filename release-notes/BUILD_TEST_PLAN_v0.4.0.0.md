# Build / Test Plan — v0.4.0.0

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.0.0-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Expected: validation passes, Windows persistent host publishes, and `Failed : 0`.
