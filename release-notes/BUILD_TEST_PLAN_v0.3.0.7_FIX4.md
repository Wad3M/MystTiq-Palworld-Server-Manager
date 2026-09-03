# Build / Test Plan — v0.3.0.7 FIX4

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.3.0.7-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
.\Build.ps1 LinuxHeadless
```

Then from the extracted Linux package:

```bash
./mysttiq-server --help
```

`production-doctor` must appear.

FIX4 changes help/docs/roadmap, not accepted runtime logic. The final v0.3.0.7 promotion gate remains clean-install acceptance on disposable Ubuntu Server 24.04.4 LTS.
