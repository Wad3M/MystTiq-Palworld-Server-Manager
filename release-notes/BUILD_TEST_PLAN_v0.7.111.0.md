# v0.7.111.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the smoke and live checks use current code.
2. Run strict validation and `scripts/Test-v0.7.111.0-Logic.ps1 -RunBuild`, including the frozen v0.7.110.0
   checkpoint gate, every carried-forward smoke (now including v0.7.110.0's), the new v0.7.111.0 smoke, and
   `MystTiq.LogicHarness`.
3. New surface: `AlertRuleSet.CrashAlerts`/`MutedUntilUtc`, `AlertMutePolicy`, `POST /api/v1/alerts/mute`,
   `CrashAlertObserver`'s rules provider, the Alert Center's Crash Alerts block and Mute Alerts card.
4. Live: on a non-production profile, Mute 1 hour shows the muted text and an Unmute button; confirm other
   profiles are not muted; Unmute returns to "Alerts are on."
5. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
