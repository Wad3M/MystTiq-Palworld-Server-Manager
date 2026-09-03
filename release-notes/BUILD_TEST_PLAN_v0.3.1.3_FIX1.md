# Build / Test Plan — v0.3.1.3 FIX1

Apply FIX1 over v0.3.1.3.

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.3-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected:

```text
Validation summary: 0 error(s), 0 warning(s)

[PASS] FIX1 Compile :: System.Net namespace is imported for HttpVersion
[PASS] FIX1 Compile :: HTTP 1.1 version pin remains explicit

[PASS] Build :: Windows Avalonia desktop compiles/publishes
[PASS] Build :: Linux Avalonia desktop compiles/publishes
[PASS] Build :: Linux headless monitoring service compiles/publishes

Failed : 0
```
