# v0.7.108.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the smoke and the UI check use current code.
2. Run strict validation and `scripts/Test-v0.7.108.0-Logic.ps1 -RunBuild`, including the frozen v0.7.107.0
   checkpoint gate, every carried-forward smoke (v0.7.107.0's own reminder smoke included, unmodified, still
   using the env-var override path), the new v0.7.108.0 smoke, and `MystTiq.LogicHarness`.
3. New surface: `AlertRuleSet.ReminderMinutes`, `HeadlessAlertCenterService.GetReminderInterval()`, the Alert
   Center page's new field.
4. Live UI check (window rendered with PrintWindow; helper `ui-capture-helper.ps1`): open Alert Center on a
   profile, confirm the new "Reminder every (minutes, 0 = off)" row renders with its tooltip and the 1440
   default, saved by the existing Save Rules button.
5. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
