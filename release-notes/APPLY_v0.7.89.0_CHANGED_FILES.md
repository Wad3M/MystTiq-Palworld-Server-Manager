# v0.7.89.0 Changed Files

- `Directory.Build.props` — version bump to 0.7.89.0
- `src/PalworldManager/app.manifest` — version bump to 0.7.89.0
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — `EnsureManagementConnectionForLifecycleAsync`
  loopback-address check, `ApplyDashboardSupport` stale-world-data clear, new
  `InstallPalworldServerFromWizardCommand`/`InstallPalworldServerFromWizardAsync`,
  `HasDistributionOutput`, `FinishInstallAndAdvanceAsync` auto-scan trigger
- `src/MystTiq.Desktop/ViewModels/TabSession.cs` — `ConnectionKindText` loopback-address check
- `src/MystTiq.Desktop/MainWindow.axaml` — "OPERATION PROGRESS" relabel, Install step auto-refresh
  command + SteamCMD output display, MODs step "Install MOD ZIP…" button
- `docs/architecture/v0.7.89.0-second-server-lifecycle-and-wizard-polish.md` (new)
- `release-notes/v0.7.89.0.md`, `release-notes/APPLY_v0.7.89.0_CHANGED_FILES.md`,
  `release-notes/BUILD_TEST_PLAN_v0.7.89.0.md` (new)
- `CHANGELOG.md` — new v0.7.89.0 entry
- `README.md` — version line + roadmap table row
- `docs/index.html` — current development target
- `scripts/Test-v0.7.89.0-Logic.ps1` (new)
