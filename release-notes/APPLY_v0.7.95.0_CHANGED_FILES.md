# v0.7.95.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.95.0
- `src/MystTiq.Core/Models/DiscordBotModels.cs` — new config/view fields, `DiscordSnowflake`
- `src/MystTiq.HeadlessHost/DiscordBotFormatting.cs` (new) — pure status/diff/autocomplete/role logic
- `src/MystTiq.HeadlessHost/HeadlessDiscordBotService.cs` — live loop, status message, events feed,
  autocomplete, save/backup/unban, no-mentions replies, channel-id validation
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs` — backup service into the bot, channel-id validation on PUT
- `src/MystTiq.Desktop/Models/DiscordBotDtos.cs`, `ViewModels/MainWindowViewModel.cs`, `MainWindow.axaml` —
  new form fields and client-side channel-id check
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 6 Discord scenarios
- `docs/architecture/v0.7.95.0-discord-bot-live-features.md`, `release-notes/v0.7.95.0.md`,
  `release-notes/APPLY_v0.7.95.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.95.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.95.0-Logic.ps1` (new)
