# Build Test Plan — v0.2.15.3 FIX2

From the repository root:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Expected:

- Validation: 0 errors, 0 warnings.
- `ModService.cs` compiles without CS8087/CS1009.
- Portable ZIP, installer, and SHA256SUMS are produced.
- Continue the original v0.2.15.3 migration, ZIP-normalization, and FIX1 installer runtime tests after the build passes.
