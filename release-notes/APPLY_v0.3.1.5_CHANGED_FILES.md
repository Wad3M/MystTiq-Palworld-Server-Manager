# Apply Instructions — v0.3.1.5

Extract the Changed Files ZIP over the v0.3.1.4 baseline, then run:

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\scripts\Apply-v0.3.1.5-Cleanup.ps1
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.3.1.5-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

After the local gate passes, deploy the Linux headless service with:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```
