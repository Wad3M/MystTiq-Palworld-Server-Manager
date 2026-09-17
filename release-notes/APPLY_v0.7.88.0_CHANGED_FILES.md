# v0.7.88.0 Changed Files

- `Directory.Build.props` — version bump to 0.7.88.0
- `src/PalworldManager/app.manifest` — version bump to 0.7.88.0
- `src/MystTiq.Desktop/Models/ServerStatusDto.cs` — new `IsProcessLive` computed property
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — `ApplyStatus`'s three `IsProcessLive`
  call sites, `RefreshAsync`'s wizard short-circuit scoped to Connect only,
  `NewServerInstallDirectoryEffectivePath`'s public setter, `CloneableSourceProfiles` no longer
  filters by open tabs
- `src/MystTiq.Desktop/MainWindow.axaml` — Install Directory editable path field + Browse button,
  Game Port highlight border + permanent note
- `src/MystTiq.Desktop/MainWindow.axaml.cs` — `WorkspaceBrowse_Click`'s new `installDirectory` branch
- `docs/architecture/v0.7.88.0-new-server-wizard-and-dashboard-bug-fixes.md` (new)
- `release-notes/v0.7.88.0.md`, `release-notes/APPLY_v0.7.88.0_CHANGED_FILES.md`,
  `release-notes/BUILD_TEST_PLAN_v0.7.88.0.md` (new)
- `CHANGELOG.md` — new v0.7.88.0 entry
- `README.md` — version line + roadmap table row
- `docs/index.html` — current development target
- `scripts/Test-v0.7.88.0-Logic.ps1` (new)
