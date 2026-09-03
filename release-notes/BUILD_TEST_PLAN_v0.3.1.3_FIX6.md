# Build / Test Plan — v0.3.1.3 FIX6

Apply FIX6 over FIX5, then run:

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

[PASS] FIX6 Deployment :: Headless deploy Version parameter has a parser-safe default
[PASS] FIX6 Deployment :: Headless deploy detects empty Version before resolving project version
[PASS] FIX6 Deployment :: Headless deploy resolves project version with Get-ProjectVersion
[PASS] FIX6 Deployment :: Headless deploy references current-version archive pattern

Failed : 0
```

The deployment script itself does not need to be changed or re-proven for FIX6 because it already passed the live deployment test under FIX5.
