# v0.4.6.6 Build / Test Plan

Run the one-command updater or the explicit gate.

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.6.6-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Acceptance focus:

- Resource History shows CPU and RAM lines.
- Range selector offers 1 Hour, 6 Hours, 24 Hours, 7 Days and 30 Days.
- CPU/RAM averages and peaks update.
- CPU, memory and thread values remain active when PalServer uses wrapper + Shipping processes.
- No extra periodic status polling loop is introduced.
- v0.4.6.4 Server Setup and navigation selection remain intact.
