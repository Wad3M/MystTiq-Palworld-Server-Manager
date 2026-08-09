# Build Test Plan — v0.2.15.2 FIX1

From the repository root:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

## Expected

- `Validate`: 0 errors, 0 warnings.
- Release build succeeds.
- Portable package succeeds.
- Installer succeeds.
- SHA256 checksum generation and verification succeed.

No application runtime behavior changed in FIX1. Continue the v0.2.15.2 MOD-operation runtime tests after the build passes.
