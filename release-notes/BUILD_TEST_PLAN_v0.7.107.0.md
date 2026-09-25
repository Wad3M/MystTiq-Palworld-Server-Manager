# v0.7.107.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the smoke uses current code.
2. Run strict validation and `scripts/Test-v0.7.107.0-Logic.ps1 -RunBuild`, including the frozen v0.7.106.0
   checkpoint gate, every carried-forward smoke, the new v0.7.107.0 smoke, and `MystTiq.LogicHarness`.
3. New surface: `AlertEpisodeTracker`'s reminder clock (`Observe`'s `reminderInterval` parameter,
   `EpisodeState.LastReminderAt`), `HeadlessAlertCenterService.ReminderInterval` and its
   `MYSTTIQ_ALERT_REMINDER_MINUTES` override.
4. Live smoke shortens both the evaluation tick and the reminder interval so a full reminder cycle (including
   recovery) completes inside the smoke instead of needing 24 real hours; no UI surface changed this version.
5. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
