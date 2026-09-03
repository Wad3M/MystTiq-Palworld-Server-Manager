# Build / Test Plan — v0.3.1.1

## Automated Windows gate

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.1-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected:

- validation 0 errors / 0 warnings
- Windows Avalonia publish PASS
- Linux Avalonia publish PASS
- logic failures 0

## Windows visual acceptance

```powershell
.\artifacts\publish\desktop-win-x64\MystTiq.Desktop.exe
```

Verify navigation across all seven pages.

In Settings:

1. create/select a remote profile
2. set service URL, e.g. `https://192.168.1.248:8213`
3. enter bearer token for this process only
4. for a known self-signed endpoint, enter the certificate SHA-256 pin
5. Connect
6. Save Profile
7. restart the GUI and confirm the URL/profile returns but the bearer-token field is empty

## Linux desktop deployment

With the existing XFCE/RDP session active:

```powershell
.\scripts\Deploy-Test-MystTiqDesktopLinux.ps1 -Launch
```

Verify the same shell/navigation/profile UI appears on Linux.

Promotion requires zero build/logic failures and successful Windows/Linux GUI smoke tests.
