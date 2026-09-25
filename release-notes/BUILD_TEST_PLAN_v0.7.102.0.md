# v0.7.102.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the smokes and the UI check use current code.
2. Run strict validation and `scripts/Test-v0.7.102.0-Logic.ps1 -RunBuild`, including the frozen v0.7.101.0
   checkpoint gate, every carried-forward smoke and the new v0.7.102.0 smoke, and `MystTiq.LogicHarness`.
3. New surface: slider coercion guard, `AlertEpisodeTracker`, `DiskSpaceRules`, the NotRunning network state,
   the Automation hint.
4. Live UI check (window rendered with PrintWindow, input as window messages; helper `ui-capture-helper.ps1`):
   open Configuration on a profile that has an out-of-range slider value and confirm "No unsaved changes" and that
   the slider shows the file's real value; leave the page and confirm there is no prompt; check Diagnostics,
   Backups, Automation and Update Center. Do not run actions on the real Default Server.
5. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
