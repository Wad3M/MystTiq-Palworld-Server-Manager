<!-- MystTiq v1.1.1.3: file reviewed for this release (2026-10-19). -->
# v1.0.7.3 Changed Files

- `src/MystTiq.HeadlessHost/HeadlessIdentityGuardService.cs`: a loading grace (90 s) for a player with no character yet;
  a clock for tests.
- `src/MystTiq.Desktop/Services/BulkObservableCollection.cs` (new); `ViewModels/MainWindowViewModel.cs`: unchanged log
  polls skipped, one update per list, first-seen times for untimed lines, the Console filter only while shown;
  `MainWindow.axaml.cs`: an export brings the filter up to date first.
- Tests: `scripts/Test-v1.0.7.3-Logic.ps1` (generated from the v1.0.7.2 gate, every earlier check carried), a LogicHarness
  scenario replaying the owner's join, and ArtworkHarness checks (notification counts, 10,000 lines, stable times).
