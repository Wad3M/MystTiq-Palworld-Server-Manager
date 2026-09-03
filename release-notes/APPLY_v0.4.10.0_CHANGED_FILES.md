# Apply v0.4.10.0 Changed Files

The Changed Files package is intended only for a verified v0.4.9.1 source tree. Back up local changes first, extract at the repository root, and allow paths to merge. Do not use it as a replacement for the Full Source package on an unknown or older baseline.

After applying, run:

```powershell
.\Build.ps1 Clean
.\Build.ps1 Validate -Strict
.\scripts\Test-v0.4.10.0-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Promotion is allowed only when strict validation, Windows/Linux builds, runtime smoke, and the complete logic gate all pass.
