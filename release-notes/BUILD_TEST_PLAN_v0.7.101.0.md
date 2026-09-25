# v0.7.101.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the smokes use current code.
2. Run strict validation and `scripts/Test-v0.7.101.0-Logic.ps1 -RunBuild`, including the frozen v0.7.100.0
   checkpoint regression gate, every carried-forward smoke and the new v0.7.101.0 smoke, and
   `MystTiq.LogicHarness` (its recovery scenarios run the real supervisor loop against a scripted server).
3. New surface: `ISupervisorObserver` events, `CrashAlertText`, `CrashAlertObserver`, composition per profile.
4. Live check: the smoke seeds a persisted "running" state with a process id that does not exist so the real
   lifecycle service reports a crash, then checks the notifications route for the crash and failed-restart alerts.
5. Still needs a person: a real Palworld crash, delivery through a configured Discord/email/webhook route, and
   the Notifications page rendering the alerts.
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
