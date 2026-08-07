# Apply Instructions — v0.2.14.10 Changed Files

1. Back up the current v0.2.14.9 FIX3 source folder.
2. Extract the changed-files ZIP into the repository root.
3. Allow files to overwrite existing files.
4. Confirm the new files exist:
   - `scripts/Build-Release.ps1`
   - `scripts/Build-Checksums.ps1`
5. Run:

```powershell
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

The changed-files package does not include generated `artifacts`, `bin`, or `obj` directories.
