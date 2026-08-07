# Build Test Plan — v0.2.14.9

## Build

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Expected:
- Validation reports zero errors.
- Release build succeeds.
- Portable package is created only after validation and build succeed.
- Installer is created when Inno Setup 6 is available.

## Negative validation checks
Perform on a disposable copy only:
1. Temporarily rename `release-notes/v0.2.14.9.md`; validation must fail.
2. Temporarily add a root `BUILD_TEST_PLAN_TEST.md`; validation must fail.
3. Restore both files and confirm validation passes.

## Runtime smoke tests
- Installed and portable startup.
- Immediate close during startup.
- Dashboard monitoring for at least 30 seconds.
- Server start, stop, and restart.
- Notification self-test and final bell auto-hide.
- Backup while stopped and coordinated backup while running.
- Player identity deduplication.
- Startup player/guild/base totals.
- Workspace validation.
- World Management, Repair Center, Transaction Center, and Diagnostics Center.
- Support-package generation and redaction review.

## UI audit
Review at 100%, 125%, and 150% Windows scaling:
- no clipped labels;
- no oversized action buttons;
- shared semantic colors preserved;
- tooltips visible on destructive or technical actions;
- no dead or duplicate navigation entries.
