# Apply v0.2.14.9 Changed Files

Apply the changed-files archive at the repository root, preserving paths.

Changed or added:
- `Directory.Build.props`
- `Build.ps1`
- `scripts/Validate-Release.ps1`
- `CHANGELOG.md`
- `RELEASE_CHECKLIST.md`
- `release-notes/v0.2.14.9.md`
- `release-notes/BUILD_TEST_PLAN_v0.2.14.9.md`
- `release-notes/APPLY_v0.2.14.9_CHANGED_FILES.md`

Then run:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```
