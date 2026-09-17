# v0.7.87.0 Changed Files

- `Directory.Build.props` — version bump to 0.7.87.0
- `src/PalworldManager/app.manifest` — version bump to 0.7.87.0
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — `Navigate`/`PerformNavigation` split,
  `ConfigurationUnsavedChangesNavigationBlocked` event, pending-navigation state, three continuation
  methods (`ContinuePendingNavigationSavingConfigurationAsync`,
  `ContinuePendingNavigationDiscardingConfigurationChanges`, `CancelPendingConfigurationNavigation`),
  `SetConfigurationView(true)` added to the Configuration navigation case
- `src/MystTiq.Desktop/MainWindow.axaml` — Advanced Settings table `Border.rowDirty` style,
  `Classes.rowDirty`/`Classes.dirty` bindings
- `src/MystTiq.Desktop/MainWindow.axaml.cs` — `DataContextProperty` subscription,
  `OnConfigurationUnsavedChangesNavigationBlockedAsync` handler
- `src/MystTiq.Desktop/Views/ConfirmSaveDiscardDialog.axaml` (new)
- `src/MystTiq.Desktop/Views/ConfirmSaveDiscardDialog.axaml.cs` (new)
- `docs/architecture/v0.7.87.0-configuration-dirty-highlighting-and-save-prompt.md` (new)
- `release-notes/v0.7.87.0.md`, `release-notes/APPLY_v0.7.87.0_CHANGED_FILES.md`,
  `release-notes/BUILD_TEST_PLAN_v0.7.87.0.md` (new)
- `CHANGELOG.md` — new v0.7.87.0 entry
- `README.md` — version line + roadmap table row
- `docs/index.html` — current development target
- `scripts/Test-v0.7.87.0-Logic.ps1` (new)
- `docs/roadmap/PRODUCT_ROADMAP.md` — Live-Session Backlog entry cleared
