# Build / Test Plan — v0.3.1.0 FIX1

Apply FIX1 over v0.3.1.0.

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.0-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

`-RunBuild` now performs the release validation plus both desktop publishes:

- `DesktopWindows`
- `DesktopLinux`

Expected:

- validation: 0 errors / 0 warnings
- all logic checks PASS
- Windows Avalonia publish succeeds
- Linux Avalonia publish succeeds

Publish folders:

```text
artifacts\publish\desktop-win-x64
artifacts\publish\desktop-linux-x64
```
