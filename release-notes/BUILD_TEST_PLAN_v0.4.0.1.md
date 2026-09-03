# Build / Test Plan — v0.4.0.1

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.0.1-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected: validation 0 errors/0 warnings and `Failed : 0`.
