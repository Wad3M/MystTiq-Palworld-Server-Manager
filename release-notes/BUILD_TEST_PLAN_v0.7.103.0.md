# v0.7.103.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the smokes and the UI check use current code.
2. Run strict validation and `scripts/Test-v0.7.103.0-Logic.ps1 -RunBuild`, including the frozen v0.7.102.0
   checkpoint gate, every carried-forward smoke and the new v0.7.103.0 smoke, and `MystTiq.LogicHarness`.
3. New surface: `DoctorHealthRules.ScheduledBackups`, the `create-backup-rule` fix and its Admin check, the
   action-labelled Doctor buttons.
4. Live UI check (window rendered with PrintWindow; helper `ui-capture-helper.ps1`): open Server Doctor on a
   profile with no automation rules and confirm the Scheduled backups row and the "Create Nightly Backup Rule"
   button. Do not press it on a real profile.
5. Still needs a person or a token: the Admin-role refusal for an Operator, and the message shown on the Doctor page
   after pressing the button.
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
