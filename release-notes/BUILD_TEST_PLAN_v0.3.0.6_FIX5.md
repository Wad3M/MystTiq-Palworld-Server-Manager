# Build / Test Plan — v0.3.0.6 FIX5

Because FIX5 changes only test/display contracts, the required gate is the Windows validation + logic harness.

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Validate

.\scripts\Test-v0.3.0.6-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected:

- validation: 0 errors / 0 warnings
- logic harness: 0 failures
- the prior two false failures no longer appear:
  - Linux Enrollment :: Enrollment script version is current
  - Windows LAN Acceptance :: Token persistence is explicitly false

The previously completed Linux and Windows LAN runtime acceptance does not need to be repeated unless runtime code is changed.
