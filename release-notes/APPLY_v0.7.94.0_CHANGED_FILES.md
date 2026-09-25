# v0.7.94.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.94.0
- `src/MystTiq.Core/Services/KitEntryText.cs` (new) — kit entry line parser/formatter
- `src/MystTiq.HeadlessHost/HeadlessKitService.cs` (new) — kits, validation, commands, claims, auto-gift,
  `IKitCommandRunner` and the RCON runner
- `src/MystTiq.HeadlessHost/ServerProfileHost.cs`, `LocalManagementApiHost.cs` — composition, poll hook, routes
- `src/MystTiq.Desktop/Models/KitDtos.cs` (new)
- `src/MystTiq.Desktop/Services/IMystTiqApiClient.cs`, `MystTiqApiClient.cs` — kit calls
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — kit state, editor, commands
- `src/MystTiq.Desktop/MainWindow.axaml` — Starter Kits card
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 9 kit scenarios
- `docs/architecture/v0.7.94.0-starter-kits.md`, `release-notes/v0.7.94.0.md`,
  `release-notes/APPLY_v0.7.94.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.94.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.94.0-Logic.ps1` (new)
