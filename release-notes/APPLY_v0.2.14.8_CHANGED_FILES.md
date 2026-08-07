# Apply v0.2.14.8 Changed Files

Apply the Changed Files ZIP over the validated v0.2.14.7 repository root.

## Added

- `src/PalworldManager/MainWindow.Lifecycle.cs`
- `src/PalworldManager/Services/Infrastructure/ApplicationConstants.cs`
- `release-notes/v0.2.14.8.md`
- `release-notes/BUILD_TEST_PLAN_v0.2.14.8.md`
- `release-notes/APPLY_v0.2.14.8_CHANGED_FILES.md`

## Updated

- `Directory.Build.props`
- `src/PalworldManager/MainWindow.xaml.cs`
- `CHANGELOG.md`
- `SOURCE_MANIFEST_SHA256.txt`

After applying, run `Build.ps1 Clean` followed by `Build.ps1 All`.
