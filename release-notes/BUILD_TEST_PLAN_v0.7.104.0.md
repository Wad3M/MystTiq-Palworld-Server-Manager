# v0.7.104.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the smokes and the UI check use current code.
2. Run strict validation and `scripts/Test-v0.7.104.0-Logic.ps1 -RunBuild`, including the frozen v0.7.103.0
   checkpoint gate, every carried-forward smoke and the new v0.7.104.0 smoke, and `MystTiq.LogicHarness`.
3. New surface: `AlertEpisodeTracker.SetAlertNotificationId`/the `Observe` and `Close` id handoff,
   `HeadlessNotificationService.Create`'s id overload, `HeadlessAlertCenterService.Unpin`.
4. Live smoke covers both endings of an episode (recovery, and the rule switched off mid-episode) against a real
   sidecar; no UI-only behavior was added, so no separate window walk is required this version.
5. Still needs a real, longer-lived pinned condition to see live in the desktop app: nobody has triggered a real
   Critical alert on the actual Default Server during this session.
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
