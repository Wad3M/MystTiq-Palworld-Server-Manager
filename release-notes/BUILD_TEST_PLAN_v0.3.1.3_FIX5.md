# Build / Test Plan — v0.3.1.3 FIX5

Apply FIX5 over FIX4.

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

[PASS] FIX5 Deployment :: Headless deploy Version parameter has a parser-safe default
[PASS] FIX5 Deployment :: Headless deploy resolves project version after parameter binding
[PASS] FIX5 Deployment :: Headless deploy references current-version archive pattern

Failed : 0
```

Then run:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

Expected deployment header:

```text
==> Building v0.3.1.3 Linux headless package
```
