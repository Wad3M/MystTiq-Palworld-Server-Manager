# v0.7.113.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.113.0
- `src/MystTiq.Core/Services/TeleportPointText.cs` (new) — point text parsing/formatting
- `src/MystTiq.HeadlessHost/HeadlessTeleportService.cs` (new) — `TeleportChat`, `PalDefenderChatLogSource`,
  `HeadlessTeleportService`, records
- `src/MystTiq.HeadlessHost/ServerProfileHost.cs` — `Teleport`
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs` — service wiring, start/stop, `/api/v1/teleport` routes
- `src/MystTiq.Desktop/Models/TeleportDtos.cs` (new)
- `src/MystTiq.Desktop/Services/IMystTiqApiClient.cs`, `MystTiqApiClient.cs` — teleport calls
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — Teleport Points state and commands, loaded on
  the Map page
- `src/MystTiq.Desktop/MainWindow.axaml` — Teleport Points card on the Map page
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 5 new scenarios, `FakeChatSource`
- `scripts/Testing/FakePalServerEndpoints.ps1` (new) — fake PalServer RCON/REST for smokes
- `scripts/Test-v0.7.113.0-RouteSmoke.ps1` (new), `scripts/Test-v0.7.113.0-Logic.ps1` (new)
- `docs/architecture/v0.7.113.0-teleport-points.md`, `release-notes/v0.7.113.0.md`,
  `release-notes/APPLY_v0.7.113.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.113.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
