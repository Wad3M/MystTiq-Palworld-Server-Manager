# Build / Test Plan — v0.3.0.7 FIX2

Apply FIX2 over v0.3.0.7 FIX1.

Run:

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.7-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson

.\Build.ps1 LinuxHeadless
```

Expected:

- validation: 0 errors / 0 warnings
- build: successful
- logic harness: 0 failed checks
- the previous `Automation :: Extended Linux acceptance invokes production readiness` failure is PASS
- LinuxHeadless successfully packages the production-readiness runner

After the Windows gate is clean:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

The remote Linux acceptance output must include:

```text
==> Production-readiness integration gate
[PASS] Production readiness integration
```

before the final summary.
