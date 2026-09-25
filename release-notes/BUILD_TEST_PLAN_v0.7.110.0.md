# v0.7.110.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the smoke and live checks use current code.
2. Run strict validation and `scripts/Test-v0.7.110.0-Logic.ps1 -RunBuild`, including the frozen v0.7.109.0
   checkpoint gate, every carried-forward smoke, the new v0.7.110.0 smoke, and `MystTiq.LogicHarness`.
3. New surface: `SupervisorEventKind.ManualRecovery`, `HeadlessFleetCrashRecoveryService`'s watch-then-resume
   loop, `CrashAlertObserver`'s pinned-notice tracking.
4. The new route smoke takes its own real time (give-up at default timings is ~90 seconds) and launches a real
   process (a renamed copy of Windows' `waitfor.exe`) to stand in for a manually-restarted PalServer — allow a
   few minutes and confirm no stray `PalServer.exe`-named process is left running afterward.
5. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
