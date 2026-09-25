# v0.7.105.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.105.0
- `src/MystTiq.Desktop/Models/DiagnosticFindingDtos.cs` — `DiagnosticFindingDto` implements
  `INotifyPropertyChanged`, new `FixConfirmed` and `FixConfirmText`
- `src/MystTiq.Desktop/MainWindow.axaml` — Fix button `IsEnabled` gated on `FixConfirmed`, new per-row
  "Confirmed" checkbox with a tooltip naming the fix
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — `FixDiagnosticCommand` and `FixDiagnosticAsync`
  both also require `FixConfirmed`
- `scripts/Testing/MystTiq.LogicHarness/MystTiq.LogicHarness.csproj` — compiles `DiagnosticFindingDtos.cs`
  directly (same technique as `PalworldConfigurationDtos.cs`)
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 1 scenario
- `docs/architecture/v0.7.105.0-doctor-fix-confirmation.md`, `release-notes/v0.7.105.0.md`,
  `release-notes/APPLY_v0.7.105.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.105.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.105.0-Logic.ps1` (new) — no new route smoke this version (Desktop-only change, no server
  route touched)
