# v0.7.115.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.115.0
- `src/MystTiq.Desktop/Services/MapLabelLayout.cs` — rectangle label placement with nearby candidate spots
- `src/MystTiq.Desktop/Models/WorldMapDtos.cs` — `LabelOffsetX`, `LabelPosition`; `LabelMargin` removed
- `src/MystTiq.Desktop/MainWindow.axaml` — dot-only marker buttons (MinWidth/MinHeight 0), separate label layers
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — label widths passed to the layout; X/Y label offsets
- `src/MystTiq.Core/Services/SupervisorRecoveryState.cs` (new) — `SupervisorRecoveryState`, `SupervisorRecoveryStateStore`
- `src/MystTiq.Core/Services/HeadlessSupervisor.cs` — persisted restart window, readiness before success,
  `WaitUntilReadyAsync`
- `src/MystTiq.HeadlessHost/HeadlessFleetCrashRecoveryService.cs` — persisted give-up, resume watching, "back up"
  waits for Ready
- `src/MystTiq.HeadlessHost/CrashAlerts.cs` — pinned DOWN id kept in the recovery-state store
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs` — per-profile recovery-state store wiring
- `src/MystTiq.Core/Operations/OperationCoordinator.cs` — journals reloaded at start-up, Interrupted handling
- `scripts/Validate-Release.ps1` — `-AllowBuildOutputs`
- `scripts/Testing/MystTiq.TestFramework.ps1` — SKIP state
- `RELEASE_CHECKLIST.md` — version-agnostic current-release section
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — map-label scenario rewritten, 4 new scenarios,
  `ScriptedLifecycle.ReadyOverride`
- `scripts/Test-v0.7.110.0-RouteSmoke.ps1` — fixture game port bound once the fake PalServer starts (readiness)
- `scripts/Test-v0.7.115.0-RouteSmoke.ps1` (new), `scripts/Test-v0.7.115.0-Logic.ps1` (new)
- `docs/architecture/v0.7.115.0-deficiency-fixes.md`, `release-notes/v0.7.115.0.md`,
  `release-notes/APPLY_v0.7.115.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.115.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
