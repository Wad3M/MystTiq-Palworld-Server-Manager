# v0.7.105.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the live UI check uses current code.
2. Run strict validation and `scripts/Test-v0.7.105.0-Logic.ps1 -RunBuild`, including the frozen v0.7.104.0
   checkpoint gate, every carried-forward smoke, and `MystTiq.LogicHarness`. No new server route was touched
   this version, so no new route smoke exists.
3. New surface: `DiagnosticFindingDto.FixConfirmed`/`FixConfirmText`, the Fix button's `IsEnabled` binding, the
   new per-row Confirmed checkbox.
4. Live UI check (window rendered with PrintWindow; helper `ui-capture-helper.ps1`): open Diagnostics on a
   profile with a fixable finding (the clone's "Scheduled backups" Warning is the known one), confirm the Fix
   button starts disabled and the Confirmed checkbox unticked, tick it, confirm the button enables. Do not press
   it on a real profile.
5. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
