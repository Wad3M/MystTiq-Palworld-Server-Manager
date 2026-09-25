# v0.7.107.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.107.0
- `src/MystTiq.HeadlessHost/AlertEpisodes.cs` — `EpisodeAction.Reminder`, `EpisodeState.LastReminderAt`,
  `Observe` gained an opt-in `reminderInterval` parameter (default `null`, no existing call site affected),
  `Close` also resets the reminder clock
- `src/MystTiq.HeadlessHost/HeadlessAlertCenterService.cs` — `ReminderInterval` (24h default,
  `MYSTTIQ_ALERT_REMINDER_MINUTES` override), `Track` handles `EpisodeAction.Reminder`
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 2 scenarios
- `scripts/Test-v0.7.107.0-RouteSmoke.ps1` (new) — live reminder-cadence and recovery smoke
- `docs/architecture/v0.7.107.0-alert-reminders.md`, `release-notes/v0.7.107.0.md`,
  `release-notes/APPLY_v0.7.107.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.107.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.107.0-Logic.ps1` (new)
