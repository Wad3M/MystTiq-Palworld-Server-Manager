# v0.7.111.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.111.0
- `src/MystTiq.HeadlessHost/HeadlessAlertCenterService.cs` — `AlertRuleSet.CrashAlerts`, `MutedUntilUtc`,
  `CrashAlertRule`, `AlertMuteRequest`, `AlertMutePolicy`, `Mute(minutes)`, evaluation skipped while muted
- `src/MystTiq.HeadlessHost/CrashAlerts.cs` — `CrashAlertObserver` reads the profile's rules per event,
  logs suppressed alerts to Activity, still unpins on ManualRecovery
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs` — alert center created before the crash observer and
  passed to it; `POST /api/v1/alerts/mute` (Admin)
- `src/MystTiq.Desktop/Models/AlertCenterDtos.cs` — `CrashAlerts`, `MutedUntilUtc`, `IsMuted`,
  `MuteStatusText`, `CrashAlertRuleDto`, `AlertMuteRequestDto`
- `src/MystTiq.Desktop/Services/IMystTiqApiClient.cs`, `MystTiqApiClient.cs` — `MuteAlertsAsync`
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — `MuteAlertsCommand`, `MuteAlertsAsync`
- `src/MystTiq.Desktop/MainWindow.axaml` — Crash Alerts block and Mute Alerts card on the Alert Center page
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 3 new scenarios, 1 extended
- `scripts/Test-v0.7.111.0-RouteSmoke.ps1` (new), `scripts/Test-v0.7.111.0-Logic.ps1` (new)
- `docs/architecture/v0.7.111.0-alert-mute-and-crash-alert-settings.md`, `release-notes/v0.7.111.0.md`,
  `release-notes/APPLY_v0.7.111.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.111.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
