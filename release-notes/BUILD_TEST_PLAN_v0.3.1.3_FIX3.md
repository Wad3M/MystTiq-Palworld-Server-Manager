# Build / Test Plan — v0.3.1.3 FIX3

Apply FIX3 over FIX2 and run:

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

[PASS] FIX3 Linux Gate :: Current Linux acceptance runner exists
[PASS] FIX3 Linux Gate :: Current production readiness runner exists
[PASS] FIX3 Linux Gate :: Acceptance verifies players monitoring endpoint
[PASS] FIX3 Linux Gate :: Acceptance verifies log-tail monitoring endpoint
[PASS] FIX3 Linux Gate :: Acceptance verifies metrics monitoring endpoint
[PASS] FIX3 Linux Gate :: Acceptance verifies AdminPassword exclusion

Failed : 0
```

After that clean gate, deploy the v0.3.1.3 headless package to the Ubuntu host and perform functional monitoring acceptance.
