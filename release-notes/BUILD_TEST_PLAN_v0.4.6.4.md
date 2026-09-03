# v0.4.6.4 Build & Test Plan

## One-command update + gate

Place the latest `MystTiqPalworldServer_v*_FullSource*.zip` in Downloads, then from an already installed v0.4.6.4+ tree run:

```powershell
cd C:\GameServers\MystTiqPalLinux
.\Update-FromDownloads.ps1
```

The script stages and validates the archive, runs the old tree's `Build.ps1 Clean`, replaces the source tree, then runs `Test-CurrentRelease.ps1`, which performs Clean, strict Validate, version logic tests, Windows/Linux builds and runtime smoke.

## Manual equivalent

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.6.4-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Manual GUI acceptance

1. Server Setup shows the v0.2-style environment checklist and health count.
2. Verify Files refreshes the checklist and completes the operation monitor.
3. Selected navigation remains highlighted after direct and programmatic navigation.
4. Start Server still reaches Running / Ready.
5. Live Console contains lifecycle launch information and Palworld/log evidence.
6. Dashboard Live Activity shows console/log events rather than repeated polling status.
7. Continue parity review using `docs/GUI_PARITY_v0.4.6.4.md`.
