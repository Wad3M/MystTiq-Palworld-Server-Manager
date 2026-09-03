# Build / Test Plan — v0.3.1.0 FIX3

Apply FIX3 over FIX2.

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

Expected:

```text
Validation summary: 0 error(s), 0 warning(s)

[PASS] FIX3 Build Gate :: RunBuild block does not inspect LASTEXITCODE
[PASS] FIX3 Build Gate :: Release validation passes after non-throwing Build.ps1 completion
[PASS] FIX3 Build Gate :: Windows desktop build passes after non-throwing completion
[PASS] FIX3 Build Gate :: Linux desktop build passes after non-throwing completion

[PASS] Build :: Release validation
[PASS] Build :: Windows Avalonia desktop compiles/publishes
[PASS] Build :: Linux Avalonia desktop compiles/publishes

Failed : 0
```

No additional standalone DesktopWindows/DesktopLinux build is necessary because `-RunBuild` performs both.
