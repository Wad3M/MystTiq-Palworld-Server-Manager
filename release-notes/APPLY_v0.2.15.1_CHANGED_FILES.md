# Apply Instructions — v0.2.15.1 Changed Files

Apply the changed-files package over the validated v0.2.14.11 repository root, preserving paths.

## New files

- `src/PalworldManager/Models/Ue4ssRuntimeInfo.cs`
- `src/PalworldManager/Services/Ue4ssRuntimeResolver.cs`
- `release-notes/v0.2.15.1.md`
- `release-notes/BUILD_TEST_PLAN_v0.2.15.1.md`
- `release-notes/APPLY_v0.2.15.1_CHANGED_FILES.md`

## Updated files

- `Directory.Build.props`
- `src/PalworldManager/app.manifest`
- `src/PalworldManager/MainWindow.xaml.cs`
- `README.md`
- `CHANGELOG.md`
- `RELEASE_CHECKLIST.md`
- `docs/index.html`
- `SOURCE_MANIFEST_SHA256.txt`

## Build

```powershell
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Do not promote v0.2.15.1 to the official baseline until compile and runtime resolver tests pass.
