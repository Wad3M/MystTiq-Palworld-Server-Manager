<!-- MystTiq v1.0.0.1: file reviewed for this release (2026-10-04). -->
# v0.9.10.0 Changed Files

- `src/MystTiq.Desktop/Services/LocalManagementBootstrapper.cs`: a recorded helper that is alive is waited for
  (`WaitForAnswerAsync`, 4 s per answer) and reused; one that cannot be used is stopped and must have exited before a
  replacement (`StopHelperProcess` returns whether it stopped); `IsRecordedHelper` matches path and start time; an
  optional runtime folder for tests.
- `src/MystTiq.Desktop/Services/SidecarState.cs`: `StartedUtc`.
- `src/MystTiq.Desktop/Services/MessageCatalog.cs`: placeholders that hold a name (quoted, or after server, world,
  player, guild and the like) keep their value.
- `src/MystTiq.Desktop/MainWindow.axaml`: the Dashboard's server name and description are bound directly
  (`DashboardServerName`, `DashboardServerDescriptionVerbatim`); `scripts/Update-MystTiqUiText.ps1` treats a
  `...Verbatim` binding as the user's own text.
- `src/MystTiq.HeadlessHost/HeadlessModManagementService.cs`: `UpdateChecked` on each MOD; `Compared` on the check.
- `src/MystTiq.HeadlessHost/HeadlessAlertCenterService.cs`: `ComponentAlerts.ModsBehind`, `ModUpdateState`, the
  count-neutral message.
- `src/MystTiq.HeadlessHost/HeadlessComponentUpdateService.cs`: the release list and `ReleaseChannel`.
- `src/MystTiq.Core/Services/CrashSignatureCatalog.cs`: `ExitAfterJoinDetector.Detect` without a time window; the
  signature's new title and evidence wording (the old wording still matches).
- `src/MystTiq.HeadlessHost/HeadlessCrashAndSaveToolsService.cs`: the new `Detect` call.
- `src/MystTiq.HeadlessHost/HeadlessDiagnosticsService.cs`: the port finding's wording allows for port forwarding.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 5 new texts, 6 reworded, and the review's label and wording fixes, in all
  12 languages.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: names kept in messages; slow and stuck stand-in helpers.
- `scripts/Testing/MystTiq.LogicHarness/Program.cs`: the MOD alert, the release channel, stable crash evidence.
- `scripts/Test-v0.9.10.0-Logic.ps1` (new gate); `scripts/Test-v0.9.9.0-RouteSmoke.ps1` accepts the renamed finding;
  `scripts/Test-v0.9.5.0-Upgrade.ps1` repeats its stop until the stand-in stays down.
- Docs: this release's notes, test plan and architecture doc; CHANGELOG, README, site, roadmap, publishing guide.
