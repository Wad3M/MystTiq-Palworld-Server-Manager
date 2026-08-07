# v0.2.13.2 FIX1 — Startup Logging & Unified Build

## Fixes
- Initializes `SessionLogService` before the first constructor-time call to `Log()`.
- Prevents the startup `NullReferenceException` in both installed and portable modes.
- Adds a root `Build.ps1` entry point for version, clean, build, package, installer, and full release preparation.

## Commands
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Version
.\Build.ps1 Clean
.\Build.ps1 Build
.\Build.ps1 Package
.\Build.ps1 Installer
.\Build.ps1 All
```

`All` builds and packages the portable release. It also builds the installer when Inno Setup 6 is installed at the standard path; otherwise it reports a warning and leaves the portable artifacts intact.

## Required tests
1. Start the normal build and confirm no startup error.
2. Start an extracted portable package and confirm no startup error.
3. Confirm startup logs are written to the appropriate installed or portable log directory.
4. Run `.\Build.ps1 Package` and confirm the portable ZIP and checksum are created.
5. Run `.\Build.ps1 All`; if Inno Setup is absent, confirm only the installer step is skipped.
