# v0.7.108.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.108.0
- `src/MystTiq.HeadlessHost/HeadlessAlertCenterService.cs` — `AlertRuleSet.ReminderMinutes`,
  `GetReminderInterval()` replaces the old static env-var-only interval
- `src/MystTiq.Desktop/Models/AlertCenterDtos.cs` — `AlertRuleSetDto.ReminderMinutes`
- `src/MystTiq.Desktop/MainWindow.axaml` — new "Reminder every (minutes, 0 = off)" row on the Alert Center's
  Threshold Rules card
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 1 scenario
- `scripts/Test-v0.7.108.0-RouteSmoke.ps1` (new) — live configured-reminder smoke (no env var set)
- `docs/architecture/v0.7.108.0-configurable-alert-reminders.md`, `release-notes/v0.7.108.0.md`,
  `release-notes/APPLY_v0.7.108.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.108.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.108.0-Logic.ps1` (new)
