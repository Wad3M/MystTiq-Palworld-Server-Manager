# Build / Test Plan — v0.3.1.0

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.3.1.0-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
.\Build.ps1 DesktopWindows
.\Build.ps1 DesktopLinux
```

Then launch the Windows desktop publish output and verify connection to the local MystTiq service. Transfer/run the Linux desktop publish output inside the Ubuntu desktop test environment for the matching acceptance.

The Avalonia package references intentionally follow the 11.3 servicing line during this initial foundation candidate. After the first successful Windows/Linux restore/build acceptance, record and pin the exact resolved Avalonia version for reproducible subsequent builds.
