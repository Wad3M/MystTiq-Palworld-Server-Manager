# Apply Instructions — v0.3.0.3

Apply over official Linux/headless baseline **v0.3.0.2 FIX2**.

Preserve repository-relative paths.

Then run:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.3.0.3-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
.\Build.ps1 LinuxHeadless
```

The Changed Files package includes a deletion notice for `scripts\Test-v0.3.0.2-Logic.ps1`.

Do not expose the v0.3.0.3 API on the LAN. This phase is intentionally loopback-only.
